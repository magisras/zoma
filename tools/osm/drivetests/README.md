# Drive tests from the terminal

With the editor open and the CLI bridge connected (`unity status`), these C# snippets run inside
the editor through `unity command eval "$(cat file)"`. They enter the scene's `PlayerBusDrive`
through its scripted pedals and step it at 60 Hz without waiting for editor frames (the editor
stops ticking Play mode when its window is not in front). Replace `SHOTS` with a folder for the
PNGs first (`sed "s#SHOTS#/tmp/shots#g"`). Enter Play mode before running them
(`unity command eval 'UnityEditor.EditorApplication.isPlaying = true;'`).

- `pedals.cs`: full throttle from rest; stops from 40 km/h with worn and new brakes, empty and with
  90 riders; a full-lock swerve; a run into the median; 0-40 loaded against empty.
- `route.cs`: the whole route to Kazipara with a driver's eyes (aim point ahead on the lane), a log
  line every 10 s, photos at Mirpur 11, Mirpur 10 and Kazipara, a photo where it gets stuck.

Results of 8 Oct 2026 are in `PROGRESS.md`, Step 31.
