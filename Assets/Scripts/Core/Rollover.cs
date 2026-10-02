using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// The rollover (RESEARCH.md, from the Fraser film): "A driver dozes off, drifts into the railing,
    /// and the bus tips onto its side with everyone inside. Locals arrive with ropes and a winch, drag
    /// it back onto its wheels, and the bus goes straight back into service." In the game: tip →
    /// passengers and crew tumble → the crowd → ropes and a tractor for a price, or walk away. No
    /// cinematic, no reward; it is what happens when the fatigue meter is ignored.
    /// </summary>
    public sealed class RolloverState
    {
        public bool Active;             // on its side right now
        public bool Pending;            // the crowd's offer is on the table
        public float RightingUntil = -1f;
        public int HurtPassengers;
        public int Count;               // rollovers today
        public string Cause;
    }

    public static class Rollover
    {
        /// <summary>Two ways to tip a twenty-ton box: leave the road at speed, or turn it too hard.</summary>
        public static void Check(TrafficSim sim, float dt)
        {
            RolloverState r = sim.Rollover;
            Agent bus = sim.Player;
            BusSettings b = sim.Tuning.Bus;
            if (bus == null || sim.Economy.DayOver) return;

            if (r.Active)
            {
                if (r.RightingUntil >= 0f && sim.Metrics.Time >= r.RightingUntil) Righted(sim);
                return;
            }

            float offRoad = Mathf.Abs(bus.Lateral) - (sim.Corridor.HalfWidth + b.OffRoadToleranceMetres);
            // On the oncoming side "off road" is beyond their kerb; measure from whichever road we are on.
            if (sim.Oncoming != null && sim.Metrics.WrongSideNow && sim.PlayerGhost != null)
                offRoad = Mathf.Abs(sim.PlayerGhost.Lateral) - (sim.Oncoming.HalfWidth + b.OffRoadToleranceMetres);

            bool kerbAtSpeed = offRoad > b.OffRoadRolloverMetres && bus.Speed >= b.RolloverSpeedMs;
            float lateralAccel = Mathf.Abs(bus.Speed * sim.Bus.LastYawRate);
            bool thrown = lateralAccel >= b.RolloverLateralAccelMs2 && bus.Speed >= b.RolloverSpeedMs;

            if (kerbAtSpeed) Tip(sim, sim.Fatigue.Asleep ? "drifted into the railing asleep" : "left the road at speed");
            else if (thrown) Tip(sim, "turned too hard for twenty tons");
        }

        public static void Tip(TrafficSim sim, string cause)
        {
            RolloverState r = sim.Rollover;
            EconomySettings e = sim.Tuning.Economy;
            Agent bus = sim.Player;
            BusLoad load = bus.Load;

            r.Active = true;
            r.Pending = true;
            r.Count++;
            r.Cause = cause;
            r.RightingUntil = -1f;
            bus.Rolled = true;
            bus.Speed = 0f;
            sim.Bus.Held = true;
            sim.Fatigue.Asleep = false;

            // Everyone inside tumbles. Some are hurt; all of them get out and leave.
            r.HurtPassengers = Mathf.RoundToInt(load.Count * e.TumbleHurtFraction);
            load.Aboard.Clear();
            load.DoorOpen = false;
            load.AtDoor = null; load.Leaving = null;
            load.Injuries += r.HurtPassengers;

            // The bus keeps the damage whatever happens next.
            sim.Condition.Dents += e.RolloverDentsAdded;
            sim.Condition.WindscreenCracked = true;
            sim.Condition.DoorBent = true;
            sim.Condition.BrakeWear = Mathf.Clamp01(sim.Condition.BrakeWear + e.RolloverBrakeWear);

            sim.Economy.Ledger.Log("The bus is on its side (" + cause + "). " + r.HurtPassengers + " hurt. A crowd. Someone has ropes and a tractor, for Tk " + (e.RopesTk * e.MoneyScale).ToString("0") + ".");
        }

        /// <summary>Pay the men with the ropes, or walk away from the bus.</summary>
        public static void Answer(TrafficSim sim, bool pay)
        {
            RolloverState r = sim.Rollover;
            EconomySettings e = sim.Tuning.Economy;
            if (!r.Pending) return;
            r.Pending = false;
            if (pay)
            {
                sim.Economy.Ledger.RopesTk += e.RopesTk * e.MoneyScale;
                r.RightingUntil = sim.Metrics.Time + e.RightingSeconds;
                sim.Economy.Ledger.Log("Paid for the ropes. " + Mathf.RoundToInt(e.RightingSeconds / 60f) + " minutes of shouting and a tractor.");
            }
            else
            {
                sim.Economy.Ledger.Log("Walked away from the bus. The owner's deposit is still owed.");
                sim.Economy.WalkAway();
            }
        }

        private static void Righted(TrafficSim sim)
        {
            RolloverState r = sim.Rollover;
            r.Active = false;
            r.RightingUntil = -1f;
            sim.Player.Rolled = false;
            sim.Bus.Held = sim.Economy.Sergeant.Active || sim.Economy.InjuryHoldUntil >= 0f;
            // Back on its wheels, in the road, pointing the right way.
            sim.Player.Lateral = Mathf.Clamp(sim.Player.Lateral, -(sim.Corridor.HalfWidth - sim.Player.HalfWidth), sim.Corridor.HalfWidth - sim.Player.HalfWidth);
            sim.Player.Position = sim.Corridor.PositionAt(sim.Player.S, sim.Player.Lateral);
            sim.Player.Yaw = sim.Corridor.YawAt(sim.Player.S);
            sim.Economy.Ledger.Log("Back on its wheels. Cracked windscreen, bent door, straight back into service.");
        }
    }
}
