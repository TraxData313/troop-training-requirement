# Manager progress: Troop Training Requirement

## Task (Toni, 2026-11-05, evening — he is asleep; no in-game testing until 2026-11-06)
New separate Bannerlord mod `troop-training-requirement` (folder `Documents\BannerlordMods\troop-training-requirement`,
module Id `TroopTrainingRequirement`, display "Troop Training Requirement", dev deploy `Modules\TroopTrainingRequirement.Dev`).
Sister mod of `item-skill-requirement-expanded` (same folder level) — copy its conventions, tooling and code style.

A troop can only be UPGRADED into a target whose combat skills do not exceed what someone in the party can teach:
for each combat skill (One Handed, Two Handed, Polearm, Bow, Crossbow, Throwing, Riding, Athletics), the target
troop's skill must be <= the best value of that skill among the "trainers" (the player + companion heroes in the
same party). Forces the player to level up or bring a good companion. Recruiting already-trained troops (any tier,
from towns/villages/prisoners) is unaffected.

### Design
- Hook: DECORATOR over the abstract `PartyTroopUpgradeModel` (decompile:
  `%TEMP%\trax_decomp\full_TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\PartyTroopUpgradeModel.cs`,
  `...GameComponents\DefaultPartyTroopUpgradeModel.cs`): forward every member to BaseModel; in
  `CanPartyUpgradeTroopToTarget` (and `IsTroopUpgradeable` if needed) additionally require the skill check.
  Pattern for decorators: `trax_combat_enhancements\src\TraxCombat.Module\SubModule.cs` RegisterModels; never subclass Default*.
- Who: only parties of the PLAYER's clan (main party first; clan parties led by companions use THEIR party's heroes —
  setting). AI lords' parties: vanilla (setting off by default).
- Trainers = heroes in the party (player + companions/clan members present). Setting: "Count the whole clan" (off).
- Settings (MCM, optional like the sister mod): enabled, which skills count (each toggle, default all 8), allowance
  (troop may exceed the best trainer by N points, default 0), apply to companion-led clan parties (on), apply to AI (off).
- UI, party screen: when blocked, the upgrade button is disabled and its hint says why, vanilla style, e.g.
  "Requires Athletics 120 — your best is 110 (Cadan)". All failing skills listed. Orange (not vanilla red) where a
  colour is used, matching the sister mod's orange #FFA028 brush approach (no UIExtenderEx if avoidable).
- Encyclopedia troop tree (unit pages): targets the current party couldn't train get an orange outline/tint (if
  feasible without heavy UI deps; otherwise report and skip).
- Upgrade XP keeps accumulating; nothing is saved into the campaign (removable mid-campaign).

## Steps
1. [x] Scaffold (copy item-skill-requirement-expanded's layout: Core netstandard2.0 no game refs + Module net472 + xunit
   tests + tools\deploy.ps1/package.ps1 + CLAUDE.md + README + .gitignore + LICENSE Unlicense). Builds, empty mod deploys.
2. [ ] Rule: Core logic (pure, tested) + PartyTroopUpgradeModel decorator + trainers lookup + MCM settings.
3. [ ] Party screen UI: disabled upgrade + reason hint (+ orange).
4. [ ] Encyclopedia troop tree orange outline.
5. [ ] Independent review, docs (README, store texts draft, thumbnail later after Toni's screenshots), deploy, commit,
   create PRIVATE GitHub repo TraxData313/troop-training-requirement and push. Do NOT publish anywhere.

## Log
- 2026-11-05 step 1 done: scaffold copied from item-skill-requirement-expanded (Core/Module/tests, tools, CLAUDE.md, README, Unlicense). Build 0/0, 1 test passes, AssemblyGuard OK, deployed to Modules\TroopTrainingRequirement.Dev. git init (main), no remote.
