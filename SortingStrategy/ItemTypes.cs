using System;
using EFT.InventoryLogic;

namespace StashManagementHelper.SortingStrategy;

public static class ItemTypes
{
    public enum ItemType
    {
        Weapons,
        Armor,
        Magazines,
        Ammo,
        Meds,
        Food,
        Drink,
        Melee,
        Mods,
        Grenades,
        Barter,
        Rigs,
        Eyewear,
        Containers,
        Headgear,
        Facecovers,
        Headsets,
        Keys,
        RepairKits,
        NightAndThermalVision,
        SpecialEquipment,
        BallisticPlates,
        Money,
        Backpacks,
        Info,
        HeadgearArmor,
        Armband
    }

    /// <summary>
    /// Matchers in specificity order. <see cref="SpecialWeapon"/> is a <see cref="Weapon"/> and
    /// <see cref="SpecialScope"/> is a <see cref="Mod"/>, so night/thermal must be checked first.
    /// Armor pieces must be checked before the remaining <see cref="ArmoredEquipment"/> catch-all.
    /// </summary>
    public static readonly (ItemType Type, Func<Item, bool> Matches)[] Matchers =
    [
        (ItemType.NightAndThermalVision, item => item is SpecialScope or SpecialWeapon),
        (ItemType.Armor, item => item is Armor),
        (ItemType.BallisticPlates, item => item is ArmorPlate),
        (ItemType.Eyewear, item => item is Visors),
        (ItemType.Headgear, item => item is Headwear),
        (ItemType.HeadgearArmor, item => item is ArmoredEquipment),
        (ItemType.Melee, item => item is Knife),
        (ItemType.Weapons, item => item is Weapon),
        (ItemType.Magazines, item => item is Magazine),
        (ItemType.Ammo, item => item is Ammo or AmmoBox),
        (ItemType.Meds, item => item is Meds),
        (ItemType.Food, item => item is Food),
        (ItemType.Drink, item => item is Drink),
        (ItemType.Mods, item => item is Mod),
        (ItemType.Grenades, item => item is ThrowWeap),
        (ItemType.Barter, item => item is BarterItem),
        (ItemType.Rigs, item => item is Vest),
        (ItemType.Facecovers, item => item is FaceCover),
        (ItemType.Headsets, item => item is Headphones),
        (ItemType.Keys, item => item is Key),
        (ItemType.Containers, item => item is SimpleContainer),
        (ItemType.Backpacks, item => item is Backpack),
        (ItemType.RepairKits, item => item is RepairKit),
        (ItemType.SpecialEquipment, item => item is SpecItem),
        (ItemType.Money, item => item is Money),
        (ItemType.Info, item => item is Info),
        (ItemType.Armband, item => item is ArmBand),
    ];
}
