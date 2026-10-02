using System.Collections.Generic;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// The steering layer for NPC vehicles: how a vehicle moves, never what it wants
    /// (RESEARCH.md keeps those two layers apart). The behaviours, in the order the research says
    /// gives most of the feel:
    ///   1. follow the corridor and keep a speed-scaled distance (critical gap by nerve)
    ///   2. stop for the officer's cane, unless you are one of the leakers
    ///   3. seek a gap sideways when blocked
    ///   4. yield by mass, unless nerve calls the bluff
    ///   5. react to horns (in HornSystem; it calls Yield() here)
    ///
    /// Everything is a plain static function over the agent list, so it reads top to bottom and can
    /// be unit-tested without a scene. The player's bus is not driven here (BusController), but every
    /// NPC sees it through the same functions because it is in the same list.
    ///
    /// All along-the-road distances go through Corridor.DeltaS so loops work.
    /// </summary>
    public static class Steering
    {
        /// <summary>Extra sideways clearance before two agents count as "in the same band", metres.</summary>
        public const float LateralMargin = 0.3f;

        // ---------------------------------------------------------------- queries

        /// <summary>
        /// The nearest agent ahead of <paramref name="self"/> on the same corridor whose body would be
        /// in the way if self were at <paramref name="lateral"/>. Gap is bumper to bumper (negative =
        /// overlapping). Returns null when the road is clear for <paramref name="lookAhead"/> metres.
        /// </summary>
        public static Agent FindAhead(List<Agent> agents, Agent self, float lateral, float lookAhead, out float gap)
        {
            Agent nearest = null;
            gap = lookAhead;
            for (int i = 0; i < agents.Count; i++)
            {
                Agent other = agents[i];
                if (other == self || other.Corridor != self.Corridor) continue;
                if (!self.WouldOverlapLaterally(lateral, other, LateralMargin)) continue;

                float ds = self.Corridor.DeltaS(self.S, other.S);
                if (ds <= 0f) continue;                                   // not ahead of my centre
                float g = ds - self.HalfLength - other.HalfLength;
                if (g < gap)
                {
                    gap = g;
                    nearest = other;
                }
            }
            return nearest;
        }

        /// <summary>
        /// The nearest clearly heavier agent behind self, in the same band, moving at least as fast.
        /// This is the one whose mass self is expected to yield to.
        /// </summary>
        public static Agent FindHeavierBehind(List<Agent> agents, Agent self, float lookBehind)
        {
            Agent nearest = null;
            float best = lookBehind;
            for (int i = 0; i < agents.Count; i++)
            {
                Agent other = agents[i];
                if (other == self || other.IsPedestrian || other.Corridor != self.Corridor) continue;
                if (other.Mass < self.Mass * 1.5f) continue;            // not clearly heavier
                float ds = self.Corridor.DeltaS(self.S, other.S);
                if (ds >= 0f) continue;                                   // not behind
                if (other.Speed < self.Speed - 0.5f) continue;            // not closing in
                if (!self.OverlapsLaterally(other, LateralMargin)) continue;

                float g = -ds - self.HalfLength - other.HalfLength;
                if (g < best)
                {
                    best = g;
                    nearest = other;
                }
            }
            return nearest;
        }

        /// <summary>
        /// Is the band at <paramref name="lateral"/> occupied beside self, or about to be by a faster
        /// vehicle right behind? Then moving there would be a sideswipe, and nobody wants that:
        /// damage comes out of the crew's day.
        /// </summary>
        public static bool SideBlocked(List<Agent> agents, Agent self, float lateral)
        {
            for (int i = 0; i < agents.Count; i++)
            {
                Agent other = agents[i];
                if (other == self || other.Corridor != self.Corridor) continue;
                if (!self.WouldOverlapLaterally(lateral, other, LateralMargin)) continue;

                float alongside = self.HalfLength + other.HalfLength + 1.5f;
                float ds = self.Corridor.DeltaS(self.S, other.S);
                if (Mathf.Abs(ds) < alongside) return true;
                // Someone right behind in that band and faster than me: let them pass first.
                if (ds < 0f && ds > -alongside - 8f && other.Speed > self.Speed + 1f) return true;
            }
            return false;
        }

        /// <summary>
        /// Speed at which the gap ahead equals the driver's headway plus the stopped floor.
        /// Following at this speed means: gap = floor + speed × headway.
        /// </summary>
        public static float AllowedSpeed(float gapMetres, float headwaySeconds, float floorMetres)
        {
            return Mathf.Max(0f, (gapMetres - floorMetres) / Mathf.Max(0.05f, headwaySeconds));
        }

        // ---------------------------------------------------------------- actions

        /// <summary>
        /// Move aside for <paramref name="to"/>: pick the side away from them and hold it for a while.
        /// Called by the mass rule and by the horn.
        /// </summary>
        public static void Yield(Agent self, Agent to, float seconds, Corridor corridor)
        {
            float edge = corridor.HalfWidth - self.HalfWidth;
            // If they are on my left (or dead centre), I go right; otherwise left.
            float direction = to.Lateral <= self.Lateral ? 1f : -1f;
            float clearance = self.HalfWidth + to.HalfWidth + 0.6f;
            self.TargetLateral = Mathf.Clamp(to.Lateral + direction * clearance, -edge, edge);
            self.YieldTimer = seconds;
            self.BluffTimer = 0f;
        }

        // ---------------------------------------------------------------- the per-step drive

        /// <summary>One simulation step for an NPC vehicle.</summary>
        public static void Drive(TrafficSim sim, Agent a, float dt)
        {
            TuningTable tuning = sim.Tuning;
            Corridor corridor = a.Corridor;
            float edge = corridor.HalfWidth - a.HalfWidth;

            TickTimers(a, dt);

            // 1. Follow: how fast may I go given what is ahead in my band?
            float headway = tuning.Gap.CriticalGapSeconds(a.Nerve);
            Agent ahead = FindAhead(sim.Agents, a, a.Lateral, tuning.Gap.LookAheadMetres, out float gap);
            float allowed = a.DesiredSpeed;
            if (ahead != null)
            {
                allowed = Mathf.Min(allowed, AllowedSpeed(gap, headway, tuning.Gap.FollowDistanceFloorMetres));
            }

            // 2. The officer: a closed stop line (or a box full of cross traffic) is an obstacle too.
            float stopLine = sim.StopDistanceAhead(a, tuning.Gap.LookAheadMetres);
            if (stopLine < tuning.Gap.LookAheadMetres)
            {
                allowed = Mathf.Min(allowed, AllowedSpeed(stopLine, headway, tuning.Gap.FollowDistanceFloorMetres));
            }

            // 3. Seek gap: blocked by the vehicle ahead? Look for a band with more free road.
            bool blocked = ahead != null && allowed < a.DesiredSpeed * tuning.Gap.BlockedFraction;
            if (blocked && !a.IsYielding)
            {
                SeekGap(sim, a, gap, edge);
            }

            // 4. Yield by mass: something clearly heavier pressing from behind?
            if (!a.IsYielding && a.BluffTimer <= 0f)
            {
                Agent heavy = FindHeavierBehind(sim.Agents, a, tuning.Nerve.YieldLookBehindMetres);
                if (heavy != null)
                {
                    // Chances are "per second", so scale by dt for a frame-rate independent roll.
                    if (sim.Random.Chance(tuning.Nerve.YieldChancePerSecondWithoutHorn * dt))
                    {
                        Yield(a, heavy, tuning.Nerve.YieldSeconds, corridor);
                    }
                    else if (sim.Random.Chance(tuning.Nerve.BluffChanceAtFullNerve * a.Nerve * dt))
                    {
                        a.BluffTimer = tuning.Nerve.BluffSeconds;   // "I'm not moving."
                    }
                }
            }

            // 5. Integrate speed toward the allowed speed, within what the vehicle can do.
            float accel = Mathf.Clamp((allowed - a.Speed) * tuning.Gap.ClosingGain, -a.Shape.Braking, a.Shape.Acceleration);
            a.Speed = Mathf.Max(0f, a.Speed + accel * dt);
            a.S = corridor.Wrap(a.S + a.Speed * dt);

            // Sideways: you can't drift much when standing still.
            float lateralRate = Mathf.Min(a.Shape.LateralSpeed, 0.3f + a.Speed * 0.3f);
            // The target may sit up to a metre onto the pavement: a vehicle shoved there by a bus
            // stays put until it picks a new target, instead of fighting its way back into the bus.
            // Targets chosen by SeekGap and Yield are always on the road.
            float target = Mathf.Clamp(a.TargetLateral, -edge - 1f, edge + 1f);
            float before = a.Lateral;
            a.Lateral = Mathf.MoveTowards(a.Lateral, target, lateralRate * dt);
            float lateralVelocity = dt > 0f ? (a.Lateral - before) / dt : 0f;

            // 6. World pose for rendering: on the corridor, nose slightly turned into the drift.
            a.Position = corridor.PositionAt(a.S, a.Lateral);
            a.Yaw = corridor.YawAt(a.S) + Mathf.Atan2(lateralVelocity, Mathf.Max(a.Speed, 1f));
        }

        private static void TickTimers(Agent a, float dt)
        {
            a.YieldTimer = Mathf.Max(0f, a.YieldTimer - dt);
            a.BluffTimer = Mathf.Max(0f, a.BluffTimer - dt);
            a.HornTimer = Mathf.Max(0f, a.HornTimer - dt);
        }

        /// <summary>
        /// Consider bands to the left and right; take the one with the most free road if it beats the
        /// current one by enough. "Enough" shrinks with nerve: a nerve-1 driver moves for a metre.
        /// </summary>
        private static void SeekGap(TrafficSim sim, Agent a, float currentGap, float edge)
        {
            GapSettings g = sim.Tuning.Gap;
            float bestLateral = a.TargetLateral;
            float bestFree = currentGap;
            float needed = g.SeekGapMinAdvantageMetres * Mathf.Max(0.05f, 1.2f - a.Nerve);

            for (int step = 1; step <= 3; step++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float candidate = Mathf.Clamp(a.Lateral + side * step * g.LateralStepMetres, -edge, edge);
                    if (Mathf.Abs(candidate - a.Lateral) < 0.2f) continue;      // clamped onto myself
                    if (SideBlocked(sim.Agents, a, candidate)) continue;

                    FindAhead(sim.Agents, a, candidate, g.LookAheadMetres, out float free);
                    if (free > bestFree + needed)
                    {
                        bestFree = free;
                        bestLateral = candidate;
                    }
                }
            }
            a.TargetLateral = bestLateral;
        }
    }
}
