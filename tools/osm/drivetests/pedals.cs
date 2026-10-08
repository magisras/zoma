var d = TwentyTons.Unity.PlayerBusDrive.Instance; var sb = new System.Text.StringBuilder();
TwentyTons.Unity.PlayerBusDrive.Scripted = true;
// A. full throttle from rest, empty bus, starting air 0.4
d.SetUp(); sb.Append("A full throttle, empty\n");
for (int i = 0; i < 10; i++) sb.Append("  " + d.Simulate(2f, 1f, 0f, 0f) + "\n");
TwentyTons.EditorTools.SceneShots.Chase("SHOTS/drive_a.png");
// B1. brake from ~40 km/h, worn (0.5) brakes, air as it is after the run
d.SetUp(); float t0 = 0f; string st = ""; int guard = 0;
while (d.Agent.Speed * 3.6f < 40f && guard++ < 60 * 40) st = d.Simulate(1f / 60f, 1f, 0f, 0f);
float s0 = d.Agent.S; sb.Append("B1 worn brakes: at " + st + "\n"); guard = 0;
while (d.Agent.Speed > 0.05f && guard++ < 60 * 30) st = d.Simulate(1f / 60f, 0f, 1f, 0f);
sb.Append("   stopped after " + (d.Agent.S - s0).ToString("0.0") + " m: " + st + "\n");
// B2. same with new brakes and full air
d.SetUp(); d.Bus.BrakeWear = 0f; guard = 0;
while (d.Agent.Speed * 3.6f < 40f && guard++ < 60 * 40) st = d.Simulate(1f / 60f, 1f, 0f, 0f);
d.Bus.AirPressure = 1f; s0 = d.Agent.S; sb.Append("B2 new brakes, full air: at " + st + "\n"); guard = 0;
while (d.Agent.Speed > 0.05f && guard++ < 60 * 30) st = d.Simulate(1f / 60f, 0f, 1f, 0f);
sb.Append("   stopped after " + (d.Agent.S - s0).ToString("0.0") + " m: " + st + "\n");
// B3. worn brakes, 90 riders
d.SetUp(); d.Bus.Passengers = 90; guard = 0;
while (d.Agent.Speed * 3.6f < 40f && guard++ < 60 * 60) st = d.Simulate(1f / 60f, 1f, 0f, 0f);
s0 = d.Agent.S; sb.Append("B3 worn brakes, 90 riders: at " + st + "\n"); guard = 0;
while (d.Agent.Speed > 0.05f && guard++ < 60 * 30) st = d.Simulate(1f / 60f, 0f, 1f, 0f);
sb.Append("   stopped after " + (d.Agent.S - s0).ToString("0.0") + " m: " + st + "\n");
// C. swerve at ~40: full right 2 s, then full left 2 s, then straight
d.SetUp(); guard = 0;
while (d.Agent.Speed * 3.6f < 40f && guard++ < 60 * 40) st = d.Simulate(1f / 60f, 1f, 0f, 0f);
sb.Append("C swerve from " + st + "\n");
for (int i = 0; i < 4; i++) sb.Append("  right: " + d.Simulate(0.5f, 1f, 0f, 1f) + "\n");
for (int i = 0; i < 4; i++) sb.Append("  left:  " + d.Simulate(0.5f, 1f, 0f, -1f) + "\n");
sb.Append("  straight: " + d.Simulate(2f, 1f, 0f, 0f) + "\n");
TwentyTons.EditorTools.SceneShots.Chase("SHOTS/drive_c.png");
// D. into the median: throttle and steer right until the bus stops against something
d.SetUp(); guard = 0;
while (d.Agent.Speed > 0.05f || guard < 60) { st = d.Simulate(1f / 60f, 1f, 0f, 1f); if (guard++ > 60 * 20) break; }
sb.Append("D steer right into the median: " + st + "\n");
TwentyTons.EditorTools.SceneShots.Chase("SHOTS/drive_d.png");
// E. 0 to 40 with 90 riders vs empty
d.SetUp(); guard = 0; while (d.Agent.Speed * 3.6f < 40f && guard++ < 60 * 60) st = d.Simulate(1f / 60f, 1f, 0f, 0f);
sb.Append("E 0-40 empty: " + st + "\n");
d.SetUp(); d.Bus.Passengers = 90; guard = 0; while (d.Agent.Speed * 3.6f < 40f && guard++ < 60 * 60) st = d.Simulate(1f / 60f, 1f, 0f, 0f);
sb.Append("E 0-40 with 90: " + st + "\n");
d.SetUp(); TwentyTons.Unity.PlayerBusDrive.Scripted = false;
return sb.ToString();
