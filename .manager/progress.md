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
4. [x] Encyclopedia troop tree orange outline.
5. [x] Independent review, docs (README, store texts draft, thumbnail later after Toni's screenshots), deploy, commit,
   create PRIVATE GitHub repo TraxData313/troop-training-requirement and push. Do NOT publish anywhere.

## Log
- 2026-11-05 step 1 done: scaffold copied from item-skill-requirement-expanded (Core/Module/tests, tools, CLAUDE.md, README, Unlicense). Build 0/0, 1 test passes, AssemblyGuard OK, deployed to Modules\TroopTrainingRequirement.Dev. git init (main), no remote.
- 2026-11-05 step 2 done: Core TrainingRule/TrainingSettings/SettingsMenu (+ clan tier bypass, 21 tests); module Training (trainers, party kinds), TrainingUpgradeModel decorator, PartyScreenGuardPatch, AutoUpgradePatch, McmSettings (4 groups, 15 settings). Build 0/0, AssemblyGuard OK, deployed. Not tested in game.
- 2026-11-05 step 3 done: Patches\UpgradeButtonPatch - InitializeUpgrades postfix (blocked target -> IsInsufficient=true, AvailableUpgrades=0, reason lines appended to vanilla's hint, NumOfUpgradeableTroops/IsTroopUpgradable recomputed), PartyVM.Update postfix (hero moved / transfer all -> RefreshTroopsUpgradeable + UpdateTroopManagerPopUpCounts), orange via PartyUpgradeButtonWidget.UpdateVisual postfix keyed by the portrait ImageId (+ ImageIdentifierWidget.ImageId setter postfix for bind order), brush module\GUI\Brushes\TroopTrainingRequirement.xml. Core UpgradeButtonLook (+9 tests, 30 total). Build 0/0, AssemblyGuard OK, deployed. Not tested in game.
- 2026-11-05 compat step (Troop Equipment Upgrade v1.2.1, Workshop 3674824323): decompiled (scratchpad, not in repo). It prefixes PartyScreenLogic.UpgradeTroop (reserves gear in a static UpgradeSession, prices market gear) and charges at DoneLogic; vanilla UpgradeTroop calls ValidateCommand (our guard) only INSIDE its body, so a blocked upgrade reaching UpgradeTroop got its gear reserved and charged at Done. Fix: Patches\UpgradeGatePatch - UpgradeTroop prefix, Priority.First + HarmonyBefore("troop.equipment.upgrade") (Core\CompatOrder); blocked -> quick message + return false (Harmony 2.4.2 skips the original and all later prefixes) + clears its UpgradeSession.LastUpgradeSucceeded by reflection (its gold-text postfix still runs). New tests\TroopTrainingRequirement.Compat.Tests (net472, game's 0Harmony.dll, 4 tests) prove the order either patch order. CLAUDE.md "Compatibility: Troop Equipment Upgrade", README compatibility section. Build 0/0, 34 tests pass, AssemblyGuard OK. Not tested in game; Nexus page for that mod not found reliably (not linked).
- 2026-11-05 step 4 done: Patches\EncyclopediaTreePatch - EncyclopediaUnitPageVM ctor postfix (every unit page open) walks the tree, Training.Check(PartyBase.MainParty, parent, child) per edge (real roster, MCM + clan tier bypass via Training); blocked units keyed by portrait character code + CharacterObject. EncyclopediaUnitTreeNodeItemBrushWidget.OnLateUpdate postfix (per node, once per page via a generation counter in a ConditionalWeakTable) swaps the node button's brush to TroopTrainingRequirement.Encyclopedia.ElementBlocked (copy of Encyclopedia.SubPage.Element, frame #FFA028) and tints the portrait; undone if the verdict changes. Tooltip: EncyclopediaUnitVM.ExecuteBeginHint/EndHint prefixes mark the hovered blocked unit, TooltipRefresherCollection.RefreshCharacterTooltip postfix appends a blank line + orange reason lines for that unit only. No prefab change, no UIExtenderEx. Build 0/0, 34 tests pass, AssemblyGuard OK, deployed. Not tested in game.
- 2026-11-05 step 5 done (independent review): checked every patch against the decompile. Sound: decorator forwards all 8 abstract members (War Sails' model is a decorator too); UpgradeGatePatch prio 800 + HarmonyBefore vs TEU's default 400 and returns false correctly; TEU names match its decompile (TroopEquipmentUpgrade.Patches.UpgradeSession.LastUpgradeSucceeded, internal static bool) and clearing it is safe (TEU's own prefix sets it on every run); AutoUpgradePatch removing a target is safe (each TroopUpgradeArgs carries its own UpgradeChance, SelectPossibleUpgrade sums what is left, a two-target troop takes the allowed one); the InitializeUpgrades postfix mirrors vanilla's bandit path; vanilla UpdateVisual resets the portrait colour, so the orange tint never sticks; the tooltip's args[0] is the CharacterObject; every game entry point in try/catch; nothing saved. Fixed: (1) companion-led caravans and hero-led bandit parties were covered as clan / AI parties - now always vanilla (Core PartyKinds.Classify, 8 tests); (2) UpgradeButtonPatch.Track scanned the whole weak list on every UpdateVisual (quadratic on big rosters) - now a ConditionalWeakTable + amortised prune. Docs: README for players; tools\ NEXUS / STEAM-DESCRIPTION.bbcode, WorkshopCreate / WorkshopUpdate.xml (ITEM_ID placeholder), WORKSHOP-UPLOAD.md, make_workshop_update.py, make_thumbnail.py + preview_thumbnail.html (the thumbnail waits for Toni's screenshots). Build 0/0, 38 + 4 tests pass, AssemblyGuard OK, deployed. Nothing uploaded or published. Not tested in game.
  Uncertain, left as is: party screen orange is keyed by the target's character code, so two troop rows sharing one target (modded trees) where vanilla also blocks one of them can drop the orange on the other (still disabled, vanilla red); encyclopedia _hovered can go stale if HoverEnd never fires (lines still true); OrangeIds / BlockedIds are not cleared at game end (harmless, rebuilt on the next screen); store-text GitHub links 404 while the repo is private.

## Notes for step 5 (review)
All Harmony patches (applied in SubModule.ApplyCampaignPatches at the first campaign start, each in its own try/catch):
- PartyScreenGuardPatch: PartyScreenLogic.ValidateCommand postfix (refuse + quick message).
- UpgradeGatePatch: PartyScreenLogic.UpgradeTroop prefix, Priority.First + HarmonyBefore troop.equipment.upgrade (+ reflection on TEU's UpgradeSession.LastUpgradeSucceeded).
- UpgradeButtonPatch: PartyCharacterVM.InitializeUpgrades postfix, PartyVM.Update postfix, PartyUpgradeButtonWidget.UpdateVisual postfix, ImageIdentifierWidget.ImageId setter postfix (ALL image widgets - hot path, cheap type check).
- AutoUpgradePatch: PartyUpgraderCampaignBehavior.GetPossibleUpgradeTargets postfix.
- EncyclopediaTreePatch: EncyclopediaUnitPageVM ctor postfix, EncyclopediaUnitTreeNodeItemBrushWidget.OnLateUpdate postfix (every frame per tree node; early-outs on the generation check, but does two FindChild calls first), EncyclopediaUnitVM.ExecuteBeginHint/ExecuteEndHint prefixes, TooltipRefresherCollection.RefreshCharacterTooltip postfix (shared by every CharacterObject tooltip in the game; gated by the hovered unit).
- Model: TrainingUpgradeModel decorator (not Harmony).
Risky spots to review:
- Orange keyed by character code strings (party screen and encyclopedia): two different troops with identical codes would share a verdict (unlikely). Static sets are never cleared on game end except via the next page/screen.
- Encyclopedia _hovered: if HoverEnd never fires (page closed mid-hover), a later tooltip for the SAME CharacterObject elsewhere could show the reasons until the next unit page opens (the lines are still true for the main party).
- Encyclopedia verdict is computed once at page open (Toni: refresh when the page opens); changes while the page is open (MCM toggle) show on the next page.
- The tree checks every parent->child edge; a target reachable from two parents uses the last edge's reasons (skill reasons depend only on the target).
- Brush swap on the node ButtonWidget assumes the prefab layout ButtonContainer -> first child ButtonWidget -> ElementImage; a mod that replaces EncyclopediaUnitTreeNodeItem.xml silently gets no orange frame (no error).
- Not tested in game at all yet (steps 2-4).
In-game checks for Toni (step 4): open a unit page (e.g. a culture's recruit), blocked targets have an orange frame + tinted portrait, allowed ones are vanilla; hover a blocked one -> vanilla tooltip + orange lines "Requires Bow 120 - your best is 64 (Cadan)" / "or reach clan tier 5"; turn the mod off in MCM and reopen the page -> all vanilla. Log: 'applied: EncyclopediaUnitPageVM..ctor ...' and no ERROR lines.

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
- 2026-11-06 v1.1.0 (Toni): same culture bonus. Build 0 errors, 53 Core + 4 Compat tests pass, AssemblyGuard (MCMv5) OK. Deployed to `Modules\TroopTrainingRequirement.Dev` (game closed); nothing published.
  - Rule (Core `CultureBonus` + `TrainingRule.Check(..., targetCultureId, targetCultureName)`, tested in `CultureBonusTests`): per trainer, target troop culture == trainer's culture (StringId; none / `neutral_culture` never) -> his skill counts as `skill x 1.2 + 5`. Best = max of boosted values (doubles); allowed when `effective >= required`. Rounded to 4 decimals (float 1.15f safe). `Trainer` gained `CultureId`; `Shortfall` gained `BestRaw`, `Bonus`, `BonusCulture`; `Best` is the boosted value rounded DOWN.
  - Fian Champion (SandBoxCore spnpccharacters.xml): Bow 220, Two Handed 220, Athletics 170. Battanian thresholds: Bow / Two Handed 180, Athletics 138. Reason at Bow 179: "Requires Bow 220 - your best is 219 (Cadan, +40 Battanian)".
  - Settings: `SameCultureBonusEnabled` (on), `SameCultureBonusPoints` (int 0..50, 5), `SameCultureBonusMultiplier` (new `MenuKind.Float`, 1..2, snapped to 0.05, 1.2; MCM AddFloatingInteger, format 0.00). New MCM group "Same culture bonus" at order 1 (later groups shifted; ids unchanged). Log values now printed with "0.###".
  - Module: `Training.Check` passes `target.Culture` (id + name) and each trainer's `Hero.Culture.StringId`; the model, auto-upgrades, party screen buttons / guard / gate and the encyclopedia all use it. Clan tier bypass unchanged (checked first).
  - **Check in game**: a Battanian companion near the threshold unlocks Fian Champions; the hint shows "+N Battanian"; toggle off in MCM -> next button refresh uses raw skills.
