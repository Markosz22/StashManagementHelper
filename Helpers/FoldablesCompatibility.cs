using BepInEx.Bootstrap;
using BepInEx.Logging;

namespace StashManagementHelper.Helpers;

/// <summary>
/// Helper class to detect and manage compatibility with the Foldables mod.
/// https://github.com/ozen-m/SPT-Foldables
/// </summary>
public static class FoldablesCompatibility
{
    private const string FoldablesPluginGuid = "com.ozen.foldables";

    /// <summary>
    /// Gets whether the Foldables mod is currently installed and loaded.
    /// </summary>
    public static bool IsFoldablesInstalled { get; private set; }

    /// <summary>
    /// Initialize Foldables detection.
    /// </summary>
    /// <param name="logger">Logger instance for logging detection results.</param>
    public static void Initialize(ManualLogSource logger)
    {
        IsFoldablesInstalled = Chainloader.PluginInfos.ContainsKey(FoldablesPluginGuid);

        if (IsFoldablesInstalled)
        {
            logger.LogInfo($"Foldables mod detected ({FoldablesPluginGuid}).");
        }
    }
}

