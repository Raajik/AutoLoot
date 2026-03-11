namespace AutoLoot.Loot;

/// <summary>
/// A loot rule requirement that checks a string property on an item against a regex pattern.
///
/// In plain English: "Does this item's [property] match this [pattern]?"
///
/// For example:
///   Prop  = PropertyString.Name
///   Value = "Sword|Blade"
///   → Matches any item whose name contains "Sword" or "Blade" (regex OR)
///
/// The Value field is a regular expression pattern, not a plain text string.
/// That means you can use regex features like:
///   |   = OR (e.g. "Sword|Axe" matches either word)
///   ^   = start of string (e.g. "^Fire" matches names starting with "Fire")
///   $   = end of string (e.g. "necklace$" matches names ending with "necklace")
///   .   = any character
///   .{10} = any 10 characters (useful to match "names that are at least 10 chars long")
///
/// Matching is case-insensitive — "sword" matches "Sword", "SWORD", etc.
///
/// IMPORTANT: You must call Initialize() before calling VerifyRequirement().
/// Initialize() compiles the regex pattern into an efficient executable form.
/// </summary>
public class StringRequirement
{
    // The compiled regex, populated by Initialize(). Kept private because the Regex
    // object is created from the public Value string and shouldn't be set externally.
    // Todo: consider making this readonly (requires refactoring to a constructor-based approach)
    Regex? _regex { get; set; }

    /// <summary>
    /// Which string property on the item to match against.
    /// Most commonly PropertyString.Name (the item's display name).
    /// Other options include PropertyString.LongDesc (the item's description).
    /// </summary>
    public PropertyString Prop { get; set; }

    /// <summary>
    /// The regex pattern to match against the item's property value.
    /// Must be valid regex syntax. Call Initialize() after setting this.
    /// </summary>
    public string Value { get; set; } = "";

    /// <summary>
    /// Checks whether the item's string property matches this requirement's pattern.
    ///
    /// Returns false if:
    ///   - The item doesn't have the specified property (e.g. no Name set)
    ///   - The property value doesn't match the compiled regex pattern
    ///
    /// Returns true if the regex finds a match anywhere in the property value.
    ///
    /// NOTE: Initialize() must have been called before this method, or _regex will be null
    /// and this will throw a NullReferenceException.
    /// </summary>
    public bool VerifyRequirement(WorldObject item)
    {
        // Read the item's string property (e.g. its Name)
        var itemValue = item.GetProperty(Prop);

        // If the property doesn't exist on this item, it can't match
        if (itemValue is null)
            return false;

        // Test the property value against the compiled regex
        return _regex.IsMatch(itemValue);
    }

    /// <summary>
    /// Compiles the Value regex pattern so it's ready for fast matching.
    ///
    /// RegexOptions.Compiled   — pre-compiles the regex to native code for faster repeated matching
    /// RegexOptions.IgnoreCase — makes matching case-insensitive ("sword" matches "Sword")
    ///
    /// Call this once after creating or loading the requirement, before calling VerifyRequirement().
    /// </summary>
    public void Initialize()
    {
        _regex = new Regex(Value, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
