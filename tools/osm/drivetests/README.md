# Drive tests from the terminal

With the editor open and the CLI bridge connected (`unity status`), these C# snippets run inside
the editor through `unity command eval "$(cat file)"`. They enter the scene's `PlayerBusDrive`
through its scripted pedals and step it at 60 Hz without waiting for editor frames (the editor
stops ticking Play mode when its window is not in front). Replace `SHOTS` with a folder for the
PNGs first (`sed "s#SHOTS#/tmp/shots#g"`). Enter Play mode before running them
(`unity command eval 'UnityEditor.EditorApplication.isPlaying = true;'`).

- `pedals.cs`: full throttle from rest; stops from 40 km/h with worn and new brakes, empty and with
  90 riders; a full-lock swerve; a run into the median; 0-40 loaded against empty.
- `fullroute.cs`: one leg of the route on the physics bus with a driver who slows for corners he can
  see: `LEG = 0` Mirpur 12 to Azimpur, `LEG = 1` the way back (`sed 's#int LEG = 0;#int LEG = 1;#'`).
  A log line every 30 s, photos at three points and the end, a tow when stuck for 4 s with a photo
  and a top-down of the first six. The run ends when the leg turns at the far stand
  (`PlayerBusDrive.WatchStands`), so it checks the turnaround too. Writes `SHOTS/full_<LEG>.log`
  because the editor's bridge
  gives a command 5 s of the main thread and then answers with a timeout, while the script runs on
  to the end (a minute or two). Before it: load every chunk, since a scene load only completes at a
  frame and the scripted drive runs none:
  `var w = TwentyTons.Unity.WorldStreamer.Instance; w.LoadWithin = 99999f; w.UnloadBeyond = 999999f;
  for (int i = 0; i < 80; i++) UnityEditor.EditorApplication.Step(); UnityEditor.EditorApplication.isPaused = false;`
  (`Step()` runs one Play-mode frame by hand; this editor does not run them on its own while
  driven from the terminal, `Time.frameCount` stays at 1.) Results of 10 Oct 2026 in `PROGRESS.md`,
  Step 43.
- `street.cs`: the first 3.6 km with the street alive (`StreetSim` on: crowds, rivals, traffic,
  people, the sergeant, the ledger) and a driver who brakes for the gap the sim reports ahead and
  for people on the carriageway, and stands at a crowd until the door is quiet. Logs every 20 s with
  aboard, fares, the gap and the zone, every ledger event, who is in front after 30 s standing, and
  the sim's counts at the end. `fullroute.cs` switches the street off for its runs: the road alone.
  A frame (`Step()`) is run before each photo, since the street is drawn in LateUpdate.
- `route.cs`: the first 3 km to Kazipara with a driver's eyes (aim point ahead on the lane), a log
  line every 10 s, photos at Mirpur 11, Mirpur 10 and Kazipara, a photo where it gets stuck.

Results of 8 Oct 2026 are in `PROGRESS.md`, Step 31.
