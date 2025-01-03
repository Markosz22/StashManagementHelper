﻿using System.Collections.Generic;
using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace StashManagementHelper;

public class ItemListSortPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GClass3106), "Sort", [typeof(IEnumerable<Item>)]);

    [PatchPostfix]
    private static void PatchPostfix(ref IEnumerable<Item> __result)
    {
        __result = __result.Sort();
    }
}