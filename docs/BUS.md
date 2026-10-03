# The bus: what runs Mirpur, and how it moves

Research for `BusSettings` in `TuningTable` and for `BusController`. Written 3 Oct 2026. Where a number
is from a source it is cited; where it is an estimate it says so. Replaces the placeholders the
prototype started with.

## 1. What the bus is

- **Chassis.** Dhaka's city buses are a chassis from India or Japan with a body built locally.
  The Hino AK1J is described as the most popular bus in Bangladesh; Tata's LP/LPO 1618 and Ashok
  Leyland's Viking are the others sold here as bare chassis. Minibuses are 8 m Isuzu, Hino or Tata
  chassis with local bodies. Sources: [Hino AK1J spec](https://praanaa.ae/how-many-ejgyrkn/hino-ak1j-bus-specification-8031fe),
  [Hino AK1JRKA brochure](http://www.hino.com.my/site/listing.php?fn=brochure&filename=AK1JRKA.pdf),
  [Tata buses Bangladesh](https://www.tatamotors.com.bd/buses), [Ashok Leyland chassis in Dhaka](https://www.clickbd.com/bangladesh/2820979-ashok-leyland-bus-chassis-super.html),
  [BRT feasibility study](https://www.academia.edu/47633250/A_Preliminary_Feasibility_Study_of_Bus_Rapid_Transit_System_in_the_Context_of_Present_Road_Network_in_Dhaka).
- **Engine.** Hino AK1J: J08C, 7.96 litre inline six diesel, 210 PS (155 kW) at 2,900 rpm, 554 Nm at
  1,500 rpm, design gross weight 14,200 kg, wheelbase 5.2–6.0 m. Tata LP 1618: Cummins 5.6 litre,
  186 hp, 850 Nm, GVW 16,200 kg, G750 gearbox, 380 mm clutch. Ashok Leyland Viking: 5.66 litre,
  200 hp, 700 Nm, GVW 16,200 kg. All diesel, all manual, all front-engined.
  Sources: [Hino AK1J](https://praanaa.ae/how-many-ejgyrkn/hino-ak1j-bus-specification-8031fe),
  [Tata LP 1618](https://www.trucksbuses.com/buses/inter-city/tata-lp-1618/specifications),
  [Ashok Leyland Viking](https://en.wikipedia.org/wiki/Ashok_Leyland_Viking).
- **Fuel.** Diesel. Many Dhaka buses were CNG for years; by 2026 operators across Mirpur, Farmgate,
  Shahbag and the rest declare them diesel (fares are set per kilometre for diesel buses and are not
  raised for gas buses, so the switch is also a fare argument). Diesel: Tk 135 a litre since
  21 September 2026 (from Tk 115). Sources: [Where are the CNG buses](https://bangladeshpost.net/posts/where-are-the-cng-run-buses-72483),
  [diesel Tk 135](https://www.thedailystar.net/news/power-and-energy/news/fuel-prices-hiked-tk-20-litre-4278041),
  [fares Tk 2.70/km](https://www.bssnews.net/news/427466).
- **Body and load.** Local steel bodies, "patched together using sheets of tin". Local buses have
  36 seats, counter buses 52–58; registration allows 10 standing; no bus in Dhaka follows that.
  Sources: [users' experiences](https://www.banglajol.info/index.php/JBIP/article/view/76987/50801),
  [overcrowding levels](https://www.researchgate.net/publication/270889809_Levels_of_Overcrowding_in_Bus_System_of_Dhaka_Bangladesh),
  [unfit buses](https://www.dhakatribune.com/bangladesh/dhaka/318943/plight-of-dhaka-commuters-unfit-buses-create).
  Estimate: a bare AK1J chassis is about 5.5 t; with a heavy local body the empty bus is 11–12 t.
  Ninety people at 65–70 kg add 6 t: the title's twenty tons, two tons over the chassis's design
  weight, on brakes and tyres sized for fourteen.
- **Condition.** Economic life 20 years; buses from 2006 are now past it. Nationwide 41,168 of
  86,338 buses had no renewed fitness certificate in February 2026; one in four buses on Dhaka's
  roads has none. Brakes, steering and wheels are the defects that cause crashes. Sources:
  [DTCA data](https://en.bonikbarta.com/bangladesh/tlAl2xCIvjZo1JDj), [fitness mockery](https://www.thedailystar.net/news/bangladesh/transport/news/vehicle-fitness-mockery-the-cost-life-and-limb-3276396),
  [unfit bus syndicates](https://en.bd-pratidin.com/city/2026/05/16/62747).

## 2. How it moves

- **Acceleration.** Bus standards ask a fully laden 12.5 m bus to reach 50 km/h in 14 s (an
  average of 1.0 m/s²). Measured sudden starts average 2.0 m/s² on asphalt, 2.6 on concrete, peak
  2.8; standing passengers move naturally below 1.0 m/s², feel uncomfortable at 1.5, fall without a
  handrail above 2.0. Sources: [bus acceleration study](https://doi.org/10.3390/s23063125),
  [acceleration limit and bus service](https://www.researchgate.net/publication/344804592_The_impact_of_a_passenger-safety-driven_acceleration_limit_on_the_operation_of_a_bus_service),
  [real-world signals](https://www.researchgate.net/publication/276542145_Characterisation_of_Real-World_Bus_Acceleration_and_Deceleration_Signals).
  In the sim: power-limited, 155 kW; capped at 1.8 m/s² at low speed (a Dhaka driver stabs the
  pedal; the cap is the laden chassis, not comfort). A full bus at 36 km/h gets 1.0 m/s².
- **Braking.** Normal service braking 1.2–3.0 m/s², typically a peak of 1.8 m/s² held for ten
  seconds; the UN R13 cold test asks for about 5 m/s² from the service brake, 2.2–2.5 from the
  secondary; emergency-braking signals start at 6. Air brakes reach peak deceleration about 0.35 s
  after the pedal, and the jerk on release is bigger than on application. Sources:
  [real-world signals](https://www.researchgate.net/publication/276542145_Characterisation_of_Real-World_Bus_Acceleration_and_Deceleration_Signals),
  [R13 explained](https://airbrakecompressor.com/ece-r13-air-brake-regulations/),
  [UN R13-H](https://eur-lex.europa.eu/legal-content/EN/TXT/PDF/?uri=CELEX%3A42023X0401).
  In the sim: 5 m/s² with new brakes, 0.35 s lag to full, and wear that takes up to 60 % of it.
- **Air.** The brakes are air brakes. Air pressure holds while the engine runs and drops when it is
  off; a driver who starts and pulls away at once has no brakes at the first stop. That is the
  account of the Padma river bus. Pumping the pedal in a jam also spends air faster than the
  compressor makes it. Source: [why the brakes did not work](https://www.jagonews24.com/en/national/news/91164).
  In the sim: `AirPressure` 0..1; the day starts at 0.4, the compressor refills in 45 s of running,
  each second of full braking spends 0.04, and braking force scales with pressure. Nothing is shown;
  the pedal goes soft.
- **Speed.** The 2024 speed limit guideline: 40 km/h for buses inside city corporations, 30 on
  other urban roads, 70–80 on highways. Source: [speed limit guideline](https://www.thedailystar.net/news/bangladesh/transport/news/speed-limit-set-all-types-vehicles-3604456).
  The chassis will do 90–100; the sim caps at 80. The limit is for the sergeant, later.
- **Steering and turning.** A bus wheel is about five turns lock to lock for ~35° at the road
  wheel; at a standstill a driver spins one to one and a half turns a second, so the road wheel
  moves 15–20°/s, slower at speed. Estimate. In the sim: 30°/s at rest, halving by 8 m/s.
- **Rolling and drag.** Rolling resistance of a loaded truck tyre is about 1 % of weight
  (0.1 m/s²); add driveline losses and the sim coasts at 0.15 m/s². Air drag at bus size
  (Cd ~0.7, 8.5 m², 15 t) is 0.0002–0.0003 m/s² per (m/s)². Estimates from the usual formulas.
- **Fuel burn.** Operators' figures for an AK1J in Dhaka traffic are 3–4 km per litre; the sim
  uses 3.5, unsourced. At Tk 135 that is Tk 39 a kilometre, against a fare of Tk 2.70 per
  passenger-kilometre: fourteen fares a kilometre to cover the diesel alone.

## 3. What this changes in the game

Twenty tons on fourteen-ton brakes, with no air after a restart and pads worn to half, is a
machine that must be driven a second ahead of the road. The player feels it as: the bus takes a
moment to bite when they brake, longer when they have been pumping it; it gathers speed slowly
with a full load and stops slowly too; and the kerb crowd flinches at a bus that overhangs the
kerb because the driver turned late. None of it is explained; the helper says "brakes are soft,
ostad" once a day, as he does.

Not yet modelled, on purpose: gears (the Unity vehicle physics will carry them), engine braking,
the retarder none of these buses have, and the CNG/diesel choice.

## 4. Fuel: who pays, when, how much (added 3 Oct 2026)

- **Who pays.** Under the daily-deposit system the crew does. The owner is guaranteed the zoma;
  the crew keeps what is left after the zoma, fuel and the roadside payments, and the loss from a
  slow day is theirs. Gross takings in the city are around Tk 3,000 a day before those deductions.
  Owners who run buses "on trips" pay the crew per completed trip instead and fuel the bus
  themselves; the Mirpur buses in this game are the first kind. Sources:
  [why blaming drivers misses the problem](https://www.thedailystar.net/slow-reads/big-picture/news/why-blaming-dhakas-bus-drivers-misses-the-real-problem-4244066),
  [unstable income](https://www.dhakatribune.com/bangladesh/dhaka/319002/dhaka-bus-drivers-plagued-by-unstable-income-lack),
  [trip-to-trip contracts](https://m.theindependentbd.com/arcprint/details/183243/2019-01-15).
- **How much.** Diesel Tk 135 a litre (21 Sep 2026; Tk 115 before, Tk 100 before April). The
  BRTA fare committee's rule of thumb is that every Tk 1 on a litre of diesel adds about one paisa
  per passenger-kilometre to the fare; on a 52-seat basis that implies the committee's bus burns
  roughly 0.35 litres a kilometre, about 2.8 km per litre, which is what operators say of an AK1J in
  Dhaka traffic (3–4 km/l, worse in jams). A 15-trip day on a 3 km route is ~90 km and 30 litres:
  about Tk 4,000 of diesel against Tk 3,000–5,000 of fares before the zoma, which is why the Raida
  driver in RESEARCH.md ends with under Tk 1,000. CNG: Tk 43 a cubic metre at the pump (Tk 38 of
  gas), with a rise to about Tk 65 proposed; a cubic metre does roughly the work of a litre of
  diesel, so a gas bus's fuel bill is a third of a diesel one's, which is why the fare rules treat
  them separately and why so many were converted. Sources:
  [one paisa per taka](https://www.tbsnews.net/bangladesh/transport/brta-proposes-20-paisa-km-fare-hike-intercity-buses-18-paisa-city-buses-1549666),
  [diesel Tk 135](https://www.thedailystar.net/news/power-and-energy/news/fuel-prices-hiked-tk-20-litre-4278041),
  [CNG Tk 43, Tk 65 proposed](https://bdnews24.com/bangladesh/e49fe8fe622c).
- **When.** There is no depot fuelling: the crew buys at a filling station on or near the route, in
  cash from the day's takings, and chooses when. Diesel buses fill before the first trip or when the
  gauge says so; a diesel fill is minutes. CNG buses are different: the tanks hold a few trips'
  worth, a fill takes a quarter of an hour at the dispenser plus the queue, and the stations are
  closed by order every evening (6–9 pm since January 2025; 3–9 pm through Ramadan 2026) to save
  gas for the power plants, so gas buses queue before the closure and again at 9 pm. In this April's
  diesel crunch (import shipments lost to the Iran war, panic buying), queues ran to a thousand
  vehicles at one Dhaka pump, drivers spent four to five hours at CNG stations for a handful of
  trips, and on some corridors a gas bus made a single morning trip after a night in the queue.
  Sources: [CNG stations closed 6–9 pm](https://en.prothomalo.com/bangladesh/tv91ikups8),
  [Ramadan 2026 closure](https://en.prothomalo.com/bangladesh/n4cjgpt2j1),
  [1,011 vehicles at one pump](https://en.prothomalo.com/bangladesh/kn9slg0amt),
  [queues empty Dhaka's roads](https://www.dhakatribune.com/bangladesh/dhaka/416133/fuel-queues-empty-dhaka%E2%80%99s-roads-as-ride-fares),
  [hours-long waits](https://www.france24.com/en/live-news/20260421-bangladesh-fuel-crunch-forces-hours-long-wait-at-the-pump).
- **What the game should do with it.** Today fuel is a line on the ledger charged per metre, which
  nobody sees. The street's version: a tank that empties with distance and idling, a pump on the
  route where a fill costs cash out of the box and minutes out of the day, a queue on a bad day, and
  a bus that stops where it runs dry, with the helper's line. The choice of when to fill is the
  crew's, as it is. Not built yet; see PROGRESS.

## 5. Rollover: how hard it really is to tip a bus (3 Oct 2026)

The owner, after driving the sandbox: it rolled far too easily; a real bus takes much steeper
manoeuvres and goes over only when something extreme happens. The physics agrees.

- **Static stability factor.** A loaded city bus has a track of about 2.0 m and a centre of gravity
  around 1.2–1.5 m up with standing passengers: SSF (half track over CG height) of 0.65–0.85, so the
  body would tip at roughly 0.4–0.5 g *if the tyres held that long*. (NHTSA SSF method; coach and
  transit figures in the 0.6–0.9 range are widely published; the Dhaka load is at the heavy end.)
- **The tyres give first.** Bus tyres on dry tarmac hold about 0.7 g before they scrub. Asked for
  more, the front washes out and the bus runs wide: a plough, not a roll. Wet or dusty tarmac holds
  less, which makes a rollover on flat road *harder*, not easier. Untripped rollovers of buses on
  flat road are rare in every crash database that separates them; nearly all are **tripped**: a kerb
  or ditch catches the outer wheels, a railing, a soft verge, a steep camber, or another vehicle.
- **It takes time.** Twenty tons rolls onto its outer springs before the inner wheels lift; a flick
  of the wheel for a tenth of a second is a lurch, not a rollover. Sustained lateral acceleration at
  speed is what does it, or a trip.

In the sim: the bicycle model now has a grip cap (`TyreGripMs2` 7 m/s²: past it the yaw rate is what
the tyres allow and the bus runs wide); the untripped rollover needs the lateral acceleration above
`RolloverLateralAccelMs2` (6.5, just under the grip) for `RolloverHoldSeconds` (0.8 s) at or above
`RolloverSpeedMs` (10 m/s, 36 km/h); the tripped one is still the kerb or the railing beyond
`OffRoadRolloverMetres` at that speed, which is the Fraser film's set piece and the fatigue meter's
payoff. Full lock at 30 km/h in the sandbox now ploughs and scrubs; full lock held for seconds at
60 km/h still tips. Figures for the SSF are published ranges, not a measurement of a Dhaka bus:
confirm against the real bus when it is modelled in Unity.

## 6. The door that is not there (owner, 3 Oct 2026)

"Have you seen the buses they have? They don't have doors at all, or they are always open so the
passengers just jump in, jump out." Confirmed by the ride-along footage and `docs/ROLLING_DOOR.md`:
the front doorway stands open all day with the helper hanging in it; many local buses have had the
leaves removed. In the sim there is nothing to open or shut: people get on and off whenever the bus
is slow enough (under the jump speed; walking pace for an ordinary boarding), at any bus of any
company that comes to a crowd. "First door" now means the first bus to arrive slow at a kerb. The
E key is the helper's call ("stop here, stop here!") in the helper role and nothing in the driver's
seat. A sitting-service bus with a real door, if one is ever modelled, gets the field back.
