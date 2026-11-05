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
3. [x] Party screen UI: disabled upgrade + reason hint (+ orange).
4. [ ] Encyclopedia troop tree orange outline.
5. [ ] Independent review, docs (README, store texts draft, thumbnail later after Toni's screenshots), deploy, commit,
   create PRIVATE GitHub repo TraxData313/troop-training-requirement and push. Do NOT publish anywhere.

## Log
- 2026-11-05 step 1 done: scaffold copied from item-skill-requirement-expanded (Core/Module/tests, tools, CLAUDE.md, README, Unlicense). Build 0/0, 1 test passes, AssemblyGuard OK, deployed to Modules\TroopTrainingRequirement.Dev. git init (main), no remote.
- 2026-11-05 step 2 done: Core TrainingRule/TrainingSettings/SettingsMenu (+ clan tier bypass, 21 tests); module Training (trainers, party kinds), TrainingUpgradeModel decorator, PartyScreenGuardPatch, AutoUpgradePatch, McmSettings (4 groups, 15 settings). Build 0/0, AssemblyGuard OK, deployed. Not tested in game.
- 2026-11-05 step 3 done: Patches\UpgradeButtonPatch - InitializeUpgrades postfix (blocked target -> IsInsufficient=true, AvailableUpgrades=0, reason lines appended to vanilla's hint, NumOfUpgradeableTroops/IsTroopUpgradable recomputed), PartyVM.Update postfix (hero moved / transfer all -> RefreshTroopsUpgradeable + UpdateTroopManagerPopUpCounts), orange via PartyUpgradeButtonWidget.UpdateVisual postfix keyed by the portrait ImageId (+ ImageIdentifierWidget.ImageId setter postfix for bind order), brush module\GUI\Brushes\TroopTrainingRequirement.xml. Core UpgradeButtonLook (+9 tests, 30 total). Build 0/0, AssemblyGuard OK, deployed. Not tested in game.
- 2026-11-05 compat step (Troop Equipment Upgrade v1.2.1, Workshop 3674824323): decompiled (scratchpad, not in repo). It prefixes PartyScreenLogic.UpgradeTroop (reserves gear in a static UpgradeSession, prices market gear) and charges at DoneLogic; vanilla UpgradeTroop calls ValidateCommand (our guard) only INSIDE its body, so a blocked upgrade reaching UpgradeTroop got its gear reserved and charged at Done. Fix: Patches\UpgradeGatePatch - UpgradeTroop prefix, Priority.First + HarmonyBefore("troop.equipment.upgrade") (Core\CompatOrder); blocked -> quick message + return false (Harmony 2.4.2 skips the original and all later prefixes) + clears its UpgradeSession.LastUpgradeSucceeded by reflection (its gold-text postfix still runs). New tests\TroopTrainingRequirement.Compat.Tests (net472, game's 0Harmony.dll, 4 tests) prove the order either patch order. CLAUDE.md "Compatibility: Troop Equipment Upgrade", README compatibility section. Build 0/0, 34 tests pass, AssemblyGuard OK. Not tested in game; Nexus page for that mod not found reliably (not linked).

## Notes for step 4 (encyclopedia troop tree)
- Troop Equipment Upgrade adds no encyclopedia or button UI; nothing to coordinate there. Log at campaign start should show 'compat: Troop Equipment Upgrade found ...' and the UpgradeTroop prefix list with ours (800) before troop.equipment.upgrade (400).
- In-game check for Toni (compat): with TEU, a blocked target stays disabled/orange; no "Equipping:/Buying:" message ever appears for it, and Done charges only for upgrades that happened.
- Step 3 pattern to reuse: the VM decides, a widget postfix swaps the brush, keyed by something the widget already binds (there: the portrait ImageId = the target's character code). No UIExtenderEx.
- Use Training.Check(PartyBase.MainParty, troop, target) (real member roster) in the encyclopedia - no pending roster there.
- Vanilla has no separate "Upgrade all" button: the top "Upgrade" button opens the troop manager pop-up (PartyUpgradeTroopVM), whose per-troop buttons are the same UpgradeTargetVM / PartyUpgradeButtonWidget (covered); Ctrl/Shift-click (whole stack / 5) go through ExecuteUpgrade (covered). Troops blocked on every target land in the pop-up's disabled section; its open button is disabled when nothing is upgradable.
- In-game checks for Toni (step 3): hover a blocked target (orange frame + tint, reason lines under vanilla's cost), move a companion out/in and watch the button flip, the Upgrade pop-up, no quick message in normal play; the log must show 'applied: PartyCharacterVM.InitializeUpgrades ...' and no ERROR lines.

## Notes from step 2 (party screen internals)
- Vanilla routes NOTHING but bandit troops through CanPartyUpgradeTroopToTarget: PartyCharacterVM.InitializeUpgrades (CVMC\...Party\PartyCharacterVM.cs ~1257) asks it only when Character.Culture.IsBandit, and PartyScreenLogic.ValidateCommand (UpgradeTroop branch, ~651) checks xp/gold/items only; PartyUpgraderCampaignBehavior.GetPossibleUpgradeTargets asks it only for Occupation.Bandit. Hence the two enforcement patches.
- Enforcement now: PartyScreenGuardPatch (ValidateCommand postfix -> false + quick message with the reason; every button incl. troop manager pop-up goes through UpgradeTroop -> ValidateCommand) and AutoUpgradePatch (removes blocked targets from the auto-upgrade list; main party is skipped by vanilla there).
- API: Training.Check(PartyBase party, TroopRoster roster, CharacterObject troop, CharacterObject target) -> TrainingCheck (Allowed, Shortfalls, ClanTierNeeded); Training.BlockReason(party, roster, troop, target) -> string or null; TrainingRule.ReasonLines/ReasonText. In the screen pass PartyScreenLogic.RightOwnerParty and MemberRosters[(int)PartyRosterSide.Right] (pending roster).
- Where vanilla builds the button: PartyCharacterVM.InitializeUpgrades computes per target flag (IsAvailable = level/xp ok and upgrades not disabled), flag2 (IsInsufficient = no items/gold, or bandit + !CanPartyUpgradeTroopToTarget), num (AvailableUpgrades), and upgradeHint = CampaignUIHelper.GetUpgradeHint(...) (CVMC\CampaignUIHelper.cs ~2316), then Upgrades[i].Refresh(num, flag, flag2, itemsOk, perksOk, hint, mariner). UpgradeTargetVM.ExecuteUpgrade needs IsAvailable && !IsInsufficient; hint via GetHint() -> _hintString. Simplest step 3: postfix InitializeUpgrades, for each blocked target call Upgrades[i].Refresh(0, flag, true, ..., hint + reason) or postfix GetUpgradeHint + set IsInsufficient; also NumOfUpgradeableTroops/IsTroopUpgradable (recompute after) and PartyUpgradeTroopVM (troop manager pop-up lists by NumOfReadyToUpgradeTroops).
- Party kinds: Main = PartyBase.MainParty; Clan = hero-led MobileParty whose LeaderHero.Clan is the player clan; AI = other hero-led; Other (garrisons, militia, villagers, hero-less caravans/patrols, bandits) never covered.
