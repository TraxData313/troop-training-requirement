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
- Clan tier bypass (Toni, 2026-11-05): if the TARGET troop's tier <= the party owner's clan tier (player: Clan.PlayerClan.Tier),
  the upgrade is allowed whatever the skills (MCM "Clan tier unlocks troops up to that tier", General group, default on).
  When it is why a check passes, nothing is shown; when blocked, the reason ends with "or reach clan tier N" (N = target
  tier, only when N <= 6 and the party has a clan).
- Whose parties (step 2 decision): Main = the player's party; Clan = hero-led parties of the player's clan; AI = other
  hero-led parties. Garrisons, militia, villagers, hero-less caravans / patrols and bandits are never covered.

## Steps
1. [x] Scaffold (copy item-skill-requirement-expanded's layout: Core netstandard2.0 no game refs + Module net472 + xunit
   tests + tools\deploy.ps1/package.ps1 + CLAUDE.md + README + .gitignore + LICENSE Unlicense). Builds, empty mod deploys.
2. [x] Rule: Core logic (pure, tested) + PartyTroopUpgradeModel decorator + trainers lookup + MCM settings.
3. [ ] Party screen UI: disabled upgrade + reason hint (+ orange).
4. [ ] Encyclopedia troop tree orange outline.
5. [ ] Independent review, docs (README, store texts draft, thumbnail later after Toni's screenshots), deploy, commit,
   create PRIVATE GitHub repo TraxData313/troop-training-requirement and push. Do NOT publish anywhere.

## Log
- 2026-11-05 step 1 done: scaffold copied from item-skill-requirement-expanded (Core/Module/tests, tools, CLAUDE.md, README, Unlicense). Build 0/0, 1 test passes, AssemblyGuard OK, deployed to Modules\TroopTrainingRequirement.Dev. git init (main), no remote.
- 2026-11-05 step 2 done: Core TrainingRule/TrainingSettings/SettingsMenu (+ clan tier bypass, 21 tests); module Training (trainers, party kinds), TrainingUpgradeModel decorator, PartyScreenGuardPatch, AutoUpgradePatch, McmSettings (4 groups, 15 settings). Build 0/0, AssemblyGuard OK, deployed. Not tested in game.

## Notes for step 3 (party screen UI)
- Vanilla routes NOTHING but bandit troops through CanPartyUpgradeTroopToTarget: PartyCharacterVM.InitializeUpgrades (CVMC\...Party\PartyCharacterVM.cs ~1257) asks it only when Character.Culture.IsBandit, and PartyScreenLogic.ValidateCommand (UpgradeTroop branch, ~651) checks xp/gold/items only; PartyUpgraderCampaignBehavior.GetPossibleUpgradeTargets asks it only for Occupation.Bandit. Hence the two enforcement patches.
- Enforcement now: PartyScreenGuardPatch (ValidateCommand postfix -> false + quick message with the reason; every button incl. troop manager pop-up goes through UpgradeTroop -> ValidateCommand) and AutoUpgradePatch (removes blocked targets from the auto-upgrade list; main party is skipped by vanilla there).
- API: Training.Check(PartyBase party, TroopRoster roster, CharacterObject troop, CharacterObject target) -> TrainingCheck (Allowed, Shortfalls, ClanTierNeeded); Training.BlockReason(party, roster, troop, target) -> string or null; TrainingRule.ReasonLines/ReasonText. In the screen pass PartyScreenLogic.RightOwnerParty and MemberRosters[(int)PartyRosterSide.Right] (pending roster).
- Where vanilla builds the button: PartyCharacterVM.InitializeUpgrades computes per target flag (IsAvailable = level/xp ok and upgrades not disabled), flag2 (IsInsufficient = no items/gold, or bandit + !CanPartyUpgradeTroopToTarget), num (AvailableUpgrades), and upgradeHint = CampaignUIHelper.GetUpgradeHint(...) (CVMC\CampaignUIHelper.cs ~2316), then Upgrades[i].Refresh(num, flag, flag2, itemsOk, perksOk, hint, mariner). UpgradeTargetVM.ExecuteUpgrade needs IsAvailable && !IsInsufficient; hint via GetHint() -> _hintString. Simplest step 3: postfix InitializeUpgrades, for each blocked target call Upgrades[i].Refresh(0, flag, true, ..., hint + reason) or postfix GetUpgradeHint + set IsInsufficient; also NumOfUpgradeableTroops/IsTroopUpgradable (recompute after) and PartyUpgradeTroopVM (troop manager pop-up lists by NumOfReadyToUpgradeTroops).
- Party kinds: Main = PartyBase.MainParty; Clan = hero-led MobileParty whose LeaderHero.Clan is the player clan; AI = other hero-led; Other (garrisons, militia, villagers, hero-less caravans/patrols, bandits) never covered.
