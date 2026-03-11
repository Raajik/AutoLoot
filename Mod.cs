namespace AutoLoot;

/// <summary>
/// The entry point for the AutoLoot mod.
///
/// Every ACE mod built on ACE.BaseMod needs exactly one class that inherits from BasicMod.
/// This is the class that ACE's ModManager discovers and loads when it finds the DLL in the
/// Mods folder. Think of it as the "main" of the mod — it just hands off to PatchClass
/// where all the real work happens.
///
/// You rarely need to change this file.
/// </summary>
public class Mod : BasicMod
{
    /// <summary>
    /// Called automatically by ACE when the mod is loaded.
    /// Passes the mod's name (used for logging) and a new PatchClass instance to BasicMod.Setup,
    /// which handles all the Harmony patching and lifecycle wiring.
    /// </summary>
    public Mod() : base() => Setup(nameof(AutoLoot), new PatchClass(this));
}
