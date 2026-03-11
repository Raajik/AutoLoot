namespace AutoLoot.Lib;

/// <summary>
/// Extension methods that bridge VTClassic's property key enums to ACE's WorldObject properties.
///
/// Background:
///   VirindiTank (VT) was a popular third-party plugin for Asheron's Call that used its own
///   set of enums to identify item properties: IntValueKey, DoubleValueKey, BoolValueKey, StringValueKey.
///   ACE uses different enums: PropertyInt, PropertyFloat, PropertyBool, PropertyString.
///
///   The VTClassic library (in Lib/VTClassic/) was ported from VirindiTank's open-source code.
///   It uses VT's property keys internally. These extension methods translate between VT's keys
///   and ACE's WorldObject, so the VTClassic loot engine can read item data from ACE objects.
///
/// How to read the code:
///   Each GetValue* method takes a VT key enum and a default value.
///   Known VT keys that don't map 1:1 to an ACE property are handled with explicit cases.
///   Everything else falls through to a direct GetProperty() call using the numeric key value
///   (which often lines up between VT and ACE since both systems descended from the same game data).
/// </summary>
public static class WorldObjectExtensions
{
    /// <summary>
    /// Gets a floating-point (double) property value from a WorldObject using a VTClassic DoubleValueKey.
    ///
    /// Handles special cases where the VT key name doesn't match the ACE property name exactly.
    /// Falls back to GetProperty((PropertyFloat)key) for any key not explicitly handled.
    ///
    /// Returns defaultValue if the property is not set on the object.
    /// </summary>
    public static double GetValueDouble(this WorldObject wo, DoubleValueKey key, double defaultValue)
    {
        switch (key)
        {
            case DoubleValueKey.SlashProt:      return wo.ArmorModVsSlash.HasValue     ? wo.ArmorModVsSlash.Value     : defaultValue;
            case DoubleValueKey.PierceProt:     return wo.ArmorModVsPierce.HasValue    ? wo.ArmorModVsPierce.Value    : defaultValue;
            case DoubleValueKey.BludgeonProt:   return wo.ArmorModVsBludgeon.HasValue  ? wo.ArmorModVsBludgeon.Value  : defaultValue;
            case DoubleValueKey.AcidProt:       return wo.ArmorModVsAcid.HasValue      ? wo.ArmorModVsAcid.Value      : defaultValue;
            case DoubleValueKey.LightningProt:  return wo.ArmorModVsElectric.HasValue  ? wo.ArmorModVsElectric.Value  : defaultValue;
            case DoubleValueKey.FireProt:       return wo.ArmorModVsFire.HasValue      ? wo.ArmorModVsFire.Value      : defaultValue;
            case DoubleValueKey.ColdProt:       return wo.ArmorModVsCold.HasValue      ? wo.ArmorModVsCold.Value      : defaultValue;
            case DoubleValueKey.ApproachDistance: return wo.UseRadius.HasValue         ? wo.UseRadius.Value           : defaultValue;
            case DoubleValueKey.SalvageWorkmanship: return wo.Workmanship.HasValue     ? wo.Workmanship.Value         : defaultValue;
            case DoubleValueKey.Scale:          return wo.ObjScale.HasValue            ? wo.ObjScale.Value            : defaultValue;
            case DoubleValueKey.Variance:       return wo.DamageVariance.HasValue      ? wo.DamageVariance.Value      : defaultValue;
            case DoubleValueKey.DamageBonus:    return wo.DamageMod.HasValue           ? wo.DamageMod.Value           : defaultValue;
            default:
                // For any key not explicitly listed, try casting the VT enum value directly
                // to ACE's PropertyFloat — many IDs match between the two systems.
                return wo.GetProperty((PropertyFloat)key) ?? defaultValue;
        }
    }

