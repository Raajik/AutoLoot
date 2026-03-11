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
    /// You can override this in Settings.json to point at any folder on the server machine,
    /// for example a shared network drive or a per-server config directory.
    ///
    /// The /loot command creates this folder automatically if it doesn't exist yet.
    /// </summary>
    public string LootProfilePath { get; set; } = Path.Combine(ModManager.ModPath, "LootProfiles");

    /// <summary>
    /// When true, the mod will look for a subfolder inside LootProfilePath that matches
    /// the player's account username, and search that subfolder for .utl files.
    ///
    /// For example, if a player's username is "Alice", the mod would look in:
    ///   LootProfilePath\Alice\
    ///
    /// This lets you give each player their own private set of loot profiles.
    /// When false, all players share the same profiles from LootProfilePath directly.
    /// </summary>
    public bool LootProfileUseUsername { get; set; } = true;
}
