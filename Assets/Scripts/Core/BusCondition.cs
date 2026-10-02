using TwentyTons.Tuning;
using UnityEngine;

namespace TwentyTons.Core
{
    /// <summary>
    /// The bus as a character (RESEARCH.md): "A 20-ton vehicle with worn brakes and heavy steering.
    /// Repairs cost real money you don't have; skipping them is a loan against tomorrow. The fitness
    /// certificate can be bought and means nothing." This is the state that carries from day to day.
    /// </summary>
    public sealed class BusCondition
    {
        public float BrakeWear;            // 0 new .. 1 metal on metal; the brakes fade with it
        public int Dents;                  // scrapes that were never fixed: cosmetic, and the sergeant notices
        public int PapersValidUntilDay;    // fitness certificate: a number on a piece of paper, until this day
        public float BrakeWearToday;       // how much the day's driving added
        public bool WindscreenCracked;     // after a rollover; the owner never replaces it (Khurshid's story)
        public bool DoorBent;              // after a rollover; people take longer at the door

        public bool PapersValid(int day) => day <= PapersValidUntilDay;

        /// <summary>Braking grinds the pads: wear grows with how hard and how fast you brake.</summary>
        public void Brake(float brakeInput, float speed, float dt, BusSettings b)
        {
            float added = Mathf.Clamp01(brakeInput) * Mathf.Max(0f, speed) * dt * b.WearPerMetreSecondBraked;
            BrakeWear = Mathf.Clamp01(BrakeWear + added);
            BrakeWearToday += added;
        }

        /// <summary>The mechanic at the roadside: new pads, more or less.</summary>
        public void ServiceBrakes(BusSettings b)
        {
            BrakeWear = b.WearAfterService;
        }
    }
}
