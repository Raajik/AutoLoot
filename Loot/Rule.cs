namespace AutoLoot.Loot;

/// <summary>
/// A single loot rule — one "if this, then do that" decision in a loot profile.
///
/// A Rule has:
///   - A name (for display in messages and debugging)
///   - An Action (what to do if the rule matches)
///   - Zero or more ValueRequirements (numeric property checks, e.g. ArmorLevel >= 140)
///   - Zero or more StringRequirements (regex name checks, e.g. name contains "Sword")
///
/// ALL requirements must pass for the rule to match (AND logic).
/// If a rule has no requirements at all, it matches everything — useful as a catch-all
/// last rule ("keep anything I haven't decided about yet").
///
/// Evaluation order:
///   Value requirements are checked first, then string requirements.
///   If any single requirement fails, the rest are skipped (short-circuit evaluation).
/// </summary>
public class Rule
{
    /// <summary>Display name for this rule, shown in loot summary messages and debug output.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Reserved for future use — intended to control the order rules are evaluated.
    /// Currently rules are evaluated in list order regardless of this value.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Whether this rule is active. False rules are skipped during evaluation.
    /// Intended for use by a future UI that lets players toggle individual rules on/off
    /// without deleting them.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>What to do with the item if this rule matches (Keep, Salvage, Sell, etc.)</summary>
    public Action Action { get; set; } = Action.Keep;

    /// <summary>
    /// Numeric property requirements (e.g. ArmorLevel >= 140, ItemWorkmanship > 8).
    /// All must pass for the rule to match.
    /// </summary>
    public List<ValueRequirement> ValueReqs { get; set; } = new();

    /// <summary>
    /// Regex string property requirements (e.g. Name matches "Sword|Blade").
    /// All must pass for the rule to match.
    /// </summary>
    public List<StringRequirement> StringReqs { get; set; } = new();

    /// <summary>
    /// Returns true if the given item satisfies ALL of this rule's requirements.
    ///
    /// Checks value requirements first, then string requirements.
    /// Returns false immediately when any requirement fails (no need to check the rest).
    /// Returns true if all requirements pass (or if there are no requirements).
    /// </summary>
    public bool SatisfiesRequirements(WorldObject item)
    {
        if (!SatisfiesValueRequirements(item))
            return false;

        if (!SatisfiesStringRequirements(item))
            return false;

        // Todo: additional requirement types (object class, spell count, etc.) could be added here

        return true;
    }

    /// <summary>
    /// Returns true if the item passes all numeric (value) requirements.
    /// Returns true immediately if there are no value requirements.
    /// </summary>
    public bool SatisfiesValueRequirements(WorldObject item)
    {
        foreach (var req in ValueReqs)
        {
            if (!req.VerifyRequirement(item))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true if the item passes all string (regex) requirements.
    /// Returns true immediately if there are no string requirements.
    /// </summary>
    public bool SatisfiesStringRequirements(WorldObject item)
    {
        foreach (var req in StringReqs)
        {
            if (!req.VerifyRequirement(item))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Prepares the rule's requirements for use. Must be called before evaluating items.
    ///
    /// Currently this compiles the Regex patterns in all StringRequirements.
    /// Compiled regexes are much faster to execute repeatedly than interpreted ones,
    /// which matters when evaluating hundreds of items against hundreds of rules.
    /// </summary>
    public void Initialize()
    {
        foreach (var req in StringReqs)
            req.Initialize();
    }
}
