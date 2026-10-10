using System.Collections.Generic;
using UnityEngine;

namespace TwentyTons.Unity
{
    /// <summary>
    /// The route's stops, read from Assets/World/Corridor01/stops.json (tools/osm/greybox.py): the OSM
    /// bus stop nodes on the kerb side of each leg, and the research's named pickups and stands
    /// (RESEARCH.md, S-007.6), which are the hot ones. Each stop knows where it is along the way out
    /// (leg 0, Mirpur 12 to Azimpur) and the way back (leg 1); -1 where it is not on that leg. These
    /// are the demand zones of milestone 4 (TrafficSim.AddZone takes a name, an S and whether it is
    /// hot); for now they are signs on the kerb and a name in the readout.
    /// </summary>
    public sealed class RouteStops
    {
        [System.Serializable]
        public sealed class Stop
        {
            public string name, nameBn, kind, source;
            public float x, z;
            public bool hot;
            public float outS, outLat, backS, backLat;
            public float outX, outZ, backX, backZ;        // where the sign stands on each leg

            public float SOn(int leg) => leg == 0 ? outS : backS;
            public Vector3 SignOn(int leg) => leg == 0 ? new Vector3(outX, 0f, outZ) : new Vector3(backX, 0f, backZ);
            public bool IsStand => kind == "stand";
        }

        [System.Serializable] private sealed class File { public List<Stop> stops; }

        public readonly List<Stop> All = new List<Stop>();

        public static RouteStops Load(TextAsset json)
        {
            var stops = new RouteStops();
            if (json == null) return stops;
            File f = JsonUtility.FromJson<File>(json.text);
            if (f != null && f.stops != null) stops.All.AddRange(f.stops);
            return stops;
        }

        /// <summary>The nearest stop ahead on a leg (a stop the door is level with still counts), or null.</summary>
        public Stop Next(int leg, float s, out float ahead)
        {
            Stop best = null;
            ahead = float.MaxValue;
            foreach (Stop st in All)
            {
                float at = st.SOn(leg);
                if (at < 0f) continue;
                float d = at - s;
                if (d < -10f || d >= ahead) continue;
                best = st; ahead = d;
            }
            return best;
        }
    }
}
