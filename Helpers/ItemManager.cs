using System;
using System.Linq;
using BepInEx.Logging;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using StashManagementHelper.Configuration;

namespace StashManagementHelper.Helpers;

public static class ItemManager
{
    public const string StashGridId = "hideout";
    private const string StashTemplateId = "566abbc34bdc2d92178b4576";
    private const int MaxMergeIterations = 4096;

    public static ManualLogSource Logger { get; set; }

    /// <summary>
    /// Folds every foldable item in the container. Skips pinned/locked items to match vanilla Sort.
    /// </summary>
    public static void FoldItems(CompoundItem items, InventoryController inventoryController, bool simulate)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (inventoryController == null) throw new ArgumentNullException(nameof(inventoryController));

        foreach (var grid in items.Grids.OrderBy(g => g.GridHeight * g.GridWidth))
        {
            foreach (var item in grid.Items.ToList())
            {
                if (!IsFreeToManipulate(item)) continue;
                if (!ItemManipulator.CanFold(item, out var foldable) || foldable?.Folded == true) continue;

                if (FoldablesCompatibility.IsFoldablesInstalled
                    && Settings.FoldItemsWithContents != null
                    && !Settings.FoldItemsWithContents.Value
                    && item is CompoundItem compound
                    && compound.Grids?.Any(g => g.Items.Any()) == true)
                {
                    continue;
                }

                Logger.LogDebug($"Folding {item}");

                TryExecuteOperation(inventoryController, ItemManipulator.Fold(foldable, true, simulate));
            }
        }
    }

    /// <summary>
    /// Merge separate stacks of the same item. Skips pinned/locked stacks.
    /// </summary>
    public static void MergeItems(CompoundItem items, InventoryController inventoryController, bool simulate)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (inventoryController == null) throw new ArgumentNullException(nameof(inventoryController));

        foreach (var grid in items.Grids)
        {
            var stackableGroups = grid.Items
                .Where(i => IsFreeToManipulate(i) && i.Owner != null && i.StackObjectsCount < i.StackMaxSize)
                .GroupBy(i => new { i.TemplateId, i.SpawnedInSession })
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in stackableGroups)
            {
                var iterations = 0;
                while (iterations++ < MaxMergeIterations)
                {
                    var stacks = group
                        .Where(i => i.StackObjectsCount > 0 && IsFreeToManipulate(i))
                        .OrderByDescending(i => i.StackObjectsCount)
                        .ToList();

                    if (stacks.Count <= 1) break;

                    var targetStack = stacks.FirstOrDefault(s => s.StackObjectsCount < s.StackMaxSize);
                    if (targetStack == null) break;

                    var sourceStack = stacks.Last();
                    if (sourceStack == targetStack) break;

                    var sourceCount = sourceStack.StackObjectsCount;
                    var targetCount = targetStack.StackObjectsCount;

                    Logger.LogDebug($"Merging {sourceStack} ({sourceCount}) into {targetStack} ({targetCount})");

                    if (!TryExecuteOperation(inventoryController, ItemManipulator.TransferOrMerge(sourceStack, targetStack, inventoryController, simulate)))
                    {
                        Logger.LogDebug("Merge transaction did not succeed; stopping this stack group.");
                        break;
                    }

                    if (sourceStack.StackObjectsCount == sourceCount && targetStack.StackObjectsCount == targetCount)
                    {
                        Logger.LogDebug("Merge reported success but stack counts did not change; stopping this stack group.");
                        break;
                    }
                }

                if (iterations >= MaxMergeIterations)
                {
                    Logger.LogError("Merge abort: iteration limit reached. This would previously freeze the game.");
                }
            }
        }
    }

    /// <summary>
    /// Vanilla Sort only moves items with PinLockState.Free. Fold/merge must use the same rule.
    /// </summary>
    public static bool IsFreeToManipulate(Item item)
    {
        if (item == null || item.PinLockState != EItemPinLockState.Free)
        {
            return false;
        }

        return !ItemManipulator.HasLockedContent(item, out _);
    }

    /// <summary>
    /// Runs an inventory operation on the calling thread. SPT's Execute invokes the
    /// completion callback inline; waiting on an incomplete task on the Unity thread can freeze.
    /// </summary>
    public static bool TryExecuteOperation(InventoryController controller, OperationResult operation)
    {
        if (operation.Failed)
        {
            Logger.LogDebug($"Operation failed locally: {operation.Error}");
            return false;
        }

        var task = controller.TryRunNetworkTransaction(operation);
        if (task == null)
        {
            return false;
        }

        if (!task.IsCompleted)
        {
            Logger.LogError("Inventory transaction did not complete synchronously; skipping to avoid a main-thread freeze.");
            return false;
        }

        if (task.IsFaulted)
        {
            Logger.LogError($"Inventory transaction faulted: {task.Exception?.GetBaseException().Message}");
            return false;
        }

        var result = task.Result;
        if (result == null || result.Failed)
        {
            Logger.LogDebug($"Transaction failed: {result?.Error}");
            return false;
        }

        return true;
    }

    public static bool IsStashGrid(Grid grid)
        => grid != null && string.Equals(grid.ID, StashGridId, StringComparison.Ordinal);

    /// <summary>
    /// True for the stash itself and anything nested inside it (backpacks, rigs, etc.).
    /// </summary>
    public static bool IsItemInStash(Item item)
    {
        if (item is null)
            return false;

        if (IsStashIdentity(item))
            return true;

        foreach (var parent in item.GetAllParentItems())
        {
            if (IsStashIdentity(parent))
                return true;
        }

        return false;
    }

    public static bool IsItemInTrader(Item item) => item?.Owner?.OwnerType == EOwnerType.Trader;

    private static bool IsStashIdentity(Item item)
        => item is Stash
           || string.Equals(item.StringTemplateId, StashTemplateId, StringComparison.Ordinal)
           || string.Equals((item.Owner as ItemController)?.ID, StashGridId, StringComparison.Ordinal);
}
