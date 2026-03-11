namespace AutoLoot.Loot;

/// <summary>
/// Identifies which "family" of ACE property type a ValueRequirement is checking.
///
/// ACE stores item properties in several different typed dictionaries. To look up a property
/// value on a WorldObject, you need to know not just the property's key (e.g. ArmorLevel = 29)
/// but also which typed getter to call (GetProperty(PropertyInt) vs GetProperty(PropertyFloat), etc.)
///
/// ValueProp tells ValueRequirement which typed lookup to perform.
///
/// Example usage in a loot rule:
///   PropType = ValueProp.PropertyInt
///   PropKey  = (int)PropertyInt.ArmorLevel
///   → calls item.GetProperty((PropertyInt)PropKey) to get the ArmorLevel integer
///
/// Commented-out entries are property types that aren't yet supported as loot rule conditions.
/// PropertyString is handled separately by StringRequirement (regex matching).
/// PropertyPosition doesn't make sense as a numeric comparison.
/// </summary>
public enum ValueProp
{
    //PropertyAttribute,    // Primary attributes (Strength, Endurance, etc.) — not yet supported
    //PropertyAttribute2nd, // Secondary attributes/vitals (Health, Stamina, Mana) — not yet supported
    //PropertyBook,         // Book-related properties — not yet supported

    /// <summary>True/false properties — converted to 1.0 (true) or 0.0 (false) for comparison.</summary>
    PropertyBool,

    /// <summary>Data ID properties — references to game data resources (icons, models, spell IDs).</summary>
    PropertyDataId,

    /// <summary>Floating-point numeric properties (e.g. weapon damage bonus, crit chance).</summary>
    PropertyDouble,

    /// <summary>Instance ID properties — references to specific in-game object instances.</summary>
    PropertyInstanceId,

    /// <summary>32-bit integer properties — the most common type (ArmorLevel, ItemWorkmanship, etc.).</summary>
    PropertyInt,

    /// <summary>64-bit integer properties — used for large numbers like XP values.</summary>
    PropertyInt64,

    //PropertyString,   // String properties are handled by StringRequirement instead
    //PropertyPosition  // Positions can't be meaningfully compared as numbers
}
