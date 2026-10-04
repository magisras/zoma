# How long a real trip is, how many make a day, and why a slow street kills

Research annex to `RESEARCH.md`, 3 Oct 2026, Bangla first (CLAUDE.md). Most Bangla news sites are
blocked from the cloud sessions this was written in; points rest on search-result snippets of the
articles listed, and each says how sure it is.

## The numbers

| What | Figure | How sure |
|---|---|---|
| Mirpur 12 → Azimpur, one way | about 15 km | RESEARCH.md map plan; not re-measured |
| Dhaka bus routes, length | 26 % of routes are 10–15 km; whole range 0.5–66 km | study (English, ResearchGate) |
| Average bus speed in Dhaka | **9.7 km/h** (cycle-rickshaw pace); **5–7 km/h** at peak, under 5 in long jams | DTCA survey via Bonik Barta: ঢাকার সড়কে বাসের গড় গতি সাইকেল-রিকশার সমান, "the average speed of buses on Dhaka's roads equals a bicycle or rickshaw" |
| Pallabi (Mirpur) → Motijheel by bus, before the metro | **2–2.5 hours**; one office worker from Mirpur 10 took **over 3 hours** | Prothom Alo / TBS Bangla on the metro (মেট্রোরেলে … ২৬ থেকে ২৭ মিনিট, the same trip by metro: 26–27 minutes) |
| Round trips a day, Mirpur–Jatrabari | **at most four** (a trip = there and back) | search snippet of a Prothom Alo piece on Dhaka's old buses; unconfirmed |
| Daily deposit | **Tk 3,000–5,000**; a helper: "সাড়ে চার হাজার টাকা জমা" (Tk 4,500) | Ajker Patrika (state minister: দৈনিক ৩–৫ হাজার টাকা জমার চাপেই অশুভ প্রতিযোগিতা, "the pressure of a Tk 3–5 thousand daily deposit drives the deadly competition"); helper quote unconfirmed |
| Takings (ticket sales) | **Tk 6,000–6,500 a day** before the metro, **4,000–5,500** after (owners, 2024, RESEARCH.md); the Tk 3,000–5,000 a driver reports is what was *left* after deposit, fuel and food in good times (RESEARCH.md, Raida driver). *Corrected 4 Oct: this row first said "gross about Tk 3,000", which is a net; the builder caught it.* | RESEARCH.md |

## What a real day looks like, from those numbers

One way Mirpur 12 → Azimpur: 15 km at 6–10 km/h is **1.5–2.5 hours**. A round trip is 3–5 hours.
A 12–17 hour shift (RESEARCH.md) is **3–4 round trips, about 90–120 km**, with one stop roughly
every 500 m (Prothom Alo: 23 pickups in 12 km, `docs/ROLLING_DOOR.md`).

## Slow on average, deadly in bursts

The 9.7 km/h average is mostly time standing still. The danger is in what happens between.

- **The worst record in the world, per bus.** বিশ্বে সবচেয়ে ভয়ংকর বাংলাদেশের বাস — "Bangladesh's
  buses are the most dangerous in the world" (Bonik Barta, from World Bank and WHO road-safety
  data): **287 deaths a year per 10,000 buses**, the highest anywhere; next is Zimbabwe at 217.
- **Who dies: people on foot.** In Dhaka over six and a half years, 1,384 killed on the roads;
  **42.8 % pedestrians** (592), 38.7 % motorcyclists (Road Safety Foundation). Nationally in
  2022–23, 56 % of the dead were pedestrians (search snippet). In 2025, 9,111 killed nationally;
  buses in 14.5 % of the vehicles identified (Jatri Kalyan Samity, via Ajker Patrika).
- **When: night and early morning.** ঢাকায় দুর্ঘটনার হটস্পট পাঁচটি, বেশি হয় রাতে-সকালে — "five
  accident hotspots in Dhaka; most crashes at night and in the morning" (Jatrabari, Demra,
  Mohammadpur, Kuril, Airport Road). From 10 pm to dawn heavy vehicles run at reckless speed on
  empty roads and pedestrians crossing are killed; buses racing for passengers add to it.
