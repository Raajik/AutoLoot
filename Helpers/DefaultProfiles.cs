using AutoLoot.Lib.VTClassic;

namespace AutoLoot.Helpers;

/// <summary>
/// Generates a set of default server-wide loot profiles on first startup.
///
/// These profiles are written as standard .utl files into the global LootProfiles folder.
/// Players can enable them with /autoloot just like any other profile.
///
/// The files are only created if they don't already exist — so admins can freely
/// replace them with custom ones and they won't be overwritten on the next server restart.
///
/// Profiles created:
///   Weapons.utl   — keeps melee weapons, missile weapons, wands/staves/orbs
///   Armor.utl     — keeps armor
///   Jewelry.utl   — keeps jewelry
///   Valuables.utl — keeps trade notes, mana stones, and gems
/// </summary>
internal static class DefaultProfiles
{
    /// <summary>
    /// Creates default .utl profile files in the given folder, if they don't exist yet.
    /// Safe to call every startup — existing files are never overwritten.
    /// </summary>
    public static void GenerateIfMissing(string globalProfilePath)
    {
        // Make sure the folder exists before trying to write into it
        Directory.CreateDirectory(globalProfilePath);

        // Each entry: (filename, method that builds the profile's rules)
        // Adding more default profiles is as simple as adding a new line here.
        Generate(globalProfilePath, "Weapons.utl",   BuildWeaponsProfile);
        Generate(globalProfilePath, "Armor.utl",     BuildArmorProfile);
        Generate(globalProfilePath, "Jewelry.utl",   BuildJewelryProfile);
        Generate(globalProfilePath, "Valuables.utl", BuildValuablesProfile);
    }

    /// <summary>
    /// Writes a single profile file to disk, skipping it if it already exists.
    ///
    /// Uses the VTClassic cLootRules.Write() method to produce a valid .utl file
    /// that players can load in-game with /autoloot.
    /// </summary>
    static void Generate(string folder, string filename, Func<cLootRules> builder)
    {
        var path = Path.Combine(folder, filename);

        // Don't overwrite — an admin may have already customized this file
        if (File.Exists(path))
            return;

        try
        {
            // Build the profile rules in memory, then write them to disk
            var rules = builder();
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var sw = new CountedStreamWriter(fs);
            rules.Write(sw);

            ModManager.Log($"[AutoLoot] Created default profile: {filename}");
        }
        catch (Exception ex)
        {
            ModManager.Log($"[AutoLoot] Failed to create default profile {filename}: {ex.Message}", ModManager.LogLevel.Error);
        }
    }

    /// <summary>
    /// Helper: creates a single loot rule that keeps all items of a given object class.
    ///
    /// ObjectClass is VTClassic's broad category system (Armor, MeleeWeapon, Jewelry, etc.).
    /// A rule with only an ObjectClass requirement matches every item of that type —
    /// no other conditions like armor level or damage are checked.
    ///
    /// Parameters:
    ///   name     — displayed in the /autoloot loot message when this rule fires
    ///   objClass — which object category to match (e.g. ObjectClass.Armor)
    ///   action   — what to do with matching items (default: Keep)
    /// </summary>
    static cLootItemRule Rule(string name, ObjectClass objClass, eLootAction action = eLootAction.Keep)
    {
        var rule = new cLootItemRule();
        rule.name = name;
        rule.CustomExpression = "";  // no custom scripted expression
        rule.pri = 0;                // priority (lower = checked first; all defaults are equal)
        rule.act = action;

        // ObjectClassE is the VTClassic requirement type that checks an item's broad category
        var req = new ObjectClassE();
        req.vk = objClass;
        rule.IntRules.Add(req);

        return rule;
    }

    // ── Profile builders ─────────────────────────────────────────────────────────

    /// <summary>
    /// Weapons.utl — keeps all melee weapons, missile weapons, and casting implements.
    ///
    /// MeleeWeapon  = swords, axes, maces, daggers, etc.
    /// MissileWeapon = bows, crossbows, thrown weapons
    /// WandStaffOrb  = wands, staves, and orbs (used for magic combat)
    /// </summary>
    static cLootRules BuildWeaponsProfile()
    {
        var profile = new cLootRules();
        profile.Rules.Add(Rule("Keep Melee Weapons",        ObjectClass.MeleeWeapon));
        profile.Rules.Add(Rule("Keep Missile Weapons",      ObjectClass.MissileWeapon));
        profile.Rules.Add(Rule("Keep Wands, Staves, Orbs",  ObjectClass.WandStaffOrb));
        return profile;
    }

    /// <summary>
    /// Armor.utl — keeps all armor pieces.
    ///
    /// Covers everything from leather armor to plate armor to shields.
    /// </summary>
    static cLootRules BuildArmorProfile()
    {
        var profile = new cLootRules();
        profile.Rules.Add(Rule("Keep All Armor", ObjectClass.Armor));
        return profile;
    }

    /// <summary>
    /// Jewelry.utl — keeps all jewelry.
    ///
    /// Covers rings, amulets, and bracelets.
    /// </summary>
    static cLootRules BuildJewelryProfile()
    {
        var profile = new cLootRules();
        profile.Rules.Add(Rule("Keep All Jewelry", ObjectClass.Jewelry));
        return profile;
    }

    /// <summary>
    /// Valuables.utl — keeps high-value currency and material items.
    ///
    /// TradeNote = Asheron's Call's paper currency (worth large amounts of pyreals)
    /// ManaStone = used to fill mana in magic items; also has vendor value
    /// Gem        = precious stones; sold to vendors or used in crafting
    /// </summary>
    static cLootRules BuildValuablesProfile()
    {
        var profile = new cLootRules();
        profile.Rules.Add(Rule("Keep Trade Notes", ObjectClass.TradeNote));
        profile.Rules.Add(Rule("Keep Mana Stones", ObjectClass.ManaStone));
        profile.Rules.Add(Rule("Keep Gems",        ObjectClass.Gem));
        return profile;
    }
}
