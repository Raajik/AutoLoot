namespace AutoLoot;

/// <summary>
/// Stores a single player's AutoLoot preferences so they survive server restarts.
///
/// One JSON file per character is written to LootProfilePath/PlayerData/{guid}.json
/// whenever the player changes any setting. On their next session, AutoLoot loads
/// the file and restores everything exactly as they left it.
/// </summary>
public class PlayerPrefs
{
    /// <summary>
    /// The filenames (not full paths) of every .utl profile the player had active
    /// when they last saved. Full paths can change if the server moves files;
    /// filenames are stable.
    /// </summary>
    public List<string> ActiveProfileNames { get; set; } = new();

    /// <summary>
    /// Whether the player wants a chat summary when items are auto-looted.
    /// Defaults to true — players opt out with /autoloot details.
    /// </summary>
    public bool LootNotifications { get; set; } = true;

    /// <summary>
    /// Whether the VendorTrash ratio filter is active.
    /// Toggled with /autoloot vt.
    /// </summary>
    public bool VendorTrashEnabled { get; set; } = false;

    /// <summary>
    /// The minimum value:burden ratio for the VendorTrash filter.
    /// Items with (sell value / burden) >= this threshold are looted.
    /// Default 50 = "worth at least 50× its weight."
    /// Changed with /autoloot vt [number].
    /// </summary>
    public int VendorTrashRatio { get; set; } = 50;

    /// <summary>
    /// Whether the Unknown Scrolls filter is active.
    /// When on, any scroll dropped by a monster that the player has not yet
    /// learned is automatically looted. Already-known spells are skipped.
    /// Toggled with /autoloot scrolls.
    /// </summary>
    public bool UnknownScrollsEnabled { get; set; } = false;

    /// <summary>
    /// Whether the player wants a server-wide broadcast when they loot a rare item.
    /// Defaults to false — players opt in with /autoloot rares.
    /// </summary>
    public bool BroadcastRares { get; set; } = false;
}
