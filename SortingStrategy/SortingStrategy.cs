using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using EFT.Trading;
using Newtonsoft.Json;
using StashManagementHelper.Configuration;
using StashManagementHelper.Helpers;

namespace StashManagementHelper.SortingStrategy;

public static class SortingStrategy
{
    private static bool inSynch = false;
    private static DateTime lastConfigFileWriteTime = DateTime.MinValue;
    private static readonly string configPath;

    private static List<string> SortOrder { get; set; } = ["ContainerSize", "CellSize", "ItemType", "Weight", "Value"];

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

    private readonly struct SortKey
    {
        public readonly float ContainerSize;
        public readonly int ItemType;
        public readonly int CellSize;
        public readonly float Weight;
        public readonly double Value;

        public SortKey(float containerSize, int itemType, int cellSize, float weight, double value)
        {
            ContainerSize = containerSize;
            ItemType = itemType;
            CellSize = cellSize;
            Weight = weight;
            Value = value;
        }
    }

    public static List<Item> Sort(this IEnumerable<Item> items)
        => Settings.SortingStrategy.Value == SortEnum.Custom
            ? items.SortByCustomOrder()
            : [.. items];

    private static List<Item> SortByCustomOrder(this IEnumerable<Item> items)
    {
        LoadSortOrder();

        var source = items as IList<Item> ?? items.ToList();
        if (source.Count <= 1)
        {
            return [.. source];
        }

        var criteria = new List<(string Type, bool Descending)>();
        foreach (var type in SortOrder)
        {
            var option = Settings.GetSortOption(type);
            if (!option.HasFlag(SortOptions.Enabled))
            {
                continue;
            }

            criteria.Add((type, option.HasFlag(SortOptions.Descending)));
        }

        if (criteria.Count == 0)
        {
            return [.. source];
        }

        var needValue = criteria.Exists(c => c.Type == "Value");
        List<(Trader Trader, SupplyData Supply)> traders = null;
        if (needValue)
        {
            TraderExtensions.EnsureSupplyDataUpdated();
            traders = SnapshotTraders();
        }

        var keyed = new (Item Item, SortKey Key)[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            keyed[i] = (item, CreateSortKey(item, traders));
        }

        IEnumerable<(Item Item, SortKey Key)> ordered = keyed;
        for (var i = 0; i < criteria.Count; i++)
        {
            var (type, descending) = criteria[i];
            ordered = ApplyCriterion(ordered, type, descending, isPrimary: i == 0);
        }

        return [.. ordered.Select(x => x.Item)];
    }

    private static SortKey CreateSortKey(Item item, List<(Trader Trader, SupplyData Supply)> traders)
    {
        var size = item.CalculateCellSize();
        return new SortKey(
            GetContainerSize(item),
            GetItemType(item),
            size.X * size.Y,
            item.TotalWeight,
            traders != null ? GetItemValue(item, traders) : 0d);
    }

    private static IOrderedEnumerable<T> Apply<T, TKey>(
        IEnumerable<T> source,
        Func<T, TKey> selector,
        bool descending,
        bool isPrimary)
        where TKey : IComparable<TKey>
    {
        if (isPrimary)
        {
            return descending ? source.OrderByDescending(selector) : source.OrderBy(selector);
        }

        var ordered = (IOrderedEnumerable<T>)source;
        return descending ? ordered.ThenByDescending(selector) : ordered.ThenBy(selector);
    }

    private static IOrderedEnumerable<(Item Item, SortKey Key)> ApplyCriterion(
        IEnumerable<(Item Item, SortKey Key)> source,
        string sortType,
        bool descending,
        bool isPrimary)
    {
        return sortType switch
        {
            "ContainerSize" => Apply(source, x => x.Key.ContainerSize, descending, isPrimary),
            "ItemType" => Apply(source, x => x.Key.ItemType, descending, isPrimary),
            "CellSize" => Apply(source, x => x.Key.CellSize, descending, isPrimary),
            "Weight" => Apply(source, x => x.Key.Weight, descending, isPrimary),
            "Value" => Apply(source, x => x.Key.Value, descending, isPrimary),
            _ => Apply(source, _ => 0, descending, isPrimary),
        };
    }

    private static int GetItemType(Item item)
    {
        foreach (var (type, matches) in ItemTypes.Matchers)
        {
            if (!matches(item))
            {
                continue;
            }

            var index = ItemTypeOrder.IndexOf(type);
            if (index < 0)
            {
                ItemManager.Logger.LogDebug($"Item type {type} is not in the sort order list: {item.GetType().Name}");
                return ItemTypeOrder.Count + 100;
            }

            return index;
        }

        ItemManager.Logger.LogDebug($"Unknown item type: {item.GetType().Name}");
        return ItemTypeOrder.Count + 100;
    }

    private static float GetContainerSize(Item item)
    {
        var attr = item.Attributes.FirstOrDefault(y => y.Id.Equals(EItemAttributeId.ContainerSize));
        if (attr?.Base == null)
        {
            return -1f;
        }

        return attr.Base.Invoke();
    }

    private static List<(Trader Trader, SupplyData Supply)> SnapshotTraders()
    {
        var snapshot = new List<(Trader Trader, SupplyData Supply)>();
        foreach (var trader in TraderExtensions.Traders)
        {
            snapshot.Add((trader, trader.GetSupplyData()));
        }

        return snapshot;
    }

    /// <summary>
    /// Highest sell-to-trader value in roubles.
    /// </summary>
    private static double GetItemValue(Item item, List<(Trader Trader, SupplyData Supply)> traders)
    {
        var best = 0d;

        foreach (var (trader, supply) in traders)
        {
            var price = trader.GetUserItemPrice(item);
            if (!price.HasValue)
                continue;

            var pd = price.Value;
            var currencyId = pd.CurrencyId.HasValue ? (string)pd.CurrencyId.Value : null;
            var course = currencyId != null && supply?.CurrencyCourses.TryGetValue(currencyId, out var c) == true ? c : 1.0;

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

        var mapItemTypes = ItemTypes.Matchers.Select(m => m.Type).ToList();
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