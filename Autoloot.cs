using AutoLoot.Lib.VTClassic;
using System.Collections.Concurrent;

namespace AutoLoot;

/// <summary>
/// The core AutoLoot class.
///
/// Contains:
///   1. The /autoloot command — lets players view and toggle server-hosted .utl loot
///      profiles on and off, enable/disable all at once, and toggle loot notifications.
///   2. A Harmony patch on Creature.GenerateTreasure — fires after any creature dies
///      and auto-loots items from its corpse according to each player's active profiles.
///
/// How it works end-to-end:
///   1. A player types /autoloot to see available profiles with [ON]/[OFF] status.
///   2. They type /autoloot <#> to toggle a profile. Multiple profiles can be active
///      at the same time; toggling again turns one off.
///   3. The server's DefaultProfile (PyrealsTradeNotes.utl by default) is automatically
///      enabled the first time a player uses /autoloot each session.
///   4. When any creature is killed, GenerateTreasure fires. Our patch checks each item
///      in the corpse against the player's active profiles and moves matches to their inventory.
///   5. If the player has loot notifications enabled, a summary message is sent to chat.
/// </summary>
[HarmonyPatch]
public class AutoLoot
{
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
    /// true  = show a summary message when items are auto-looted (default for new players)
    /// false = loot silently with no chat output
    ///
    /// Toggled with: /autoloot details
    /// </summary>
    static readonly ConcurrentDictionary<Player, bool> lootNotifications = new();

    /// <summary>
    /// Tracks whether VendorTrash tier 1 (25:1 ratio) is active for each player.
    ///
    /// Loots any item whose sell value is at least 25× its burden weight.
    /// A broader net — catches most decent vendor fodder.
    ///
    /// Example: a gem worth 500 pyreals, burden 5 → ratio 100 ≥ 25 → looted
    ///
    /// Toggled with: /autoloot vt1
    /// </summary>
    static readonly ConcurrentDictionary<Player, bool> vendorTrash1 = new();

    /// <summary>
    /// Tracks whether VendorTrash tier 2 (50:1 ratio) is active for each player.
    ///
    /// Loots any item whose sell value is at least 50× its burden weight.
    /// A tighter filter — only the most value-dense items make the cut.
    ///
    /// Example: a gem worth 500 pyreals, burden 5 → ratio 100 ≥ 50 → looted
    ///          a ring worth 1,000 pyreals, burden 25 → ratio 40 &lt; 50 → skipped
    ///
    /// Toggled with: /autoloot vt2
    /// </summary>
    static readonly ConcurrentDictionary<Player, bool> vendorTrash2 = new();

    /// <summary>
    /// In-game command: /autoloot [on | off | details | index | name]
    ///
    /// No arguments:   shows all commands + full profile list with [ON]/[OFF] status.
    /// on:             enables every available profile at once.
    /// off:            disables all currently active profiles.
    /// details:        toggles loot notifications in chat on or off.
    /// A number:       toggles the profile at that index on or off.
    /// A name/partial: toggles the first profile whose filename contains that text.
    ///
    /// The first time a player uses this command each session, the server's DefaultProfile
    /// (set in Settings.json) is automatically enabled for them.
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
            // Ensure the LootProfiles directory exists (the .utl files are deployed by the build,
            // but the folder might be missing if someone deleted it manually)
            Directory.CreateDirectory(PatchClass.Settings.LootProfilePath);

            // All profiles are server-hosted — .utl files in LootProfilePath.
            // Every player on the server sees the same list.
            var profiles = Directory.GetFiles(
                PatchClass.Settings.LootProfilePath, "*.utl", SearchOption.TopDirectoryOnly);

            // Detect first-time players this session and auto-enable the default profile
            bool isNewPlayer = !lootProfiles.ContainsKey(player);
            var playerProfiles = lootProfiles.GetOrAdd(player, _ => new ConcurrentDictionary<string, LootCore>());

            if (isNewPlayer && !string.IsNullOrEmpty(PatchClass.Settings.DefaultProfile))
            {
                // Find and load the default profile (case-insensitive filename match)
                var defaultPath = profiles.FirstOrDefault(p =>
                    Path.GetFileName(p).Equals(PatchClass.Settings.DefaultProfile, StringComparison.OrdinalIgnoreCase));

                if (defaultPath != null)
                {
                    var defaultCore = new LootCore();
                    defaultCore.Startup();
                    defaultCore.LoadProfile(defaultPath, false);
                    playerProfiles[defaultPath] = defaultCore;
                }
            }

