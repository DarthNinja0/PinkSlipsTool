# Pink Slips Tool

A Windows desktop tool (WPF / .NET 10) for **EA SPORTS College Football 27** dynasty leagues.

Earn **stars** for big wins, spend them on **perks**, spin the **Pink Slips Wheel**, and edit your
dynasty save file directly — steal players, upgrade dev traits, and more.

---

## Requirements

- **Windows 10/11** (x64)
- No install needed. Run the self-contained exe:
  - `C:\Users\<you>\Documents\Mods\PinkSlipsTool\PinkSlipsTool.exe`
  - (or any copy of the single-file build)
- Dynasty saves live in `C:\Users\<you>\Documents\EA SPORTS College Football 27\saves\`

---

## Getting Started

1. Launch Pink Slips Tool.
2. Click **Load Dynasty File** and pick your save from the saves folder
   (e.g. `DYNASTY-TEST`).
3. A backup is created automatically the moment you load — see *Backups* below.
4. Enter your game stats and hit **Calculate Stars**, or open the wheel directly.
5. Spend stars on perks, or use the file-editing tools on a loaded save.
6. Click **💾 Save** when done, then load that save file in the game.

> The game writes its saves periodically (after games, advancing weeks, etc.). Make a manual copy
> of your save in the game menu before editing if you want an extra safety net.

---

## Features

### ⭐ Star Calculator

Enter your final game stats and earn up to 10 stars:

| Condition | Stars |
|---|---|
| Win | 1 |
| Win by 14+ | 1 |
| Win by 21+ | 1 |
| Shutout (0 points allowed) | 2 |
| 300+ pass yards / 100+ rush yards / 100+ rec yards | 1 each |
| 3+ pass TD / 2+ rush TD / 2+ rec TD | 1 each |
| 2+ sacks | 1 |
| 1+ INT | 1 |
| Defensive TD | 1 each |
| Win turnover battle | 1 |
| Special teams TD | 1 each |

10 stars = **Perfect Game** and an immediate wheel spin. Stars are your currency for the perk shop.

### 🎡 Pink Slips Wheel

Spin for a random perk (weighted odds — extra spins and weaker perks land more often). Each wheel
result can be applied once.

### 🛍️ Perk Shop

Spend stars on any perk:

| Perk | Cost | Effect |
|---|---|---|
| Steal Player | 4 | Take a player from any team (needs dynasty file) |
| Dev Upgrade | 2 | Upgrade a player's dev trait (needs dynasty file) |
| Emergency QB | 2 | Convert any WR to QB for one game |
| Retire Player | 3 | Force a player to retire immediately |
| Chat Picks | 4 | View opponent's play calls for one quarter |
| Drug Test | 3 | Give any player a one-game injury (needs dynasty file) |
| Recruit Boost | 5 | +10% interest on top recruit |
| Transfer Portal | 4 | Guaranteed 5-star transfer next season |
| Stadium Upgrade | 5 | Unlock facility upgrade now |
| Extra Spin | 3 | Earn another free wheel spin |

### 🏈 Dynasty File Editing

These open from perks (Steal Player, Dev Upgrade, Drug Test) once a dynasty file is loaded.

#### Steal Player / Transfer / Cut
- Pick any team, then a player.
- **STEAL** — moves the player to your team permanently. If your roster is at 85 (full), cut
  someone first to make room.
- **TRANSFER** — swaps the selected player with one of your players, so **both rosters stay the
  same size** and nobody is cut.
- **CUT** — releases a player from your roster to free agency.

#### Dev Upgrade
- Pick a player from your roster and raise their dev trait one level:
  Normal → Impact → Star → Elite.

#### Drug Test (injure)
- Give any player a one-game injury.

### 💾 Save / Backup

- **Load** always creates a timestamped backup: `{save}.{yyyyMMdd-HHmmss}.bak` in the saves folder.
- **Save** rewrites the file in place (same format the game expects).
- **Restore Backup** lists all `.bak` backups for the loaded save so you can roll back any edit.
  After restoring, hit **Save** to write it out.

---

## Known Limitations

- **Steal / Transfer roster-adds do not persist in the game yet.** The write to the save file is
  correct and verified (the player's team field is changed and rosters stay 85 on a transfer), but
  when the game loads and re-saves, it **reverts added players to their original team**. Evidence:
  - Dev-trait upgrades and **cuts stick** in-game (cut = released to free agency, which matches the
    game's own logic).
  - Only roster *additions* get reverted — so this is not anti-cheat / tamper protection, it's the
    game recomputing team membership from its own (as-yet unidentified) roster table and
    overwriting the field the tool edits.
- **Cuts and dev-trait upgrades work and persist.** Use those for reliable edits today.
- No mod is required to make transfers stick — the game's authoritative roster table just needs to
  be located and written alongside the field the tool currently edits. Work in progress.

---

## Save File Format Notes

- Dynasty saves are a compressed blob: zlib (`78 9C` header) + DEFLATE payload, big-endian
  Adler-32 checksum, trailing 4-byte length. Fixed size for a given save: **9,646,981 bytes**.
- The tool parses the decompressed payload into "tables" (player, team, coach, depth chart, etc.)
  via `FranchiseTable.ScanTables`, edits records bit-level, recompresses, and rewrites the checksum.
- The player's team is stored as an 8-bit field at bit offset 616 within the player record
  (`FreeAgentTeamIndex = 255`, roster cap = 85).

---

## Building from Source

```powershell
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -o .\publish
```

Output: a single `PinkSlipsTool.exe` (~67 MB) in `.\publish`. No .NET runtime install needed on
target machines.

- `.NET 10 SDK` is required to build.
- `PinkSlipsTool.ico` and `Images\*` must stay in the project root / `Images\` folder.

---

## Disclaimer

This tool edits save files directly. Always keep the automatic backups, and consider backing up
your save in the game menu too. Edits are made at your own risk — a corrupted save can usually be
recovered from the `.bak` files via **Restore Backup**.
