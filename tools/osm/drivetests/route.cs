var d = TwentyTons.Unity.PlayerBusDrive.Instance; var sb = new System.Text.StringBuilder();
TwentyTons.Unity.PlayerBusDrive.Scripted = true; d.SetUp();
float lane = -2.5f, nextLog = 10f, stuckFor = 0f; bool shot11 = false, shot10 = false, shotK = false; int frames = 0;
float minLat = 99f, maxLat = -99f, maxV = 0f, offRoadFrames = 0f;
while (frames++ < 60 * 400)
{
    // A driver's eyes: aim at the lane 12 m ahead and turn the wheel toward it; ease off in a bend.
    Vector3 aim = d.Corridor.PositionAt(d.Agent.S + 8f + d.Agent.Speed * 1.0f, lane);
    Vector3 to = aim - d.Agent.Position;
    float wantedYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
    float err = Mathf.DeltaAngle(d.Agent.Yaw * Mathf.Rad2Deg, wantedYaw);
    float steer = Mathf.Clamp(err / 15f, -1f, 1f);
    float kmh = d.Agent.Speed * 3.6f;
    float throttle = kmh < (Mathf.Abs(err) > 6f ? 25f : 45f) ? 1f : 0f;
    float brake = Mathf.Abs(err) > 20f && kmh > 20f ? 0.5f : 0f;
    if (d.Agent.S > d.Corridor.Length - 60f) { throttle = 0f; brake = 0.6f; }
    d.Tick(1f / 60f, throttle, brake, steer);
    minLat = Mathf.Min(minLat, d.Agent.Lateral); maxLat = Mathf.Max(maxLat, d.Agent.Lateral); maxV = Mathf.Max(maxV, kmh);
    if (Mathf.Abs(d.Agent.Lateral) > d.Corridor.HalfWidth) offRoadFrames++;
    float t = frames / 60f;
    if (t >= nextLog) { sb.Append("  " + d.Status + " err=" + err.ToString("0") + "\n"); nextLog += 10f; }
    if (!shot11 && d.Agent.S > 980f) { shot11 = true; TwentyTons.EditorTools.SceneShots.Chase("SHOTS/route_m11.png"); }
    if (!shot10 && d.Agent.S > 2290f) { shot10 = true; TwentyTons.EditorTools.SceneShots.Chase("SHOTS/route_m10.png"); }
    if (!shotK && d.Agent.S > d.Corridor.Length - 80f) { shotK = true; TwentyTons.EditorTools.SceneShots.Chase("SHOTS/route_kazipara.png"); }
    if (t > 5f && kmh < 0.5f) { stuckFor += 1f / 60f; if (stuckFor > 3f) { sb.Append("STUCK: " + d.Status + "\n"); TwentyTons.EditorTools.SceneShots.Chase("SHOTS/route_stuck.png"); TwentyTons.EditorTools.SceneShots.TopDown(d.Agent.Position.x, d.Agent.Position.z, 50f, "SHOTS/route_stuck_top.png"); break; } } else stuckFor = 0f;
    if (d.Agent.S > d.Corridor.Length - 30f && kmh < 1f) { sb.Append("ARRIVED: " + d.Status + "\n"); break; }
}
sb.Append("lateral range " + minLat.ToString("0.0") + " .. " + maxLat.ToString("0.0") + " (kerb at -" + d.Corridor.HalfWidth + "), top speed " + maxV.ToString("0") + " km/h, off the carriageway " + (offRoadFrames / 60f).ToString("0.0") + " s, route " + d.Corridor.Length.ToString("0") + " m\n");
d.SetUp(); TwentyTons.Unity.PlayerBusDrive.Scripted = false; return sb.ToString();
