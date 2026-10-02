using NUnit.Framework;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Tests
{
    /// <summary>
    /// Sanity checks on the default tuning table. Run from Window > General > Test Runner > EditMode.
    /// They don't test gameplay; they make sure the research figures survive refactors.
    /// </summary>
    public class TuningTableTests
    {
        private static TuningTable Fresh() => ScriptableObject.CreateInstance<TuningTable>();

        [Test]
        public void MassHierarchyMatchesTheStreet()
        {
            // "Size is right of way": bus > truck > car > CNG > rickshaw > pedestrian.
            var mass = Fresh().Mass;
            Assert.Greater(mass.Bus, mass.Truck);
            Assert.Greater(mass.Truck, mass.Car);
            Assert.Greater(mass.Car, mass.Cng);
            Assert.Greater(mass.Cng, mass.Rickshaw);
            Assert.Greater(mass.Rickshaw, mass.Pedestrian);
        }

        [Test]
        public void EnumOrderAgreesWithMass()
        {
            // The enum is documented as sorted by mass; keep that promise.
            var mass = Fresh().Mass;
            float previous = -1f;
            foreach (VehicleClass vehicleClass in System.Enum.GetValues(typeof(VehicleClass)))
            {
                float current = mass.Of(vehicleClass);
                Assert.Greater(current, previous, $"{vehicleClass} should be heavier than the class before it");
                previous = current;
            }
        }

        [Test]
        public void CriticalGapStaysInsideResearchRange()
        {
            // RESEARCH: 0.5–1.5 s. Nerve 1 → brave minimum, nerve 0 → cautious maximum.
            var gap = Fresh().Gap;
            Assert.AreEqual(0.5f, gap.CriticalGapSeconds(1f), 1e-4f);
            Assert.AreEqual(1.5f, gap.CriticalGapSeconds(0f), 1e-4f);
            Assert.AreEqual(1.0f, gap.CriticalGapSeconds(0.5f), 1e-4f);
            // Out-of-range nerve is clamped, never extrapolated.
            Assert.AreEqual(0.5f, gap.CriticalGapSeconds(7f), 1e-4f);
        }

        [Test]
        public void BusHornMovesRickshawMoreThanRickshawHornMovesBus()
        {
            var table = Fresh();
            float busOnRickshaw = table.Horn.YieldBoost(table.Horn.TapStrength, table.Mass.Bus, table.Mass.Rickshaw);
            float rickshawOnBus = table.Horn.YieldBoost(table.Horn.TapStrength, table.Mass.Rickshaw, table.Mass.Bus);
            Assert.Greater(busOnRickshaw, rickshawOnBus);
            Assert.LessOrEqual(busOnRickshaw, 1f);
        }

        [Test]
        public void PersonalitiesMatchResearch()
        {
            // "reckless: race ×1.5, back off ×0.5; cautious: reverse; spiteful: block ×2"
            var reckless = DriverPersonality.Reckless();
            Assert.AreEqual(1.5f, reckless.MultiplierFor(BusAction.RaceForNextStop));
            Assert.AreEqual(0.5f, reckless.MultiplierFor(BusAction.BackOff));

            var cautious = DriverPersonality.Cautious();
            Assert.AreEqual(0.5f, cautious.MultiplierFor(BusAction.RaceForNextStop));
            Assert.AreEqual(1.5f, cautious.MultiplierFor(BusAction.BackOff));

            Assert.AreEqual(2f, DriverPersonality.Spiteful().MultiplierFor(BusAction.BlockThePlayer));
        }
    }
}
