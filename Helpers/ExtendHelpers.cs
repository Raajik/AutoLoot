using ACE.Database;
using ACE.Entity;
using ACE.Entity.Models;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects.Entity;
using static ACE.Server.WorldObjects.Player;

namespace AutoLoot.Helpers;

/// <summary>
/// Extension methods for removing items from a player's inventory or equipped slots.
///
/// "Extension methods" are a C# feature that lets you add new methods to existing classes
/// (like Player) without modifying the original class. You call them as if they were
/// built-in methods: player.TryTakeItems(...)
/// </summary>
public static class InventoryHelpers
{
    /// <summary>
    /// Removes a specified number of items (identified by Weenie Class ID) from the player's
    /// inventory or equipped slots, then sends the player a confirmation message.
    ///
    /// WCID (Weenie Class ID) is the game's unique identifier for an item type — like a
    /// template ID. For example, all Pyrals share the same WCID.
    ///
    /// Parameters:
    ///   weenieClassId — the WCID of the item to remove
    ///   amount        — how many to remove. Use -1 to remove all of them.
    ///
    /// Returns true if the items were successfully removed, false if the player doesn't
    /// have enough or the amount is invalid.
    /// </summary>
    public static bool TryTakeItems(this Player player, uint weenieClassId, int amount = 1)
    {
        if (player is null) return false;

        if (amount < 1)
        {
            ModManager.Log($"Invalid amount of items to take: {amount} of WCID {weenieClassId}", ModManager.LogLevel.Warn);
            return false;
        }

        // Try removing from inventory first; if not there, try equipped slots.
        // amount == -1 means "take all", so we pass int.MaxValue to consume everything.
        if ((player.GetNumInventoryItemsOfWCID(weenieClassId) > 0 && player.TryConsumeFromInventoryWithNetworking(weenieClassId, amount == -1 ? int.MaxValue : amount))
            || (player.GetNumEquippedObjectsOfWCID(weenieClassId) > 0 && player.TryConsumeFromEquippedObjectsWithNetworking(weenieClassId, amount == -1 ? int.MaxValue : amount)))
        {
            // Look up the item's weenie definition to get its display name
            var itemTaken = DatabaseManager.World.GetCachedWeenie(weenieClassId);
            if (itemTaken != null)
            {
                var amt = amount == -1 ? "all" : amount.ToString();
                var msg = $"You hand over {amount} of your {itemTaken.GetPluralName()}.";

                // Send the message to the player's client
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
                return true;
            }
        }
        return true;
    }

