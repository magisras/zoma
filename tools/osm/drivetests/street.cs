// The street alive: the first kilometres out of Mirpur 12 with the sim running (crowds, rivals,
// traffic, people crossing, the sergeant, the ledger) and a driver who brakes for what is in front:
// the gap the sim reports ahead, and a person within 25 m in the lane. Stops at the end of the
// stretch, when the day ends, or after 1200 s. Logs every 20 s and the ledger's events as they come,
// to SHOTS/street.log; a photo at the first crowd worked. Replace SHOTS with a folder.
float FROM = 30f, UNTIL = 3600f;      // metres along the way out: FROM 5500, UNTIL 7600 covers the Agargaon officers
var d = TwentyTons.Unity.PlayerBusDrive.Instance; var w = TwentyTons.Unity.WorldStreamer.Instance; var sb = new System.Text.StringBuilder();
var street = d.GetComponent<TwentyTons.Unity.StreetSim>(); street.enabled = true;
float startWas = d.StartAlong; d.StartAlong = FROM;
TwentyTons.Unity.PlayerBusDrive.Scripted = true; d.SetUp(); w.Refresh(true);
var sim = d.Street.Sim; var ledger = sim.Economy.Ledger; int seenEvents = 0;
float lane = -2.5f, nextLog = 20f, nextStream = 0f, t = 0f, dt = 1f / 60f, stuckFor = 0f; bool shot = false, caneShot = false; int stops = 0, dumps = 0; float stoodAt = -1f;
string st = "";
sb.Append("zones " + sim.Zones.Count + ", checkpoints " + sim.Checkpoints.Count + ", agents " + sim.Agents.Count + ", aboard " + sim.Player.Load.Count + "\n");
while (t < 1200f)
{
    if (t >= nextStream) { w.Refresh(true); nextStream = t + 2f; }
    Vector3 aim = d.Corridor.PositionAt(d.Agent.S + 6f + d.Agent.Speed * 0.7f, lane);
    Vector3 to = aim - d.Agent.Position;
    float err = Mathf.DeltaAngle(d.Agent.Yaw * Mathf.Rad2Deg, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
    float steer = Mathf.Clamp(err / 15f, -1f, 1f);
    float kmh = d.Agent.Speed * 3.6f;
    // Eyes: the sim's gap ahead, and people in the lane ahead.
    float gap = sim.Metrics.GapAheadMetres; float person = 999f;
    // People on the carriageway in our lane, or crossing towards it; the ones standing on the kerb (|lat| > 4) are the crowd.
    foreach (var a in sim.Agents) { if (!a.IsPedestrian) continue; float ds = sim.Corridor.DeltaS(d.Agent.S, a.S) - d.Agent.HalfLength; bool onRoad = Mathf.Abs(a.Lateral) < sim.Corridor.HalfWidth - 0.2f; if (ds > -2f && ds < 30f && onRoad && Mathf.Abs(a.Lateral - d.Agent.Lateral) < 2.2f) person = Mathf.Min(person, ds); }
    float limit = 40f;
    if (gap < 25f) limit = Mathf.Min(limit, Mathf.Max(0f, (gap - 6f) * 2f));
    if (person < 25f) limit = Mathf.Min(limit, Mathf.Max(0f, (person - 8f) * 1.5f));
    // A crowd: stand at a zone with people waiting until the door has been quiet for a few seconds (the helper's call).
    var zone = TwentyTons.Core.Boarding.ZoneInReach(sim, sim.Player);
    bool working = zone != null && (zone.Waiting.Count > 0 || sim.Player.Load.AtDoor != null || sim.Player.Load.Leaving != null);
    if (working) { limit = 0f; if (stoodAt < 0f) { stoodAt = t; stops++; if (!shot) { UnityEditor.EditorApplication.Step(); UnityEditor.EditorApplication.isPaused = false; TwentyTons.EditorTools.SceneShots.Chase("SHOTS/street_crowd.png"); shot = true; } } }
    else stoodAt = -1f;
    // The cane: the sim's stop line ahead when the officer holds our stream. Brake to it, like the traffic does.
    float line = sim.StopDistanceAhead(sim.Player, 80f);
    if (line < 80f) { limit = Mathf.Min(limit, Mathf.Max(0f, (line - 4f) * 2f)); if (!caneShot && line < 15f) { caneShot = true; UnityEditor.EditorApplication.Step(); UnityEditor.EditorApplication.isPaused = false; TwentyTons.EditorTools.SceneShots.Chase("SHOTS/street_cane.png"); } }
    float throttle = kmh < limit ? 1f : 0f;
    float brake = kmh > limit + 3f ? 0.6f : 0f;
    if (limit <= 0f) { throttle = 0f; brake = 1f; }
    st = d.Simulate(dt, throttle, brake, steer, false);
    t += dt;
    for (; seenEvents < ledger.Events.Count; seenEvents++) sb.Append("  t=" + t.ToString("0") + " s=" + d.Agent.S.ToString("0") + " EVENT " + ledger.Events[seenEvents] + "\n");
    if (t >= nextLog) { sb.Append("t=" + t.ToString("0") + " " + st + " gap=" + gap.ToString("0") + " person=" + (person < 999f ? person.ToString("0") : "-") + " aboard=" + sim.Player.Load.Count + " fares=" + ledger.FaresTk.ToString("0") + " agents=" + sim.Agents.Count + (zone != null ? " at " + zone.Name + " waiting " + zone.Waiting.Count : "") + "\n"); nextLog += 20f; }
    // Standing a long time: who is in front? (the test driver has no horn and no patience of its own)
    if (kmh < 0.5f) { stuckFor += dt; if (stuckFor > 30f && dumps < 3) { dumps++; sb.Append("  STANDING 30 s at s=" + d.Agent.S.ToString("0") + " held=" + d.Bus.Held + " limit=" + limit.ToString("0") + ":"); foreach (var a in sim.Agents) { if (a.IsPlayer) continue; float ds = sim.Corridor.DeltaS(d.Agent.S, a.S); if (ds > -15f && ds < 40f) sb.Append(" [" + a.Class + (a.Brain != null ? " " + a.Brain.CrewName : "") + " ds=" + ds.ToString("0.0") + " lat=" + a.Lateral.ToString("0.0") + " v=" + (a.Speed * 3.6f).ToString("0") + (a.IsPedestrian ? " " + a.PedState + " mid=" + a.MidRoadSeconds.ToString("0") : "") + "]"); } sb.Append("\n"); stuckFor = 0f; } } else stuckFor = 0f;
    if (sim.Economy.DayOver) { sb.Append("DAY OVER: " + sim.Economy.DayOverReason + " " + st + "\n"); break; }
    if (d.Agent.S > UNTIL) { sb.Append("DONE: " + st + "\n"); break; }
}
sb.Append("end " + st + "; stops " + stops + ", fares Tk " + ledger.FaresTk.ToString("0") + ", paid out Tk " + ledger.PaidOutTk.ToString("0") + ", contacts " + sim.Metrics.Contacts + " hard " + sim.Metrics.HardContacts + ", near misses " + sim.Metrics.NearMisses + ", knocked down " + sim.Metrics.PeopleKnockedDown + ", stops lost " + sim.Metrics.StopsLost + " first " + sim.Metrics.StopsFirst + "/" + sim.Metrics.StopsContested + ", horn " + sim.Metrics.HornPresses + ", cane runs " + sim.Metrics.CaneRuns + ", held at the rope " + sim.Metrics.RopeHeldSeconds.ToString("0") + " s, waited at lines " + sim.Metrics.CaneWaitSeconds.ToString("0") + " s, junctions " + sim.Junctions.Count + ", " + w.Status + "\n");
UnityEditor.EditorApplication.Step(); UnityEditor.EditorApplication.isPaused = false; TwentyTons.EditorTools.SceneShots.Chase("SHOTS/street_end.png");   // a frame first: the street is drawn in LateUpdate, which the scripted drive never runs
d.StartAlong = startWas; d.SetUp(); w.Refresh(true); TwentyTons.Unity.PlayerBusDrive.Scripted = false; System.IO.File.WriteAllText("SHOTS/street.log", sb.ToString()); return "see SHOTS/street.log";
