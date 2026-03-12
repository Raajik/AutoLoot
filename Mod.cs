namespace AutoLoot;

/// <summary>
/// The entry point for the AutoLoot mod.
///
/// Every ACE mod built on ACE.BaseMod needs exactly one class that inherits from BasicMod.
/// This is the class that ACE's ModManager discovers and loads when it finds the DLL in the
/// Mods folder. It hands off to PatchClass where all the real logic lives.
///
/// The .utl loot profile files are shipped alongside the DLL in the LootProfiles\ subfolder
/// as part of the build output — no runtime generation needed.
/// </summary>
public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AutoLoot), new PatchClass(this));
}
