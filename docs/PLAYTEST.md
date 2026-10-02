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
