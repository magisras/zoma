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

## After the owner drove it: no door, a bus that does not tip (3 Oct 2026, 6 seeds × 900 s)

Two corrections from the owner at the wheel. The bus rolled far too easily (one frame of lateral
acceleration above 6 m/s² at 29 km/h): now the tyres hold 0.7 g and scrub past it, and an untripped
rollover takes 0.8 s above 6.5 m/s² at 36 km/h or more (`docs/BUS.md` §5). And the bus has no door:
people get on at any bus at a crawl, get off at a crawl or, if the bus is about to carry them past,
are forced off at up to the jump speed with the fall rules answering (`docs/BUS.md` §6). The first
doorless batch scooped people onto the step at 20 km/h and dropped them, and dropped riders off
careful buses at speed too; four days ended with the second injury. Boarding is a crawl-only thing,
alighting at speed only in the last metres of a place.

### `./tools/headless.sh --batch 6 900`

| seed | policy | net Tk | fares | km | scrapes | stopsLost | caneRuns | hit |
|---|---|---|---|---|---|---|---|---|
| 1 | Careful | 547 | 905 | 1.42 | 2 | 2 | 0 | 0 |
| 1 | Dhaka | 566 | 980 | 1.97 | 8 | 3 | 2 | 0 |
| 2 | Careful | 401 | 785 | 1.96 | 7 | 3 | 0 | 0 |
| 2 | Dhaka | 508 | 935 | 1.80 | 4 | 1 | 1 | 0 |
| 3 | Careful | 114 | 775 | 1.64 | 6 | 5 | 2 | 0 |
| 3 | Dhaka | 378 | 815 | 1.80 | 3 | 4 | 1 | 0 |
| 4 | Careful | 399 | 805 | 1.50 | 3 | 3 | 1 | 0 |
| 4 | Dhaka | 375 | 795 | 1.60 | 1 | 3 | 0 | 0 |
| 5 | Careful | 455 | 820 | 1.61 | 4 | 3 | 0 | 0 |
| 5 | Dhaka | 640 | 1070 | 2.00 | 2 | 2 | 1 | 0 |
| 6 | Careful | 251 | 655 | 1.59 | 3 | 2 | 1 | 0 |
| 6 | Dhaka | 583 | 1025 | 1.91 | 5 | 0 | 1 | 0 |

```
careful  mean net Tk    361  fares   791  km 1.62  scrapes  4.2  nearMiss  1.5  stopsLost  3.0  wrongSide    0s  people hit 0/6
dhaka    mean net Tk    508  fares   937  km 1.85  scrapes  3.8  nearMiss  2.8  stopsLost  2.2  wrongSide    0s  people hit 0/6
VERDICT: the trap holds. Dhaka driving nets Tk 147 more per day than careful driving.
```

### `./tools/headless.sh --batch 6 900 0.5`

| seed | policy | net Tk | fares | km | scrapes | stopsLost | caneRuns | hit |
|---|---|---|---|---|---|---|---|---|
| 1 | Careful | 316 | 695 | 1.83 | 3 | 3 | 0 | 0 |
| 1 | Dhaka | 43 | 435 | 2.17 | 4 | 4 | 0 | 0 |
| 2 | Careful | 9 | 405 | 2.28 | 5 | 3 | 0 | 0 |
| 2 | Dhaka | 74 | 485 | 2.59 | 5 | 3 | 1 | 0 |
| 3 | Careful | -36 | 425 | 2.16 | 3 | 3 | 2 | 0 |
| 3 | Dhaka | 111 | 525 | 1.98 | 4 | 2 | 1 | 0 |
| 4 | Careful | 287 | 665 | 1.81 | 1 | 2 | 0 | 0 |
| 4 | Dhaka | 141 | 535 | 1.97 | 3 | 3 | 0 | 0 |
| 5 | Careful | 203 | 580 | 1.78 | 3 | 2 | 0 | 0 |
| 5 | Dhaka | 81 | 580 | 2.55 | 6 | 1 | 2 | 0 |
| 6 | Careful | 225 | 630 | 1.73 | 2 | 2 | 1 | 0 |
| 6 | Dhaka | 66 | 455 | 2.09 | 5 | 3 | 1 | 0 |

