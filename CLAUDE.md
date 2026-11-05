# Troop Training Requirement

Bannerlord 1.4.8 (+ War Sails) mod: a troop can only be **upgraded** into a target whose combat skills do not exceed
what someone in the party can teach. Forces the player to level up, or bring a good companion. Recruiting
already-trained troops (any tier, from towns / villages / prisoners) is unaffected.

Sister mod of `..\item-skill-requirement-expanded` - same layout, tooling, code style and rules; when in doubt, do what
it does. Status and the agreed design: `.manager\progress.md` (the resume file, committed). Steps 1-2 done: the rule is enforced (model decorator + party screen guard +
auto-upgrade filter) with an MCM page; the party screen hint / disabled button is step 3. Player docs: `README.md`.

## Design (agreed with Toni, 2026-11-05)

- Rule: for each combat skill (One Handed, Two Handed, Polearm, Bow, Crossbow, Throwing, Riding, Athletics) the
  target troop's skill must be <= the best value of that skill among the party's "trainers" (+ an allowance N,
  default 0). Trainers = the heroes in the party (player + companions / clan members present); setting "Count the
  whole clan" (off).
- Hook: a DECORATOR over the abstract `PartyTroopUpgradeModel` (forward every member to BaseModel; in
  `CanPartyUpgradeTroopToTarget`, and `IsTroopUpgradeable` if needed, additionally require the skill check).
- Who: parties of the PLAYER's clan only (main party; companion-led clan parties use THEIR party's heroes - setting,
  on). AI lords' parties: vanilla (setting "apply to AI", off).
- Settings (MCM, optional): enabled, each of the 8 skills (default all on), allowance, companion-led clan parties,
  AI parties, count the whole clan.
- Party screen: when blocked, the upgrade button is disabled and its hint says why, vanilla style, every failing skill
  listed, e.g. "Requires Athletics 120 - your best is 110 (Cadan)". Orange #FFA028 where a colour is used (the sister
  mod's brush approach, no UIExtenderEx if avoidable).
- Encyclopedia troop tree: targets the current party couldn't train get an orange outline / tint, if feasible
  without heavy UI deps (else report and skip).
- Clan tier bypass: a target whose tier <= the owner's clan tier is always allowed (setting, on); blocked reasons end
  with 'or reach clan tier N'.
- Upgrade XP keeps accumulating; nothing is saved into the campaign (removable mid-campaign).

## Layout

- `src\TroopTrainingRequirement.Core` - netstandard2.0, **no game references**: the trainer rule (pure, unit-tested),
  settings and the MCM menu description (game-free).
- `src\TroopTrainingRequirement.Module` - net472 module (assembly `TroopTrainingRequirement.dll`, namespace
  `TroopTrainingRequirement`): `SubModule`, `ModLog`; later `Models\` (the decorator), `Patches\` (UI), `McmSettings`.
  Depends on `Bannerlord.Harmony` (not shipped); MCM referenced, never shipped.
- `tests\TroopTrainingRequirement.Core.Tests` - xunit, net8.0.
- `module\` - SubModule.xml (+ `GUI\` / `ModuleData\` when needed; deploy.ps1 / package.ps1 copy them). Build output
  lands in `module\bin` (git-ignored).
- Machine paths: `Directory.Build.props.user` (git-ignored) sets `GameFolder`, `HarmonyBinFolder` (falls back to
  `Modules\Bannerlord.Harmony`) and `McmBinFolder` (falls back to `Modules\Bannerlord.MBOptionScreen`).

## Commands

```
dotnet build TroopTrainingRequirement.sln -c Release
dotnet test tests\TroopTrainingRequirement.Core.Tests -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1     # builds + installs Modules\TroopTrainingRequirement.Dev, "Troop Training Requirement (dev)"
powershell -ExecutionPolicy Bypass -File tools\package.ps1    # dist\TroopTrainingRequirement + zip (release identity)
dotnet run --project ..\trax_combat_enhancements\tools\AssemblyGuard -c Release -- module\bin\Win64_Shipping_Client\TroopTrainingRequirement.dll MCMv5
```

Release identity in `module\SubModule.xml` is Id `TroopTrainingRequirement`; only deploy.ps1 re-badges it `.Dev`.
Never install the non-dev module into the game's Modules (it would load beside `TroopTrainingRequirement.Dev`).

deploy.ps1 refuses if the DLLs are locked (game running). Never launch, restart or kill the game without asking Toni.
Mod log: `Documents\Mount and Blade II Bannerlord\Configs\TroopTrainingRequirement\troop_training_requirement.log`.

## Design rules

- **Decorator models only.** `starter.AddModel<PartyTroopUpgradeModel>(...)`: subclass the ABSTRACT model, take the
  previously registered one as `BaseModel`, delegate every member, then adjust. **Never subclass `Default*` /
  `Sandbox*` models.** Pattern: `..\trax_combat_enhancements\src\TraxCombat.Module\SubModule.cs` (`RegisterModels`).
- Code that runs inside game methods must never throw: try/catch + `ModLog.ErrorOnce`, fall back to the base
  model's answer.
- Patch nothing at module load: any Harmony patch goes in `OnGameStart` (first campaign start), each in its own
  try/catch with a log line.
- No saved types: the mod must stay removable mid-campaign.
- **MCM is optional**: MCM types only inside method bodies of `McmSettings` (fluent builder, `Action<object>` instance
  methods - never a type deriving from / a field of / a lambda over an MCM type), else the game's `GetTypes()` refuses
  the whole mod without MCM. Run the AssemblyGuard check above after touching it.
- Pure logic goes in Core with tests; the module only reads numbers from the game and feeds them in.
- Never commit `Directory.Build.props.user`, logs or `dist\`.
