using AutoLoot.Loot;

namespace AutoLoot.Helpers;

/// <summary>
/// Developer-only test commands for AutoLoot.
///
/// These commands are for testing and benchmarking the loot rule system during development.
/// They are NOT intended for use by regular players — all are locked to AccessLevel.Developer.
///
/// How to use them:
///   1. Appraise (click-examine) an item so it becomes your "current appraisal target."
///   2. Type the command in-game. The command finds that item and runs the loot system against it.
///
/// Commands:
///   /t1    — tests the hardcoded SampleProfile against the appraised item
///   /t2    — benchmarks 500-rule random profiles (with both value AND string requirements)
///   /t3    — benchmarks 500-rule random profiles (value requirements only)
///   /t4    — benchmarks 500-rule random profiles (string requirements only)
///   /clean — Admin-only: removes all items from your inventory (cleanup after testing)
/// </summary>
internal class Commands
{
    /// <summary>
    /// /t1 — Tests the hardcoded SampleProfile against the currently-appraised item.
    ///
    /// SampleProfile is defined in Profile.cs and contains a few hand-written rules.
    /// Useful for quickly verifying that a profile evaluates items as expected.
    /// </summary>
    [CommandHandler("t1", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
    public static void HandleTest(Session session, params string[] parameters)
    {
        var player = session.Player;

        // CurrentAppraisalTarget is set when the player appraises (examines) an object.
        // If they haven't appraised anything, bail out.
        var targetID = player.CurrentAppraisalTarget ?? 0;
        var item = player.FindObject(targetID, Player.SearchLocations.Everywhere, out _, out _, out _);
        if (item is null)
            return;

        // Load the hardcoded sample profile
        var profile = Profile.SampleProfile;

        // Initialize compiles the Regex patterns inside any StringRequirements
        profile.Initialize();

        // Evaluate the item — "match" will be set to the first Rule that the item satisfies
        Rule match = null;
        var action = Profile.SampleProfile.Evaluate(item, ref match);

        if (action == Loot.Action.None)
            player.SendMessage($"{item.Name} did not match the profile.");
        else
            player.SendMessage($"{item.Name}: {action} @ {match.Name}");
    }

    /// <summary>
    /// /t2 — Benchmarks profile generation and evaluation with 500 random rules (value + string requirements).
    ///
    /// Generates a random profile with 500 rules, each having both a ValueRequirement
    /// and a StringRequirement. Reports how long generation and evaluation took in milliseconds.
    /// Useful for measuring worst-case performance when profiles are very large.
    /// </summary>
    [CommandHandler("t2", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
    public static void HandleTest2(Session session, params string[] parameters)
    {
        var player = session.Player;

        var targetID = player.CurrentAppraisalTarget ?? 0;
        var item = player.FindObject(targetID, Player.SearchLocations.Everywhere, out _, out _, out _);
        if (item is null)
            return;

        var sb = new StringBuilder("\n");
        var watch = Stopwatch.StartNew();
        var numProfiles = 500;

        // RandomProfile(count, requirementsPerRule, includeValueReqs, includeStringReqs)
        var profile = RandomHelper.RandomProfile(numProfiles);
        profile.Initialize();
        var time = watch.ElapsedMilliseconds;
        sb.Append($"\nGenerated {numProfiles} in {time}ms");
        watch.Restart();

        Rule match = null;
        var action = profile.Evaluate(item, ref match);
        time = watch.ElapsedMilliseconds;
        sb.Append($"\nEvaluated in {time}ms");

        if (action == Loot.Action.None)
            sb.Append($"\n{item.Name} did not match the profile.");
        else
            sb.Append($"{item.Name}: {action} @ {match.Name}");

        player.SendMessage(sb.ToString());
    }

    /// <summary>
    /// /t3 — Benchmarks 500-rule random profile with value requirements only (no string matching).
    ///
    /// Same as /t2 but rules only have ValueRequirements (numeric comparisons).
    /// Useful for isolating numeric property lookup performance from Regex overhead.
    /// </summary>
    [CommandHandler("t3", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
    public static void HandleTest3(Session session, params string[] parameters)
    {
        var player = session.Player;

        var targetID = player.CurrentAppraisalTarget ?? 0;
        var item = player.FindObject(targetID, Player.SearchLocations.Everywhere, out _, out _, out _);
        if (item is null)
            return;

        var sb = new StringBuilder("\n");
        var watch = Stopwatch.StartNew();
        var numProfiles = 500;

        // true = include value reqs, false = skip string reqs
        var profile = RandomHelper.RandomProfile(numProfiles, 1, true, false);
        profile.Initialize();
        var time = watch.ElapsedMilliseconds;
        sb.Append($"\nGenerated {numProfiles} in {time}ms");
        watch.Restart();

        Rule match = null;
        var action = profile.Evaluate(item, ref match);
        time = watch.ElapsedMilliseconds;
        sb.Append($"\nEvaluated in {time}ms");

        if (action == Loot.Action.None)
            sb.Append($"\n{item.Name} did not match the profile.");
        else
            sb.Append($"{item.Name}: {action} @ {match.Name}");

        player.SendMessage(sb.ToString());
    }

    /// <summary>
    /// /t4 — Benchmarks 500-rule random profile with string requirements only (no value matching).
    ///
    /// Same as /t2 but rules only have StringRequirements (Regex name matching).
    /// Isolates Regex evaluation performance from numeric property lookups.
    /// </summary>
    [CommandHandler("t4", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
    public static void HandleTest4(Session session, params string[] parameters)
    {
        var player = session.Player;

        var targetID = player.CurrentAppraisalTarget ?? 0;
        var item = player.FindObject(targetID, Player.SearchLocations.Everywhere, out _, out _, out _);
        if (item is null)
            return;

        var sb = new StringBuilder("\n");
        var watch = Stopwatch.StartNew();
        var numProfiles = 500;

        // false = skip value reqs (string reqs still included by default)
        var profile = RandomHelper.RandomProfile(numProfiles, 1, false);
        profile.Initialize();
        var time = watch.ElapsedMilliseconds;
        sb.Append($"\nGenerated {numProfiles} in {time}ms");
        watch.Restart();

        Rule match = null;
        var action = profile.Evaluate(item, ref match);
        time = watch.ElapsedMilliseconds;
        sb.Append($"\nEvaluated in {time}ms");

        if (action == Loot.Action.None)
            sb.Append($"\n{item.Name} did not match the profile.");
        else
            sb.Append($"{item.Name}: {action} @ {match.Name}");

        player.SendMessage(sb.ToString());
    }

    /// <summary>
    /// /clean — Admin-only: removes every item from the player's inventory.
    ///
    /// Useful after running loot tests to reset a character's inventory back to empty.
    /// Requires AccessLevel.Admin so it can't be accidentally used by regular players.
    ///
    /// WARNING: This is permanent — items are not dropped to the ground, just removed.
    /// </summary>
    [CommandHandler("clean", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0)]
    public static void Clean(Session session, params string[] parameters)
    {
        var player = session.Player;

        // Iterate over a snapshot of the inventory keys to avoid modifying the collection while iterating
        foreach (var item in player.Inventory)
        {
            player.TryRemoveFromInventoryWithNetworking(item.Key, out var i, Player.RemoveFromInventoryAction.None);
        }
    }
}