- **Why a slow street kills.** The average hides the bursts: a bus that has stood ten minutes
  races to the next crowd the moment a gap opens, to beat the bus behind. Twenty tons at even
  25–40 km/h, among people crossing anywhere and getting off anywhere (`docs/ROLLING_DOOR.md`),
  with worn brakes and a tired driver: the speed that kills is not high, it is sudden. Add the
  door (falls while rolling), the gap between two buses (crushing), and the highways, where the
  same crews drive at 60–80 km/h.

**For the simulation**: the average speed is not the thing to match; the shape is. Long stands,
short violent bursts, the burst aimed at a crowd with people in the road, and the night shift's
empty roads. A day where the bus crawls at a steady 10 km/h would be safe and wrong.

## Against the sandbox

- The sandbox ring is about 1.6 km and a sandbox day is 15 minutes standing for 06:00–20:00. The
  buses drive at real speeds, so a day covers 1.4–2.2 km: **about one sixtieth of a real day's
  distance**, which is the time compression (15 min for 14 h), not a speed error. The ring is a test
  loop, not the route; README milestone 1 is 2–3 km of the real corridor.
- What it costs the game: the "trip" (lineman paid at the stand, the next crowd at the far end, the
  turn back) happens 0–1 times a day, so the rhythm the research describes (race to the end, turn,
  race back, three or four times, each time paying the line) never forms.
- The design question for the owner and builder: real speeds and real distances cannot both fit a
  short play-day. Options: play a trip as a session (1.5–2.5 h of real time is too long for most
  play); compress the standstill (time passes faster while the bus is jammed, the race in between
  plays at real speed); or keep one corridor chunk and treat the rest of the route as off-screen
  time with its costs and takings simulated.

## Builder's notes (4 Oct 2026, from the build session)

Read against the sandbox's numbers. The route, the speeds, the trip count and "slow on average, deadly
in bursts" all hold; the shape is the brief, and the headless report should show it (share of the day
standing, share above 25 km/h: to add). Three things I would not sign as written:

1. **The takings row contradicts `RESEARCH.md`.** "Gross about Tk 3,000 a day" against a deposit of
   Tk 3,000–5,000 cannot be the norm; nobody would drive it. `RESEARCH.md` line 195 has the owners'
   figure: ticket sales Tk 6,000–6,500 a day before the metro, 4,000–5,500 after (2024). The 3,000 is
   a driver's *net* in good times, not gross. Report 1639 finding 3 leans on this row; its conclusion
   survives with the right figures (in the sim fares are 2.6× the deposit; in life about 1.3–1.5×, and
   after fuel and the line a careful day nets about nothing), but the row should be corrected.
2. **It is demand compression, not time compression.** A sandbox day covers 1.4–2.2 km in 15 min:
   6–9 km/h, a real quarter-hour of Dhaka driving, not a day squeezed 56×. What is compressed is the
   crowd: six stands in 1.6 km refilling at 3 a minute fill the bus once per lap, so one lap takes one
   trip's fares (Tk 800–950) on a ninth of a trip's road. That is why charging a lap a trip's share of
   the deposit is coherent, and why fuel, sergeants and junction time per lap are under-represented.
   Framing it as time compression points at the wrong fix (a faster clock).
3. **90–120 km a day does not square with the fuel figure.** At Tk 135 a litre and 3.5 km/l that is
   Tk 3,500–4,600 of diesel a day against gross of 4,000–5,500 and a deposit of 3,000: impossible.
   Either many of these buses run on CNG (a third of the cost per km; a lot of Dhaka's local buses
   do), or the daily fuel figure predates the price rise, or the day is two round trips. `docs/BUS.md`
   §4 assumed diesel and now carries the caveat. Worth a Bangla search: বাস সিএনজি ডিজেল খরচ দিনে.

