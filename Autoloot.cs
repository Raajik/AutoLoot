using AutoLoot.Lib.VTClassic;
using System.Collections.Concurrent;

namespace AutoLoot;

/// <summary>
/// The core AutoLoot class.
///
/// This class contains:
///   1. The /loot in-game command — lets a player list and load .utl loot profiles.
///   2. A Harmony patch on Creature.GenerateTreasure — fires after any creature dies
///      and auto-loots items from its corpse according to the player's active profile.
///
/// How it works end-to-end:
///   1. A player types /loot in-game to select a .utl profile file.
///   2. A LootCore object (the VTClassic loot engine) is created and the profile is loaded into it.
///   3. When any creature is killed, GenerateTreasure runs. Our PostGenerateTreasure patch
///      intercepts this, checks if the killer has a profile loaded, and moves matching items
///      directly from the corpse into the player's inventory.
/// </summary>
[HarmonyPatch]
public class AutoLoot
{
    /// <summary>
    /// Tracks which LootCore profile is active for each player.
    ///
    /// ConcurrentDictionary is used here because the game is multi-threaded — multiple
    /// players may kill creatures at the same time. ConcurrentDictionary is safe to read
    /// and write from multiple threads simultaneously, unlike a regular Dictionary.
    ///
    /// Key   = the Player object
    /// Value = their currently loaded LootCore (VTClassic loot engine instance)
    /// </summary>
    static readonly ConcurrentDictionary<Player, LootCore> lootProfiles = new();

    /// <summary>
    /// In-game command: /loot [index or name]
    ///
    /// With no arguments:    lists all available .utl profile files in LootProfilePath.
    /// With a number:        loads the profile at that index from the list.
    /// With a name/partial:  loads the first profile whose path contains that text.
    ///
    /// Examples:
    ///   /loot           → shows list: "0) MyProfile.utl  == 12kb"
    ///   /loot 0         → loads the first profile
    ///   /loot MyProfile → loads any profile whose name contains "MyProfile"
    ///
    /// AccessLevel.Player means any logged-in player can use this command.
    /// CommandHandlerFlag.RequiresWorld means the player must be in the game world (not the character select screen).
    /// -1 for the parameter count means "accept any number of arguments."
    /// </summary>
    [CommandHandler("loot", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, -1)]
#if REALM
public static void HandleLoadProfile(ISession session, params string[] parameters)
#else
    public static void HandleLoadProfile(Session session, params string[] parameters)
#endif
    {
        var player = session.Player;

        try
        {
            // Create the LootProfiles directory if it doesn't exist yet
            Directory.CreateDirectory(PatchClass.Settings.LootProfilePath);

            // Find all .utl files in the LootProfiles folder and its subfolders
            var profiles = Directory.GetFiles(PatchClass.Settings.LootProfilePath, "*.utl", SearchOption.AllDirectories);

            var sb = new StringBuilder("\nProfiles:");

            // No arguments: list all available profiles and their file sizes
            if (parameters.Length == 0)
            {
                for (var i = 0; i < profiles.Length; i++)
                {
                    var profilePath = profiles[i];
                    var fi = new FileInfo(profilePath);
                    sb.Append($"  \n{i}) {fi.Name}  ==  {fi.Length / 1024:0}kb");
                }
                player.SendMessage(sb.ToString());
                return;
            }

            // Determine which profile was selected (by index number or by name search)
            var selected = "";
            if (uint.TryParse(parameters[0], out var index) && index < profiles.Length)
            {
                // Player typed a number — use it as the list index
                selected = profiles[index];
            }
            else
            {
                // Player typed a name — find the first profile whose path contains it (case-insensitive)
                var joined = string.Join(' ', parameters);
                selected = profiles.Where(x => x.Contains(joined, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            }

            // If nothing matched (bad index or name not found), clear any existing profile and bail
            if (string.IsNullOrEmpty(selected) || !File.Exists(selected))
            {
                player.SendMessage($"No loot profile selected.");

                // Shut down any previously-loaded profile to free its resources
                if (lootProfiles.TryGetValue(player, out var oldProfile))
                    oldProfile.Shutdown();

                return;
            }

            // Load the selected profile into a LootCore instance for this player
            if (!lootProfiles.TryGetValue(player, out var profile))
            {
                // First time loading — create a fresh LootCore, start it up, and load the file
                profile = new();
                profile.Startup();
                profile.LoadProfile(selected, false);
                lootProfiles.TryAdd(player, profile);
            }
            else
            {
                // Already had a profile — shut it down cleanly before loading the new one
                profile.Shutdown();
                profile.LoadProfile(selected, false);
            }

            player.SendMessage($"Loaded profile: {selected}");
        }
        catch (Exception ex)
        {
            ModManager.Log(ex.Message, ModManager.LogLevel.Error);
            player.SendMessage($"Failed to load loot profile!");
        }
    }



    #region Patches

    /// <summary>
    /// Harmony Postfix patch on Creature.GenerateTreasure.
    ///
    /// What is a Postfix? It runs AFTER the original method finishes.
    /// So by the time we get here, the creature is dead and its corpse already has all
    /// loot items inside it (that's what GenerateTreasure does — it fills the corpse).
    ///
    /// Parameters explained:
    ///   killer   — info about who/what killed the creature
    ///   corpse   — the Corpse WorldObject that now holds all the loot
    ///   __instance — the Creature that died (the "ref" means we could modify it, but we don't)
    ///   __result   — the list of WorldObjects that was returned by GenerateTreasure (we don't use it)
    ///
    /// What this does:
    ///   1. Figures out which Player (if any) killed the creature.
    ///   2. Checks if that player has a loot profile loaded.
    ///   3. Asks the LootCore to evaluate each item in the corpse.
    ///   4. Moves matching items straight into the player's inventory.
    ///   5. Tells the player a summary of what was looted.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Creature), nameof(Creature.GenerateTreasure), new Type[] { typeof(DamageHistoryInfo), typeof(Corpse) })]
    public static void PostGenerateTreasure(DamageHistoryInfo killer, Corpse corpse, ref Creature __instance, ref List<WorldObject> __result)
    {
        // TryGetPetOwnerOrAttacker resolves the killer to a Player (handles pet kills too).
        // If the killer wasn't a player, skip auto-loot entirely.
        if (killer.TryGetPetOwnerOrAttacker() is not Player player)
            return;

        // If this player hasn't loaded a loot profile, nothing to do
        if (!lootProfiles.TryGetValue(player, out var profile))
            return;

        try
        {
            // Track timing and build a summary message for the player
            var watch = Stopwatch.StartNew();
            var sb = new StringBuilder();

            // Snapshot the corpse's inventory — we need a stable list to iterate over
            var items = corpse.Inventory.Values.ToList();

            int looted = 0;
            foreach (var item in items)
            {
                // Ask the VTClassic loot engine: what should we do with this item?
                var lootAction = profile.GetLootDecision(item, player);

                // IsNoLoot means the profile said "skip this item" — leave it in the corpse
                if (lootAction.IsNoLoot)
                    continue;

                looted++;
                sb.Append($"\n {item.Name} matches {lootAction._lootAction} rule {lootAction.RuleName}");

                // Move the item from the corpse directly into the player's inventory
                player.TryCreateInInventoryWithNetworking(item);
            }

            // Only send a message if at least one item was looted
            if (looted == 0)
                return;

            watch.Stop();
            sb.Append($"\n=====Looted {looted}/{items.Count} items in {watch.ElapsedMilliseconds} ms=====");

            player.SendMessage(sb.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }

    }
    #endregion
}