    /// <summary>
    /// Gets a boolean property value from a WorldObject using a VTClassic BoolValueKey.
    ///
    /// Handles special cases:
    ///   Inscribable — directly uses the ACE Inscribable property
    ///   Lockable    — true if the object is DefaultLocked OR currently locked
    ///
    /// Returns defaultValue if the property is not set.
    /// </summary>
    public static bool GetValueBool(this WorldObject wo, BoolValueKey key, bool defaultValue)
    {
        switch (key)
        {
            case BoolValueKey.Inscribable:
                return wo.Inscribable;
            case BoolValueKey.Lockable:
                // An object is "lockable" if it was created locked OR is currently locked
                return wo.DefaultLocked || wo.IsLocked;
            default:
                return wo.GetProperty((PropertyBool)key) ?? defaultValue;
        }
    }

    /// <summary>
    /// Gets a string property value from a WorldObject using a VTClassic StringValueKey.
    ///
    /// Handles special cases:
    ///   SecondaryName — maps to PluralName in ACE (e.g. "Swords" for "Sword")
    ///
    /// Returns defaultValue if the property is not set.
    /// </summary>
    public static string GetValueString(this WorldObject wo, StringValueKey key, string defaultValue)
    {
        switch (key)
        {
            case StringValueKey.SecondaryName:
                return wo.PluralName;
            default:
                return wo.GetProperty((PropertyString)key) ?? defaultValue;
        }
    }

