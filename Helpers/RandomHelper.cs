using AutoLoot.Loot;
using Action = AutoLoot.Loot.Action;

namespace AutoLoot.Helpers;

/// <summary>
/// Helpers for generating random loot profiles and formatting CompareType values.
///
/// This class exists purely for development/testing purposes. It lets the /t2, /t3,
/// and /t4 commands generate large profiles with randomized rules so you can benchmark
/// how fast the loot evaluation engine runs under realistic workloads.
///
/// None of this code affects normal gameplay — it's only called from the dev test commands.
/// </summary>
public static class RandomHelper
{
    /// <summary>
    /// The set of string properties that random string requirements can target.
    /// Currently only PropertyString.Name (the item's display name) is enabled.
    /// PropertyString.LongDesc is commented out because it would make matches too unlikely.
    /// </summary>
    static PropertyString[] stringEnum = new[]
    {
        PropertyString.Name,
        //PropertyString.LongDesc
    };

    /// <summary>
    /// Generates a Profile containing a given number of randomly-constructed loot rules.
    ///
    /// Each rule is given a human-readable Name summarizing its first requirement,
    /// which makes it easier to read benchmark output.
    ///
    /// Parameters:
    ///   rules        — how many Rule objects to create (e.g. 500 for a stress test)
    ///   requirements — how many requirements each rule should have (default: 1)
    ///   valReqs      — if true, each rule gets a random ValueRequirement (numeric property check)
    ///   stringReqs   — if true, each rule gets a random StringRequirement (regex name check)
    ///
    /// Example: RandomProfile(500)           → 500 rules, each with 1 value req + 1 string req
    ///          RandomProfile(500, 1, true, false) → 500 rules, value reqs only
    ///          RandomProfile(500, 1, false)        → 500 rules, string reqs only
    /// </summary>
    public static Profile RandomProfile(int rules, int requirements = 1, bool valReqs = true, bool stringReqs = true)
    {
        Profile profile = new();

        // Get all possible values for each enum so we can pick random ones
        var valEnum = Enum.GetValues<ValueProp>();
        var compareEnum = Enum.GetValues<CompareType>();
        var actionEnum = Enum.GetValues<Action>();

        for (var i = 0; i < rules; i++)
        {
            List<StringRequirement> sReqs = new();
            List<ValueRequirement> vReqs = new();

            for (var j = 0; j < requirements; j++)
            {
                // Optionally add a random numeric property requirement
                if (valReqs)
                    vReqs.Add(new ValueRequirement()
                    {
                        PropKey = ThreadSafeRandom.Next(0, 200),    // random property enum value
                        TargetValue = ThreadSafeRandom.Next(0, 200), // random target number
                        PropType = valEnum.Random(),                  // random property type (int, float, etc.)
                        Type = compareEnum.Random(),                  // random comparison operator
                    });

                // Optionally add a random string/regex requirement
                if (stringReqs)
                {
                    StringRequirement sReq = new()
                    {
                        Prop = stringEnum.Random(),   // which string property to match against
                        Value = randomWords.Random(),  // a random word from words.txt as the regex pattern
                    };
                    sReqs.Add(sReq);
                }
            }

            Rule rule = new()
            {
                ValueReqs = vReqs,
                StringReqs = sReqs,
                Action = actionEnum.Random(), // random loot action (Keep, Sell, Salvage, etc.)
            };

            // Give the rule a descriptive name based on its first requirement
            if (valReqs && vReqs.Count > 0)
            {
                var r = vReqs.FirstOrDefault();
                rule.Name = $"VRule {r.PropType} {r.Type.Friendly()} {r.TargetValue} --> {rule.Action}";
            }
            else if (stringReqs && sReqs.Count > 0)
            {
                var r = sReqs.FirstOrDefault();
                rule.Name = $"SRule {r.Prop} {r.Value} --> {rule.Action}";
            }

            profile.Rules.Add(rule);
        }

        return profile;
    }

    /// <summary>
    /// Converts a CompareType enum value to a short human-readable symbol.
    ///
    /// Used when generating descriptive rule names in RandomProfile.
    /// For example, CompareType.GreaterThanEqual becomes ">=".
    /// </summary>
    public static string Friendly(this CompareType type) => type switch
    {
        CompareType.GreaterThan => ">",
        CompareType.LessThanEqual => "<=",
        CompareType.LessThan => "<",
        CompareType.GreaterThanEqual => ">=",
        CompareType.NotEqual => "!=",
        CompareType.NotEqualNotExist => "!=??",
        CompareType.Equal => "==",
        CompareType.NotExist => "??",
        CompareType.Exist => "?",
        CompareType.NotHasBits => "!B",
        CompareType.HasBits => "B",
        _ => "",
    };

    /// <summary>
    /// A lazily-loaded array of random words read from words.txt in the mod's folder.
    ///
    /// "Lazy" means we don't read the file until the first time we need it.
    /// After that, the words stay in memory (_words) so we don't re-read the file every time.
    ///
    /// words.txt should live at: [ModPath]\words.txt
    /// (e.g. C:\ACE\Mods\AutoLoot\words.txt)
    ///
    /// Each line in the file is treated as one word.
    /// </summary>
    static string[]? _words;
    static string[] randomWords
    {
        get
        {
            if (_words is null) _words = File.ReadAllLines(Path.Combine(Mod.Instance.ModPath, "words.txt"));
            return _words;
        }
    }
}
