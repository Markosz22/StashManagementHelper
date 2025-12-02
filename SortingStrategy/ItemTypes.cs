using EFT.InventoryLogic;
using System;
using System.Collections.Generic;

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

    public static readonly Dictionary<ItemType, Func<Item, bool>> ItemTypeMap = new()
    {
        {ItemType.Weapons, item => item is Weapon},
        {ItemType.Armor, item => item is ArmorItemClass},
        {ItemType.Magazines, item => item is MagazineItemClass},
        {ItemType.Ammo, item => item is AmmoItemClass or AmmoBox},
        {ItemType.Meds, item => item is MedsItemClass},
        {ItemType.Food, item => item is FoodItemClass},
        {ItemType.Drink, item => item is DrinkItemClass},
        {ItemType.Melee, item => item is KnifeItemClass},
        {ItemType.Mods, item => item is Mod},
        {ItemType.Grenades, item => item is ThrowWeapItemClass},
        {ItemType.Barter, item => item is BarterItemItemClass},
        {ItemType.Rigs, item => item is VestItemClass},
        {ItemType.Eyewear, item => item is VisorsItemClass},
        {ItemType.Headgear, item => item is HeadwearItemClass},
        {ItemType.Facecovers, item => item is FaceCoverItemClass},
        {ItemType.Headsets, item => item is HeadphonesItemClass},
        {ItemType.Keys, item => item is KeyItemClass},
        {ItemType.Containers, item => item is SimpleContainerItemClass },
        {ItemType.Backpacks, item => item is BackpackItemClass },
        {ItemType.RepairKits, item => item is RepairKitsItemClass},
        {ItemType.SpecialEquipment, item => item is SpecItemItemClass},
        {ItemType.NightAndThermalVision, item => item is SpecialScopeItemClass or SpecialWeaponItemClass},
        {ItemType.BallisticPlates, item => item is ArmorPlateItemClass},
        {ItemType.Money, item => item is MoneyItemClass},
        {ItemType.Info, item => item is InfoItemClass},
        {ItemType.HeadgearArmor, item => item is ArmoredEquipmentItemClass},
        {ItemType.Armband, item => item is ArmBandItemClass},
    };
}


// TODO: Check TemplateIdToObjectMappingsClass