    /// <summary>
    /// Gets an integer property value from a WorldObject using a VTClassic IntValueKey.
    ///
    /// This is the most complex mapper because many VT integer keys have specific names
    /// that correspond to ACE properties with different names.
    ///
    /// Notable mappings:
    ///   Type           → WeenieType (what kind of object: weapon, armor, container, etc.)
    ///   Icon           → IconId (the item's icon graphic ID)
    ///   Category       → ItemType (broad item category)
    ///   SpellCount     → total spells in the spell book
    ///   ActiveSpellCount → currently active enchantments from Life, Creature, and Item magic
    ///   Flags          → ObjectDescriptionFlags (packed bitfield of object traits)
    ///   CreateFlags1/2 → WeenieHeader flags used during object creation
    ///   EquipableSlots / Coverage → ValidLocations (where the item can be equipped)
    ///
    /// Returns defaultValue if the property is not set.
    /// </summary>
    public static int GetValueInt(this WorldObject wo, IntValueKey key, int defaultValue)
    {
        switch (key)
        {
            case IntValueKey.Type:          return (int)wo.WeenieType;
            case IntValueKey.Icon:          return (int)wo.IconId;
            case IntValueKey.Container:     return wo.ContainerId.HasValue ? (int)wo.ContainerId.Value : defaultValue;
            case IntValueKey.Landblock:     return (int)wo.CurrentLandblock.Id.Raw;
            case IntValueKey.ItemSlots:     return wo.ItemCapacity.HasValue ? wo.ItemCapacity.Value : defaultValue;
            case IntValueKey.PackSlots:     return wo.ContainerCapacity.HasValue ? wo.ContainerCapacity.Value : defaultValue;
            case IntValueKey.StackCount:    return wo.StackSize.HasValue ? wo.StackSize.Value : defaultValue;
            case IntValueKey.StackMax:      return wo.MaxStackSize.HasValue ? wo.MaxStackSize.Value : defaultValue;
            case IntValueKey.AssociatedSpell: return wo.SpellDID.HasValue ? (int)wo.SpellDID.Value : defaultValue;
            case IntValueKey.Wielder:       return wo.WielderId.HasValue ? (int)wo.WielderId.Value : defaultValue;
            case IntValueKey.WieldingSlot:
            case IntValueKey.Slot:          return wo.CurrentWieldedLocation.HasValue ? (int)wo.CurrentWieldedLocation.Value : defaultValue;
            case IntValueKey.Monarch:       return wo.MonarchId.HasValue ? (int)wo.MonarchId.Value : defaultValue;
            case IntValueKey.EquipableSlots:
            case IntValueKey.Coverage:      return wo.ValidLocations.HasValue ? (int)wo.ValidLocations.Value : defaultValue;
            case IntValueKey.IconOutline:   return wo.UiEffects.HasValue ? (int)wo.UiEffects.Value : defaultValue;
            case IntValueKey.UsageMask:     return wo.ItemUseable.HasValue ? (int)wo.ItemUseable.Value : defaultValue;
            case IntValueKey.HouseOwner:    return wo.HouseOwner.HasValue ? (int)wo.HouseOwner.Value : defaultValue;
            case IntValueKey.HookMask:      return wo.HookItemType.HasValue ? wo.HookItemType.Value : defaultValue;
            case IntValueKey.HookType:      return wo.HookType.HasValue ? wo.HookType.Value : defaultValue;
            case IntValueKey.Model:         return (int)wo.SetupTableId;
            case IntValueKey.Flags:         return (int)wo.ObjectDescriptionFlags;
            case IntValueKey.CreateFlags1:  return (int)wo.CalculateWeenieHeaderFlag();
            case IntValueKey.CreateFlags2:  return (int)wo.CalculateWeenieHeaderFlag2();
            case IntValueKey.Category:      return (int)wo.ItemType;
            case IntValueKey.Behavior:      return wo.RadarBehavior.HasValue ? (int)wo.RadarBehavior.Value : defaultValue;
            case IntValueKey.MagicDef:      return wo.WeaponMagicDefense.HasValue ? (int)wo.WeaponMagicDefense.Value : defaultValue;
            case IntValueKey.SpellCount:    return wo.Biota.PropertiesSpellBook.Count;
            case IntValueKey.WeapSpeed:     return wo.WeaponTime.HasValue ? wo.WeaponTime.Value : defaultValue;
            case IntValueKey.EquipSkill:    return wo.UseRequiresSkillLevel.HasValue ? wo.UseRequiresSkillLevel.Value : defaultValue;
            case IntValueKey.DamageType:    return (int)wo.W_DamageType;
            case IntValueKey.MaxDamage:     return wo.Damage.HasValue ? wo.Damage.Value : defaultValue;
            case IntValueKey.ItemUsabilityFlags: return wo.ItemUseable.HasValue ? (int)wo.ItemUseable.Value : defaultValue;
            case IntValueKey.PhysicsDataFlags:   return (int)wo.CalculatedPhysicsDescriptionFlag();
            case IntValueKey.ActiveSpellCount:   return wo.GetActiveSpellCount();
            case IntValueKey.IconOverlay:   return wo.IconOverlayId.HasValue ? (int)wo.IconOverlayId.Value : defaultValue;
            case IntValueKey.IconUnderlay:  return wo.IconUnderlayId.HasValue ? (int)wo.IconUnderlayId.Value : defaultValue;
            case IntValueKey.EquippedBy:    return wo.WielderId.HasValue ? (int)wo.WielderId.Value : defaultValue;
            case IntValueKey.LastAttacker:  return wo.CurrentAttacker.HasValue ? (int)wo.CurrentAttacker.Value : defaultValue;
            case IntValueKey.Patron:        return wo.PatronId.HasValue ? (int)wo.PatronId.Value : defaultValue;
            default:
                return wo.GetProperty((PropertyInt)key) ?? defaultValue;
        }
    }

    /// <summary>
    /// Returns true if this WorldObject has the Magical UI effect flag set.
    ///
    /// The Magical flag appears as the glowing blue border on items in-game.
    /// It is set whenever an item has active spells (enchantments or imbued spells).
    /// </summary>
    public static bool IsMagical(this WorldObject wo)
    {
        return (wo.UiEffects & UiEffects.Magical) != 0;
    }

    /// <summary>
    /// Returns all spells in the item's spell book as a list of Spell objects.
    ///
    /// PropertiesSpellBook is a dictionary of SpellId → intensity stored on the item's Biota.
    /// This method converts those IDs to full Spell objects so callers can read spell names,
    /// schools, and other metadata.
    ///
    /// Returns an empty list if the item has no spell book.
    /// </summary>
    public static List<Spell> GetSpells(this WorldObject wo)
    {
        return wo.Biota?.PropertiesSpellBook?.Keys.ToList().Select(s => new Spell(s)).ToList() ?? new List<Spell>();
    }

