using System;
using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using StashManagementHelper.Configuration;
using StashManagementHelper.Helpers;

namespace StashManagementHelper.Patches;

public class SortPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(InteractionsHandlerClass), "Sort");

    [PatchPrefix]
    private static async void PatchPrefix(CompoundItem sortedItem, InventoryController controller, bool simulate)
    {
        try
        {
            if (!Settings.SortOtherContainers.Value && !ItemManager.IsItemInStash(sortedItem))
            {
                ItemManager.Logger.LogDebug($"Skipping custom sorting in {sortedItem.Template._name} - not in hideout stash container");
                return;
            }

            Settings.Sorting = true;

            // Merge separate stacks of the same item
            if (Settings.MergeItems.Value)
            {
                await ItemManager.MergeItems(sortedItem, controller, simulate);
            }

            // Fold weapons to take up less space
            if (Settings.FoldItems.Value)
            {
                await ItemManager.FoldItemsAsync(sortedItem, controller, simulate);
            }
        }
        catch (Exception e)
        {
            ItemManager.Logger.LogError(e.Message);
        }
    }

    [PatchPostfix]
    private static void PatchPostfix()
    {
        Settings.Sorting = false;
        Settings.RestoreSortOptions();
    }
}