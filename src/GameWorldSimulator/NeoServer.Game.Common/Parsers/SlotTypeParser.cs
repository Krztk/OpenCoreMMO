﻿using NeoServer.Game.Common.Contracts.Items;
using NeoServer.Game.Common.Creatures.Players;
using NeoServer.Game.Common.Item;

namespace NeoServer.Game.Common.Parsers;

public class SlotTypeParser
{
    public static Slot Parse(IItemAttributeList itemAttributes)
    {
        if (itemAttributes is null) return Slot.None;

        var slotType = itemAttributes.GetAttribute(ItemAttribute.BodyPosition);

        if (slotType is null && itemAttributes.TryGetAttribute(ItemAttribute.WeaponType, out var weaponType))
        {
            slotType = weaponType;
        }

        return slotType switch
        {
            "body" => Slot.Body,
            "legs" => Slot.Legs,
            "head" => Slot.Head,
            "feet" => Slot.Feet,
            "shield" => Slot.Right,
            "ammo" => Slot.Ammo,
            "backpack" => Slot.Backpack,
            "ring" => Slot.Ring,
            "necklace" => Slot.Necklace,
            "two-handed" => Slot.TwoHanded,
            "weapon" => Slot.Left,
            "club" => Slot.Left,
            "distance" => Slot.Left,
            "sword" => Slot.Left,
            "axe" => Slot.Left,
            "ammunition" => Slot.Ammo,
            _ => Slot.None
        };
    }
}