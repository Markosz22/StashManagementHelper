﻿using System;
using BepInEx.Configuration;

namespace StashManagementHelper;

public static class Settings
{
    private const string SortingSection = "(1) Options";

    private const string SortingStrategySection = "(2) Custom sorting strategy";

    public static bool Sorting = false;

    public static ConfigEntry<SortEnum> SortingStrategy { get; set; }
    public static ConfigEntry<bool> FoldItems { get; set; }
    public static ConfigEntry<bool> MergeItems { get; set; }
    public static ConfigEntry<bool> RotateItems { get; set; }
    public static ConfigEntry<bool> FlipSortDirection { get; set; }
    public static ConfigEntry<int> SkipRows { get; set; }

    public static ConfigEntry<SortOptions> ContainerSize { get; set; }
    public static ConfigEntry<SortOptions> ItemType { get; set; }
    public static ConfigEntry<SortOptions> CellSize { get; set; }

    public static void BindSettings(ConfigFile config)
    {
        // Overall
        SortingStrategy = config.Bind(SortingSection, "Sorting strategy", SortEnum.Custom,
            new ConfigDescription("Changes how items are ordered during sorting.", null, new ConfigurationManagerAttributes { Order = 100 }));

        FoldItems = config.Bind(SortingSection, "Fold items", true,
            new ConfigDescription("Fold items to save space.", null, new ConfigurationManagerAttributes { Order = 99 }));

        MergeItems = config.Bind(SortingSection, "Merge items", true,
            new ConfigDescription("Merge stacking items to save space.", null, new ConfigurationManagerAttributes { Order = 98 }));

        RotateItems = config.Bind(SortingSection, "Rotate items", true,
            new ConfigDescription("Rotate items for best fit.", null, new ConfigurationManagerAttributes { Order = 97 }));

        FlipSortDirection = config.Bind(SortingSection, "Flip sort direction", false,
            new ConfigDescription("Start sorting from bottom up.", null, new ConfigurationManagerAttributes { Order = 96 }));

        SkipRows = config.Bind(SortingSection, "Skip rows", 0,
            new ConfigDescription("Skips the first # rows in stash.", new AcceptableValueRange<int>(0, 10), new ConfigurationManagerAttributes { Order = 95 }));

        // Sorting strategy
        ContainerSize = config.Bind(SortingStrategySection, "Sort by container size", SortOptions.Enabled | SortOptions.Descending,
            new ConfigDescription("Sort by container size", null, new ConfigurationManagerAttributes { Order = 50 }));

        ItemType = config.Bind(SortingStrategySection, "Sort by item type", SortOptions.None,
            new ConfigDescription("Sort by item type", null, new ConfigurationManagerAttributes { Order = 49 }));

        CellSize = config.Bind(SortingStrategySection, "Sort by item size", SortOptions.Enabled | SortOptions.Descending,
            new ConfigDescription("Sort by item size", null, new ConfigurationManagerAttributes { Order = 48 }));
    }

    public static SortOptions GetSortOption(string typeName)
    {
        return typeName switch
        {
            "ContainerSize" => ContainerSize.Value,
            "ItemType" => ItemType.Value,
            "CellSize" => CellSize.Value,
            _ => throw new ArgumentException("Invalid sort type")
        };
    }
}