            // ── No arguments: show the full menu ────────────────────────────────────

            if (parameters.Length == 0)
            {
                bool notificationsOn = lootNotifications.GetOrAdd(player, true);

                var sb = new StringBuilder("\nAutoLoot Commands:");
                sb.Append("\n  /autoloot <#>        — toggle profile on/off by number");
                sb.Append("\n  /autoloot <name>     — toggle profile on/off by partial name");
                sb.Append("\n  /autoloot on         — enable all profiles");
                sb.Append("\n  /autoloot off        — disable all profiles");
                sb.Append($"\n  /autoloot details      — toggle loot notifications [{(notificationsOn ? "ON" : "OFF")}]");
                sb.Append("\n  /autoloot vt1          — toggle VendorTrash 25:1 (broader)");
                sb.Append("\n  /autoloot vt2          — toggle VendorTrash 50:1 (stricter)");

                // Developer-only commands (also visible to Admins since Admin > Developer)
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

                // List every profile with its current ON/OFF status
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

                // VendorTrash tiers are C#-powered filters — show them separately from .utl profiles
                bool vt1On = vendorTrash1.GetOrAdd(player, false);
                bool vt2On = vendorTrash2.GetOrAdd(player, false);
                sb.Append($"\n  V1) {(vt1On ? "[ON] " : "[OFF]")} VendorTrash 25:1  —  loot items worth ≥ 25× their burden");
                sb.Append($"\n  V2) {(vt2On ? "[ON] " : "[OFF]")} VendorTrash 50:1  —  loot items worth ≥ 50× their burden");

                player.SendMessage(sb.ToString());
                return;
            }

            var arg = parameters[0];

