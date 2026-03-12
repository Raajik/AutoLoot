namespace AutoLoot;

/// <summary>
/// The main patch class for the AutoLoot mod.
///
/// In ACE.BaseMod, PatchClass is where all your mod logic, Harmony patches, and
/// in-game commands live. BasicPatch&lt;Settings&gt; is the base class that handles
/// loading/saving Settings.json, hot-reloading, and mod lifecycle events.
///
/// The [HarmonyPatch] attribute on the class tells Harmony to scan this class for
/// any methods decorated with [HarmonyPrefix] / [HarmonyPostfix] etc. and automatically
/// apply them when the mod loads.
/// </summary>
[HarmonyPatch]
public class PatchClass : BasicPatch<Settings>
{
    // Patches and command handlers live in AutoLoot.cs.
    // See AutoLoot.cs for the main /autoloot command and the GenerateTreasure patch.

    /// <summary>
    /// Constructor. Called immediately when ACE loads the mod.
    ///
    /// We initialize Settings here rather than relying on OnWorldOpen, because
    /// OnWorldOpen doesn't reliably fire before players issue their first command.
    /// The null-coalescing fallback (new Settings()) uses all the defaults defined
    /// in Settings.cs, so the mod still works even if Settings.json doesn't exist yet.
    /// </summary>
    public PatchClass(BasicMod mod, string settingsName = "Settings.json") : base(mod, settingsName)
    {
        try { Settings ??= SettingsContainer.Settings; }
        catch { Settings ??= new Settings(); }
    }

    /// <summary>
    /// Called each time the world becomes active (including after hot-reloads).
    /// Re-assigns Settings so any edits to Settings.json are picked up without a restart.
    /// </summary>
    public override async Task OnWorldOpen()
    {
        try { Settings = SettingsContainer.Settings; }
        catch { Settings ??= new Settings(); }
    }
}

/// <summary>
/// Defines the types of comparisons that can be used in loot rules.
///
/// When you write a loot rule like "armor level is greater than 140", you choose
/// a CompareType to describe how the item's actual value should be measured against
/// your target value. These are used by ValueRequirement.VerifyRequirement().
///
/// Think of these like the math operators you'd write:
///   GreaterThan       = item value >  target
///   GreaterThanEqual  = item value >= target
///   LessThan          = item value &lt;  target
///   LessThanEqual     = item value &lt;= target
///   Equal             = item value == target
///   NotEqual          = item value != target (treats missing values as 0)
///   NotEqualNotExist  = item value != target OR the property doesn't exist at all
///   Exist             = the property is present (non-null) on the item
///   NotExist          = the property is NOT present on the item
///   HasBits           = item value has ALL the specified bit flags set
///   NotHasBits        = item value has NONE of the specified bit flags set
/// </summary>
public enum CompareType
{
    GreaterThan,
    LessThanEqual,
    LessThan,
    GreaterThanEqual,
    NotEqual,
    NotEqualNotExist,
    Equal,
    NotExist,
    Exist,
    NotHasBits,
    HasBits,
}
