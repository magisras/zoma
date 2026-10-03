# Playtest numbers without a player

RESEARCH.md asks for one tuning test above all: "'polite driving still wins' means rivals too
timid." The headless runner answers it in minutes, before anyone sits down with a controller.

## Running it

```
./tools/headless.sh --batch 6 600          # 6 seeds × 600 s, careful vs Dhaka driving
./tools/headless.sh --batch 6 600 0.5      # same, with crowds growing at half the rate
./tools/headless.sh 2 600 25 --dhaka -v    # one Dhaka day on seed 2, door events logged
```

Both scripted drivers live in `sandbox/ScriptedDriver.cs` and are also the sandbox autopilots
(P cycles off → careful → Dhaka). They are instruments, not opponents:

- **Careful**: holds a line, 2 s headway, under 25 km/h, respects the cane, pays everyone, stops
  for any crowd, waits up to 25 s.
- **Dhaka**: 0.6 s headway, horn whenever something is close, cuts into the freest band, runs the
  cane when the box is clear, takes the wrong side after 4 s stuck, stops for crowds of three or
  more, leaves 10 s after the last rider got off, pays the sergeant. Both brake for a person in
  the way: that reflex is the street's one hard rule.

## First result (2 Oct 2026, 6 seeds × 600 s)

| policy  | crew net Tk | fares | km   | scrapes | near misses | stops lost | people hit |
|---------|------------:|------:|-----:|--------:|------------:|-----------:|-----------:|
| careful |        −313 |   133 | 0.75 |    11.7 |         2.7 |        0.5 |        0/6 |
| Dhaka   |        −827 |    53 | 1.01 |    18.3 |        11.3 |        1.3 |        1/6 |

With crowds at half the rate: careful −282, Dhaka −1234. **Verdict: polite driving still wins.**

After step 9 (people get on while others get off; the moving door): careful −218 with fares 216,
Dhaka −784 with fares 146 and 23.5 scrapes a day. Loading in parallel lifted both drivers by
Tk 80–90; the scrapes still decide it.

After step 12 (contact under 1.5 m/s relative is cosmetic; brake wear now grows at a real day's
rate): careful −154 with fares 228 and 8 scrapes, Dhaka −650 with fares 81 and 10.8 scrapes.
Dhaka's fares fell: its brakes fade over the day and it overshoots zones it would have worked.
A policy that knows its brakes are going would slow in earlier. The verdict stands.

## After the street's control (2 Oct 2026, 6 seeds × 900 s)

With ropes, police boxes, drive days and cameras in (`docs/STREET_CONTROL.md`): careful −175 with
fares 268 and 13.8 scrapes a day, Dhaka −287 with fares 146 and 21.8 scrapes. No sergeant took
money in six days at the default chances and no camera was on yet (day 1). The rope changes the
queue at the first junction, not the day: over ten minutes the Dhaka driver covers 1.2 km with or
without it. **Verdict unchanged: polite driving still wins.**

## After the swerve (3 Oct 2026, 6 seeds × 900 s)

A yield is now driven, not slid (PROGRESS step 17), and the standoffs that sliding had hidden are
fixed: careful +2 Tk with fares 420, 1.68 km and 0 people hit in six days (two days in profit, the
first ever); Dhaka −656 with fares 151, 1.04 km, 21.7 scrapes and 1 person hit, killed while
overtaking along the median strip at 34 km/h where people stand. The Dhaka driver's stopsLost fell
below the careful driver's (2.5 vs 3.8): it gets to the crowds first and still earns less, because
it leaves them half-loaded. **Verdict unchanged: polite driving still wins.** The gap is now about
what happens at the stand, not on the road.

## After the first test report (3 Oct 2026, 6 seeds × 900 s, `main` at 9c0ac02)

Blocking the box fixed, people brushed rather than killed by a creep or a graze, the Dhaka driver
taking the wrong side when there is room and seeing the people on the median: careful −58 with
fares 353, 1.47 km, two days in profit; Dhaka −705 with fares 161, 1.28 km. **Nobody hit in twelve
days.** The Dhaka driver's losses are now rollovers: five in six days ("turned too hard for twenty
tons"), Tk 500 of ropes each, which is its own steering at speed and the first thing the tuning pass
should look at, before the stand. **Verdict unchanged: polite driving still wins.**

## After the bus research and test report 0817 (3 Oct 2026, 6 seeds × 900 s, `main` at fb94880)

Air brakes with a lag and a cold start, the ring repaired, the Dhaka driver taking the wrong side and
cornering under the tipping point, head-on contacts by mass: careful +10 with fares 447, 1.63 km;
Dhaka −394 with fares 236, 1.68 km, 34.5 scrapes, wrong side 13 s a day, **no rollovers, nobody
hit in twelve days**. The Dhaka driver now covers as much road as the careful one and still earns
half the fares: the gap is entirely at the stand (serial boarding, crowds that never run dry), not on
the road. **Verdict unchanged: polite driving still wins.** This is the batch the tuning pass starts from.

