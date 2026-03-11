namespace AutoLoot.Loot;

/// <summary>
/// A loot profile — an ordered list of Rules that determine what happens to items.
///
/// Think of a Profile like a list of "if this, then do that" decisions for loot.
/// When a creature dies, each item in its corpse is passed to Profile.Evaluate().
/// The profile goes through its rules in order and returns the Action from the FIRST
/// rule whose requirements the item satisfies.
///
/// If no rule matches, Action.None is returned and the item stays in the corpse.
///
/// How profiles are loaded:
///   - The /loot command loads .utl files using the VTClassic LootCore engine.
///   - This Profile class is the custom/native profile system used by the dev test commands (/t1-t4).
///   - In production, you'd typically use VTClassic's LoadProfile() with a real .utl file.
///
/// Rule priority:
///   Rules are evaluated strictly in list order (index 0 first). The first match wins.
///   More specific rules should come before more general ones, just like if/else chains.
/// </summary>
public class Profile
{
    /// <summary>
    /// The ordered list of loot rules in this profile.
    /// Rules are evaluated from first to last — the first match stops evaluation.
    /// </summary>
    public List<Rule> Rules { get; set; } = new();

    /// <summary>
    /// A hardcoded sample profile used by the /t1 developer test command.
    ///
    /// Contains four example rules showing different types of matching:
    ///
    ///   1. "Characters" — Salvage any item with a name 10+ characters long (.{10} is a regex
    ///      meaning "any 10 characters"). This catches most named loot.
    ///
    ///   2. "Axe" — Salvage items whose name contains "Axe", "Ono", "Silifi", "Tewhate",
    ///      or "Hammer". The | is a regex OR operator.
    ///
    ///   3. "Name is Cloak/Ends with Necklace" — Keep items named "Cloak" or whose name
    ///      ends with "Necklace" (necklace$ means "ends with necklace", case-insensitive).
    ///
    ///   4. "Armor Over 140" — Keep items with ArmorLevel >= 140 (a numeric value rule).
    ///
    /// Note: This is a static field (shared), not a property. Calling Initialize() on it
    /// modifies the shared Regex objects — that's fine for testing but not for production use.
    /// </summary>
    public static Profile SampleProfile = new()
    {
        Rules = new()
        {
            new() {
                Name = "Characters",
                Action = Action.Salvage,
                StringReqs = new()
                {
                    new()
                    {
                        Prop = PropertyString.Name,
                        Value = ".{10}",  // regex: match any name with 10+ characters
                    },
                },
            },
            new() {
                Name = "Axe",
                Action = Action.Salvage,
                StringReqs = new()
                {
                    new()
                    {
                        Prop = PropertyString.Name,
                        Value = "Axe|Ono|Silifi|Tewhate|Hammer",  // regex OR — matches any of these words in the name
                    },
                },
            },
            new() {
                Name = "Name is Cloak/Ends with Necklace",
                Action = Action.Keep,
                StringReqs = new()
                {
                    new()
                    {
                        Prop = PropertyString.Name,
                        Value = "Cloak|necklace$",  // matches "Cloak" anywhere OR "necklace" at the end
                    },
                },
            },
            new() {
                Name = "Armor Over 140",
                Action = Action.Keep,
                ValueReqs = new()
                {
                    new()
                    {
                        Type = CompareType.GreaterThanEqual,   // >=
                        PropType = ValueProp.PropertyInt,      // it's an integer property
                        PropKey = (int)PropertyInt.ArmorLevel, // the ArmorLevel property
                        TargetValue = 140,                     // must be >= 140
                    }
                },
            }
        }
    };

    /// <summary>
    /// Evaluates an item against this profile's rules and returns what action to take.
    ///
    /// Goes through rules in order. Returns the Action from the first rule the item satisfies.
    /// Also sets the "match" parameter to that winning rule so the caller can report its name.
    ///
    /// Returns Action.None if no rule matched (item stays in the corpse).
    ///
    /// Parameters:
    ///   item  — the WorldObject (loot item) to evaluate
    ///   match — set to the winning Rule if one is found; null if no match
    /// </summary>
    public Action Evaluate(WorldObject item, ref Rule match)
    {
        foreach (var rule in Rules)
        {
            if (!rule.SatisfiesRequirements(item))
                continue;

            // First match wins — stop evaluating and return this rule's action
            match = rule;
            return rule.Action;
        }

        // No rule matched
        return Action.None;
    }

    /// <summary>
    /// Prepares all rules in the profile for use.
    ///
    /// Must be called before Evaluate(). Currently this compiles the Regex patterns
    /// inside any StringRequirements so matching is fast at runtime.
    ///
    /// Think of Initialize() like "get ready" — you call it once after loading the profile,
    /// and then the profile is ready to evaluate items.
    /// </summary>
    public void Initialize()
    {
        foreach (var rule in Rules)
            rule.Initialize();
    }
}