```
careful  mean net Tk    168  fares   567  km 1.93  scrapes  2.8  nearMiss  1.0  stopsLost  2.5  wrongSide    0s  people hit 0/6
dhaka    mean net Tk     86  fares   503  km 2.23  scrapes  4.5  nearMiss  2.8  stopsLost  2.7  wrongSide    0s  people hit 0/6
VERDICT: polite driving still wins (careful net >= Dhaka net). RESEARCH says: rivals too timid, or the street too kind.
```

**At full crowds the acceptance is met: Dhaka ahead on 5 of 6 seeds, Tk +147 a day, nobody hit, no
rollovers.** At half crowds it is not: careful ahead on 4 of 6, by Tk 82 a day. The doorless bus
widened the gap in the rich market (the Dhaka driver's rolling pickups now load without a stop) and
narrowed it in the thin one (the careful bus, stopping for anyone who steps on, trails and finds
the fuller kerb). The next lever is the owner's approved getting-off design (`2026-10-03_1131.md`):
fares collected in the ride and settled at the door, riders carried past paying short, which
changes what each stop is worth.

## After the owner's street notes (3 Oct 2026, 6 seeds × 900 s)

People run from a bus bearing down and from the horn; a nose hit under 29 km/h knocks down instead of
killing; nose to tail the two share momentum by mass (a bus shoves a rickshaw along, a truck slows the
bus). `docs/BUS.md` §7. Batch at crowd rate 1.0: careful +469, Dhaka +539 (+70 a day, ahead on 5 of
6); at 0.5: careful +161, Dhaka +154. Nobody knocked down, nobody hit, no rollovers in 24 days: the
autopilots never needed the new pedestrian rules; a human at the wheel at 30 km/h does. The absolute
premise (a careful crew cannot eat) is still not in these numbers: that is the economics decision in
report 1639.

## The day is a trip (4 Oct 2026, owner's decisions 1 and 2)

One sandbox day is now charged like one real trip of six (`docs/ROUTE_AND_TRIPS.md`, Builder's
notes): the deposit, the helper's and conductor's wages and the crew's food at a sixth of the day
(Tk 500, 133, 33); fuel for the real kilometres a lap stands for (`TripKm` 15 over the ring's 1.6 km,
Tk 300 a trip); fares by the real kilometres a ride stands for; and everything that happens per event
at its real taka: a sergeant Tk 300, a camera SMS Tk 2,000, a case Tk 3,000, a knock-down Tk 3,000,
a scrape Tk 100, the lineman Tk 50 and the party man Tk 30 a trip. Crowds at 2 a minute (3 filled
every kerb to the cap and a lap boarded 90 against a real trip's 60–70). The batch verdict is the
eat line: the household's food is Tk 50 a day-share; careful must not clear it, Dhaka must.

### `./tools/headless.sh --batch 6 900` (crowds 2 a minute)

| seed | policy | net Tk | fares | km | scrapes | caneRuns | down | hit |
|---|---|---|---|---|---|---|---|---|
| 1 | Careful | 211 | 1295 | 1.79 | 3 | 0 | 0 | 0 |
| 1 | Dhaka | 131 | 1489 | 2.55 | 7 | 1 | 0 | 0 |
| 2 | Careful | 395 | 1396 | 1.61 | 4 | 1 | 0 | 0 |
| 2 | Dhaka | 399 | 1490 | 1.82 | 3 | 1 | 0 | 0 |
| 3 | Careful | 432 | 1404 | 1.46 | 0 | 2 | 0 | 0 |
| 3 | Dhaka | -2119 | 1284 | 1.89 | 4 | 0 | 0 | 0 |
| 4 | Careful | -21 | 1081 | 1.88 | 4 | 0 | 0 | 0 |
| 4 | Dhaka | 546 | 1768 | 1.98 | 9 | 2 | 0 | 0 |
| 5 | Careful | -265 | 1270 | 1.79 | 6 | 1 | 0 | 0 |
| 5 | Dhaka | -25 | 1301 | 2.38 | 9 | 1 | 0 | 0 |
| 6 | Careful | -181 | 921 | 1.61 | 6 | 1 | 0 | 0 |
| 6 | Dhaka | -233 | 1349 | 2.31 | 8 | 2 | 0 | 0 |

```
careful  mean net Tk     95  fares  1228  km 1.69  scrapes  3.8  nearMiss  1.3  stopsLost  2.7  wrongSide    0s  knocked down 0  people hit 0/6
dhaka    mean net Tk   -217  fares  1447  km 2.16  scrapes  6.7  nearMiss  2.5  stopsLost  3.2  wrongSide    3s  knocked down 0  people hit 0/6
eat line: Tk 50 a day (the household's food). careful EATS (95), Dhaka CANNOT EAT (-217).
VERDICT: polite driving still wins (careful net >= Dhaka net). RESEARCH says: rivals too timid, or the street too kind.
```

### `--batch 6 900 0.5` (1 a minute)

| seed | policy | net Tk | fares | km | scrapes | caneRuns | down | hit |
|---|---|---|---|---|---|---|---|---|
| 1 | Careful | -605 | 651 | 2.54 | 3 | 0 | 0 | 0 |
| 1 | Dhaka | -916 | 643 | 3.09 | 9 | 1 | 0 | 0 |
| 2 | Careful | -353 | 795 | 2.13 | 2 | 0 | 0 | 0 |
| 2 | Dhaka | -468 | 913 | 3.20 | 1 | 2 | 0 | 0 |
| 3 | Careful | -952 | 638 | 2.72 | 4 | 0 | 0 | 0 |
| 3 | Dhaka | -1000 | 611 | 2.83 | 7 | 3 | 0 | 0 |
| 4 | Careful | -491 | 648 | 2.08 | 1 | 0 | 0 | 0 |
| 4 | Dhaka | -812 | 485 | 2.76 | 2 | 1 | 0 | 0 |
| 5 | Careful | -515 | 760 | 2.64 | 6 | 0 | 0 | 0 |
| 5 | Dhaka | -543 | 713 | 2.54 | 3 | 0 | 0 | 0 |
| 6 | Careful | -502 | 652 | 2.16 | 3 | 0 | 0 | 0 |
| 6 | Dhaka | -512 | 757 | 2.61 | 6 | 1 | 0 | 0 |

```
careful  mean net Tk   -570  fares   691  km 2.38  scrapes  3.2  nearMiss  0.7  stopsLost  2.2  wrongSide    0s  knocked down 0  people hit 0/6
dhaka    mean net Tk   -709  fares   687  km 2.84  scrapes  4.7  nearMiss  3.5  stopsLost  2.8  wrongSide    0s  knocked down 0  people hit 0/6
eat line: Tk 50 a day (the household's food). careful cannot eat (-570), Dhaka CANNOT EAT (-709).
VERDICT: nobody eats. The street is too hard for both; the costs or the crowds are off.
```

### `--batch 6 900 1.5` (3 a minute, for reference)

| seed | policy | net Tk | fares | km | scrapes | caneRuns | down | hit |
|---|---|---|---|---|---|---|---|---|
| 1 | Careful | 475 | 1477 | 1.62 | 1 | 1 | 0 | 0 |
| 1 | Dhaka | -106 | 1711 | 2.34 | 5 | 1 | 0 | 0 |
| 2 | Careful | 519 | 1762 | 1.30 | 6 | 2 | 0 | 0 |
| 2 | Dhaka | 705 | 1825 | 1.98 | 5 | 0 | 0 | 0 |
| 3 | Careful | 552 | 1553 | 1.61 | 5 | 1 | 0 | 0 |
| 3 | Dhaka | 431 | 1815 | 1.79 | 4 | 2 | 0 | 0 |
| 4 | Careful | -222 | 1217 | 1.55 | 1 | 0 | 0 | 0 |
| 4 | Dhaka | 507 | 1593 | 1.80 | 3 | 0 | 0 | 0 |
| 5 | Careful | 665 | 1665 | 1.61 | 4 | 0 | 0 | 0 |
| 5 | Dhaka | 671 | 1756 | 1.79 | 5 | 1 | 0 | 0 |
| 6 | Careful | 645 | 1646 | 1.61 | 3 | 0 | 0 | 0 |
| 6 | Dhaka | 203 | 1482 | 2.29 | 7 | 2 | 0 | 0 |

```
careful  mean net Tk    439  fares  1553  km 1.55  scrapes  3.3  nearMiss  0.5  stopsLost  3.2  wrongSide    0s  knocked down 0  people hit 0/6
dhaka    mean net Tk    402  fares  1697  km 2.00  scrapes  4.8  nearMiss  2.3  stopsLost  3.3  wrongSide    0s  knocked down 0  people hit 0/6
eat line: Tk 50 a day (the household's food). careful EATS (439), Dhaka eats (402).
VERDICT: polite driving still wins (careful net >= Dhaka net). RESEARCH says: rivals too timid, or the street too kind.
```

**Reading.** With real costs the margins are real: a careful lap clears about Tk 1,230 in fares against
Tk 1,130 of costs and keeps Tk 95, Tk 45 over the household's food. The Dhaka lap collects 18 % more
fares (1,447), burns 28 % more fuel, pays the sergeant more often, and on seed 3 took the wrong side
under a camera: an SMS to the owner for Tk 2,000, the whole trip gone (−2,119). Without that day the
Dhaka mean is about +160; with it, −217. That is the premise with its teeth in: the racing crew eats
when it gets away with it and loses a day's food in one text message when it does not. The careful
crew eats, barely. At half crowds nobody eats (careful −570, Dhaka −709): the post-metro street,
where ticket sales fell a third and the deposit did not. At 3 a minute everyone eats.

**Not met as written**: careful clears the eat line by Tk 45 at normal crowds. The honest remaining
levers are small and all within the research's ranges: the household's food (Tk 300 a day is low for
a family in Dhaka; 400–500 puts the line at Tk 67–83 and the careful crew under it), the crew's wages
(Tk 300–500 each; 800 is the low end), fuel per trip (250–333). None of them is a rule change. The
owner decides; this file records the batch as it is.

## The pack (4 Oct 2026, owner's correction, PROGRESS step 26)

The owner, after the trip tables above: buses never wait for passengers; they leave the terminal as a
pack of three on one route, and the whole competition is to be first at each stop (`docs/ROUTE_AND_TRIPS.md`,
"The pack", with the Sayedabad ride-along). Built: the three buses leave the stand together with three
minutes of crowd already on every kerb; nobody fishes; the first door takes the whole kerb (boarding
1.5–4 s a head); a crew bus passes a bus that is loading instead of queuing behind it, leaves a kerb with
two or fewer on it when a route bus closes from behind, and blocks an overtaking crew bus as a matter of
trade; a full bus still stops to let people off. The batch now prints riders, first-door share and the
lead over the nearest route bus behind, for the player and for each crew bus.

### `./tools/headless.sh --batch 6 900`

| seed | policy | net Tk | fares | riders | first | lead m | each crew bus's fares | km | scrapes | stopsLost |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Careful | 308 | 1403 | 90 | 4/7 | 352 | 1652 | 1.84 | 3 | 2 |
| 1 | Dhaka | -15 | 1377 | 90 | 2/10 | 400 | 2196 | 2.73 | 3 | 1 |
| 2 | Careful | 319 | 1421 | 92 | 4/7 | 243 | 1569 | 1.88 | 5 | 3 |
| 2 | Dhaka | -984 | 566 | 35 | 2/10 | 184 | 1708 | 3.31 | 10 | 11 |
| 3 | Careful | 295 | 1463 | 85 | 3/5 | 363 | 1719 | 1.70 | 3 | 4 |
| 3 | Dhaka | -730 | 511 | 35 | 3/8 | 303 | 2489 | 2.46 | 6 | 8 |
| 4 | Careful | 77 | 1631 | 108 | 6/8 | 302 | 1539 | 1.89 | 2 | 2 |
| 4 | Dhaka | -761 | 1309 | 76 | 5/7 | 348 | 1502 | 2.24 | 2 | 1 |
| 5 | Careful | -553 | 675 | 42 | 5/6 | 400 | 2075 | 2.39 | 4 | 3 |
| 5 | Dhaka | -541 | 615 | 35 | 3/7 | 292 | 1716 | 2.17 | 3 | 4 |
| 6 | Careful | -187 | 977 | 63 | 3/8 | 400 | 1769 | 2.21 | 2 | 2 |
| 6 | Dhaka | -872 | 591 | 37 | 0/9 | 357 | 2104 | 3.10 | 8 | 10 |

```
careful  mean net Tk     43  fares  1262  riders   80 (each crew bus  107)  first  61 %  lead  343 m  scrapes 3.2  stopsLost 2.7
dhaka    mean net Tk   -650  fares   828  riders   51 (each crew bus  123)  first  29 %  lead  314 m  scrapes 5.3  stopsLost 5.8
the pack: careful boarded 80 against each crew bus's 107; Dhaka boarded 51 against 123.
          careful is third, but Dhaka does not lead its pack either.
```

What it says. The careful bus is third in its pack in every run of the day (80 riders against the crew
buses' 107; the same in four earlier passes of the batch while the autopilots were being fixed): it pulls in
behind a loading bus and waits, it never passes at the door and never blocks, and the two crews take the
kerbs. That half of the premise now comes from position, not from a fine: no fines, no cameras, no
knock-downs in any of the twelve days. The Dhaka autopilot does not lead its pack. Watching it (`-v`,
`--watch`): it loses the first stop boxed in behind the leader at the kerb (fixed: it now stops outside a
loader, clear of its band, and creeps out when boxed), then the first junction in a queue the two crews ran
before the cane dropped, and it never gets the lead back on a 1.6 km ring where the kerbs refill in a minute.
A faster cap (50 km/h) doubled its scrapes without a rider more. So the thesis instrument for the Dhaka side
is the weak part now: the premise is about the player's driving, and the autopilot is not that driver. Next
for the instrument: build the Dhaka policy on the crews' own decision layer (pass at the door, hold the
middle, run the cane when the box is clear) so the batch compares like with like. For the owner: the pack is
in the sandbox (artifact): three buses leave the stand together, and the first kerb is Block 11.

### Empty out of the depot, and the first door takes all (5 Oct 2026)

Owner: "we all leave the depot with 0, we just started our shift." The player's 30 starting passengers were the
first physics sandbox's test load; now every bus leaves empty (`StartingPassengers` 0; the rollover tests set
their own load). And the trace above showed the leader leaving 8–14 people on every kerb because of the 40 s
dwell cap, against the video's "the first takes everything": the cap is gone (`RaceDwellSeconds` and the
autopilots' `MaxDwellSeconds` 180; a kerb is worked until bare, or, Dhaka, until a route bus is on the tail).

`./tools/headless.sh 1 900 25 --dhaka --trace`, the first minutes: Rafiq takes Block 11 (14 waiting, +10, 0 left);
you arrive second to 4 and get 0; Jamal takes Market (23, +18, 0 left) and Kazipara (19, +25 in 118 s, 0 left)
while Rafiq arrives second to 0 and 13 and gets 1 and 0; Rafiq takes the Stand (25, +34, 0 left) and Jamal
arrives second to 8 and gets 0; at 11:44 you take Kazipara first (23 waiting, +34) and the two crews behind you
get 0 and 2. The second door gets nothing now, as the video says. `--batch 6 900`:

```
careful  mean net Tk   -589  fares   635  riders   40 (each crew bus  118)  first  53 %  stopsLost 5.0
dhaka    mean net Tk   -476  fares   857  riders   51 (each crew bus   98)  first  48 %  stopsLost 6.2
```

Careful is a distant third now (40 against 118; on seed 1 it boarded 14 all day and was first at no kerb).
The Dhaka autopilot is still not the leader (51 against 98; it won seed 5 with 87 and lost seed 4 with 27),
for the reasons above. The "first %" column counts a kerb as first when no other door took anyone there in
the last minute, which flatters a bus arriving to leftovers; the trace is the honest view.
