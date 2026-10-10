using System.Collections.Generic;
using UnityEngine;

namespace TwentyTons.Unity
{
    /// <summary>
    /// The inside of the bus, for the inside mirror and the door view, from Andrew Fraser's ride-along
    /// (S-026.11): two-and-two high-backed seats in red floral vinyl covers on green cushions down a
    /// narrow aisle of bare metal floor, luggage racks along both sides overhead, the painted walls
    /// below and above the window band, the roof. Passengers are capsules: the first on the cab's
    /// bench and the seats, the rest standing in the aisle, as many as the model says are aboard.
    /// Primitives, no colliders, built at Play start under the bus body; the art pass replaces them.
    /// </summary>
    public sealed class BusInterior
    {
        private readonly List<Vector3> _seatSpots = new List<Vector3>();
        private readonly List<Vector3> _standSpots = new List<Vector3>();
        private readonly List<Transform> _people = new List<Transform>();
        private readonly Transform _root;
        private readonly Material _person;

        public BusInterior(Transform bus, Core.VehicleShape shape, Vector3 driverEye)
        {
            Transform old = bus.Find("Interior");
            if (old != null) Object.Destroy(old.gameObject);
            _root = new GameObject("Interior").transform;
            _root.SetParent(bus, false);
            float floor = 0.45f, h = shape.Height, w = shape.Width, l = shape.Length;   // BusBody's floor and shell
            float bandY = floor + (h - floor) * 0.62f, bandH = (h - floor) * 0.32f;
            Material paint = Make(new Color(0.72f, 0.33f, 0.14f)), vinyl = Make(new Color(0.62f, 0.14f, 0.16f));
            Material cushion = Make(new Color(0.42f, 0.48f, 0.28f)), metal = Make(new Color(0.30f, 0.29f, 0.27f)), rack = Make(new Color(0.45f, 0.45f, 0.42f));
            _person = Make(new Color(0.30f, 0.25f, 0.35f));

            // The floor plate, the walls below and above the windows (the shell's inside faces are culled), the roof.
            Part("Floor", metal, new Vector3(0f, floor + 0.02f, 0f), new Vector3(w - 0.1f, 0.03f, l - 0.2f));
            float lowH = (bandY - bandH * 0.5f) - floor, highH = h - (bandY + bandH * 0.5f);
            foreach (float side in new[] { -1f, 1f })
            {
                Part("Wall low", paint, new Vector3(side * (w * 0.5f - 0.03f), floor + lowH * 0.5f, 0f), new Vector3(0.02f, lowH, l - 0.2f));
                Part("Wall high", paint, new Vector3(side * (w * 0.5f - 0.03f), h - highH * 0.5f, 0f), new Vector3(0.02f, highH, l - 0.2f));
                // The luggage rack: a shelf over the seats, the length of the saloon.
                Part("Rack", rack, new Vector3(side * (w * 0.5f - 0.25f), bandY + bandH * 0.5f + 0.05f, -1.2f), new Vector3(0.4f, 0.04f, l * 0.72f));
                Part("Rack lip", rack, new Vector3(side * (w * 0.5f - 0.45f), bandY + bandH * 0.5f + 0.1f, -1.2f), new Vector3(0.03f, 0.12f, l * 0.72f));
            }
            Part("Roof", paint, new Vector3(0f, h - 0.03f, 0f), new Vector3(w - 0.1f, 0.02f, l - 0.2f));

            // Seats: two and two, rows from behind the cab to the back, a 0.5 m aisle.
            float seatW = 0.42f, pitch = 0.72f, cushionY = floor + 0.45f;
            float[] xs = { -w * 0.5f + 0.3f, -w * 0.5f + 0.3f + seatW + 0.04f, w * 0.5f - 0.3f - seatW - 0.04f, w * 0.5f - 0.3f };
            for (float z = driverEye.z - 1.9f; z > -l * 0.5f + 0.6f; z -= pitch)
            {
                foreach (float x in xs)
                {
                    Part("Cushion", cushion, new Vector3(x, cushionY, z), new Vector3(seatW, 0.1f, 0.45f));
                    Part("Back", vinyl, new Vector3(x, cushionY + 0.5f, z - 0.2f), new Vector3(seatW, 1.0f, 0.08f));
                    Part("Seat frame", metal, new Vector3(x, floor + 0.22f, z), new Vector3(seatW - 0.1f, 0.4f, 0.35f));
                    _seatSpots.Add(new Vector3(x, cushionY + 0.55f, z + 0.05f));
                }
                _standSpots.Add(new Vector3(0f, floor + 0.85f, z + 0.3f));
            }
            // The cab's bench (BusCabin): two sit beside the driver first.
            _seatSpots.Insert(0, new Vector3(-w * 0.5f + 0.3f, driverEye.y - 0.9f, driverEye.z + 0.5f));
            _seatSpots.Insert(1, new Vector3(-w * 0.5f + 0.3f, driverEye.y - 0.9f, driverEye.z - 0.3f));
        }

        public int Seats => _seatSpots.Count;

        /// <summary>Show this many people aboard: the seats first, then standing in the aisle, then the doorway.</summary>
        public void Show(int aboard)
        {
            int spots = _seatSpots.Count + _standSpots.Count * 3;
            aboard = Mathf.Clamp(aboard, 0, spots);
            while (_people.Count < aboard)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.Destroy(go.GetComponent<Collider>());
                go.name = "Rider";
                go.transform.SetParent(_root, false);
                go.GetComponent<MeshRenderer>().sharedMaterial = _person;
                _people.Add(go.transform);
            }
            for (int i = 0; i < _people.Count; i++)
            {
                bool on = i < aboard;
                _people[i].gameObject.SetActive(on);
                if (!on) continue;
                if (i < _seatSpots.Count)
                {
                    _people[i].localPosition = _seatSpots[i];
                    _people[i].localScale = new Vector3(0.4f, 0.45f, 0.4f);       // seated: a capsule from the hips up
                }
                else
                {
                    int k = i - _seatSpots.Count;
                    Vector3 s = _standSpots[k % _standSpots.Count];
                    s.x += ((k / _standSpots.Count) - 1) * 0.3f;                  // three abreast in the aisle at the crush
                    _people[i].localPosition = s;
                    _people[i].localScale = new Vector3(0.4f, 0.8f, 0.4f);
                }
            }
        }

        private Transform Part(string name, Material m, Vector3 centre, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private static Material Make(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.3f);
            return m;
        }
    }
}
