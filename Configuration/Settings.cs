using System.Collections.Generic;
using BepInEx.Configuration;
using StashManagementHelper.SortingStrategy;

namespace StashManagementHelper.Configuration;

public static class Settings
{
    private const string SortingSection = "(1) Options";

    private const string SortingStrategySection = "(2) Custom sorting strategy";

    public static bool Sorting = false;

    private static Dictionary<string, SortOptions> _backup;

    public static ConfigEntry<SortEnum> SortingStrategy { get; set; }
    public static ConfigEntry<bool> FoldItems { get; set; }
    public static ConfigEntry<bool> MergeItems { get; set; }
    public static ConfigEntry<bool> RotateItems { get; set; }
    public static ConfigEntry<bool> FlipSortDirection { get; set; }
    public static ConfigEntry<bool> SortTraders { get; set; }
    public static ConfigEntry<bool> SortOtherContainers { get; set; }
    public static ConfigEntry<int> SkipRows { get; set; }

    public static ConfigEntry<SortOptions> ContainerSize { get; set; }
    public static ConfigEntry<SortOptions> CellSize { get; set; }
    public static ConfigEntry<SortOptions> ItemType { get; set; }
    public static ConfigEntry<SortOptions> Weight { get; set; }
    public static ConfigEntry<SortOptions> TraderValue { get; set; }
    public static ConfigEntry<SortOptions> MarketValue { get; set; }

    public static void BackupSortOptions()
    {
        _backup = new Dictionary<string, SortOptions>
        {
            { "ContainerSize", ContainerSize.Value },
            { "CellSize", CellSize.Value },
            { "ItemType", ItemType.Value },
            { "Weight", Weight.Value },
            { "Value", TraderValue.Value },
            { "FleaValue", MarketValue.Value }
        };
    }

    public static void RestoreSortOptions()
    {
        if (_backup == null)
        {
            return;
        }

        ContainerSize.Value = _backup["ContainerSize"];
        CellSize.Value = _backup["CellSize"];
        ItemType.Value = _backup["ItemType"];
        Weight.Value = _backup["Weight"];
        TraderValue.Value = _backup["Value"];
        MarketValue.Value = _backup["FleaValue"];
        _backup = null;
    }

    public static void BindSettings(ConfigFile config)
    {
        // Overall
        SortingStrategy = config.Bind(SortingSection, "Sorting strategy", SortEnum.Custom,
            new ConfigDescription("Changes how items are ordered during sorting.", null, new ConfigurationManagerAttributes { Order = 100 }));

        SortTraders = config.Bind(SortingSection, "Sort traders", false,
            new ConfigDescription("Apply sorting strategy to trader window.", null, new ConfigurationManagerAttributes { Order = 99 }));

        SortOtherContainers = config.Bind(SortingSection, "Sort other containers", false,
            new ConfigDescription("Apply sorting strategy to containers other than Stash.", null, new ConfigurationManagerAttributes { Order = 98 }));

        FoldItems = config.Bind(SortingSection, "Fold items", true,
            new ConfigDescription("Fold items to save space.", null, new ConfigurationManagerAttributes { Order = 97 }));

        MergeItems = config.Bind(SortingSection, "Merge items", true,
            new ConfigDescription("Merge stacking items to save space.", null, new ConfigurationManagerAttributes { Order = 96 }));

        RotateItems = config.Bind(SortingSection, "Rotate items", true,
            new ConfigDescription("Rotate items for best fit.", null, new ConfigurationManagerAttributes { Order = 95 }));

        FlipSortDirection = config.Bind(SortingSection, "Flip sort direction", false,
            new ConfigDescription("Start sorting from bottom up.", null, new ConfigurationManagerAttributes { Order = 94 }));

        SkipRows = config.Bind(SortingSection, "Skip rows", 0,
            new ConfigDescription("Skips the first # rows in stash.", new AcceptableValueRange<int>(0, 10), new ConfigurationManagerAttributes { Order = 93 }));

        // Sorting strategy
        ContainerSize = config.Bind(SortingStrategySection, "Sort by container size", SortOptions.Enabled | SortOptions.Descending,
            new ConfigDescription("Sort by container size", null, new ConfigurationManagerAttributes { Order = 50 }));

        CellSize = config.Bind(SortingStrategySection, "Sort by item size", SortOptions.Enabled | SortOptions.Descending,
            new ConfigDescription("Sort by item size", null, new ConfigurationManagerAttributes { Order = 49 }));

        ItemType = config.Bind(SortingStrategySection, "Sort by item type", SortOptions.None,
            new ConfigDescription("Sort by item type", null, new ConfigurationManagerAttributes { Order = 48 }));

        Weight = config.Bind(SortingStrategySection, "Sort by item weight", SortOptions.None,
            new ConfigDescription("Sort by item weight", null, new ConfigurationManagerAttributes { Order = 47 }));

        MarketValue = config.Bind(SortingStrategySection, "Sort by item flea market value", SortOptions.None,
            new ConfigDescription("Sort by market value", null, new ConfigurationManagerAttributes { Order = 46 }));

        TraderValue = config.Bind(SortingStrategySection, "Sort by item trader value", SortOptions.None,
            new ConfigDescription("Sort by trader value", null, new ConfigurationManagerAttributes { Order = 45 }));
    }

    public static SortOptions GetSortOption(string typeName)
    {
        var option = typeName switch
        {
            "ContainerSize" => ContainerSize.Value,
            "CellSize" => CellSize.Value,
            "ItemType" => ItemType.Value,
            "Weight" => Weight.Value,
            "Value" => TraderValue.Value,
            "FleaValue" => MarketValue.Value,
            _ => throw new System.ArgumentException("Invalid sort type")
        };

        return option;
    }
}