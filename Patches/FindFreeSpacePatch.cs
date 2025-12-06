using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using StashManagementHelper.Configuration;
using StashManagementHelper.Helpers;

namespace StashManagementHelper.Patches;

public class FindFreeSpacePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(StashGridClass), "FindFreeSpace");

    public static StashGridClass GridInstance { get; private set; }
    public static List<bool> OccupiedCells { get; private set; }
    public static List<int> HorizontalSpaces { get; private set; }
    public static List<int> VerticalSpaces { get; private set; }
    public static int CurrentSkipRows { get; private set; }

    /// <summary>
    /// Path for FindFreeSpace method of <see cref="StashGridClass"/>
    /// </summary>
    /// <param name="__instance">Instance of <see cref="StashGridClass"/></param>
    /// <param name="__result">The <see cref="LocationInGrid"/></param>
    /// <param name="item"><see cref="Item"/> to sort</param>
    /// <param name="___List_0">Indicates of which square in grid is occupied by index</param>
    /// <param name="___List_1">Number of free space horizontally from current square by index</param>
    /// <param name="___List_2">Number of free space vertically from current square by index</param>
    [PatchPrefix]
    private static bool PatchPrefix(StashGridClass __instance, ref LocationInGrid __result, Item item, List<bool> ___List_0, List<int> ___List_1, List<int> ___List_2)
    {
        GridInstance = __instance;
        OccupiedCells = ___List_0;
        HorizontalSpaces = ___List_1;
        VerticalSpaces = ___List_2;

        var sr = Settings.SkipRows.Value;
        if (sr < 0) sr = 0;
        var maxSkippable = GridInstance.GridHeight - sr;
        if (sr > maxSkippable) sr = maxSkippable;
        var skipRows = sr;
        CurrentSkipRows = skipRows;

        if (!Settings.Sorting || !string.Equals(GridInstance.ID, "hideout", StringComparison.Ordinal))
        {
            return true;
        }

        // Reject item if it cannot be accepted by the instance
        if (!__instance.CanAccept(item))
        {
            __result = null;
            return false;
        }

        // Attempt to find an existing location for the item
        var locationInGrid = __instance.GetItemLocation(item);

        // If found, remove the item temporarily to update the grid
        if (locationInGrid is not null)
        {
            __instance.ItemCollection.Remove(item, __instance);
            __instance.Add(item, locationInGrid, false);
            UpdateGridSpaces();
        }

        // Calculate the optimal placement for the item considering the cell size and skipped rows
        var cellSize = item.CalculateCellSize();
        var freeSpace = DetermineBestPlacement(cellSize, skipRows);

        // If no existing location, set the new location
        if (locationInGrid is null)
        {
            __result = freeSpace;
            return false;
        }

        // Add the item back to the collection at the new or existing location
        __instance.ItemCollection.Add(item, __instance, locationInGrid);
        __instance.Add(item, locationInGrid, true);

        UpdateGridSpaces();

        __result = freeSpace;
        ItemManager.Logger.LogInfo($"{item.Name.Localized()} location: {__result}");
        return false;
    }

    /// <summary>
    /// Determines the best placement for an item based on its size and the number of rows to skip.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LocationInGrid DetermineBestPlacement(XYCellSizeStruct cellSize, int skipRows)
    {
        // Evaluate both horizontal and vertical placements
        var freeHoriz = FindOptimalItemPlacement(cellSize.X, cellSize.Y, ItemRotation.Horizontal, skipRows);
        LocationInGrid freeVert = null;
        if (Settings.RotateItems.Value || freeHoriz == null)
        {
            freeVert = FindOptimalItemPlacement(cellSize.Y, cellSize.X, ItemRotation.Vertical, skipRows);
        }
        // If one orientation has no valid placement, use the other
        if (freeHoriz == null)
            return freeVert;
        if (freeVert == null)
            return freeHoriz;
        // Choose the placement with the smallest Y coordinate (topmost)
        return freeHoriz.y <= freeVert.y ? freeHoriz : freeVert;
    }

    /// <summary>
    /// Finds the optimal item placement considering item dimensions, rotation, and grid constraints.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static LocationInGrid FindOptimalItemPlacement(int itemWidth, int itemHeight, ItemRotation rotation, int skipRows)
    {
        // Determine the primary and secondary dimensions based on the ability to stretch horizontally
        var useInvertedDimensions = GridInstance.CanStretchHorizontally && GridInstance.GridHeight >= GridInstance.GridWidth + itemWidth;
        var primaryWidth = useInvertedDimensions ? itemWidth : itemHeight;
        var primaryHeight = useInvertedDimensions ? itemHeight : itemWidth;
        var primaryGridWidth = useInvertedDimensions ? GridInstance.GridWidth : GridInstance.GridHeight;
        var primaryGridHeight = useInvertedDimensions ? GridInstance.GridHeight : GridInstance.GridWidth;
        var primaryList = useInvertedDimensions ? HorizontalSpaces : VerticalSpaces;
        var secondaryList = useInvertedDimensions ? VerticalSpaces : HorizontalSpaces;

        // Attempt to find a suitable location with the current orientation
        var locationInGrid = FindSuitableLocation(primaryWidth, primaryHeight, rotation, primaryGridWidth, primaryGridHeight, primaryList, secondaryList, skipRows, useInvertedDimensions);
        if (locationInGrid != null)
            return locationInGrid;

        // Check if stretching is possible and return a new location accordingly
        if (GridInstance.CanStretchHorizontally && GridInstance.GridHeight >= GridInstance.GridWidth + itemWidth)
            return new LocationInGrid(GridInstance.GridWidth, 0, rotation);

        if (GridInstance.CanStretchVertically && (GridInstance.CanStretchHorizontally || itemWidth <= GridInstance.GridWidth))
            return new LocationInGrid(0, GridInstance.GridHeight, rotation);

        return null;
    }

    private static LocationInGrid FindSuitableLocation(
        int itemMainDimensionSize,
        int itemSecondaryDimensionSize,
        ItemRotation rotation,
        int gridMainDimensionSize,
        int gridSecondaryDimensionSize,
        IReadOnlyList<int> mainDimensionSpaces,
        IReadOnlyList<int> secondaryDimensionSpaces,
        int skipRows,
        bool invertDimensions = false)
    {
        // Determine starting and ending indices based on sorting direction
        var flipDir = Settings.FlipSortDirection.Value;
        var mainStartIndex = flipDir ? gridMainDimensionSize - itemMainDimensionSize - skipRows : skipRows;
        var mainEndIndex = flipDir ? 0 : gridMainDimensionSize - itemMainDimensionSize;
        var step = flipDir ? -1 : 1;

        // Iterate over possible positions in the grid to find a suitable location for the item
        for (var mainIndex = mainStartIndex; flipDir ? mainIndex >= mainEndIndex : mainIndex <= mainEndIndex; mainIndex += step)
        {
            var secondaryStart = flipDir ? gridSecondaryDimensionSize - itemSecondaryDimensionSize : 0;
            var secondaryEnd = flipDir ? 0 : gridSecondaryDimensionSize - itemSecondaryDimensionSize;
            var secondaryStep = flipDir ? -1 : 1;

            for (var secondaryIndex = secondaryStart; flipDir ? secondaryIndex >= secondaryEnd : secondaryIndex <= secondaryEnd; secondaryIndex += secondaryStep)
            {
                // Check if the current position has enough space for the item
                if (IsSpaceAvailable(mainIndex, secondaryIndex, itemMainDimensionSize, itemSecondaryDimensionSize, gridMainDimensionSize, gridSecondaryDimensionSize, mainDimensionSpaces, secondaryDimensionSpaces, invertDimensions))
                {
                    // Return the location if space is available
                    return new LocationInGrid(invertDimensions ? mainIndex : secondaryIndex, invertDimensions ? secondaryIndex : mainIndex, rotation);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if the specified space in the grid is free for the item placement.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSpaceAvailable(
        int mainIndex,
        int secondaryIndex,
        int itemMainDimensionSize,
        int itemSecondaryDimensionSize,
        int gridMainDimensionSize,
        int gridSecondaryDimensionSize,
        IReadOnlyList<int> mainDimensionSpaces,
        IReadOnlyList<int> secondaryDimensionSpaces,
        bool invertDimensions)
    {
        // Check if the secondary dimension has enough space for the item
        var availableSecondarySpace = invertDimensions ? secondaryDimensionSpaces[secondaryIndex * gridMainDimensionSize + mainIndex] : secondaryDimensionSpaces[mainIndex * gridSecondaryDimensionSize + secondaryIndex];
        if (availableSecondarySpace < itemSecondaryDimensionSize && availableSecondarySpace != -1)
        {
            return false;
        }

        // Check each cell in the main dimension to ensure all have enough space for the item
        for (var index = secondaryIndex; index < secondaryIndex + itemSecondaryDimensionSize; ++index)
        {
            var availableMainSpace = invertDimensions ? mainDimensionSpaces[index * gridMainDimensionSize + mainIndex] : mainDimensionSpaces[mainIndex * gridSecondaryDimensionSize + index];
            if (availableMainSpace < itemMainDimensionSize && availableMainSpace != -1)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Updates the grid spaces to reflect the current state of item placements.
    /// </summary>
    protected static void UpdateGridSpaces()
    {
        var skipRows = CurrentSkipRows;
        if (!Settings.Sorting || skipRows == 0)
        {
            return;
        }

        try
        {
            // Update horizontal list
            CalculateAndUpdateSpace(isHorizontal: true);

            // Update vertical list
            CalculateAndUpdateSpace(isHorizontal: false);
        }
        catch (Exception e)
        {
            ItemManager.Logger.LogError($"Error updating grid spaces: {e}");
        }
    }

    /// <summary>
    /// Calculates the available space in either horizontal or vertical direction.
    /// </summary>
    /// <param name="isHorizontal">Flag to calculate horizontally or vertically</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateAndUpdateSpace(bool isHorizontal)
    {
        var grid = GridInstance;
        var height = grid.GridHeight;
        var width = grid.GridWidth;
        var skipRows = CurrentSkipRows;
        var outerMax = (isHorizontal ? height : width) - skipRows;
        var spaces = isHorizontal ? HorizontalSpaces : VerticalSpaces;
        var occupied = OccupiedCells;
        var canStretch = isHorizontal ? grid.CanStretchHorizontally : grid.CanStretchVertically;

        for (var outer = 0; outer < outerMax; outer++)
        {
            var count = canStretch ? -1 : 0;
            var innerMax = isHorizontal ? width : height;
            for (var inner = innerMax - 1; inner >= 0; inner--)
            {
                var index = isHorizontal ? (outer * width) + inner : inner * width + outer;
                if (outer < skipRows)
                {
                    spaces[index] = 0;
                }
                else
                {
                    if (occupied[index])
                    {
                        count = 0;
                    }
                    else if (count != -1)
                    {
                        count++;
                    }

                    spaces[index] = count;
                }
            }
        }
    }
}
