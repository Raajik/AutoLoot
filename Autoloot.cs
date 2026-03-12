using System.Collections.Concurrent;

namespace AutoLoot;

/// <summary>
/// The core AutoLoot class.
///
/// Contains:
///   1. The /autoloot command — lets players view and toggle server-hosted .utl loot
///      profiles on and off, enable/disable all at once, and toggle loot notifications.
///   2. C#-powered special filters: VendorTrash (ratio-based) and Unknown Scrolls.
///   3. A Harmony patch on Creature.GenerateTreasure — fires after any creature dies
///      and auto-loots items from its corpse according to each player's active settings.
///   4. Persistence — each player's settings are saved to a JSON file and restored
///      automatically on their next session.
///
/// How it works end-to-end:
///   1. A player types /autoloot to see available profiles and filters with [ON]/[OFF] status.
///   2. They toggle profiles, VendorTrash, or scrolls on/off. Settings are saved immediately.
///   3. The server's DefaultProfile (PyrealsTradeNotes.utl by default) is automatically
///      enabled the first time a new character uses /autoloot with no saved data.
///   4. When any creature is killed, GenerateTreasure fires. Saved settings are lazy-loaded
///      if this is the player's first kill of the session, so looting works even if the
///      player has never typed /autoloot in the current session.
///   5. Matched items are moved from the corpse to the player's inventory.
///   6. If loot notifications are enabled (the default), a chat summary is sent.
///   7. Rare items trigger a server-wide broadcast.
/// </summary>
[HarmonyPatch]
public class AutoLoot
{
    // ── Per-player state ─────────────────────────────────────────────────────

    /// <summary>
    /// Tracks which LootCore profiles are currently active for each player.
    ///
    /// Outer key = the Player object
    /// Inner key = full file path of the .utl profile
    /// Inner value = the loaded LootCore engine for that profile
    ///
    /// ConcurrentDictionary at both levels is thread-safe — multiple players can kill
    /// creatures simultaneously without corrupting this shared state.
    /// </summary>
    static readonly ConcurrentDictionary<Player, ConcurrentDictionary<string, LootCore>> lootProfiles = new();

    /// <summary>
    /// Tracks whether each player wants loot notifications in chat.
    ///
    /// true  = show a summary message when items are auto-looted (default)
    /// false = loot silently with no chat output
    ///
    /// Toggled with: /autoloot details
    /// </summary>
    static readonly ConcurrentDictionary<Player, bool> lootNotifications = new();

    /// <summary>
    /// Tracks whether the VendorTrash ratio filter is on for each player.
    ///
    /// When enabled, any item whose sell value is at least (VendorTrashRatio × burden)
    /// is automatically looted — a "value per pound" check.
    ///
    /// Toggled with: /autoloot vt
    /// </summary>
    static readonly ConcurrentDictionary<Player, bool> vendorTrashEnabled = new();

    /// <summary>
    /// The minimum value:burden ratio each player has set for VendorTrash.
    ///
    /// Default: 50  — item must be worth at least 50 pyreals per unit of burden.
    /// Changed with: /autoloot vt [number]  (e.g. /autoloot vt 30)
    /// </summary>
    static readonly ConcurrentDictionary<Player, int> vendorTrashRatio = new();

    /// <summary>
    /// Tracks whether the Unknown Scrolls filter is on for each player.
    ///
    /// When enabled, any scroll dropped by a creature that the player has not yet
    /// learned is automatically looted. Already-known spells are skipped.
    ///
    /// Toggled with: /autoloot scrolls
    /// </summary>
    static readonly ConcurrentDictionary<Player, bool> unknownScrolls = new();

    /// <summary>
    /// Tracks whether the player wants a server-wide broadcast when they loot a rare item.
    ///
    /// Defaults to false — rares are still looted normally, just without the announcement.
    /// Players who want to share the moment can opt in with: /autoloot rares
    /// </summary>
    static readonly ConcurrentDictionary<Player, bool> broadcastRares = new();

    /// <summary>
    /// Tracks which characters have already had their saved preferences loaded
    /// this server session, keyed by character GUID.
    ///
    /// This prevents loading the JSON file more than once per session per character.
    /// We use the character's GUID (a stable uint) rather than the Player object
    /// so the check survives across reconnects within the same server session.
    /// </summary>
    static readonly ConcurrentDictionary<uint, bool> loadedPlayers = new();

