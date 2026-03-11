namespace AutoLoot.Loot;

/// <summary>
/// Defines what the auto-loot system should do when an item matches a loot rule.
///
/// When a creature is killed, each item in its corpse is evaluated against the player's
/// active loot profile. The first rule that matches determines the Action taken.
///
/// Currently, all matching items are moved to the player's inventory. The action value
/// is sent to the player as part of the loot summary message so they can see which rule
/// matched and what it decided.
///
/// Note: "Destroy" is commented out — it would delete the item without looting it,
/// but this hasn't been implemented yet.
/// </summary>
public enum Action
{
    /// <summary>No rule matched — leave the item in the corpse.</summary>
    None,

    /// <summary>Pick up the item and keep it in inventory.</summary>
    Keep,

    /// <summary>Pick up the item to be salvaged later.</summary>
    Salvage,

    /// <summary>Pick up the item to be sold to a vendor later.</summary>
    Sell,

    /// <summary>Pick up the item to be read (e.g. a scroll or book).</summary>
    Read,

    /// <summary>Pick up the item, but only up to a specific quantity (the "keep up to N" rule).</summary>
    KeepAmount,

    // Destroy — would delete the item without picking it up. Not yet implemented.
    //Destroy,
};
