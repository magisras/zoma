using System;
using System.Collections.Generic;
using Microsoft.JSInterop;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Sandbox
{
    /// <summary>What the page needs once, at start: the road and the buildings.</summary>
    public sealed class SceneDto
    {
        public float RoadWidth { get; set; }
        public float RoadLength { get; set; }
        public float[] LeftEdge { get; set; }
        public float[] RightEdge { get; set; }
        public float[] Buildings { get; set; }
    }

    /// <summary>What the page needs every frame.</summary>
    public sealed class FrameDto
    {
        /// <summary>Per agent: id, class, x, y, z, yaw, length, width, height, flags. Flags: 1 player, 2 horn, 4 yielding, 8 crossing, 16 bluffing.</summary>
        public float[] Agents { get; set; }
        public float SpeedKmh { get; set; }
        public float GapAheadMetres { get; set; }
        public float HeadwaySeconds { get; set; }
        public float MinHeadwaySeconds { get; set; }
        public int NearMisses { get; set; }
        public float NearMissesPerMinute { get; set; }
        public int Contacts { get; set; }
        public int HornPresses { get; set; }
        public int YieldsToHorn { get; set; }
        public int Passengers { get; set; }
        public float BrakeWear { get; set; }
        public float Time { get; set; }
        public float DistanceMetres { get; set; }
        public bool PersonHit { get; set; }
        public int AgentCount { get; set; }
    }

    /// <summary>
    /// The bridge JavaScript calls. Static because Blazor's synchronous interop
    /// (DotNet.invokeMethod) needs static [JSInvokable] methods. One simulation at a time.
    /// </summary>
    public static class SandboxApi
    {
        private const int KeyUp = 1, KeyDown = 2, KeyLeft = 4, KeyRight = 8, KeyHorn = 16;
        private const float FixedStep = 1f / 60f;

        private static TrafficSim _sim;
        private static TuningTable _tuning;
        private static float _accumulator;

        [JSInvokable]
        public static SceneDto Init(int seed)
        {
            _tuning = ScriptableObject.CreateInstance<TuningTable>();
            return Reset(seed);
        }

        [JSInvokable]
        public static SceneDto Reset(int seed)
        {
            var random = new SeededRandom(seed);
            Corridor corridor = SandboxWorld.BuildCorridor(random);
            _sim = new TrafficSim(corridor, _tuning, seed + 1);
            _sim.SpawnPlayerBus(30f, -2f);
            _accumulator = 0f;

            return new SceneDto
            {
                RoadWidth = corridor.Width,
                RoadLength = corridor.Length,
                LeftEdge = SandboxWorld.RoadEdge(corridor, -corridor.HalfWidth),
                RightEdge = SandboxWorld.RoadEdge(corridor, corridor.HalfWidth),
                Buildings = SandboxWorld.BuildBuildings(corridor, random),
            };
        }

        /// <summary>Advance the world by the frame time, in fixed 60 Hz steps so physics never depends on frame rate.</summary>
        [JSInvokable]
        public static FrameDto Tick(float dt, int keys)
        {
            BusController bus = _sim.Bus;
            bus.Throttle = (keys & KeyUp) != 0 ? 1f : 0f;
            bus.Brake = (keys & KeyDown) != 0 ? 1f : 0f;
            bus.Steer = ((keys & KeyRight) != 0 ? 1f : 0f) - ((keys & KeyLeft) != 0 ? 1f : 0f);
            bool horn = (keys & KeyHorn) != 0;

            _accumulator += Mathf.Min(dt, 0.1f);           // a hidden tab must not fast-forward the world
            while (_accumulator >= FixedStep)
            {
                _sim.HornInput(horn, FixedStep);
                _sim.Step(FixedStep);
                _accumulator -= FixedStep;
            }
            return Snapshot();
        }

        /// <summary>Live tuning from the page's sliders. Names are explicit on purpose: no reflection to learn.</summary>
        [JSInvokable]
        public static void SetParam(string name, float value)
        {
            switch (name)
            {
                case "nerve": _tuning.Nerve.DefaultNerve = value; break;
                case "gapMin": _tuning.Gap.CriticalGapMinSeconds = value; break;
                case "gapMax": _tuning.Gap.CriticalGapMaxSeconds = value; break;
                case "hornTap": _tuning.Horn.TapStrength = value; break;
                case "yieldSilent": _tuning.Nerve.YieldChancePerSecondWithoutHorn = value; break;
                case "bluff": _tuning.Nerve.BluffChanceAtFullNerve = value; break;
                case "vehicles": _tuning.Spawn.VehiclesAround = (int)value; break;
                case "pedestrians": _tuning.Spawn.PedestriansAround = (int)value; break;
                case "brakeWear": _sim.Bus.BrakeWear = value; break;
                case "passengers": _sim.Bus.Passengers = (int)value; break;
                default: throw new ArgumentException("Unknown tuning parameter: " + name);
            }
            _tuning.Gap.Clamp();
        }

        private static FrameDto Snapshot()
        {
            List<Agent> agents = _sim.Agents;
            var data = new float[agents.Count * 10];
            int k = 0;
            for (int i = 0; i < agents.Count; i++)
            {
                Agent a = agents[i];
                int flags = (a.IsPlayer ? 1 : 0) | (a.IsHorning ? 2 : 0) | (a.IsYielding ? 4 : 0)
                          | (a.PedState == PedestrianState.Crossing ? 8 : 0) | (a.BluffTimer > 0f ? 16 : 0);
                data[k++] = a.Id;
                data[k++] = (int)a.Class;
                data[k++] = a.Position.x;
                data[k++] = a.Shape.Height * 0.5f;
                data[k++] = a.Position.z;
                data[k++] = a.Yaw;
                data[k++] = a.Shape.Length;
                data[k++] = a.Shape.Width;
                data[k++] = a.Shape.Height;
                data[k++] = flags;
            }

            SimMetrics m = _sim.Metrics;
            return new FrameDto
            {
                Agents = data,
                SpeedKmh = _sim.Player.Speed * 3.6f,
                GapAheadMetres = m.GapAheadMetres,
                HeadwaySeconds = m.HeadwayAheadSeconds,
                MinHeadwaySeconds = m.MinHeadwaySeconds,
                NearMisses = m.NearMisses,
                NearMissesPerMinute = m.NearMissesPerMinute,
                Contacts = m.Contacts,
                HornPresses = m.HornPresses,
                YieldsToHorn = m.YieldsToHorn,
                Passengers = _sim.Bus.Passengers,
                BrakeWear = _sim.Bus.BrakeWear,
                Time = m.Time,
                DistanceMetres = m.DistanceMetres,
                PersonHit = m.PersonHit,
                AgentCount = agents.Count,
            };
        }
    }
}