On the design question in "Against the sandbox": I would take none of the three as written. Play one
trip of the real corridor chunk at real time and real distance, and charge that trip a trip's share
of the day: deposit ÷ trips, the lineman at the turn, the 8–10 payment points along the way, fuel for
the kilometres driven, the crew's food. The round-trip rhythm (race to the end, turn, pay, race back)
then exists inside one session; the ring stays an instrument. Compressing the standstill hides the
fatigue; simulating the rest of the route off-screen explains instead of forcing. Owner's call.

## Tester's reply (4 Oct 2026): fuel, diesel or CNG

On the builder's note 3 ("90–120 km a day does not square with the fuel figure"). Bangla search
(বাস সিএনজি ডিজেল খরচ দিনে and around it):

- **Diesel per trip, a real measurement.** BRTC's women's "pink bus" on Mirpur–Motijheel: 21 km
  each way, 42 km a round trip; এক লিটার ডিজেলে ২ থেকে আড়াই কিলোমিটার চলে, "it does 2 to 2.5 km on
  a litre"; এক ট্রিপে প্রায় ১৬ দশমিক ৮ থেকে ২১ লিটার, "16.8 to 21 litres a trip"; Tk 1,932–2,415 a
  round trip at Tk 115 a litre (Amar Sangbad, Citizens Voice). At today's Tk 135: **Tk 2,270–2,835
  per 42 km round trip, about Tk 54–68 a km**. That is worse than `docs/BUS.md`'s 3.5 km/l.
- **So a diesel bus cannot run 90–120 km a day on these takings.** 100 km is 40–50 litres,
  Tk 5,400–6,750, against takings of Tk 4,000–5,500 and a deposit of Tk 3,000–5,000. The builder is
  right that something gives.
- **CNG: how many is unknown.** The often-quoted "৯৫ শতাংশ বাস সিএনজিতে চলে" (95 % of Dhaka's
  buses run on CNG) is, per NewsBangla24's check, an estimate nobody can source; BRTA officials put
  CNG at 1–2 % of buses nationally. One snippet has Dhaka at 11,900 gas and 626 diesel of 12,526
  buses (unconfirmed, and it contradicts BRTA). Daily Sangram reports CNG buses **posing as diesel**
  (gas kit hidden, a fake diesel filler) to charge the diesel fare. So both kinds run, in a share no
  one has counted, and the fare is set as if all were diesel.
- **The ration.** During the 2026 shortage BPC capped a local bus at **70–80 litres of diesel a day**
  (ITV, Prothom Alo): 140–200 km at 2–2.5 km/l. A cap, not a typical day.

