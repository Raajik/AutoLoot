namespace AutoLoot.Helpers;

/// <summary>
/// Extension methods that convert any nullable ACE property value into a nullable double.
///
/// "Why do we need this?"
///
/// ACE stores item properties in many different types: bool, int, long, uint, float, double.
/// When evaluating loot rules, we want to compare all of them the same way (e.g., "> 100").
/// Doing that means they all need to be the same type. We chose double because it can
/// represent every other type without losing precision for the values we care about.
///
/// If the property doesn't exist on an item, GetProperty returns null. We preserve that
/// null so that ValueRequirement can still handle "Exist" / "NotExist" checks.
///
/// Usage example:
///   int? armorLevel = item.GetProperty(PropertyInt.ArmorLevel);
///   double? normalized = armorLevel.Normalize();  // null if no armor level, else a double
/// </summary>
public static class PropertyExtensions
{
    /// <summary>Converts a nullable bool to nullable double. true = 1.0, false = 0.0, null = null.</summary>
    public static double? Normalize(this bool? value) => value.HasValue ? Convert.ToDouble(value.Value) : null;

    /// <summary>Converts a nullable int to nullable double. null stays null.</summary>
    public static double? Normalize(this int? value) => value.HasValue ? Convert.ToDouble(value.Value) : null;

    /// <summary>Converts a nullable long to nullable double. null stays null.</summary>
    public static double? Normalize(this long? value) => value.HasValue ? Convert.ToDouble(value.Value) : null;

    /// <summary>Converts a nullable uint to nullable double. null stays null.</summary>
    public static double? Normalize(this uint? value) => value.HasValue ? Convert.ToDouble(value.Value) : null;

    /// <summary>Converts a nullable float to nullable double. null stays null.</summary>
    public static double? Normalize(this float? value) => value.HasValue ? Convert.ToDouble(value.Value) : null;

    /// <summary>Converts a nullable double to nullable double (identity — here for completeness). null stays null.</summary>
    public static double? Normalize(this double? value) => value.HasValue ? Convert.ToDouble(value.Value) : null;
}
