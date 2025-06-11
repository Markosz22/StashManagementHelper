using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace StashManagementHelper.Patches;

public class TestPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Item), "CalculateExtraSize");

    [PatchPrefix]
    private static void PatchPrefix(ref Item __instance)
    {
    }

    [PatchPostfix]
    private static void PatchPostfix(ref Item __instance)
    {
    }
}