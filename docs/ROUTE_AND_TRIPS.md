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
| Gross takings | about Tk 3,000 a day (RESEARCH.md); drivers report Tk 3,000–5,000 left after deposit, fuel and food in good times (RESEARCH.md, Raida driver) | RESEARCH.md |

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

## Sources

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