    /// <summary>
    /// Cached JSON options for writing player prefs files.
    /// Creating a new JsonSerializerOptions on every save is wasteful — reuse one instance.
    /// </summary>
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // ── Inventory helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the player already owns at least one item with the given WCID,
    /// searching both the top-level pack AND one level inside any containers (backpacks).
    ///
    /// Why two levels? In Asheron's Call, inventory is at most two levels deep:
    ///   1. Items worn/held directly on the player (player.Inventory)
    ///   2. Items inside a worn backpack (container.Inventory)
    /// A pack inside a pack is possible in the client UI but the inner pack's contents
    /// are not accessible as a second equipped container — so two levels covers all
    /// items the player can actually access, closing the loophole where stashing a
    /// quest piece in a bag would let AutoLoot farm unlimited duplicates.
    /// </summary>
    static bool PlayerOwnsWcid(Player player, uint wcid)
    {
        foreach (var item in player.Inventory.Values)
        {
            if (item.WeenieClassId == wcid)
                return true;

            // If this item is a container (backpack), check one level inside it
            if (item is Container container)
                foreach (var sub in container.Inventory.Values)
                    if (sub.WeenieClassId == wcid)
                        return true;
        }
        return false;
    }

    // ── Persistence helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Returns the path to the JSON prefs file for a given player.
    /// Also creates the PlayerData directory if it doesn't exist yet.
    ///
    /// Example path: C:\ACE\Mods\AutoLoot\LootProfiles\PlayerData\12345678.json
    /// </summary>
    static string GetPlayerDataPath(Player player)
    {
        var dir = Path.Combine(PatchClass.Settings.LootProfilePath, "PlayerData");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{player.Guid.Full}.json");
    }

    /// <summary>
    /// Loads a player's saved preferences the first time they are encountered this session.
    ///
    /// Silently does nothing if:
    ///   - The player has already been loaded this session (idempotent)
    ///   - No saved file exists yet (new player — defaults apply)
    ///
    /// Call this at the start of any operation that reads or modifies player state,
    /// so prefs are always available regardless of whether the player typed /autoloot.
    /// </summary>
    static void EnsureLoaded(Player player)
    {
        // TryAdd returns false if the key already exists — means we've already loaded
        if (!loadedPlayers.TryAdd(player.Guid.Full, true))
            return;

        var path = GetPlayerDataPath(player);
        if (!File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path);
            var prefs = JsonSerializer.Deserialize<PlayerPrefs>(json);
            if (prefs == null)
                return;

            // Restore .utl profiles — resolve filenames back to full paths
            var allProfiles = Directory.GetFiles(
                PatchClass.Settings.LootProfilePath, "*.utl", SearchOption.TopDirectoryOnly);

            var playerProfileMap = lootProfiles.GetOrAdd(player, _ => new ConcurrentDictionary<string, LootCore>());

            foreach (var name in prefs.ActiveProfileNames)
            {
                var fullPath = allProfiles.FirstOrDefault(p =>
                    Path.GetFileName(p).Equals(name, StringComparison.OrdinalIgnoreCase));

                if (fullPath == null || playerProfileMap.ContainsKey(fullPath))
                    continue;

                var core = new LootCore();
                core.Startup();
                core.LoadProfile(fullPath, false);
                playerProfileMap[fullPath] = core;
            }

            // Restore scalar settings
            lootNotifications[player] = prefs.LootNotifications;
            vendorTrashEnabled[player] = prefs.VendorTrashEnabled;
            vendorTrashRatio[player] = prefs.VendorTrashRatio;
            unknownScrolls[player] = prefs.UnknownScrollsEnabled;
            broadcastRares[player] = prefs.BroadcastRares;
        }
        catch (Exception ex)
        {
            ModManager.Log($"AutoLoot: failed to load prefs for {player.Name}: {ex.Message}", ModManager.LogLevel.Warn);
        }
    }

    /// <summary>
    /// Writes the player's current preferences to their JSON file immediately.
    ///
    /// Call this after any toggle so that settings survive server restarts.
    /// The file is small (a few hundred bytes) so synchronous I/O is fine here.
    /// </summary>
    static void SavePrefs(Player player)
    {
        try
        {
            lootProfiles.TryGetValue(player, out var profileMap);

            var prefs = new PlayerPrefs
            {
                // Store just the filename, not the full path, so saves survive path changes
                ActiveProfileNames = profileMap?.Keys
                    .Select(Path.GetFileName)
                    .Where(n => n != null)
                    .Cast<string>()
                    .ToList() ?? [],

                LootNotifications     = lootNotifications.GetOrAdd(player, true),
                VendorTrashEnabled    = vendorTrashEnabled.GetOrAdd(player, false),
                VendorTrashRatio      = vendorTrashRatio.GetOrAdd(player, 50),
                UnknownScrollsEnabled = unknownScrolls.GetOrAdd(player, false),
                BroadcastRares        = broadcastRares.GetOrAdd(player, false),
            };

            var path = GetPlayerDataPath(player);
            File.WriteAllText(path, JsonSerializer.Serialize(prefs, JsonOptions));
        }
        catch (Exception ex)
        {
            ModManager.Log($"AutoLoot: failed to save prefs for {player.Name}: {ex.Message}", ModManager.LogLevel.Warn);
        }
    }

    // ── Command handler ──────────────────────────────────────────────────────

    /// <summary>
    /// In-game command: /autoloot [on | off | details | vt [ratio] | scrolls | index | name]
    ///
    /// No arguments:      shows all commands + full profile list with [ON]/[OFF] status.
    /// on:                enables every profile, VendorTrash, and Unknown Scrolls at once.
    /// off:               disables everything.
    /// details:           toggles loot notifications in chat on or off.
    /// vt:                toggles VendorTrash on/off (e.g. /autoloot vt)
    /// vt [number]:       sets VendorTrash ratio and enables it (e.g. /autoloot vt 30)
    /// scrolls:           toggles Unknown Scrolls filter on/off.
    /// A number:          toggles the profile at that index on or off.
    /// A name/partial:    toggles the first profile whose filename contains that text.
    ///
    /// The first time a brand-new character uses /autoloot (no saved data), the server's
    /// DefaultProfile (set in Settings.json) is automatically enabled for them.
    /// </summary>
    [CommandHandler("autoloot", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, -1)]
