# Troop Training Requirement

A Mount & Blade II: Bannerlord mod (1.4.8, War Sails compatible): **you can only upgrade a troop into a unit your
party can teach.** For each combat skill (One Handed, Two Handed, Polearm, Bow, Crossbow, Throwing, Riding,
Athletics), the upgraded troop may not have more skill than the best of the heroes travelling with it: you and the
companions in your party. Want veteran archers? Learn the bow, or bring someone who has.

Recruiting is never blocked. Already-trained troops from towns, villages, prisoners or anywhere else join at any
tier, as in vanilla. The rule is only about turning your own recruits into better soldiers.

Not released yet: no Nexus or Steam page so far.

## The rule, with an example

You want to upgrade an **Imperial Trained Archer** into an **Imperial Veteran Archer** (tier 4). The Veteran Archer
has Athletics 100, One Handed 100, Bow 100, Two Handed 60, Polearm 60, Crossbow 45, Throwing 45 and Riding 15.

Your party: you (One Handed 120, Athletics 110, Bow 30) and your companion Cadan (Bow 64, Polearm 80, Riding 90).
Every skill is checked against the best hero for that skill:

- One Handed 100: you have 120. Fine.
- Athletics 100: you have 110. Fine.
- Bow 100: the best is Cadan with 64. **Short.**
- The rest: someone has enough. Fine.

So the upgrade button is disabled and its tooltip says why:

```
Requires Bow 100 - your best is 64 (Cadan)
or reach clan tier 4
```

Every short skill gets its own line. Train your bow, hire a better archer as a companion, or reach clan tier 4, and
the button works again. The troop keeps all its upgrade XP in the meantime.

## Clan tier unlocks troops up to that tier

A target troop whose tier is **at most your clan tier** can always be upgraded to, whatever your skills. Clan tier 2:
every tier 1 and 2 troop is open; tier 3 and up must be taught. Clan tier 6 opens everything. When a troop is blocked
and a clan tier could open it, the tooltip ends with "or reach clan tier N". A setting, on by default.

## Whose parties

- **Your party:** party screen upgrades. Trainers are the heroes in your party right now, you included: move a
  companion out on the party screen and the buttons update at once.
- **Your clan's parties** (led by a companion or family member): their automatic daily upgrades. Each party uses its
  own heroes as trainers and your clan tier. A blocked target is simply skipped: a troop with two targets takes the
  one its party can teach, a troop with none waits and keeps its XP.
- **AI lords' parties:** vanilla by default (a setting turns the rule on for them, with their own heroes and clan
  tier).
- **Always vanilla:** garrisons, militia, villagers, caravans (also companion-led ones), patrols and bandits.

Trainers are alive heroes who are not prisoners. With "Count the whole clan" on, every adult hero of the party's
clan counts, wherever they are.

## On screen

- **Party screen:** a blocked upgrade button is disabled, the reason lines are added under vanilla's tooltip (XP,
  cost), and the button is **orange** when our rule is the only thing stopping it. When vanilla also stops it (not
  enough XP, gold or horses), vanilla's grey or red look stays. Ctrl / Shift-click and the "Upgrade" pop-up follow
  the same rule.
- **Encyclopedia:** on a unit page, the troop tree marks the units your party can't upgrade into yet with an orange
  frame and tinted portrait; their tooltip ends with the same reason lines. It is worked out when the page opens.

## Settings

With [Mod Configuration Menu](https://www.nexusmods.com/mountandblade2bannerlord/mods/612) (**optional**), under
Mod Options > Troop Training Requirement. Changes apply at once. Without MCM the mod runs on these defaults:

| Group | Setting | Default |
|---|---|---|
| General | Enabled | on |
| General | Clan tier unlocks troops up to that tier | on |
| General | Allowance (the troop may exceed the best trainer by this many points, 0-100) | 0 |
| Skills that count | One Handed, Two Handed, Polearm, Bow, Crossbow, Throwing, Riding, Athletics (one toggle each) | all on |
| Whose parties | Your party | on |
| Whose parties | Your clan's parties | on |
| Whose parties | AI lords' parties | off |
| Trainers | Count the whole clan | off |

## Compatibility

- **[Troop Equipment Upgrade](https://steamcommunity.com/sharedfiles/filedetails/?id=3674824323)** (and Culture
  Equipment Seeder): works very nicely with them. The gear has to be bought and the skill has to be taught, so better
  troops are earned, a slower and more rewarding progression. The training check always comes first: an upgrade
  your party can't teach is refused before Troop Equipment Upgrade reserves or buys any gear, so you never pay for an
  upgrade that didn't happen. Load order doesn't matter.
- **Item Skill Requirement Expanded** (the sister mod, same author pair): it makes gear skill requirements soft for
  heroes; this one makes troop upgrades ask for skill. Together your heroes' skills matter twice: for what they can
  wield and for who they can train.
- Other mods that change the troop upgrade model (War Sails included) are wrapped, not replaced.

## Known limits

- Only the 8 combat skills count, at the heroes' plain skill values (no perk or captain bonuses).
- Mods that upgrade troops with their own code, not through the party screen or the daily auto-upgrade, are not
  covered.
- The encyclopedia verdict is worked out when the unit page opens: a settings change shows on the next page. A mod
  that replaces the encyclopedia tree node layout gets no orange frame (the rule still holds).
- The reason text is English only for now.
- Nothing is saved into your campaign: add or remove the mod at any time.

## Requirements

- [Harmony](https://www.nexusmods.com/mountandblade2bannerlord/mods/2006)
- [Mod Configuration Menu](https://www.nexusmods.com/mountandblade2bannerlord/mods/612) - optional, for the settings

## Build

```
dotnet build TroopTrainingRequirement.sln -c Release
dotnet test tests\TroopTrainingRequirement.Core.Tests -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
```

Set your game path in `Directory.Build.props.user` if it differs from `Directory.Build.props`.

## License

Public domain ([Unlicense](LICENSE)).
