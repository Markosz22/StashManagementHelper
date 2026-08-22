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
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Grid), nameof(Grid.FindFreeSpace));

    /// <summary>
    /// Prefix for <see cref="Grid.FindFreeSpace"/>. HarmonyX runs every prefix, so honour
    /// <paramref name="__runOriginal"/> if another mod already skipped the original.
    /// </summary>
    [PatchPrefix]
    private static bool PatchPrefix(Grid __instance, ref LocationInGrid __result, Item item, ref bool __runOriginal)
    {
        if (!__runOriginal)
        {
            return false;
        }

        if (!Settings.Sorting || !ItemManager.IsStashGrid(__instance))
        {
            return true;
        }

        if (!__instance.CanAccept(item))
        {
            __result = null;
            __runOriginal = false;
            return false;
        }

        var skipRows = Math.Max(0, Math.Min(Settings.SkipRows.Value, __instance.GridHeight / 2));

        LocationInGrid originalLocation = null;
        var contained = __instance.Contains(item);
        if (contained)
        {
            originalLocation = __instance.ItemCollection[item];
            __instance.RemoveItem(item, originalLocation);
        }

        try
        {
            __result = FindPlacement(__instance, item, skipRows);
        }
        finally
        {
            if (contained)
            {
                __instance.PlaceItem(item, originalLocation);
            }
        }

        __runOriginal = false;
        return false;
    }

    private static LocationInGrid FindPlacement(Grid grid, Item item, int skipRows)
    {
        var cellSize = item.CalculateCellSize();
        var rotate = Settings.RotateItems.Value;
        var flip = Settings.FlipSortDirection.Value;

        if (!flip)
        {
            if (skipRows > 0)
            {
                MarkSkipRowsOccupied(grid, skipRows);
            }

            return PickBestNativePlacement(grid, cellSize, rotate, skipRows);
        }

        // Native GetFreeLocation always scans from the origin; keep a custom scan for flip.
        return DetermineBestPlacementCustom(grid, cellSize, rotate, skipRows);
    }

    /// <summary>
    /// Zero skip-row entries in the space buffers so native <see cref="Grid.GetFreeLocation"/>
    /// will not place into those rows. <see cref="Grid.PlaceItem"/> rebuilds the buffers from
    /// layout afterwards.
    /// </summary>
    private static void MarkSkipRowsOccupied(Grid grid, int skipRows)
    {
        var width = grid.GridWidth;
        var horizontal = grid.Horizontal;
        var vertical = grid.Vertical;
        for (var y = 0; y < skipRows; y++)
        {
            var rowStart = y * width;
            for (var x = 0; x < width; x++)
            {
                var index = rowStart + x;
                horizontal[index] = 0;
                vertical[index] = 0;
            }
        }
    }

    private static LocationInGrid PickBestNativePlacement(Grid grid, IntVec2 cellSize, bool rotate, int skipRows)
    {
        var horiz = FilterSkipRows(grid.FindFreeSpaceInGrid(cellSize.X, cellSize.Y, ItemRotation.Horizontal), skipRows);

        LocationInGrid vert = null;
        if (cellSize.X != cellSize.Y && (rotate || horiz == null))
        {
            vert = FilterSkipRows(grid.FindFreeSpaceInGrid(cellSize.Y, cellSize.X, ItemRotation.Vertical), skipRows);
        }

        return ChooseHigherPlacement(horiz, vert);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LocationInGrid ChooseHigherPlacement(LocationInGrid horiz, LocationInGrid vert)
    {
        if (horiz == null) return vert;
        if (vert == null) return horiz;
        return horiz.y <= vert.y ? horiz : vert;
    }

    /// <summary>
    /// Native horizontal stretch returns <c>(GridWidth, 0)</c>, which sits in the skip zone.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LocationInGrid FilterSkipRows(LocationInGrid location, int skipRows)
    {
        if (location == null || (skipRows > 0 && location.y < skipRows))
        {
            return null;
        }

        return location;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LocationInGrid DetermineBestPlacementCustom(Grid grid, IntVec2 cellSize, bool rotate, int skipRows)
    {
        var freeHoriz = FindOptimalItemPlacement(grid, cellSize.X, cellSize.Y, ItemRotation.Horizontal, skipRows);
        LocationInGrid freeVert = null;
        if (cellSize.X != cellSize.Y && (rotate || freeHoriz == null))
        {
            freeVert = FindOptimalItemPlacement(grid, cellSize.Y, cellSize.X, ItemRotation.Vertical, skipRows);
        }

        return ChooseHigherPlacement(freeHoriz, freeVert);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LocationInGrid FindOptimalItemPlacement(Grid grid, int itemWidth, int itemHeight, ItemRotation rotation, int skipRows)
    {
        var useInvertedDimensions = grid.CanStretchHorizontally && grid.GridHeight >= grid.GridWidth + itemWidth;
        var primaryWidth = useInvertedDimensions ? itemWidth : itemHeight;
        var primaryHeight = useInvertedDimensions ? itemHeight : itemWidth;
        var primaryGridWidth = useInvertedDimensions ? grid.GridWidth : grid.GridHeight;
        var primaryGridHeight = useInvertedDimensions ? grid.GridHeight : grid.GridWidth;
        var primaryList = useInvertedDimensions ? grid.Horizontal : grid.Vertical;
        var secondaryList = useInvertedDimensions ? grid.Vertical : grid.Horizontal;

        var locationInGrid = FindSuitableLocation(
            primaryWidth,
            primaryHeight,
            rotation,
            primaryGridWidth,
            primaryGridHeight,
            primaryList,
            secondaryList,
            skipRows,
            useInvertedDimensions);
        if (locationInGrid != null)
            return locationInGrid;

        if (useInvertedDimensions)
            return FilterSkipRows(new LocationInGrid(grid.GridWidth, 0, rotation), skipRows);

        if (grid.CanStretchVertically && (grid.CanStretchHorizontally || itemWidth <= grid.GridWidth))
            return FilterSkipRows(new LocationInGrid(0, grid.GridHeight, rotation), skipRows);

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
        var flipDir = Settings.FlipSortDirection.Value;
        var mainStartIndex = flipDir ? gridMainDimensionSize - itemMainDimensionSize - skipRows : skipRows;
        var mainEndIndex = flipDir ? 0 : gridMainDimensionSize - itemMainDimensionSize;
        var step = flipDir ? -1 : 1;

        for (var mainIndex = mainStartIndex; flipDir ? mainIndex >= mainEndIndex : mainIndex <= mainEndIndex; mainIndex += step)
        {
            var secondaryStart = flipDir ? gridSecondaryDimensionSize - itemSecondaryDimensionSize : 0;
            var secondaryEnd = flipDir ? 0 : gridSecondaryDimensionSize - itemSecondaryDimensionSize;
            var secondaryStep = flipDir ? -1 : 1;

            for (var secondaryIndex = secondaryStart; flipDir ? secondaryIndex >= secondaryEnd : secondaryIndex <= secondaryEnd; secondaryIndex += secondaryStep)
            {
                if (IsSpaceAvailable(mainIndex, secondaryIndex, itemMainDimensionSize, itemSecondaryDimensionSize, gridMainDimensionSize, gridSecondaryDimensionSize, mainDimensionSpaces, secondaryDimensionSpaces, invertDimensions))
                {
                    return new LocationInGrid(invertDimensions ? mainIndex : secondaryIndex, invertDimensions ? secondaryIndex : mainIndex, rotation);
                }
            }
        }

        return null;
    }

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
        var availableSecondarySpace = invertDimensions
            ? secondaryDimensionSpaces[secondaryIndex * gridMainDimensionSize + mainIndex]
            : secondaryDimensionSpaces[mainIndex * gridSecondaryDimensionSize + secondaryIndex];
        if (availableSecondarySpace < itemSecondaryDimensionSize && availableSecondarySpace != -1)
        {
            return false;
        }

        for (var index = secondaryIndex; index < secondaryIndex + itemSecondaryDimensionSize; ++index)
        {
            var availableMainSpace = invertDimensions
                ? mainDimensionSpaces[index * gridMainDimensionSize + mainIndex]
                : mainDimensionSpaces[mainIndex * gridSecondaryDimensionSize + index];
            if (availableMainSpace < itemMainDimensionSize && availableMainSpace != -1)
            {
                return false;
            }
        }

        return true;
    }
}
