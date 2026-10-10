using System.Collections.Generic;
using TwentyTons.Core;
using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Unity
{
    /// <summary>
    /// Draws what the sim knows: every agent (rival buses, other buses, trucks, cars, CNGs, rickshaws
    /// as boxes of their shape, people as capsules) and the crowd waiting at each stop. Plain
    /// primitives without colliders, pooled by agent id, so nothing here touches the physics; the bus
    /// meets them through the sim's contact rules, not Unity's. Real bodies come with the art pass.
    /// </summary>
    public sealed class StreetView : MonoBehaviour
    {
        [Tooltip("Crowds and agents further than this from the bus are not drawn (the sim despawns at 400 m anyway).")]
        public float DrawWithin = 500f;

        private StreetSim _street;
        private readonly Dictionary<int, Transform> _agents = new Dictionary<int, Transform>();
        private readonly List<int> _seen = new List<int>();
        private readonly List<Transform> _crowd = new List<Transform>();
        private Transform _root;
        private Material _bus, _ownBus, _truck, _car, _cng, _rickshaw, _person, _waiting, _officer, _rope;
        private readonly List<Transform> _officers = new List<Transform>();
        private readonly List<Transform> _ropes = new List<Transform>();

        private void Start()
        {
            _street = GetComponent<StreetSim>();
            _root = new GameObject("Street").transform;
            _ownBus = Make(new Color(0.85f, 0.55f, 0.15f));      // the company's livery, the player's own colour
            _bus = Make(new Color(0.25f, 0.45f, 0.70f));
            _truck = Make(new Color(0.45f, 0.35f, 0.25f));
            _car = Make(new Color(0.85f, 0.85f, 0.85f));
            _cng = Make(new Color(0.20f, 0.60f, 0.30f));
            _rickshaw = Make(new Color(0.75f, 0.25f, 0.30f));
            _person = Make(new Color(0.30f, 0.25f, 0.35f));
            _waiting = Make(new Color(0.55f, 0.45f, 0.30f));
            _officer = Make(new Color(0.15f, 0.20f, 0.55f));
            _rope = Make(new Color(0.85f, 0.80f, 0.60f));
        }

        private static Material Make(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.2f);
            return m;
        }

        private void LateUpdate()
        {
            if (_street == null || _street.Sim == null || !_street.enabled)
            {
                // The street is off (O): nothing of it stays on the road.
                foreach (var kv in _agents) Destroy(kv.Value.gameObject);
                _agents.Clear();
                foreach (Transform c in _crowd) c.gameObject.SetActive(false);
                foreach (Transform officerT in _officers) officerT.gameObject.SetActive(false);
                foreach (Transform ropeT in _ropes) ropeT.gameObject.SetActive(false);
                return;
            }
            TrafficSim sim = _street.Sim;
            Vector3 here = sim.Player.Position;
            _seen.Clear();

            // Agents: one primitive each, kept while the sim keeps the agent.
            foreach (Agent a in sim.Agents)
            {
                if (a.IsPlayer || a.GhostOf != null) continue;
                if ((a.Position - here).sqrMagnitude > DrawWithin * DrawWithin) continue;
                if (!_agents.TryGetValue(a.Id, out Transform t))
                {
                    t = Spawn(a);
                    _agents[a.Id] = t;
                }
                _seen.Add(a.Id);
                float h = a.Shape.Height;
                t.position = new Vector3(a.Position.x, h * 0.5f, a.Position.z);
                t.rotation = Quaternion.Euler(0f, a.Yaw * Mathf.Rad2Deg, 0f);
                // A honking driver flashes: the only cue there is until there is sound.
                if (a.IsHorning && a.Class != VehicleClass.Pedestrian) t.localScale = new Vector3(a.Shape.Width, h * 1.08f, a.Shape.Length);
                else if (a.Class != VehicleClass.Pedestrian) t.localScale = new Vector3(a.Shape.Width, h, a.Shape.Length);
            }
            // Drop the ones the sim recycled.
            var gone = new List<int>();
            foreach (var kv in _agents) if (!_seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone) { Destroy(_agents[id].gameObject); _agents.Remove(id); }

            // Crowds: a capsule per person waiting, spread along the kerb at the zone.
            int used = 0;
            foreach (DemandZone z in sim.Zones)
            {
                if ((z.Position - here).sqrMagnitude > DrawWithin * DrawWithin) continue;
                Vector3 along = sim.Corridor.TangentAt(z.S);
                Vector3 right = sim.Corridor.RightAt(z.S);
                for (int i = 0; i < z.Waiting.Count; i++)
                {
                    Transform c;
                    if (used < _crowd.Count) c = _crowd[used];
                    else
                    {
                        c = Person(_waiting);
                        c.name = "Waiting";
                        _crowd.Add(c);
                    }
                    c.gameObject.SetActive(true);
                    // A loose knot: a few deep on the pavement, a few metres along it, hashed by index so they stand still.
                    float row = (i % 3) * 0.7f, col = (i / 3) * 0.8f - Mathf.Min(z.Waiting.Count / 3, 10) * 0.4f;
                    c.position = z.Position + along * col + right * (z.Side * row) + Vector3.up * 0.85f;
                    used++;
                }
            }
            for (int i = used; i < _crowd.Count; i++) _crowd[i].gameObject.SetActive(false);

            // The officer in the middle of each junction, facing the stream he is letting through, and the
            // constable's rope across the main road's stop line when he has stretched it against us.
            int o = 0, r = 0;
            foreach (Junction j in sim.Junctions)
            {
                if ((j.Centre - here).sqrMagnitude > DrawWithin * DrawWithin) continue;
                Transform officer;
                if (o < _officers.Count) officer = _officers[o];
                else { officer = Person(_officer); officer.name = "Officer"; _officers.Add(officer); }
                officer.gameObject.SetActive(true);
                Vector3 face = j.Open == JunctionFlow.Main ? sim.Corridor.RightAt(j.MainS) : -sim.Corridor.TangentAt(j.MainS);
                officer.position = j.Centre + Vector3.up * 0.85f;
                officer.rotation = Quaternion.LookRotation(face, Vector3.up);
                o++;
                if (j.Roped && j.Open == JunctionFlow.Cross)
                {
                    Transform rope;
                    if (r < _ropes.Count) rope = _ropes[r];
                    else
                    {
                        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Destroy(go.GetComponent<Collider>());
                        go.transform.SetParent(_root, false);
                        go.GetComponent<MeshRenderer>().sharedMaterial = _rope;
                        go.name = "Rope";
                        rope = go.transform; _ropes.Add(rope);
                    }
                    rope.gameObject.SetActive(true);
                    float lineS = j.StopLineOn(sim.Corridor, sim.Tuning.Officer.StopLineSetbackMetres);
                    rope.position = sim.Corridor.PositionAt(lineS, 0f) + Vector3.up * 0.9f;
                    rope.rotation = Quaternion.LookRotation(sim.Corridor.TangentAt(lineS), Vector3.up);
                    rope.localScale = new Vector3(sim.Corridor.Width + 1f, 0.05f, 0.05f);
                    r++;
                }
            }
            for (int i = o; i < _officers.Count; i++) _officers[i].gameObject.SetActive(false);
            for (int i = r; i < _ropes.Count; i++) _ropes[i].gameObject.SetActive(false);
        }

        private Transform Spawn(Agent a)
        {
            if (a.IsPedestrian)
            {
                Transform p = Person(_person);
                p.name = "Person " + a.Id;
                return p;
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root, false);
            Material m = _car;
            switch (a.Class)
            {
                case VehicleClass.Bus: m = a.Brain != null && a.Brain.OwnCompany ? _ownBus : _bus; break;
                case VehicleClass.Truck: m = _truck; break;
                case VehicleClass.Cng: m = _cng; break;
                case VehicleClass.Rickshaw: m = _rickshaw; break;
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            go.name = (a.Brain != null ? a.Brain.CrewName + " " : "") + a.Class + " " + a.Id;
            return go.transform;
        }

        private Transform Person(Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root, false);
            go.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f);      // a capsule primitive is 2 m tall at scale 1
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go.transform;
        }
    }
}