#if REALM
public static void HandleLoadProfile(ISession session, params string[] parameters)
#else
    public static void HandleLoadProfile(Session session, params string[] parameters)
#endif
    {
        var player = session.Player;

        try
        {
            // Ensure the LootProfiles directory exists
            Directory.CreateDirectory(PatchClass.Settings.LootProfilePath);

            // Load saved prefs (no-op after the first call per session)
            EnsureLoaded(player);

            // All profiles are server-hosted — .utl files in LootProfilePath
            var profiles = Directory.GetFiles(
                PatchClass.Settings.LootProfilePath, "*.utl", SearchOption.TopDirectoryOnly);

            var playerProfiles = lootProfiles.GetOrAdd(player, _ => new ConcurrentDictionary<string, LootCore>());

            // First-ever use for this character (no saved file, no prior session activity):
            // auto-enable the server's DefaultProfile so there's something useful running
            bool isNewCharacter = !loadedPlayers.ContainsKey(player.Guid.Full) && !File.Exists(GetPlayerDataPath(player));
            // Note: loadedPlayers.TryAdd already ran in EnsureLoaded above, so check the file instead
            if (playerProfiles.IsEmpty && !File.Exists(Path.Combine(PatchClass.Settings.LootProfilePath, "PlayerData", $"{player.Guid.Full}.json"))
                && !string.IsNullOrEmpty(PatchClass.Settings.DefaultProfile))
            {
                var defaultPath = profiles.FirstOrDefault(p =>
                    Path.GetFileName(p).Equals(PatchClass.Settings.DefaultProfile, StringComparison.OrdinalIgnoreCase));

                if (defaultPath != null)
                {
                    var defaultCore = new LootCore();
                    defaultCore.Startup();
                    defaultCore.LoadProfile(defaultPath, false);
                    playerProfiles[defaultPath] = defaultCore;
                    SavePrefs(player);
                }
            }

            // ── No arguments: show the full menu ────────────────────────────────────

            if (parameters.Length == 0)
            {
                bool notificationsOn = lootNotifications.GetOrAdd(player, true);
                bool vtOn = vendorTrashEnabled.GetOrAdd(player, false);
                int vtRatio = vendorTrashRatio.GetOrAdd(player, 50);
                bool scrollsOn = unknownScrolls.GetOrAdd(player, false);
                bool raresOn = broadcastRares.GetOrAdd(player, false);

                var sb = new StringBuilder("\nAutoLoot Commands:");
                sb.Append("\n  /autoloot <#>          — toggle profile on/off by number");
                sb.Append("\n  /autoloot <name>       — toggle profile on/off by partial name");
                sb.Append("\n  /autoloot on           — enable everything");
                sb.Append("\n  /autoloot off          — disable everything");
                sb.Append($"\n  /autoloot details      — toggle loot notifications [{(notificationsOn ? "ON" : "OFF")}]");
                sb.Append($"\n  /autoloot vt [ratio]   — toggle VendorTrash (current ratio: {vtRatio}:1)");
                sb.Append("\n  /autoloot scrolls      — toggle Unknown Scrolls filter");
                sb.Append("\n  /autoloot rares        — toggle server-wide rare broadcast");

                if (session.AccessLevel >= AccessLevel.Developer)
                {
                    sb.Append("\n\n  [Developer]");
                    sb.Append("\n  /t1   — test the built-in sample profile against your appraised item");
                    sb.Append("\n  /t2   — benchmark 500-rule profile (value + string requirements)");
                    sb.Append("\n  /t3   — benchmark 500-rule profile (value requirements only)");
                    sb.Append("\n  /t4   — benchmark 500-rule profile (string requirements only)");
                }

                if (session.AccessLevel >= AccessLevel.Admin)
                {
                    sb.Append("\n\n  [Admin]");
                    sb.Append("\n  /clean — remove all items from your inventory (for testing cleanup)");
                }

                sb.Append("\n\nProfiles:");
                if (profiles.Length == 0)
                {
                    sb.Append("\n  (no profiles found)");
                }
                else
                {
                    for (var i = 0; i < profiles.Length; i++)
                    {
                        var fi = new FileInfo(profiles[i]);
                        var status = playerProfiles.ContainsKey(profiles[i]) ? "[ON] " : "[OFF]";
                        sb.Append($"\n  {i}) {status} {fi.Name}  ==  {fi.Length / 1024:0}kb");
                    }
                }

                sb.Append($"\n  V) {(vtOn ? "[ON] " : "[OFF]")} VendorTrash {vtRatio}:1  —  loot items worth ≥ {vtRatio}× their burden");
                sb.Append($"\n  S) {(scrollsOn ? "[ON] " : "[OFF]")} Unknown Scrolls  —  loot scrolls you haven't learned yet");
                sb.Append($"\n  R) {(raresOn ? "[ON] " : "[OFF]")} Rare Broadcast  —  announce rare loots server-wide");

                player.SendMessage(sb.ToString());
                return;
            }

            var arg = parameters[0];

            // ── /autoloot on — enable everything ────────────────────────────────────

            if (arg.Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                int enabled = 0;
                foreach (var path in profiles)
                {
                    if (!playerProfiles.ContainsKey(path))
                    {
                        var core = new LootCore();
                        core.Startup();
                        core.LoadProfile(path, false);
                        playerProfiles[path] = core;
                        enabled++;
                    }
                }
                if (!vendorTrashEnabled.GetOrAdd(player, false)) { vendorTrashEnabled[player] = true; enabled++; }
                if (!unknownScrolls.GetOrAdd(player, false)) { unknownScrolls[player] = true; enabled++; }
                SavePrefs(player);
                player.SendMessage(enabled > 0
                    ? $"Enabled {enabled} profile(s). Type /autoloot to see the list."
                    : "Everything is already enabled.");
                return;
            }

            // ── /autoloot off — disable everything ──────────────────────────────────

            if (arg.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                int disabled = 0;
                foreach (var key in playerProfiles.Keys.ToList())
                {
                    if (playerProfiles.TryRemove(key, out var core))
                    {
                        core.Shutdown();
                        disabled++;
                    }
                }
                if (vendorTrashEnabled.GetOrAdd(player, false)) { vendorTrashEnabled[player] = false; disabled++; }
                if (unknownScrolls.GetOrAdd(player, false)) { unknownScrolls[player] = false; disabled++; }
                SavePrefs(player);
                player.SendMessage(disabled > 0
                    ? $"Disabled {disabled} profile(s)."
                    : "Nothing was active.");
                return;
            }

            // ── /autoloot details — toggle loot notifications ────────────────────────

            if (arg.Equals("details", StringComparison.OrdinalIgnoreCase))
            {
                bool current = lootNotifications.GetOrAdd(player, true);
                lootNotifications[player] = !current;
                SavePrefs(player);
                player.SendMessage($"Loot notifications: {(!current ? "ON" : "OFF")}");
                return;
            }

            // ── /autoloot vt [ratio] — toggle VendorTrash, optionally set ratio ──────
            //
            // /autoloot vt         → toggle on/off (keeps current ratio)
            // /autoloot vt 30      → set ratio to 30 and enable
            // /autoloot vt 0       → disable (ratio 0 = off)

            if (arg.Equals("vt", StringComparison.OrdinalIgnoreCase))
            {
                // Optional second parameter: the new ratio
                if (parameters.Length > 1 && int.TryParse(parameters[1], out int newRatio) && newRatio > 0)
                {
                    vendorTrashRatio[player] = newRatio;
                    vendorTrashEnabled[player] = true;
                    SavePrefs(player);
                    player.SendMessage($"VendorTrash ON at {newRatio}:1 (loot items worth ≥ {newRatio}× their burden)");
                }
                else
                {
                    bool current = vendorTrashEnabled.GetOrAdd(player, false);
                    vendorTrashEnabled[player] = !current;
                    int ratio = vendorTrashRatio.GetOrAdd(player, 50);
                    SavePrefs(player);
                    player.SendMessage(!current
                        ? $"VendorTrash ON at {ratio}:1 (loot items worth ≥ {ratio}× their burden)"
                        : "VendorTrash OFF");
                }
                return;
            }

            // ── /autoloot scrolls — toggle Unknown Scrolls filter ────────────────────

            if (arg.Equals("scrolls", StringComparison.OrdinalIgnoreCase))
            {
                bool current = unknownScrolls.GetOrAdd(player, false);
                unknownScrolls[player] = !current;
                SavePrefs(player);
                player.SendMessage($"Unknown Scrolls: {(!current ? "ON" : "OFF")} (loot scrolls you haven't learned)");
                return;
            }

            // ── /autoloot rares — toggle server-wide rare broadcast ──────────────────

            if (arg.Equals("rares", StringComparison.OrdinalIgnoreCase))
            {
                bool current = broadcastRares.GetOrAdd(player, false);
                broadcastRares[player] = !current;
                SavePrefs(player);
                player.SendMessage($"Rare Broadcast: {(!current ? "ON" : "OFF")} (server-wide message when you loot a rare)");
                return;
            }

            // ── Toggle by index or partial name ──────────────────────────────────────

            string? selected = null;
            if (uint.TryParse(arg, out var index) && index < profiles.Length)
            {
                selected = profiles[index];
            }
            else
            {
                var joined = string.Join(' ', parameters);
                selected = profiles.FirstOrDefault(x => x.Contains(joined, StringComparison.OrdinalIgnoreCase));
            }

            if (string.IsNullOrEmpty(selected) || !File.Exists(selected))
            {
                player.SendMessage("No matching profile found. Type /autoloot to see the list.");
                return;
            }

            var displayName = Path.GetFileNameWithoutExtension(selected);

            if (playerProfiles.TryRemove(selected, out var existingCore))
            {
                existingCore.Shutdown();
                SavePrefs(player);
                player.SendMessage($"[OFF] {displayName}");
            }
            else
            {
                var newCore = new LootCore();
                newCore.Startup();
                newCore.LoadProfile(selected, false);
                playerProfiles[selected] = newCore;
                SavePrefs(player);
                player.SendMessage($"[ON]  {displayName}");
            }
        }
        catch (Exception ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            player.SendMessage("Failed to load loot profile!");
        }
    }


    #region Patches

    /// <summary>
    /// Harmony Postfix patch on Creature.GenerateTreasure.
    ///
    /// Runs AFTER the original method, so the corpse is already filled with loot.
    /// Processing order for each corpse:
    ///   1. .utl profile pass — first matching profile claims the item
    ///   2. VendorTrash pass  — unclaimed items that meet the value:burden ratio
    ///   3. Unknown Scrolls   — unclaimed scrolls the player hasn't learned yet
    ///
    /// After all passes:
    ///   - Rare items trigger a server-wide broadcast
    ///   - If notifications are on, a loot summary is sent to the player
    ///   - Learnable scrolls get a separate [!] line in the summary
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Creature), nameof(Creature.GenerateTreasure), new Type[] { typeof(DamageHistoryInfo), typeof(Corpse) })]
    public static void PostGenerateTreasure(DamageHistoryInfo killer, Corpse corpse, ref Creature __instance, ref List<WorldObject> __result)
    {
        // Only auto-loot for players (handles pet kills too via TryGetPetOwnerOrAttacker)
        if (killer.TryGetPetOwnerOrAttacker() is not Player player)
            return;

        // Lazy-load saved preferences on first kill of the session
        EnsureLoaded(player);

        // Check what the player has active — bail early if nothing is on
        lootProfiles.TryGetValue(player, out var playerProfiles);
        bool hasProfiles    = playerProfiles != null && !playerProfiles.IsEmpty;
        bool hasVT          = vendorTrashEnabled.GetOrAdd(player, false);
        bool hasScrolls     = unknownScrolls.GetOrAdd(player, false);

        if (!hasProfiles && !hasVT && !hasScrolls)
            return;

        try
        {
            // Snapshot the corpse inventory — items will be removed as we loot them
            var items = corpse.Inventory.Values.ToList();
            var activeProfiles = hasProfiles ? playerProfiles!.Values.ToList() : new List<LootCore>();

            // lootedItems:   name → total quantity, for the chat summary
            // lootedSet:     the WorldObject instances we actually moved, so later passes don't double-claim
            // lootedScrolls: names of learnable scrolls, for the special [!] notification line
            // lootedVTNames: names of items looted by VendorTrash, so they can be prefixed with [$]
            var lootedItems   = new Dictionary<string, int>();
            var lootedSet     = new HashSet<WorldObject>();
            var lootedScrolls = new List<string>();
            var lootedVTNames = new HashSet<string>();

            // ── Pass 1: .utl profiles ────────────────────────────────────────────────
            //
            // Each item is checked against every active profile in order.
            // The first profile that matches claims the item; we stop checking after that.

            // Pre-build the no-duplicate filter from Settings — avoids re-reading Settings per item.
            // An item is blocked if its name matches a configured fragment AND the player already
            // owns one of the same WCID. This prevents AutoLoot from stockpiling quest turn-in items
            // (Pincers, Tusks, Matron parts, etc.) ahead of quest timers — without this guard, a
            // player could farm unlimited pieces and bypass the intended cooldown indefinitely.
            var noDupPatterns = PatchClass.Settings?.NoDuplicateNames ?? [];

            foreach (var item in items)
            {
                // Skip quest turn-in items the player already has — prevents timer bypass farming
                if (noDupPatterns.Count > 0 &&
                    noDupPatterns.Any(p => item.Name?.Contains(p, StringComparison.OrdinalIgnoreCase) == true) &&
                    PlayerOwnsWcid(player, item.WeenieClassId))
                {
                    continue;
                }

                foreach (var profile in activeProfiles)
                {
                    var lootAction = profile.GetLootDecision(item, player);
                    if (lootAction.IsNoLoot)
                        continue;

                    // Remove from corpse first so no ghost item is left behind
                    if (!corpse.TryRemoveFromInventory(item.Guid, out var removed))
                        break;

                    var qty = removed.StackSize ?? 1;
                    lootedItems.TryGetValue(removed.Name, out var existing);
                    lootedItems[removed.Name] = existing + qty;

                    lootedSet.Add(removed);
                    player.TryCreateInInventoryWithNetworking(removed);
                    break;
                }
            }

            // ── Pass 2: VendorTrash ──────────────────────────────────────────────────
            //
            // Loot unclaimed items whose sell value is at least (ratio × burden).
            // "value per pound" — high ratio = efficient to carry to a vendor.

            if (hasVT)
            {
                int threshold = vendorTrashRatio.GetOrAdd(player, 50);

                foreach (var item in items)
                {
                    if (lootedSet.Contains(item))
                        continue;

                    var value  = item.Value ?? 0;
                    var burden = item.EncumbranceVal ?? 1;
                    if (burden <= 0) burden = 1;

                    if (value >= threshold * burden)
                    {
                        if (!corpse.TryRemoveFromInventory(item.Guid, out var removed))
                            continue;

                        var qty = removed.StackSize ?? 1;
                        lootedItems.TryGetValue(removed.Name, out var existing);
                        lootedItems[removed.Name] = existing + qty;

                        lootedSet.Add(removed);
                        lootedVTNames.Add(removed.Name); // mark for [$] prefix in notification
                        player.TryCreateInInventoryWithNetworking(removed);
                    }
                }
            }

            // ── Pass 3: Unknown Scrolls ──────────────────────────────────────────────
            //
            // Loot any scroll that the player hasn't learned yet.
            // Checks WeenieType and SpellDID, then player.SpellIsKnown.

            if (hasScrolls)
            {
                foreach (var item in items)
                {
                    if (lootedSet.Contains(item))
                        continue;

                    // Must be a scroll with a spell attached
                    if (item.WeenieType != WeenieType.Scroll)
                        continue;

                    var spellId = item.GetProperty(PropertyDataId.Spell);
                    if (spellId == null || spellId == 0)
                        continue;

                    // Skip spells the player already knows
                    if (player.SpellIsKnown((uint)spellId))
                        continue;

                    if (!corpse.TryRemoveFromInventory(item.Guid, out var removed))
                        continue;

                    var qty = removed.StackSize ?? 1;
                    lootedItems.TryGetValue(removed.Name, out var existing);
                    lootedItems[removed.Name] = existing + qty;

                    lootedSet.Add(removed);
                    lootedScrolls.Add(removed.Name); // mark for special notification
                    player.TryCreateInInventoryWithNetworking(removed);
                }
            }

            // ── Rare broadcast ───────────────────────────────────────────────────────
            //
            // If any looted item is a rare (has a non-zero RareId property) AND the
            // player has opted in to rare broadcasts, announce it server-wide.

            if (broadcastRares.GetOrAdd(player, false))
            {
                foreach (var looted in lootedSet)
                {
                    var rareId = looted.GetProperty(PropertyInt.RareId);
                    if (rareId == null || rareId == 0)
                        continue;

                    var rareMsg = $"{player.Name} has looted the rare item, {looted.Name}!";
                    foreach (var p in PlayerManager.GetAllOnline())
                        p.SendMessage(rareMsg, ChatMessageType.Broadcast);
                }
            }

            if (lootedItems.Count == 0)
                return;

            // ── Chat notification ────────────────────────────────────────────────────

            bool notify = lootNotifications.GetOrAdd(player, true);
            if (!notify)
                return;

            // Build the regular loot summary (excludes scrolls — they get their own line).
            // VendorTrash items are suffixed with [$] so the player knows why they were picked up.
            var regularItems = lootedItems
                .Where(kvp => !lootedScrolls.Contains(kvp.Key))
                .Select(kvp =>
                {
                    var suffix = lootedVTNames.Contains(kvp.Key) ? " [$]" : "";
                    return kvp.Value == 1 ? $"a {kvp.Key}{suffix}" : $"{kvp.Value:N0} {kvp.Key}{suffix}";
                })
                .ToList();

            if (regularItems.Count > 0)
            {
                string itemList = regularItems.Count switch
                {
                    1 => regularItems[0],
                    2 => $"{regularItems[0]} and {regularItems[1]}",
                    _ => string.Join(", ", regularItems.Take(regularItems.Count - 1)) + $", and {regularItems[^1]}"
                };
                player.SendMessage($"[AutoLoot] You've looted {itemList}!");
            }

            // Learnable scrolls get a distinct [!] line so they stand out
            foreach (var scrollName in lootedScrolls.Distinct())
                player.SendMessage($"[AutoLoot] [!] You can learn: {scrollName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    #endregion
}