What I'd take from it: on diesel at these figures a city bus can only afford about **two round
trips (80–90 km) or fewer**, and the crews who do more are on gas. For the game, "fuel cost per km"
of about **Tk 55–65 for diesel** (well above `docs/BUS.md`'s figure) and roughly a third of that on
CNG are the two ends; which bus the crew drives is a choice the owner makes for them. Unconfirmed
beyond the pink-bus measurement.

On the builder's notes 1 and 2: 1 is right and the row above is corrected. 2, "demand compression,
not time compression": agreed. The sandbox drives a real quarter-hour; it is the crowds that are
compressed, so charging a lap a trip's share is coherent and a faster clock would be the wrong fix.

## Sources

- [Amar Sangbad: the pink bus does not even cover its fuel (2–2.5 km/l, 16.8–21 l a trip)](https://www.amarsangbad.com/bangladesh/news/359731)
- [Citizens Voice: the women's pink bus loses Tk 50,000 a day](https://citizensvoicebd.com/bangladesh/143592/)
- [NewsBangla24: how many CNG buses are there in Dhaka, really?](https://www.newsbangla24.com/news/165855/How-many-CNG-powered-buses-in-Dhaka)
- [Daily Sangram: CNG buses charge the diesel fare](https://dailysangram.com/post/472706-%E0%A6%B8%E0%A6%BF%E0%A6%8F%E0%A6%A8%E0%A6%9C%E0%A6%BF-%E0%A6%9A%E0%A6%BE%E0%A6%B2%E0%A6%BF%E0%A6%A4-%E0%A6%AC%E0%A6%BE%E0%A6%B8%E0%A6%95%E0%A7%87-%E0%A6%A1%E0%A6%BF%E0%A6%9C%E0%A7%87%E0%A6%B2-%E0%A6%9A%E0%A6%BE%E0%A6%B2%E0%A6%BF%E0%A6%A4-%E0%A6%AC%E0%A6%B2%E0%A7%87-%E0%A6%85%E0%A6%A4%E0%A6%BF%E0%A6%B0%E0%A6%BF%E0%A6%95%E0%A7%8D%E0%A6%A4-%E0%A6%AD%E0%A6%BE%E0%A7%9C%E0%A6%BE-%E0%A6%86%E0%A6%A6%E0%A6%BE%E0%A7%9F-%E0%A6%AA%E0%A7%8D%E0%A6%B0%E0%A6%A4%E0%A6%BF%E0%A6%A6%E0%A6%BF%E0%A6%A8)
- [ITV: how much fuel a motorcycle, car or bus may take a day (the ration)](https://www.itvbd.com/national/260410/)
- [Amader Shomoy: all fares up 27 % for the 5 % of buses on diesel?](https://www.amadershomoy.com/bn/2021/11/08/1507956.asp)
- [Bonik Barta: "বিশ্বে সবচেয়ে ভয়ংকর বাংলাদেশের বাস" (287 deaths per 10,000 buses)](https://www.bonikbarta.com/bangladesh/Upbfai8b7djgWPrq)
- [Prothom Alo: five accident hotspots in Dhaka, most at night and in the morning](https://www.prothomalo.com/bangladesh/capital/lt6e8w5l60)
- [Ittefaq: 1,384 killed on Dhaka's roads in six years](https://www.ittefaq.com.bd/801667/)
- [Ajker Patrika: 9,111 killed on the roads in 2025](https://www.ajkerpatrika.com/national/ajpajuhvwfx4h)
- [Bonik Barta: bus average speed equals a cycle or rickshaw](https://bonikbarta.com/bangladesh/9XX6sEm0N3rKyojc)
- [Prothom Alo: by metro from Uttara to Motijheel in 32 minutes](https://www.prothomalo.com/bangladesh/capital/mqisz47idx)
- [TBS Bangla: the metro eased the Mirpur–Motijheel jam](https://www.tbsnews.net/bangla/%E0%A6%AC%E0%A6%BE%E0%A6%82%E0%A6%B2%E0%A6%BE%E0%A6%A6%E0%A7%87%E0%A6%B6/news-details-194186)
- [Prothom Alo: the old Dhaka buses' new controllers (trips a day)](https://www.prothomalo.com/bangladesh/m17vcpmr96)
- [Prothom Alo: "যত 'ট্রিপ' তত টাকা" (as many trips, as much money)](https://www.prothomalo.com/bangladesh/%E0%A6%AF%E0%A6%A4-%E2%80%98%E0%A6%9F%E0%A7%8D%E0%A6%B0%E0%A6%BF%E0%A6%AA%E2%80%99-%E0%A6%A4%E0%A6%A4-%E0%A6%9F%E0%A6%BE%E0%A6%95%E0%A6%BE)
- [Ajker Patrika: a Tk 3–5 thousand daily deposit drives the competition](https://www.ajkerpatrika.com/national/ajpk37j6ytxt3)
- [ResearchGate: performance evaluation of Dhaka's public transport (route lengths, speeds)](https://www.researchgate.net/publication/340531941_Performance_Evaluation_of_Public_Transportation_System_Analyzing_the_Case_of_Dhaka_Bangladesh)
- [ResearchGate: travel time characteristics of Dhaka buses](https://www.researchgate.net/publication/322073105_A_STUDY_ON_TRAVEL_TIME_CHARACTERISTICS_OF_BUS_SYSTEM_IN_DHAKA_ANALYZING_DRIVERS'_INTENTIONAL_WAITING_TIME_FOR_PASSENGER_COLLECTION)