            // ── /autoloot on — enable every profile ─────────────────────────────────

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
                if (!vendorTrash1.GetOrAdd(player, false)) { vendorTrash1[player] = true; enabled++; }
                if (!vendorTrash2.GetOrAdd(player, false)) { vendorTrash2[player] = true; enabled++; }
                player.SendMessage(enabled > 0
                    ? $"Enabled {enabled} profile(s). Type /autoloot to see the list."
                    : "All profiles are already enabled.");
                return;
            }

            // ── /autoloot off — disable every profile ───────────────────────────────

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
                if (vendorTrash1.GetOrAdd(player, false)) { vendorTrash1[player] = false; disabled++; }
                if (vendorTrash2.GetOrAdd(player, false)) { vendorTrash2[player] = false; disabled++; }
                player.SendMessage(disabled > 0
                    ? $"Disabled {disabled} profile(s)."
                    : "No profiles were active.");
                return;
            }

            // ── /autoloot details — toggle loot notifications ────────────────────────

            if (arg.Equals("details", StringComparison.OrdinalIgnoreCase))
            {
                bool current = lootNotifications.GetOrAdd(player, true);
                lootNotifications[player] = !current;
                player.SendMessage($"Loot notifications: {(!current ? "ON" : "OFF")}");
                return;
            }

            // ── /autoloot vt1 — toggle VendorTrash 25:1 ─────────────────────────────

            if (arg.Equals("vt1", StringComparison.OrdinalIgnoreCase))
            {
                bool current = vendorTrash1.GetOrAdd(player, false);
                vendorTrash1[player] = !current;
                player.SendMessage($"VendorTrash 25:1: {(!current ? "ON" : "OFF")} (loot items worth ≥ 25× their burden)");
                return;
            }

            // ── /autoloot vt2 — toggle VendorTrash 50:1 ─────────────────────────────

            if (arg.Equals("vt2", StringComparison.OrdinalIgnoreCase))
            {
                bool current = vendorTrash2.GetOrAdd(player, false);
                vendorTrash2[player] = !current;
                player.SendMessage($"VendorTrash 50:1: {(!current ? "ON" : "OFF")} (loot items worth ≥ 50× their burden)");
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

            // If already active → turn off. If inactive → turn on.
            if (playerProfiles.TryRemove(selected, out var existingCore))
            {
                existingCore.Shutdown();
                player.SendMessage($"[OFF] {displayName}");
            }
            else
            {
                var newCore = new LootCore();
                newCore.Startup();
                newCore.LoadProfile(selected, false);
                playerProfiles[selected] = newCore;
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
    /// For each item in the corpse, we check the player's active profiles in order.
    /// The first profile that matches an item claims it — the item moves to the player's
    /// inventory and we stop checking other profiles for that item.
    ///
    /// If the player has notifications enabled (the default), a summary is sent to chat
    /// listing everything that was looted, grouped by item name with quantities.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Creature), nameof(Creature.GenerateTreasure), new Type[] { typeof(DamageHistoryInfo), typeof(Corpse) })]
    public static void PostGenerateTreasure(DamageHistoryInfo killer, Corpse corpse, ref Creature __instance, ref List<WorldObject> __result)
    {
        // Only auto-loot for players (handles pet kills too via TryGetPetOwnerOrAttacker)
        if (killer.TryGetPetOwnerOrAttacker() is not Player player)
            return;

        // Check what the player has active — profiles, VendorTrash, or both.
        // We need at least one active to do anything.
        lootProfiles.TryGetValue(player, out var playerProfiles);
        bool hasProfiles = playerProfiles != null && !playerProfiles.IsEmpty;
        bool hasVT1 = vendorTrash1.GetOrAdd(player, false);
        bool hasVT2 = vendorTrash2.GetOrAdd(player, false);

        if (!hasProfiles && !hasVT1 && !hasVT2)
            return;

        try
        {
            // Snapshot both collections so they can't change while we iterate
            var items = corpse.Inventory.Values.ToList();
            var activeProfiles = hasProfiles ? playerProfiles!.Values.ToList() : new List<LootCore>();

            // lootedItems tracks name → total quantity for the chat notification.
            // lootedSet tracks which specific WorldObject instances have already been claimed
            // so the VendorTrash pass below doesn't double-pick the same item.
            var lootedItems = new Dictionary<string, int>();
            var lootedSet = new HashSet<WorldObject>();

            foreach (var item in items)
            {
                foreach (var profile in activeProfiles)
                {
                    var lootAction = profile.GetLootDecision(item, player);
                    if (lootAction.IsNoLoot)
                        continue;

                    // First profile that claims this item wins — remove from corpse, add to inventory
                    if (!corpse.TryRemoveFromInventory(item.Guid, out var removed))
                        break;

                    var qty = removed.StackSize ?? 1;
                    lootedItems.TryGetValue(removed.Name, out var existing);
                    lootedItems[removed.Name] = existing + qty;

                    lootedSet.Add(item);
                    player.TryCreateInInventoryWithNetworking(removed);
                    break;
                }
            }

            // VendorTrash pass: loot unclaimed items that meet the player's active ratio threshold.
            // vt1 uses a 25:1 ratio (broader), vt2 uses 50:1 (stricter).
            // If both are on, vt1's lower threshold already covers everything vt2 would catch,
            // so we just use whichever active tier has the lowest ratio.
            if (hasVT1 || hasVT2)
            {
                int threshold = hasVT1 ? 25 : 50;

                foreach (var item in items)
                {
                    // Skip items already moved to inventory by a profile above
                    if (lootedSet.Contains(item))
                        continue;

                    var value = item.Value ?? 0;
                    var burden = item.EncumbranceVal ?? 1; // treat 0-burden items as 1 to avoid division by zero
                    if (burden <= 0) burden = 1;

                    if (value >= threshold * burden)
                    {
                        if (!corpse.TryRemoveFromInventory(item.Guid, out var removed))
                            continue;

                        var qty = removed.StackSize ?? 1;
                        lootedItems.TryGetValue(removed.Name, out var existing);
                        lootedItems[removed.Name] = existing + qty;

                        lootedSet.Add(item);
                        player.TryCreateInInventoryWithNetworking(removed);
                    }
                }
            }

            if (lootedItems.Count == 0)
                return;

            // Only send a chat message if the player has notifications enabled
            bool notify = lootNotifications.GetOrAdd(player, true);
            if (!notify)
                return;

            // Build a natural-language list: "a Tinkerer's Crystal, 2 Copper Peas, and 1,293 Pyreals"
            var parts = lootedItems
                .Select(kvp => kvp.Value == 1
                    ? $"a {kvp.Key}"
                    : $"{kvp.Value:N0} {kvp.Key}")
                .ToList();

            string itemList = parts.Count switch
            {
                1 => parts[0],
                2 => $"{parts[0]} and {parts[1]}",
                _ => string.Join(", ", parts.Take(parts.Count - 1)) + $", and {parts[^1]}"
            };

            player.SendMessage($"[AutoLoot] You've looted {itemList}!");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    #endregion
}
