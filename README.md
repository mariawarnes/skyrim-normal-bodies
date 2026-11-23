# Normal Bodies

A [Synthesis](https://github.com/Mutagen-Modding/Synthesis) patcher for Skyrim Special Edition that gives NPCs realistic builds instead of bodybuilder physiques.

| Who | Build | Default weight |
|---|---|---|
| Everyone else: farmers, merchants, mages, beggars, nobles who don't fight | **Skinny** | 0–25 |
| Guards, soldiers, bandits, mercenaries, other fighters | **Marathon runner**: lean and wiry | 20–45 |
| Rich fighters: housecarls, Jarls who fight, Thalmor justiciars, anyone in high-tier armour | **Rugby player**: solid, not sculpted | 60–80 |

Skyrim gives each NPC a body by blending a thin mesh (weight 0) with a heavy, muscular one (weight 100). Many NPCs are set close to 100, which is where the bodybuilder look comes from. This patcher assigns each NPC to one of the three groups above and moves their weight into that group's range. Nothing gets near 100 unless you raise the ranges yourself.

It doesn't replace meshes, so it works with the vanilla body or any body mod (CBBE, 3BA, UNP, SOS, SAM, HIMBO, ...). It also works on NPCs added by other mods.

## How NPCs are sorted

An NPC is a **guard** if a regex (default `Guard`, but not `Guardian`) matches the editor ID of the NPC, its class or one of its factions. Vanilla's `IsGuardFaction` and `GuardDialogueFaction` both match.

An NPC is a **fighter** if it's a guard, or if at least half of its class's skill weights are combat skills (One-Handed, Two-Handed, Archery, Block, Heavy Armor, Light Armor). Membership in a fighter faction also counts: Bandit, Soldier, Forsworn, Mercenary, Companions, Dawnguard, Vigilant, Silverhand, Warrior.

A fighter is **rich** if any of these is true:
- Their default outfit contains high-tier armour: steel plate, orcish, dwarven, gilded elven, glass, ebony, daedric, dragon, Blades, stalhrim, Nordic carved, chitin heavy, or anything with the `ClothingRich` keyword. A leveled armour list counts only if at least half of its entries are high-tier, so a bandit chief who only rolls ebony at high level stays a marathon runner.
- The armour pieces in their outfit are worth at least 1200 septims together.
- They're in a rich faction (`Jarl`, `Housecarl`, `Thane`, `Noble`, `Thalmor`, `Penitus`, `Legate`, `Officer`, `General`, `Blades`).

Wealth only matters for fighters. A rich merchant or a Thalmor ambassador stays skinny.

The patcher skips the player, children, non-humanoid creatures, and NPCs in your exclusion list.

**Templates:** most guards and bandits don't have a weight of their own. They inherit it ("Traits") from a generic template NPC, often through a leveled list. The patcher follows these templates both ways:
- When an NPC inherits its class, factions or outfit, the patcher uses the inherited data to sort it.
- When an NPC inherits its weight, its group is passed up to the template NPC that owns the weight. If several NPCs share one template, the most muscular group wins.

The same NPC always gets the same weight within its range, so running the patcher again doesn't reshuffle anyone.

## Settings

You can change all of these in the Synthesis UI:

- The three weight ranges.
- **Mode**:
  - `KeepIfInRange` (default) only changes NPCs whose current weight is outside their group's range. This keeps vanilla variety and changes as few NPCs as possible.
  - `AlwaysReassign` gives every NPC a new weight.
- Turn males or females on or off.
- The fighter skill-share threshold, the rich outfit value, the rich armour keywords, and the guard, fighter and rich regexes.
- A list of NPCs to exclude, for followers you've tuned by hand and similar.

## Installation

1. Install [Synthesis](https://github.com/Mutagen-Modding/Synthesis) and the .NET SDK it asks for.
2. In Synthesis, add a patcher:
   - **Git Repository**: paste this repository's URL. The repo has to be public for this to work.
   - **Local Solution**: clone the repo and point Synthesis at `NormalBodies.sln`.
3. Put the patcher in a group, run it, and enable the output plugin. Place it after every mod that edits NPCs, which is the normal place for a Synthesis patch.
4. **Regenerate FaceGen.** See the next section; don't skip it.

## ⚠️ FaceGen: avoiding neck seams

Each NPC's head is a pre-built FaceGen mesh made for that NPC's original weight. If you change the weight without rebuilding the head, the neck won't line up with the body and you get a visible seam.

The output plugin contains only the NPCs whose weight actually changed. To rebuild their heads:

1. Open the Creation Kit with your full load order, including any NPC appearance overhauls, and set the Synthesis output plugin as the active file.
2. In the Object Window, open **Actors → Actor** and sort or filter so that only the records changed by the patch show. Records changed by the active file are marked with `*`.
3. Select those NPCs and press **Ctrl+F4** to export FaceGen.
4. Copy the generated `meshes\actors\character\FaceGenData` and `textures\actors\character\FaceGenData` into a mod that loads last.

The Synthesis log lists every changed NPC with its old and new weight. To get fewer NPCs to regenerate, keep `KeepIfInRange` mode or add NPCs to the exclusion list.

## Tuning tips

- If marathon runners still look too bulky, your body mod's weight-100 mesh is probably very muscular. Lower the Marathon and Rugby ranges, or build the high-weight body in BodySlide from a less extreme preset.
- If one specific NPC lands in the wrong group, add them to the exclusion list, or adjust the regexes.

## Development

```bash
dotnet test
```

The tests build small in-memory plugins (a guard, a bandit, a housecarl in steel plate, a farmer, a child, a templated guard, ...) and check how each one is sorted and what weight it gets.
