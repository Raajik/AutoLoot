namespace AutoLoot.Helpers;

/// <summary>
/// Extension methods for WorldObject — cloak/proc spell configuration.
///
/// "Cloaks" in Asheron's Call are special wearable items that can trigger spells
/// when the player takes damage. These helpers make it easier to configure a cloak's
/// proc spell without having to set all three related properties individually.
/// </summary>
public static class Helpers
{
    /// <summary>
    /// Configures a cloak WorldObject to proc (trigger) a specific spell when hit.
    ///
    /// In ACE, cloaks have three related properties:
    ///   ProcSpell            — the SpellId to cast
    ///   ProcSpellSelfTargeted — whether the spell targets the wearer (true) or the attacker (false)
    ///   CloakWeaveProc       — 1 = spell proc, 2 = damage reduction proc
    ///
    /// If spellId is SpellId.Undef (no spell), the cloak is configured for damage reduction instead.
    /// </summary>
    public static void SetCloakSpellProc(this WorldObject wo, SpellId spellId)
    {
        if (spellId != SpellId.Undef)
        {
            wo.ProcSpell = (uint)spellId;
            wo.ProcSpellSelfTargeted = spellId.IsSelfTargeting();
            wo.CloakWeaveProc = 1; // 1 = spell-based proc
        }
        else
        {
            // No spell specified — configure for damage reduction proc instead
            wo.CloakWeaveProc = 2;
        }
    }

    /// <summary>
    /// Returns true if a spell targets the caster (self-targeted) rather than an enemy.
    ///
    /// This is used when setting up cloak procs so we know whether to aim the spell
    /// at the wearer or the attacker.
    ///
    /// Creates a Spell instance to check the IsSelfTargeted property from the dat file.
    ///
    /// Note: An earlier simpler approach (just checking for CloakAllSkill) is commented out
    /// because Aetheria spells require a more general lookup. Creating a Spell object is
    /// slightly more expensive but correct for all spell types.
    /// </summary>
    public static bool IsSelfTargeting(this SpellId spellId) => new Spell(spellId).IsSelfTargeted;
}

/// <summary>
/// Extension methods for flag (bitfield) enums.
///
/// In ACE, many properties are stored as bitfields — a single integer where each bit
/// represents a different boolean flag. For example, ObjectDescriptionFlags has bits
/// for IsPlayer, IsVendor, IsCorpse, etc., all packed into one int.
///
/// C#'s built-in HasFlag() checks if ALL specified bits are set.
/// HasAny() checks if ANY of the specified bits are set (a bitwise OR check).
/// </summary>
public static class FlagExtensions
{
    /// <summary>
    /// Returns true if this enum value shares at least one bit with "other".
    ///
    /// Example:
    ///   var flags = ObjectDescriptionFlags.Player | ObjectDescriptionFlags.Stuck;
    ///   flags.HasAny(ObjectDescriptionFlags.Player)  // true
    ///   flags.HasAny(ObjectDescriptionFlags.Corpse)  // false
    ///
    /// The generic constraint (where TEnum : Enum, IConvertible) ensures this only
    /// works with actual enum types and gives us access to ToInt32() for the bitwise check.
    /// </summary>
    public static bool HasAny<TEnum>(this TEnum me, TEnum other) where TEnum : Enum, IConvertible
        => (me.ToInt32(null) & other.ToInt32(null)) != 0;
}

/// <summary>
/// Extension methods for arrays — random element selection.
/// </summary>
public static class RandomExtensions
{
    // Single shared Random instance. Using a static field means all calls share the same
    // random number generator, which gives better distribution than creating a new Random()
    // every time (multiple instances created at the same millisecond get the same seed).
    private static Random random = new Random();

    /// <summary>
    /// Picks a random element from an array and returns it via the out parameter.
    ///
    /// Returns false (and default value) if the array is null or empty.
    ///
    /// Usage:
    ///   string[] names = { "Alice", "Bob", "Carol" };
    ///   if (names.TryGetRandom(out var picked))
    ///       player.SendMessage($"Picked: {picked}");
    /// </summary>
    public static bool TryGetRandom<T>(this T[] array, out T value)
    {
        value = default;

        if (array == null || array.Length == 0)
            return false;

        value = array[random.Next(array.Length)];
        return true;
    }
}
