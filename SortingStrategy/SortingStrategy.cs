using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using StashManagementHelper.Configuration;
using StashManagementHelper.Helpers;

namespace StashManagementHelper.SortingStrategy;

public static class SortingStrategy
{
    private static bool inSynch = false;
    private static DateTime lastConfigFileWriteTime = DateTime.MinValue;
    private static readonly string configPath;

    private static List<string> SortOrder { get; set; } = new List<string> { "ContainerSize", "CellSize", "ItemType", "Weight", "Value", "FleaValue" };

    private static List<ItemTypes.ItemType> ItemTypeOrder { get; set; } =
    [
        ItemTypes.ItemType.Ammo,
        ItemTypes.ItemType.Grenades,
        ItemTypes.ItemType.Magazines,
        ItemTypes.ItemType.Weapons,
        ItemTypes.ItemType.Headgear,
        ItemTypes.ItemType.HeadgearArmor,
        ItemTypes.ItemType.Facecovers,
        ItemTypes.ItemType.Rigs,
        ItemTypes.ItemType.NightAndThermalVision,
        ItemTypes.ItemType.Eyewear,
        ItemTypes.ItemType.Melee,
        ItemTypes.ItemType.Meds,
        ItemTypes.ItemType.Food,
        ItemTypes.ItemType.Drink,
        ItemTypes.ItemType.Mods,
        ItemTypes.ItemType.RepairKits,
        ItemTypes.ItemType.SpecialEquipment,
        ItemTypes.ItemType.Barter,
        ItemTypes.ItemType.Keys,
        ItemTypes.ItemType.Money,
        ItemTypes.ItemType.Armor,
        ItemTypes.ItemType.Info,
        ItemTypes.ItemType.Backpacks,
        ItemTypes.ItemType.Headsets,
        ItemTypes.ItemType.Containers,
        ItemTypes.ItemType.BallisticPlates,
        ItemTypes.ItemType.Armband
    ];

    static SortingStrategy()
    {
        var dllPath = Assembly.GetExecutingAssembly().Location;
        configPath = Path.Combine(Path.GetDirectoryName(dllPath) ?? string.Empty, "customSortConfig.json");
    }

    public static List<Item> Sort(this IEnumerable<Item> items)
        => Settings.SortingStrategy.Value switch
        {
            SortEnum.Default => [.. items],
            SortEnum.Custom => items.SortByCustomOrder(),
            _ => [.. items]
        };

    private static List<Item> SortByCustomOrder(this IEnumerable<Item> items)
    {
        LoadSortOrder();

        if (SortOrder.Contains("FleaValue") && Settings.GetSortOption("FleaValue").HasFlag(SortOptions.Enabled))
        {
            FleaMarketHelper.StartCachingPricesForItems(items);
        }

        if (SortOrder.Contains("Value") && Settings.GetSortOption("Value").HasFlag(SortOptions.Enabled))
        {
            TraderExtensions.EnsureSupplyDataUpdated();
        }

        var sortFunctions = SortOrder
            .Select(type => (
                GetSortFunction(type),
                Settings.GetSortOption(type).HasFlag(SortOptions.Enabled),
                Settings.GetSortOption(type).HasFlag(SortOptions.Descending)
            ))
            .Where(sf => sf.Item2)
            .Reverse()
            .ToList();

        var orderedItems = items;

        foreach (var (keySelector, _, descending) in sortFunctions)
        {
            orderedItems = descending
                ? orderedItems.OrderByDescending(keySelector)
                : orderedItems.OrderBy(keySelector);
        }

        return [.. orderedItems];
    }

    private static Func<Item, object> GetSortFunction(string sortType)
    {
        return sortType switch
        {
            "ContainerSize" => GetContainerSize,
            "ItemType" => GetItemType,
            "CellSize" => item => item.CalculateCellSize().Length,
            "Weight" => item => item.TotalWeight,
            "Value" => item => GetItemValue(item),
            "FleaValue" => item => FleaMarketHelper.GetItemFleaPrice(item),
            _ => throw new ArgumentException($"Invalid sort type '{sortType}'")
        };
    }

    private static object GetItemType(Item item)
    {
        var itemType = ItemTypes.ItemTypeMap.FirstOrDefault(entry => entry.Value(item)).Key;
        var index = ItemTypeOrder.IndexOf(itemType);

        if (index == -1)
        {
            ItemManager.Logger.LogInfo($"Unknown item type: {item.GetType().Name}");
            return ItemTypeOrder.Count + 100;
        }

        return index;
    }

    private static object GetContainerSize(Item item)
    {
        return item.Attributes.FirstOrDefault(y => y.Id.Equals(EItemAttributeId.ContainerSize))?.Base.Invoke() ?? -1;
    }