## Why, and what to tune

The numbers say the aggressive policy loses money three ways, none of them the way the research
describes the real street:

1. **Scrapes.** 0.6 s headway with a slow controller rear-ends things; each scrape is repairs, and
   the jam behind a scrape costs time. Real drivers tailgate at 0.6 s without hitting: the
   scripted driver lacks their anticipation. Either sharpen the policy's braking or lower the
   repair cost for light contact (RESEARCH: paint and mirrors are the norm).
2. **Boarding is slow and serial.** One door, one person at a time, riders off before riders on.
   The careful driver's 25 s dwell earns more than the Dhaka driver's 10 s. Real buses load
   while unloading, with the helper pulling people on. Parallel streams (step 10's moving door)
   should shift this.
3. **Arriving second barely costs.** Crowds refill fast enough that the second bus still finds
   people; `StopsLost` stays near 1. Lower the refill rate at ordinary zones and let a crowd be
   taken whole by the first door, so racing for a stop decides the day the way the research says.

Also worth testing once a human drives: the fixed deposit is scaled to 10 % for the short day
while fuel is real per km, so the pressure of the zoma is softer than in life.

Numbers in this file are from the commit that introduced it; rerun the batch after tuning.

## The tuning pass (3 Oct 2026, 6 seeds × 900 s, crowd rate 1.0 and 0.5)

The owner's decision (test report 0817, finding 0): polite driving must not win, on any seed. The pass
took the day apart in three layers before the economics could be read at all.

**1. Days that never ran.** Half the batch days ended with the bus standing for 300–800 s of the 900,
and the means were made of them. Each was a standoff where two rules each treated the other party as a
wall: a nose half a metre over a stop line holding a cross bus through the middle of the box (and the
cross bus holding the nose); a crosser pinned mid-road by a stream of rickshaws in the next lane; a
person a step behind a bus's nose waiting for the bus to move while the bus waited for the person; a
rickshaw's nose in a CNG's path at the second box; two vehicles side by side a hand ahead of each
other, each "following" the other at a negative gap. Fixes: the box check is geometric (a standing
occupant blocks only what it is actually across; a moving one still blocks); NPCs approach stop lines
no faster than their brakes can stop from and back off half a metre when over the line with the box
full against them; a crosser goes round a standing vehicle's nose or tail, is never stopped by a
standing body except its middle, takes the hand-up gap after 4 s pinned, and gives up a crossing that
has taken 20 s; a person beside a vehicle's nose is not "ahead" of it; a vehicle alongside with
daylight between is not "ahead"; the autopilots creep up to someone who stands in the road after
5 s (Dhaka) or 15 s (careful) instead of waiting on them. Every day in the batch now runs 1.6–2.5 km.

**2. Scrapes that were not scrapes.** The Dhaka autopilot's 30–50 scrapes a day were mostly the
controller: following by headway with a 0.35 s brake lag rear-ended things, angling into a band
checked once rather than every frame sideswiped them, and the sandbox ring's arcs were drawn with
eight chords, so traffic on the polyline sat up to 1.9 m away from where the player's bus drove the
true curve. Now: following by stopping distance (lag, air, wear) with a coast zone; a mirror check
before each band; 32 chords; and a scrape is priced by how the two met, nose to tail by the speed
difference along the road, side against side by the speed the sides closed at. Hard contact needs
3 m/s. Dhaka scrapes: 5 a day, mostly cosmetic.

**3. The market.** With crowds at 1.2 a minute and only our three buses taking them, the slow bus
trailing far behind found the fullest kerb, and the faster driver's reward was the dregs behind the
bus it caught up with: careful fares 524 against Dhaka 451 at 4338ff9. Two things were missing from
the research's street: the other companies' buses take crowds too (each passenger boards the first
bus), and crowds are not scarce, time is (Tk 6,000 a day, 400–600 fares). Now other companies' buses
stand at a crowd and take up to six (60 % of them stop), crowds arrive at 3 a minute (7.5 at the hot
stands), and traffic is 25 vehicles around the player rather than 50, a jam at the junctions and
movement between them instead of a standing jam end to end where both autopilots crawled at 12 km/h.

**What the Dhaka driver does that the careful one does not**, after the pass: 45 km/h against 25,
tailgates by stopping distance, horn, cuts into the freest band, goes past a bus ahead, runs the cane
when the box is clear (not on a drive day), takes the wrong side when stuck with the median bare,
pulls in to the kerb with the door already open and the first people on the step before the wheels
stop, pulls away with the door open and the helper holding whoever is on the step at walking pace.
Both drivers pull to the kerb, dwell until the kerb is bare (40 s cap), and stop for anyone. Two
Dhaka-only stop rules were tried and dropped as losers: leaving a nearly bare kerb when a crew bus is
on the tail, and passing a kerb where a crew bus is already loading.

### `main` after the pass: `./tools/headless.sh --batch 6 900`

| seed | policy | net Tk | fares | km | scrapes | stopsLost | wrongSide | caneRuns | hit |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Careful | 330 | 750 | 2.12 | 6 | 6 | 0s | 1 | 0 |
| 1 | Dhaka | 515 | 900 | 1.98 | 7 | 5 | 0s | 0 | 0 |
| 2 | Careful | 298 | 675 | 1.79 | 7 | 6 | 0s | 0 | 0 |
| 2 | Dhaka | 389 | 830 | 1.89 | 3 | 4 | 0s | 1 | 0 |
| 3 | Careful | 196 | 670 | 1.97 | 7 | 6 | 0s | 1 | 0 |
| 3 | Dhaka | 460 | 875 | 1.98 | 4 | 5 | 0s | 1 | 0 |
| 4 | Careful | 328 | 705 | 1.79 | 0 | 4 | 0s | 0 | 0 |
| 4 | Dhaka | 542 | 930 | 1.81 | 4 | 5 | 0s | 0 | 0 |
| 5 | Careful | 345 | 710 | 1.62 | 3 | 6 | 0s | 0 | 0 |
| 5 | Dhaka | 282 | 705 | 1.80 | 5 | 3 | 0s | 1 | 0 |
| 6 | Careful | 322 | 700 | 1.81 | 5 | 5 | 0s | 0 | 0 |
| 6 | Dhaka | 298 | 715 | 1.78 | 7 | 4 | 0s | 1 | 0 |

```
careful  mean net Tk    303  fares   702  km 1.85  scrapes  4.7  nearMiss  0.5  stopsLost  5.5  wrongSide    0s  people hit 0/6
dhaka    mean net Tk    415  fares   826  km 1.87  scrapes  5.0  nearMiss  1.3  stopsLost  4.3  wrongSide    0s  people hit 0/6
VERDICT: the trap holds. Dhaka driving nets Tk 112 more per day than careful driving.
```

### `./tools/headless.sh --batch 6 900 0.5`

| seed | policy | net Tk | fares | km | scrapes | stopsLost | wrongSide | caneRuns | hit |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Careful | 8 | 405 | 2.32 | 3 | 7 | 0s | 0 | 0 |
| 1 | Dhaka | 391 | 820 | 2.54 | 7 | 4 | 0s | 0 | 0 |
| 2 | Careful | 158 | 555 | 2.31 | 1 | 7 | 0s | 0 | 0 |
| 2 | Dhaka | 103 | 490 | 2.06 | 2 | 3 | 0s | 0 | 0 |
| 3 | Careful | -29 | 380 | 2.54 | 4 | 3 | 0s | 0 | 0 |
| 3 | Dhaka | 138 | 545 | 2.30 | 2 | 6 | 18s | 0 | 0 |
| 4 | Careful | -23 | 495 | 1.94 | 3 | 7 | 0s | 1 | 0 |
| 4 | Dhaka | 187 | 575 | 1.83 | 4 | 6 | 0s | 0 | 0 |
| 5 | Careful | 56 | 495 | 1.98 | 4 | 4 | 0s | 1 | 0 |
| 5 | Dhaka | -4 | 425 | 1.98 | 3 | 5 | 0s | 1 | 0 |
| 6 | Careful | 86 | 495 | 1.83 | 3 | 5 | 0s | 1 | 0 |
| 6 | Dhaka | 163 | 590 | 2.31 | 5 | 3 | 0s | 1 | 0 |

```
careful  mean net Tk     43  fares   471  km 2.15  scrapes  3.0  nearMiss  1.3  stopsLost  5.5  wrongSide    0s  people hit 0/6
dhaka    mean net Tk    163  fares   574  km 2.17  scrapes  3.8  nearMiss  2.2  stopsLost  4.5  wrongSide    3s  people hit 0/6
VERDICT: the trap holds. Dhaka driving nets Tk 120 more per day than careful driving.
```

**Verdict: the trap holds at the mean on both regimes, by Tk 112 and Tk 120 a day, nobody hit, no
rollovers, every day run to the end.** The acceptance as written (Dhaka ahead on at least 5 of 6
seeds) is not met: 4 of 6 on each. The two seeds the Dhaka driver loses at full crowds are the two
where it stood second at the Stand behind a crew bus's open door for 60–85 s (Boarding: the first door
takes the crowd) and where the day's end cut its last stop; the margins are Tk 24 and Tk 63. Honest
levers left, in order: fare collection (aggressive crews overcharge, RESEARCH: the fare disputes), the
second door getting a share once the first has stood a while, and the helper's two-door loading.

The pass also found and fixed, on the way: a pedestrian stepping off the median into the flank of a
passing bus now steps back if that is nearer; the Dhaka driver crosses the median only where nobody
stands on it; a passenger on the step is carried at walking pace until they are in (at the door speed
itself a fall was an injury, Tk 2,000 and the crowd, and one such day cost Tk 778).

Instruments added for this: `--crowd X` and `--density X` on single runs and the batch, a contact
log and per-stop takings with `-v`, and every junction's cross and oncoming queue in the report.