    /// <summary>
    /// Removes an equipped item from the player and either puts it in their pack or drops it on the ground.
    ///
    /// The action parameter controls what happens to the item:
    ///   DequipToPack  — unequip and move to inventory (default)
    ///   DropItem      — unequip and drop on the ground at the player's feet
    ///
    /// Returns true if the item was successfully moved, false if something went wrong.
    ///
    /// Note: There is an unfinished "drop from inventory" path (commented out at the top)
    /// that wasn't working correctly — only the equipped-item path is currently active.
    /// </summary>
    public static bool TryDropItem(this Player player, WorldObject item, DequipObjectAction action = DequipObjectAction.DequipToPack)
    {
        var session = player.Session;
        var playerLoc = new Position(player.Location); // snapshot the player's current position
        WorldObject destItem;

        // ── Path 1: Move to pack (unequip into inventory) ───────────────
        if (action == DequipObjectAction.DequipToPack)
        {
            // DequipWithNetworking handles the stance change, sound, and client updates
            // (wielder ID cleared, wield location set to 0, pickup sound played, etc.)
            if (!player.TryDequipObjectWithNetworking(item.Guid.Full, out destItem, action))
                return false;

            // Put the item into the player's pack
            if (!player.TryCreateInInventoryWithNetworking(destItem, out var container))
                return false;

            // Tell the client which container the item ended up in
            player.Session.Network.EnqueueSend(
                new GameMessagePublicUpdateInstanceID(destItem, PropertyInstanceId.Container, destItem.Container.Guid),
                new GameEventItemServerSaysMoveItem(player.Session, destItem)
            );

            // Fire any "on drop" emotes the item might have, then save it to the database
            destItem.EmoteManager.OnDrop(player);
            destItem.SaveBiotaToDatabase();
            return true;
        }

        // ── Path 2: Drop on the ground ───────────────────────────────────
        if (!player.TryDequipObjectWithNetworking(item.Guid.Full, out destItem, DequipObjectAction.DropItem))
            return false;

        player.SavePlayerToDatabase();

        // Position the dropped item slightly above the player's feet so it doesn't clip into the floor
        destItem.Location = new Position(playerLoc);
        destItem.Location.PositionZ += .5f;
        destItem.Placement = Placement.Resting; // makes items lay flat on the ground rather than standing upright

        // Temporarily make the item ethereal (passes through geometry) while we find its final resting place.
        // This avoids the item getting stuck inside a wall or floor.
        var ethereal = destItem.Ethereal;
        destItem.Ethereal = true;

        if (player.CurrentLandblock?.AddWorldObject(destItem) ?? false)
        {
            destItem.Location.LandblockId = new LandblockId(destItem.Location.GetCell());

            // Try to slide the item to a valid cell using the physics engine
            var transit = destItem.PhysicsObj.transition(destItem.PhysicsObj.Position, new ACE.Server.Physics.Common.Position(destItem.Location), false);

            if (transit != null && transit.SpherePath.CurCell != null)
            {
                // Physics found a valid cell — update the item's position to where it actually landed
                destItem.PhysicsObj.SetPositionInternal(transit);
                destItem.SyncLocation();
                destItem.SendUpdatePosition(true);
            }

            // Restore the original ethereal state now that we have a valid position
            destItem.Ethereal = ethereal;

            // Notify the client: item is no longer in a container or wielded, play the drop sound
            player.Session.Network.EnqueueSend(
                new GameMessagePublicUpdateInstanceID(destItem, PropertyInstanceId.Container, ObjectGuid.Invalid),
                new GameMessagePublicUpdateInstanceID(destItem, PropertyInstanceId.Wielder, ObjectGuid.Invalid),
                new GameEventItemServerSaysMoveItem(player.Session, destItem),
                new GameMessageUpdatePosition(destItem));

            player.EnqueueBroadcast(new GameMessageSound(player.Guid, Sound.DropItem));

            destItem.EmoteManager.OnDrop(player);
            destItem.SaveBiotaToDatabase();
            return true;
        }

        return false;
    }
}

// ── BiotaHelpers and WeenieHelpers are commented out (kept for reference) ──────
// These classes would have added IsNpc() extension methods to Biota and Weenie.
// They're not currently used but are left here as examples for future use.

/// <summary>
/// Extension methods for WorldObject — animation and positioning utilities.
/// </summary>
public static class WorldObjectHelpers
{
    /// <summary>
    /// Plays an animation on a WorldObject and broadcasts it to nearby clients.
    ///
    /// PlayScript is an enum of all available animations in the game (AttackSlash1, Celebrate, etc.)
    /// speed defaults to 1.0 (normal speed). 0.5 would be half speed, 2.0 would be double speed.
    /// </summary>
    public static void PlayAnimation(this WorldObject worldObject, PlayScript script, float speed = 1f) =>
        worldObject.EnqueueBroadcast(new GameMessageScript(worldObject.Guid, script, speed));

    /// <summary>
    /// Teleports a WorldObject to a position just in front of a player.
    ///
    /// Creatures are placed 5 units in front; other objects are placed at their UseRadius
    /// distance (minimum 2 units) so the player can immediately interact with them.
    ///
    /// The LandblockId is recalculated after moving so the physics engine knows which
    /// landblock the object belongs to.
    /// </summary>
    public static void MoveInFrontOf(this WorldObject obj, Player player)
    {
        if (obj.WeenieType == WeenieType.Creature)
            obj.Location = player.Location.InFrontOf(5f, true);
        else
        {
            var dist = Math.Max(2, obj.UseRadius ?? 2);
            obj.Location = player.Location.InFrontOf(dist);
        }

        obj.Location.LandblockId = new LandblockId(obj.Location.GetCell());
    }
}

/// <summary>
/// Extension methods for Player — sound playback.
/// </summary>
public static class PlayerHelpers
{
    /// <summary>
    /// Plays a sound effect at the player's location and broadcasts it to nearby clients.
    ///
    /// Sound is an enum of all available game sounds (ReceiveItem, LevelUp, etc.)
    /// volume defaults to 1.0 (full volume).
    /// </summary>
    public static void PlaySound(this Player player, Sound sound, float volume = 1f) =>
        player.EnqueueBroadcast(new GameMessageSound(player.Guid, sound, volume));
}

