# AutoLoot

An ACEmulator server mod that automatically loots items from creature corpses based on configurable rules. Built with [ACE.BaseMod](https://github.com/aquafir/ACE.BaseMod) and Harmony.

---

## What it does

When a player kills a creature, AutoLoot checks the corpse and moves matching items directly into the player's inventory — no manual looting required. Each player controls their own settings independently; one player's filters have no effect on anyone else.

Each kill is processed in three passes:

1. **Profile pass** — checks every item against the player's active `.utl` loot profiles (VirindiTank-compatible rule files)
2. **VendorTrash pass** — picks up any unclaimed item whose sell value is at least N× its burden (configurable ratio)
3. **Unknown Scrolls pass** — picks up any unclaimed scroll the player hasn't learned yet

After looting, a chat summary tells you what was picked up and why.

---

## Installation

1. Build the project (or download a release) and copy the output folder to `C:\ACE\Mods\AutoLoot\`
2. Place your `.utl` loot profiles in `C:\ACE\Mods\AutoLoot\LootProfiles\`
3. Restart the server or hot-reload with `/mod f AutoLoot`
4. Players can type `/autoloot` in-game to get started

---

## Commands

All commands start with `/autoloot`.

| Command | Description |
|---|---|
| `/autoloot` | Show the full menu — available profiles, filter status, and all commands |
| `/autoloot on` | Enable everything (all profiles + VendorTrash + Scrolls) |
| `/autoloot off` | Disable everything |
| `/autoloot <#>` | Toggle a profile on/off by its index number |
| `/autoloot <name>` | Toggle a profile on/off by partial name match |
| `/autoloot details` | Toggle loot notifications in chat on/off |
| `/autoloot vt` | Toggle the VendorTrash filter on/off |
| `/autoloot vt <ratio>` | Set a custom value:burden ratio and enable VendorTrash (e.g. `/autoloot vt 30`) |
| `/autoloot scrolls` | Toggle the Unknown Scrolls filter on/off |
| `/autoloot rares` | Toggle server-wide broadcast when you loot a rare item |

---

## Included Loot Profiles

| Profile | What it loots |
|---|---|
| `PyrealsTradeNotes.utl` | Pyreals and Trade Notes (enabled by default for new characters) |
| `AltCurrency.utl` | Alternative currency items |
| `Rares.utl` | All rare items |
| `PincerTuskMatron.utl` | Pincer, Tusk, and Matron quest turn-in pieces |
| `PyrealMotes.utl` | Pyreal Motes |
| `Peas.utl` | Peas |

Server admins can add any VirindiTank-compatible `.utl` file to the `LootProfiles/` folder and it will appear in every player's profile list automatically.

---

## Loot Notifications

When loot notifications are on (the default), AutoLoot sends a chat message after each kill:

```
[AutoLoot] You've looted a Trade Note, 150 Pyreals, and a Fine Leather Coat [$]!
[AutoLoot] [!] You can learn: War Magic VI
```

- **`[$]`** — item was picked up by the VendorTrash filter (good value-to-weight ratio)
- **`[!]`** — scroll contains a spell you haven't learned yet

Turn notifications off with `/autoloot details`.

---

## Persistence

Every player's settings are saved to a JSON file when changed and restored automatically on their next session — even if the server restarts. Settings that persist:

- Which profiles are active
- Whether loot notifications are on
- VendorTrash on/off and the custom ratio
- Unknown Scrolls on/off
- Rare broadcast on/off

Files are stored at: `LootProfiles/PlayerData/{character-guid}.json`

---

## Quest Item Protection

AutoLoot includes a safeguard against using it to bypass quest timers. Items matching names in the `NoDuplicateNames` list (default: `Pincer`, `Tusk`, `Matron`) will not be looted if the player already has one of the same type anywhere in their inventory — including inside backpacks. This prevents stockpiling quest turn-in pieces ahead of cooldown timers.

Server admins can expand this list in `Settings.json`.

---

## Server Configuration (`Settings.json`)

| Setting | Default | Description |
|---|---|---|
| `LootProfilePath` | `Mods/AutoLoot/LootProfiles` | Folder where `.utl` profiles are stored |
| `DefaultProfile` | `PyrealsTradeNotes.utl` | Profile auto-enabled for new characters |
| `NoDuplicateNames` | `["Pincer", "Tusk", "Matron"]` | Name fragments that trigger the one-per-player quest item check |

---

## Requirements

- [ACEmulator](https://github.com/ACEmulator/ACE)
- [ACE.BaseMod](https://github.com/aquafir/ACE.BaseMod) (NuGet package — bundled automatically at build)
