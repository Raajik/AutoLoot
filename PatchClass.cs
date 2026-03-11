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
///
/// To add a patch: write a public static method in this class and decorate it with
/// [HarmonyPostfix] or [HarmonyPrefix] plus [HarmonyPatch(typeof(TargetClass), "MethodName")].
/// </summary>
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    // Patches and command handlers live here or in AutoLoot.cs.
    // See AutoLoot.cs for the main /loot command and the GenerateTreasure patch.
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