/// <summary>
/// Extension methods for Creature and WorldObject — health, damage, and stat scaling.
/// </summary>
public static class CreatureHelpers
{
    /// <summary>
    /// Returns the creature's current health as a value between 0.0 and 1.0.
    /// For example, 0.5 means the creature is at half health.
    /// </summary>
    public static float PercentHealth(this Creature creature) => (float)creature.Health.Current / creature.Health.MaxValue;

    /// <summary>
    /// Deals damage to a creature directly (without needing a weapon or attacker source).
    ///
    /// Useful for applying damage from environmental effects, traps, or scripted events.
    ///
    /// Parameters:
    ///   amount         — raw damage amount before resistance is applied
    ///   damageTaken    — out: how much damage was actually applied after resistance
    ///   damageType     — what type of damage (Fire, Cold, Slash, etc.; default Undef = physical)
    ///   ignoreResist   — if true, bypass the creature's resistance to this damage type
    ///
    /// Handles special cases:
    ///   - Dead or invincible creatures are skipped
    ///   - Players under lifestone protection are handled correctly
    ///   - Stamina/Mana damage is supported by changing damageType
    ///   - If the creature reaches 0 health, it dies immediately
    /// </summary>
    public static bool TryDamageDirect(this Creature creature, float amount, out uint damageTaken, DamageType damageType = DamageType.Undef, bool ignoreResistance = false)
    {
        damageTaken = 0;
        if (creature.IsDead || creature.Invincible) return false;

        // If the target is a player under lifestone protection, trigger that mechanic instead
        if (creature is Player p && p.UnderLifestoneProtection)
        {
            p.HandleLifestoneProtection();
            return false;
        }

        // Choose which vital (Health, Stamina, or Mana) to drain based on damage type
        CreatureVital vital = damageType switch
        {
            DamageType.Stamina => creature.Stamina,
            DamageType.Mana => creature.Mana,
            _ => creature.Health
        };

        // Apply resistance modifier unless ignoreResistance is set
        var resistance = ignoreResistance ? 1f : creature.GetResistanceMod(damageType, null, null);
        var damage = (uint)Math.Round(amount * resistance);

        // Apply the damage and record it in the damage history (used for kill credit, etc.)
        damageTaken = (uint)-creature.UpdateVitalDelta(vital, (int)-damage);
        creature.DamageHistory.Add(creature, damageType, damageTaken);

        // If health dropped to zero, trigger death
        if (creature.Health.Current <= 0)
        {
            creature.OnDeath(new DamageHistoryInfo(creature), damageType, false);
            creature.Die();
        }

        return true;
    }

    /// <summary>
    /// Multiplies an integer property on a WorldObject by a scaling factor.
    /// For example: wo.ScaleProperty(PropertyInt.ArmorLevel, 1.5f) increases armor by 50%.
    /// </summary>
    public static void ScaleProperty(this WorldObject wo, PropertyInt property, float amount) => wo.SetProperty(property, (int)(amount * wo.GetProperty(property) ?? 0));

    /// <summary>
    /// Multiplies a float property on a WorldObject by a scaling factor.
    /// </summary>
    public static void ScaleProperty(this WorldObject wo, PropertyFloat property, float amount) => wo.SetProperty(property, (double)(amount * wo.GetProperty(property) ?? 0));

    /// <summary>
    /// Multiplies a 64-bit integer property on a WorldObject by a scaling factor.
    /// </summary>
    public static void ScaleProperty(this WorldObject wo, PropertyInt64 property, float amount) => wo.SetProperty(property, (long)(amount * wo.GetProperty(property) ?? 0));

    /// <summary>
    /// Multiplies the base (starting) value of one or more primary attributes on a creature.
    ///
    /// This changes the creature's base stat, not a temporary buff.
    /// PropertyAttribute covers the 6 primary attributes: Strength, Endurance, Coordination,
    /// Quickness, Focus, and Self.
    ///
    /// Skips PropertyAttribute.Undef (no-op sentinel value).
    /// </summary>
    public static void ScaleAttributeBase(this Creature wo, float amount, params PropertyAttribute[] properties) =>
        Array.ForEach<PropertyAttribute>(properties, (property) =>
        {
            if (property != PropertyAttribute.Undef)
                wo.Attributes[property].StartingValue = (uint)(wo.Attributes[property].StartingValue * amount);
        });

