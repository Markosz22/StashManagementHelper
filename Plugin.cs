using BepInEx;
using StashManagementHelper.Configuration;
using StashManagementHelper.Helpers;
using StashManagementHelper.Patches;

namespace StashManagementHelper;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        ItemManager.Logger = Logger;

        Settings.BindSettings(Config);

        new FindFreeSpacePatch().Enable();
        new ItemListSortPatch().Enable();
        new SortPatch().Enable();
        new GridSortPanelContextPatch().Enable();

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }
}