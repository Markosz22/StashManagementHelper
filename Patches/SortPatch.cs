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
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ItemManipulator), nameof(ItemManipulator.Sort));

    [PatchPrefix]
    private static void PatchPrefix(CompoundItem sortedItem, InventoryController controller, bool simulate)
    {
        try
        {
            // Always apply inside the stash (including nested backpacks/rigs). The toggle only
            // covers containers outside it — equipped gear, inventory, etc.
            if (!Settings.SortOtherContainers.Value && !ItemManager.IsItemInStash(sortedItem))
            {
                ItemManager.Logger.LogDebug($"Skipping custom sorting in {sortedItem.Template._name} - not inside stash");
                return;
            }

            Settings.Sorting = true;

            if (Settings.MergeItems.Value)
            {
                ItemManager.MergeItems(sortedItem, controller, simulate);
            }

            if (Settings.FoldItems.Value)
            {
                ItemManager.FoldItems(sortedItem, controller, simulate);
            }
        }
        catch (Exception e)
        {
            ItemManager.Logger.LogError(e.ToString());
        }
    }

    [PatchPostfix]
    private static void PatchPostfix()
    {
        Settings.Sorting = false;
        Settings.RestoreSortOptions();
    }
}