    /// <summary>
    /// Multiplies the base (starting) value of one or more secondary attributes (vitals) on a creature.
    ///
    /// PropertyAttribute2nd covers the 3 vitals: MaxHealth, MaxStamina, MaxMana.
    /// Skips PropertyAttribute2nd.Undef.
    /// </summary>
    public static void ScaleAttributeBase(this Creature wo, float amount, params PropertyAttribute2nd[] properties) =>
        Array.ForEach<PropertyAttribute2nd>(properties, (property) =>
        {
            if (property != PropertyAttribute2nd.Undef)
                wo.Vitals[property].StartingValue = (uint)(wo.Vitals[property].StartingValue * amount);
        });
}

/// <summary>
/// Generic collection helpers for picking random elements.
/// </summary>
public static class RandomHelpers
{
    /// <summary>
    /// Returns a single random element from any collection.
    /// Returns default(T) if the collection is empty.
    /// </summary>
    public static T GetRandom<T>(this IEnumerable<T> list) => list.GetRandomElements<T>(1).FirstOrDefault();

    /// <summary>
    /// Returns up to elementsCount randomly-ordered elements from a collection.
    /// Uses Guid.NewGuid() as a shuffle key — not cryptographically secure, but fine for gameplay.
    /// </summary>
    public static List<T> GetRandomElements<T>(this IEnumerable<T> list, int elementsCount) =>
        list.OrderBy(arg => Guid.NewGuid()).Take(elementsCount).ToList();
}

/// <summary>
/// String and text utility helpers.
/// </summary>
public static class StringHelpers
{
    /// <summary>
    /// Placeholder: intended to return a display name for a CreatureVital (Health, Stamina, Mana).
    /// Currently unimplemented (empty switch expression).
    /// </summary>
    public static string Name(this CreatureVital vital) => vital switch
    {
    };

    /// <summary>
    /// Repeats a string a given number of times.
    ///
    /// For example: "ab".Repeat(3) → "ababab"
    ///
    /// Returns the original string unchanged if count is 0 or 1.
    /// Uses StringBuilder internally for efficiency (avoids creating many intermediate strings).
    /// </summary>
    public static string Repeat(this string input, int count)
    {
        if (string.IsNullOrEmpty(input) || count <= 1)
            return input;

        var builder = new StringBuilder(input.Length * count);

        for (var i = 0; i < count; i++) builder.Append(input);

        return builder.ToString();
    }
}

/// <summary>
/// Extension methods for reading enchantments from a Creature.
/// </summary>
public static class EnchantmentHelpers
{
    /// <summary>
    /// Picks a random active enchantment from a creature's enchantment registry.
    ///
    /// Uses a ReaderWriterLockSlim to safely read the enchantment list from a potentially
    /// multi-threaded context without blocking other readers.
    ///
    /// Returns true and sets "random" to the chosen enchantment if any exist.
    /// Returns false (and sets random to null) if the creature has no enchantments.
    /// </summary>
    public static bool TryGetRandomEnchantment(this Creature creature, out PropertiesEnchantmentRegistry random)
    {
        ICollection<PropertiesEnchantmentRegistry> value = creature.Biota.PropertiesEnchantmentRegistry;
        ReaderWriterLockSlim rwLock = creature.BiotaDatabaseLock;

        random = null;
        if (value == null)
            return false;

        // Acquire a read lock so other threads can still read simultaneously,
        // but no thread can write while we're reading.
        rwLock.EnterReadLock();
        try
        {
            random = value.GetRandom<PropertiesEnchantmentRegistry>();
        }
        finally
        {
            // Always release the lock — even if an exception occurs
            rwLock.ExitReadLock();
        }

        return random is not null;
    }
}

/// <summary>
/// Helpers for working with positions in the world.
/// Currently empty — a Translate method is stubbed out in comments as a future addition.
/// </summary>
public static class PositionHelpers
{
    // Future: Position.Translate(distanceInFront, radians) — move a position forward by
    // a distance, optionally rotated. Commented out because the rotation math needs work.
}
