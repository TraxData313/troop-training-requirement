using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TroopTrainingRequirement.Core;
using Xunit;

namespace TroopTrainingRequirement.Compat.Tests
{
    /// <summary>
    /// Proves, on the game's own 0Harmony.dll, the ordering UpgradeGatePatch relies on: a prefix built from
    /// <see cref="CompatOrder"/> runs before Troop Equipment Upgrade's UpgradeTroop prefix whatever the patch order,
    /// and when it returns false neither that prefix (which reserves and prices the gear) nor the original runs.
    /// The stand-ins mirror the real shapes: TEU patches at module load with PatchAll (default priority, bool prefix
    /// + a postfix), we patch later, at the first campaign start.
    /// </summary>
    public class UpgradeGateOrderTests
    {
        private static readonly object Gate = new object();
        private static readonly List<string> Trace = new List<string>();
        private static bool _block;

        // One stand-in "PartyScreenLogic.UpgradeTroop" per test: Harmony patches are process-wide.
        public class Logic
        {
            [MethodImpl(MethodImplOptions.NoInlining)] public void UpgradeA() { Trace.Add("original"); }
            [MethodImpl(MethodImplOptions.NoInlining)] public void UpgradeB() { Trace.Add("original"); }
            [MethodImpl(MethodImplOptions.NoInlining)] public void UpgradeC() { Trace.Add("original"); }
        }

        // Troop Equipment Upgrade's UpgradeTroopPatch.Prefix (reserves gear, returns true) and GoldChangeTextPatch.Postfix.
        internal static bool TeuPrefix() { Trace.Add("teu.reserveGear"); return true; }
        internal static void TeuPostfix() { Trace.Add("teu.postfix"); }

        // Our UpgradeGatePatch.Prefix: false when the training rule blocks.
        internal static bool GatePrefix() { Trace.Add("gate"); return !_block; }

        private static void PatchTeu(string method)
        {
            var original = AccessTools.Method(typeof(Logic), method);
            new Harmony(CompatOrder.TroopEquipmentUpgradeHarmonyId).Patch(original,
                prefix: new HarmonyMethod(typeof(UpgradeGateOrderTests), nameof(TeuPrefix)),
                postfix: new HarmonyMethod(typeof(UpgradeGateOrderTests), nameof(TeuPostfix)));
        }

        private static void PatchGate(string method)
        {
            var original = AccessTools.Method(typeof(Logic), method);
            new Harmony("trax.bannerlord.trooptrainingrequirement").Patch(original,
                prefix: new HarmonyMethod(typeof(UpgradeGateOrderTests), nameof(GatePrefix))
                {
                    priority = CompatOrder.UpgradeGatePriority,
                    before = CompatOrder.UpgradeGateBefore,
                });
        }

        private static List<string> Run(System.Action call, bool block)
        {
            Trace.Clear();
            _block = block;
            call();
            return new List<string>(Trace);
        }

        [Fact]
        public void GatePriorityIsHarmonyFirst()
        {
            Assert.Equal(Priority.First, CompatOrder.UpgradeGatePriority);
            Assert.Contains("troop.equipment.upgrade", CompatOrder.UpgradeGateBefore);
        }

        [Fact]
        public void BlockedUpgradeNeverReachesTeuOrTheOriginal_TeuPatchedFirst()
        {
            lock (Gate)
            {
                PatchTeu(nameof(Logic.UpgradeA)); // module load
                PatchGate(nameof(Logic.UpgradeA)); // our first campaign start
                var logic = new Logic();

                Assert.Equal(new[] { "gate", "teu.postfix" }, Run(logic.UpgradeA, block: true));
                Assert.Equal(new[] { "gate", "teu.reserveGear", "original", "teu.postfix" }, Run(logic.UpgradeA, block: false));
            }
        }

        [Fact]
        public void GateStillRunsFirst_WhenPatchedBeforeTeu()
        {
            lock (Gate)
            {
                PatchGate(nameof(Logic.UpgradeB));
                PatchTeu(nameof(Logic.UpgradeB));
                var logic = new Logic();

                Assert.Equal(new[] { "gate", "teu.postfix" }, Run(logic.UpgradeB, block: true));
                Assert.Equal(new[] { "gate", "teu.reserveGear", "original", "teu.postfix" }, Run(logic.UpgradeB, block: false));
            }
        }

        [Fact]
        public void WithoutTeu_GateAloneBlocksOrPasses()
        {
            lock (Gate)
            {
                PatchGate(nameof(Logic.UpgradeC));
                var logic = new Logic();

                Assert.Equal(new[] { "gate" }, Run(logic.UpgradeC, block: true));
                Assert.Equal(new[] { "gate", "original" }, Run(logic.UpgradeC, block: false));
            }
        }
    }
}
