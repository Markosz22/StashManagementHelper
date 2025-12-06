using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using StashManagementHelper.Configuration;
using StashManagementHelper.Helpers;
using StashManagementHelper.SortingStrategy;

namespace StashManagementHelper.Patches;

public class ItemListSortPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GClass3381), "Sort", [typeof(IEnumerable<Item>)]);

    [PatchPostfix]
    private static void PatchPostfix(ref IEnumerable<Item> __result)
    {
        var shouldSort = Settings.Sorting;

        // Check if we should apply sorting for traders
        if (!shouldSort && Settings.SortTraders.Value)
        {
            var firstItem = __result.FirstOrDefault();
            shouldSort = firstItem != null && ItemManager.IsItemInTrader(firstItem);
        }

        if (shouldSort)
        {
            __result = __result.Sort();
        }
    }
}