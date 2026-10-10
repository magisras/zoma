// The whole route on the physics bus with a driver's eyes, the world streaming in around it:
// LEG 0 is Mirpur 12 to Azimpur, LEG 1 the way back. Logs every 30 s, a photo at three points
// and the end, a tow (T) when stuck for 4 s, and the count of tows. Standing at the far stand
// (the loop runs on into the way back) ends the run. With a StreetSim on the bus the street is alive:
// the driver does not look at traffic, so contacts and sergeants are part of the result. Replace SHOTS with a folder.
int LEG = 0;
var d = TwentyTons.Unity.PlayerBusDrive.Instance; var w = TwentyTons.Unity.WorldStreamer.Instance; var sb = new System.Text.StringBuilder();
var street = d.GetComponent<TwentyTons.Unity.StreetSim>(); if (street != null) street.enabled = false;   // the road alone; street.cs tests the street
TwentyTons.Unity.PlayerBusDrive.Scripted = true; d.SetUp(); d.StartLeg(LEG); w.Refresh(true);
float lane = -2.5f, nextLog = 30f, stuckFor = 0f, nextStream = 0f, t = 0f, dt = 1f / 60f; int tows = 0, step = 0;
float legBase = LEG == 1 ? d.OutLength : 0f;      // the way back's S starts where the way out ends on the loop
float maxV = 0f, maxRoll = 0f; var shots = new System.Collections.Generic.List<float> { legBase + 8300f, legBase + 10800f, d.LegEndS - 60f }; int shot = 0;
string st = "";
while (t < 1800f)
{
    if (t >= nextStream) { w.Refresh(true); nextStream = t + 2f; }
    Vector3 aim = d.Corridor.PositionAt(d.Agent.S + 6f + d.Agent.Speed * 0.7f, lane);
    Vector3 to = aim - d.Agent.Position;
    float wantedYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
    float err = Mathf.DeltaAngle(d.Agent.Yaw * Mathf.Rad2Deg, wantedYaw);
    float steer = Mathf.Clamp(err / 15f, -1f, 1f);
    float kmh = d.Agent.Speed * 3.6f;
    // A driver slows for a corner he can see: the road's heading change over the next 10..50 m.
    Vector3 a1 = d.Corridor.PositionAt(d.Agent.S + 10f, 0f), a2 = d.Corridor.PositionAt(d.Agent.S + 25f, 0f), a3 = d.Corridor.PositionAt(d.Agent.S + 50f, 0f);
    float bend = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(a2.x - a1.x, a2.z - a1.z) * Mathf.Rad2Deg, Mathf.Atan2(a3.x - a2.x, a3.z - a2.z) * Mathf.Rad2Deg));
    float limit = bend > 45f ? 12f : bend > 20f ? 20f : bend > 8f ? 30f : 45f;
    if (Mathf.Abs(err) > 6f) limit = Mathf.Min(limit, 25f);
    float throttle = kmh < limit ? 1f : 0f;
    float brake = kmh > limit + 5f ? 0.5f : (Mathf.Abs(err) > 20f && kmh > 20f ? 0.5f : 0f);
    if (d.Agent.S > d.LegEndS - 60f) { throttle = 0f; brake = 0.6f; }
    st = d.Simulate(dt, throttle, brake, steer, false);
    t += dt; step++;
    maxV = Mathf.Max(maxV, kmh); maxRoll = Mathf.Max(maxRoll, Mathf.Abs(d.Physics.RollDegrees));
    if (t >= nextLog) { sb.Append("  t=" + t.ToString("0") + " " + st + " err=" + err.ToString("0") + " " + w.Status + "\n"); nextLog += 30f; }
    if (shot < shots.Count && d.Agent.S > shots[shot]) { TwentyTons.EditorTools.SceneShots.Chase("SHOTS/full_" + LEG + "_" + shot + ".png"); shot++; }
    if (t > 5f && kmh < 0.5f && d.Agent.S < d.LegEndS - 60f && !d.Bus.Held) { stuckFor += dt; if (stuckFor > 4f) { tows++; sb.Append("STUCK at " + st + "\n"); if (tows <= 6) { TwentyTons.EditorTools.SceneShots.Chase("SHOTS/full_" + LEG + "_stuck" + tows + ".png"); TwentyTons.EditorTools.SceneShots.TopDown(d.Agent.Position.x, d.Agent.Position.z, 45f, "SHOTS/full_" + LEG + "_stuck" + tows + "_top.png"); } d.Tow(); stuckFor = 0f; if (tows > 12) break; } } else stuckFor = 0f;
    if (d.Leg != LEG) { sb.Append("ARRIVED, leg turned: " + st + "\n"); break; }
    if (d.Agent.S > d.LegEndS - 30f && kmh < 1f) { sb.Append("ARRIVED: " + st + "\n"); break; }
}
sb.Append("end " + st + "; " + t.ToString("0") + " s, top " + maxV.ToString("0") + " km/h, max roll " + maxRoll.ToString("0") + " deg, tows " + tows + ", route " + d.Corridor.Length.ToString("0") + " m, " + w.Status + "\n");
if (street != null) street.enabled = true; d.SetUp(); w.Refresh(true); TwentyTons.Unity.PlayerBusDrive.Scripted = false; System.IO.File.WriteAllText("SHOTS/full_" + LEG + ".log", sb.ToString()); return "see SHOTS/full_" + LEG + ".log";
