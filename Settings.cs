namespace AutoLoot;

/// <summary>
/// Holds the configurable settings for the AutoLoot mod.
///
/// On the first run, ACE.BaseMod automatically creates a Settings.json file next to
/// the mod's DLL using the default values defined here. Server admins can edit that
/// JSON file to change behavior without recompiling the mod.
///
/// Access current settings anywhere in the mod with: PatchClass.Settings
///
/// IMPORTANT: Settings are re-assigned in OnWorldOpen() so that hot-reload picks up
/// any changes made to Settings.json while the server is running.
/// </summary>
public class Settings
{
    /// <summary>
    /// The folder path where .utl loot profile files are stored.
    ///
    /// ModManager.ModPath is the folder where this mod's DLL lives — typically something like:
    ///   C:\ACE\Mods\AutoLoot\
    ///
    /// So the default value resolves to:
    ///   C:\ACE\Mods\AutoLoot\LootProfiles\
    ///
    /// Note: ModManager.ModPath points to the Mods root (C:\ACE\Mods), not this mod's
    /// subfolder, so "AutoLoot" is included explicitly in the path.
    ///
    /// You can override this in Settings.json to point at any folder on the server machine,
    /// for example a shared network drive or a per-server config directory.
    ///
    /// The /autoloot command creates this folder automatically if it doesn't exist yet.
    /// </summary>
    public string LootProfilePath { get; set; } = Path.Combine(ModManager.ModPath, "AutoLoot", "LootProfiles");

    /// <summary>
    /// The filename of the profile that is automatically enabled for a player the first
    /// time they use /autoloot in a session. All other profiles start off.
    ///
    /// Set to an empty string to start all players with no profiles active.
    /// </summary>
    public string DefaultProfile { get; set; } = "PyrealsTradeNotes.utl";
}