    /// <summary>
    /// Returns the total number of active enchantments currently on this WorldObject.
    ///
    /// Counts active enchantments from all three magic schools that can affect items:
    ///   Life Magic         — buffs/debuffs to vitals
    ///   Creature Enchantment — buffs/debuffs to attributes and skills
    ///   Item Enchantment   — buffs/debuffs to item properties (e.g. Imperil, Bludgeoning Protection)
    ///
    /// Used by the VTClassic loot engine to support the "ActiveSpellCount" IntValueKey.
    /// </summary>
    public static int GetActiveSpellCount(this WorldObject wo)
    {
        return wo.EnchantmentManager.GetEnchantments(MagicSchool.LifeMagic).Count
             + wo.EnchantmentManager.GetEnchantments(MagicSchool.CreatureEnchantment).Count
             + wo.EnchantmentManager.GetEnchantments(MagicSchool.ItemEnchantment).Count;
    }

    /// <summary>
    /// Determines the VTClassic ObjectClass of a WorldObject by inspecting its type flags.
    ///
    /// ObjectClass is a VTClassic concept that categorizes objects into broad types
    /// (MeleeWeapon, Armor, Gem, Corpse, Player, Vendor, etc.) for use in loot rules.
    ///
    /// How it works:
    ///   ACE stores an object's type in two bitfields:
    ///     ItemType (_type)               — primary item category flags
    ///     ObjectDescriptionFlags (_bools) — additional descriptor flags
    ///   And one weenie header flag (num3) for special cases like scrolls.
    ///
    ///   This method checks each bit in priority order to classify the object.
    ///   Some categories like Player, Vendor, Corpse override the ItemType result
    ///   because those flags live in ObjectDescriptionFlags instead.
    ///
    ///   Monsters vs NPCs: A creature is classified as Npc (not Monster) if it
    ///   doesn't have the "Attackable" bit set in ObjectDescriptionFlags.
    ///
    /// Returns ObjectClass.Unknown if no flag matches.
    /// </summary>
    public static ObjectClass GetObjectClass(this WorldObject wo)
    {
        ObjectClass objectClass = ObjectClass.Unknown;

        // The three flag sources — packed integers where each bit means something different
        int _type  = (int)wo.ItemType;                    // primary item type bits
        int _bools = (int)wo.ObjectDescriptionFlags;      // descriptor bits (player, vendor, corpse, etc.)
        int num3   = (int)wo.CalculateWeenieHeaderFlag(); // weenie header bits (used for scroll detection)

        // ── Pass 1: Determine ObjectClass from ItemType bits ───────────────────────────
        // Each bit in _type corresponds to one item category (melee, armor, jewelry, etc.)
        if      ((_type & 1)          > 0) objectClass = ObjectClass.MeleeWeapon;
        else if ((_type & 2)          > 0) objectClass = ObjectClass.Armor;
        else if ((_type & 4)          > 0) objectClass = ObjectClass.Clothing;
        else if ((_type & 8)          > 0) objectClass = ObjectClass.Jewelry;
        else if ((_type & 16)         > 0) objectClass = ObjectClass.Monster;
        else if ((_type & 32)         > 0) objectClass = ObjectClass.Food;
        else if ((_type & 64)         > 0) objectClass = ObjectClass.Money;
        else if ((_type & 128)        > 0) objectClass = ObjectClass.Misc;
        else if ((_type & 256)        > 0) objectClass = ObjectClass.MissileWeapon;
        else if ((_type & 512)        > 0) objectClass = ObjectClass.Container;
        else if ((_type & 1024)       > 0) objectClass = ObjectClass.Bundle;
        else if ((_type & 2048)       > 0) objectClass = ObjectClass.Gem;
        else if ((_type & 4096)       > 0) objectClass = ObjectClass.SpellComponent;
        else if ((_type & 16384)      > 0) objectClass = ObjectClass.Key;
        else if ((_type & 32768)      > 0) objectClass = ObjectClass.WandStaffOrb;
        else if ((_type & 65536)      > 0) objectClass = ObjectClass.Portal;
        else if ((_type & 262144)     > 0) objectClass = ObjectClass.TradeNote;
        else if ((_type & 524288)     > 0) objectClass = ObjectClass.ManaStone;
        else if ((_type & 1048576)    > 0) objectClass = ObjectClass.Services;
        else if ((_type & 2097152)    > 0) objectClass = ObjectClass.Plant;
        else if ((_type & 4194304)    > 0) objectClass = ObjectClass.BaseCooking;
        else if ((_type & 8388608)    > 0) objectClass = ObjectClass.BaseAlchemy;
        else if ((_type & 16777216)   > 0) objectClass = ObjectClass.BaseFletching;
        else if ((_type & 33554432)   > 0) objectClass = ObjectClass.CraftedCooking;
        else if ((_type & 67108864)   > 0) objectClass = ObjectClass.CraftedAlchemy;
        else if ((_type & 134217728)  > 0) objectClass = ObjectClass.CraftedFletching;
        else if ((_type & 536870912)  > 0) objectClass = ObjectClass.Ust;
        else if ((_type & 1073741824) > 0) objectClass = ObjectClass.Salvage;

        // ── Pass 2: Override with ObjectDescriptionFlags bits ─────────────────────────
        // These flags take priority because Player/Vendor/Corpse etc. can have any ItemType
        if      ((_bools & 8)       > 0) objectClass = ObjectClass.Player;
        else if ((_bools & 512)     > 0) objectClass = ObjectClass.Vendor;
        else if ((_bools & 4096)    > 0) objectClass = ObjectClass.Door;
        else if ((_bools & 8192)    > 0) objectClass = ObjectClass.Corpse;
        else if ((_bools & 16384)   > 0) objectClass = ObjectClass.Lifestone;
        else if ((_bools & 32768)   > 0) objectClass = ObjectClass.Food;
        else if ((_bools & 65536)   > 0) objectClass = ObjectClass.HealingKit;
        else if ((_bools & 131072)  > 0) objectClass = ObjectClass.Lockpick;
        else if ((_bools & 262144)  > 0) objectClass = ObjectClass.Portal;
        else if ((_bools & 8388608) > 0) objectClass = ObjectClass.Foci;
        else if ((_bools & 1)       > 0) objectClass = ObjectClass.Container;

        // ── Pass 3: Special book/journal/sign/scroll detection ────────────────────────
        // Objects with ItemType bit 8192 (readable/book) are further classified by more
        // specific flags in _bools and the weenie header.
        if ((_type & 8192) > 0 && (_bools & 256) > 0 && objectClass == ObjectClass.Unknown)
        {
            if      ((_bools & 2)  > 0) objectClass = ObjectClass.Journal;
            else if ((_bools & 4)  > 0) objectClass = ObjectClass.Sign;
            else if ((_bools & 15) > 0) objectClass = ObjectClass.Book;
        }
        // Scrolls have the book ItemType bit AND the "scroll" weenie header flag
        if ((_type & 8192) > 0 && (num3 & 4194304) > 0) objectClass = ObjectClass.Scroll;

        // ── Pass 4: Monster vs NPC disambiguation ────────────────────────────────────
        // If we classified it as Monster but it doesn't have the Attackable flag, it's an NPC.
        // Also treat creatures with the "NPC" description flag as NPCs regardless.
        if (objectClass == ObjectClass.Monster && (_bools & 16)       == 0) objectClass = ObjectClass.Npc;
        if (objectClass == ObjectClass.Monster && (_bools & 67108864) != 0) objectClass = ObjectClass.Npc;

        return objectClass;
    }
}