    /// <summary>
    /// Retrieves the highest sell-to-trader value (in roubles) for this item.
    /// </summary>
    private static double GetItemValue(Item item)
    {
        var best = 0d;

        // Calculate best price in roubles
        foreach (var trader in TraderExtensions.Traders)
        {
            var price = trader.GetUserItemPrice(item);
            if (!price.HasValue)
                continue;

            var pd = price.Value;
            var supply = trader.GetSupplyData();
            var course = supply?.CurrencyCourses.TryGetValue(pd.CurrencyId.Value, out var c) == true ? c : 1.0;

            var val = pd.Amount * course;
            if (val > best)
                best = val;
        }
        return best;
    }

    private static void SyncItemTypeOrder()
    {
        if (inSynch)
        {
            return;
        }

        var mapItemTypes = ItemTypes.ItemTypeMap.Keys.ToList();
        var missingTypes = mapItemTypes.Except(ItemTypeOrder).ToList();
        var extraTypes = ItemTypeOrder.Except(mapItemTypes).ToList();

        if (missingTypes.Any())
        {
            ItemManager.Logger.LogInfo($"Found {missingTypes.Count} item types in map but not in order: {string.Join(", ", missingTypes)}");

            foreach (var missingType in missingTypes)
            {
                var enumValue = (int)missingType;

                bool inserted = false;
                for (int i = 0; i < ItemTypeOrder.Count - 1; i++)
                {
                    int currentEnumValue = (int)ItemTypeOrder[i];
                    int nextEnumValue = (int)ItemTypeOrder[i + 1];

                    if (enumValue > currentEnumValue && enumValue < nextEnumValue)
                    {
                        ItemTypeOrder.Insert(i + 1, missingType);
                        inserted = true;
                        ItemManager.Logger.LogInfo($"Inserted {missingType} after {ItemTypeOrder[i]}");
                        break;
                    }
                }

                if (!inserted)
                {
                    ItemTypeOrder.Add(missingType);
                    ItemManager.Logger.LogInfo($"Added {missingType} to the end of order list");
                }
            }
        }

        if (extraTypes.Any())
        {
            ItemManager.Logger.LogInfo($"Found {extraTypes.Count} item types in order but not in map: {string.Join(", ", extraTypes)}");
        }

        inSynch = true;
    }

    private static void LoadSortOrder()
    {
        try
        {
            if (!File.Exists(configPath))
            {
                SyncItemTypeOrder();

                var defaultConfig = new Dictionary<string, List<string>>
                {
                    { "sortOrder", SortOrder },
                    { "itemTypeOrder", ItemTypeOrder.Select(itemType => itemType.ToString()).ToList() }
                };
                var defaultJson = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
                File.WriteAllText(configPath, defaultJson);
                lastConfigFileWriteTime = File.GetLastWriteTimeUtc(configPath);
            }
            else
            {
                var currentWriteTime = File.GetLastWriteTimeUtc(configPath);
                if (currentWriteTime > lastConfigFileWriteTime)
                {
                    ItemManager.Logger.LogInfo($"Config file '{configPath}' has changed. Reloading.");
                    var json = File.ReadAllText(configPath);
                    var config = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);

                    if (config.TryGetValue("sortOrder", out var loadedSortOrder))
                    {
                        SortOrder = loadedSortOrder ?? SortOrder;
                    }
                    else
                    {
                        ItemManager.Logger.LogWarning("Config file missing 'sortOrder' key. Using default.");
                    }

                    if (config.TryGetValue("itemTypeOrder", out var loadedItemTypeOrderStrings))
                    {
                        var newItemTypeOrder = new List<ItemTypes.ItemType>();
                        if (loadedItemTypeOrderStrings != null)
                        {
                            foreach (var typeStr in loadedItemTypeOrderStrings)
                            {
                                if (Enum.TryParse<ItemTypes.ItemType>(typeStr, out var itemType))
                                {
                                    newItemTypeOrder.Add(itemType);
                                }
                                else
                                {
                                    ItemManager.Logger.LogWarning($"Skipping invalid item type in config: {typeStr}");
                                }
                            }
                            ItemTypeOrder = newItemTypeOrder;
                        }
                        else
                        {
                            ItemManager.Logger.LogWarning("Config file contains null 'itemTypeOrder'. Using previous or default.");
                        }
                    }
                    else
                    {
                        ItemManager.Logger.LogWarning("Config file missing 'itemTypeOrder' key. Using previous or default.");
                    }

                    inSynch = false;
                    SyncItemTypeOrder();

                    lastConfigFileWriteTime = currentWriteTime;
                }
                else
                {
                    SyncItemTypeOrder();
                }
            }
        }
        catch (Exception e)
        {
            ItemManager.Logger.LogError($"Error loading sort configuration: {e.Message}");
            inSynch = false;
            SyncItemTypeOrder();
        }
    }
}