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
