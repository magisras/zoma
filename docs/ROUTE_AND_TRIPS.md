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

- **Diesel per km: nobody has measured it; the figures are assumptions.** *Corrected 4 Oct: this
  bullet first called the pink-bus figure a measurement. The article's sentence is conditional:*
  একটি গোলাপি বাস **যদি** এক লিটার ডিজেলে ২ থেকে আড়াই কিলোমিটার চলে — "**if** a pink bus does 2 to
  2.5 km on a litre", then 16.8–21 litres for a 42 km Mirpur–Motijheel round trip (Amar Sangbad,
  Citizens Voice). The range in what can be found:

  | Source | km/l | L per 100 km |
  |---|---|---|
  | pink-bus article (its "if") | 2–2.5 | 40–50 |
  | general city-bus figures, stop-and-go (secondary sources) | 2–4 | 25–50 |
  | a Dhaka transport-energy study (its assumption) | 5 | 20 |
  | operators, per `docs/BUS.md` (AK1J in Dhaka traffic) | 3–4 | 25–33 |

  A fair working range is **20–50 L/100 km, about 30 in the middle**. At Tk 135 a litre that is
  **Tk 27–68 a km, about Tk 40**; a 15 km trip about **Tk 600 (400–1,000) on diesel**, about a third
  of that on CNG.
- **So a diesel bus cannot run 90–120 km a day on these takings, at any figure in the range.**
  100 km is 20–50 litres, Tk 2,700–6,750, on top of a deposit of Tk 3,000–5,000, against takings of
  Tk 4,000–5,500. Even the most generous figure leaves nothing. The builder is right that something
  gives.
- **CNG: how many is unknown.** The often-quoted "৯৫ শতাংশ বাস সিএনজিতে চলে" (95 % of Dhaka's
  buses run on CNG) is, per NewsBangla24's check, an estimate nobody can source; BRTA officials put
  CNG at 1–2 % of buses nationally. One snippet has Dhaka at 11,900 gas and 626 diesel of 12,526
  buses (unconfirmed, and it contradicts BRTA). Daily Sangram reports CNG buses **posing as diesel**
  (gas kit hidden, a fake diesel filler) to charge the diesel fare. So both kinds run, in a share no
  one has counted, and the fare is set as if all were diesel.
- **The ration.** During the 2026 shortage BPC capped a local bus at **70–80 litres of diesel a day**
  (ITV, Prothom Alo): 140–200 km at 2–2.5 km/l. A cap, not a typical day.

What I'd take from it: on diesel a city bus can only afford a short day, and the crews who do more
are on gas. **Against the game:** `FuelTkPerTrip` is Tk 300 for 15 km, Tk 20 a km. On diesel that
needs 6.7 km/l, better than every figure above; it is a CNG price. A diesel trip at the middle of
the range is about Tk 600. Which bus the crew drives is the owner's choice for them; the number
should say which. All of this is unconfirmed: no source measures a Dhaka bus.

On the builder's notes 1 and 2: 1 is right and the row above is corrected. 2, "demand compression,
not time compression": agreed. The sandbox drives a real quarter-hour; it is the crowds that are
compressed, so charging a lap a trip's share is coherent and a faster clock would be the wrong fix.

## The pack (4 Oct 2026: owner's correction, and a ride-along video)

The owner, after the batch tables in `docs/PLAYTEST.md`: *buses do not wait for passengers; you cannot
stop and fish. They go out in batches of three or more on the same route, and the whole competition
is to be first at the stop.* The ride-along video below says the same from inside the bus. Everything
in the builder's "being first adds 18 %" reading was wrong because the sandbox let a bus dwell 40 s
and fish; this section replaces that model.

### What the video shows (Sayedabad, Dhaka; one ride; single source, the guide's claims unconfirmed)

Video: [Why Bangladesh's buses race each other](https://youtu.be/MoyKNBRvONs), a ride from Sayedabad
with a local guide, transcript supplied by the owner. Facts, as the narrator and the guide state them:

- **Release.** "There's no spacing, there's no timetable … whenever they let them go, it's two or
  three buses rolling out together." Dispatch is random in time but the buses leave as a pack.
- **First takes all.** "At every stop, whoever arrives first is going to take everything. Not most
  of the passengers, all of them. The second bus maybe gets some scraps. The third bus gets nothing."
- **Pay.** No normal wage; "the team gets a cut of the takings, maybe a tiny base pay if you're
  lucky." The crew pays the owner and the fuel; "if the bus is empty, you don't earn less, you earn
  nothing." Matches the deposit system in `RESEARCH.md`.
- **Crew.** Three: the driver, the helper hanging out of the door shouting the route and dragging
  people in, and a third who floats and collects. They sleep in the bus at night; the guide says
  24-hour shifts. The driver in the video has held a licence 10 years and started driving at nine.
- **How you pass.** "The opportunity to pass really is when you're trying to pick up a customer.
  Once you're on the road you've really got no shot, they'll just cut you off." The pass in the
  video used a car as a screen between the two buses. On the road the leader swerves across to
  block: "that bus is not going to let us pass."
- **Contact.** A smack into another bus at a stop is "almost normal". Every bus at the terminal is
  dented end to end.
- **Rollover, intercity.** 20–22 aboard, driver sleepy, warned twice by passengers, hit the divider
  within five minutes, bus on its side; a broken hand, nobody dead; the driver ran; locals winched
  the bus upright and traffic moved on. "Just another Tuesday."

A second video (a day with Sujan, a Dhaka city-bus driver of 8–9 years, [youtu.be/JgRubquYnBU](https://youtu.be/JgRubquYnBU);
transcript from the owner, English narration only, Sujan's own Bangla answers not in it): the bus is
parked and locked at the kerb overnight, the day opens with checks and the attendant sweeping; one
stop for fuel and one for lunch in the whole day, "more traffic means a longer trip means lower
collection"; the attendant announces stops, does the ticketing, and **takes the wheel when the driver
has been driving for several hours**; drivers say the traffic police give them a hard time; the
depot is pre-1971 and buses spend a lot of time in it for cracked glass and scraped bodies. Thin on
the race, but the swap at the wheel and the one-fuel-one-lunch day are usable.

The owner then supplied the audio and the Bangla spans were machine-transcribed (`tools/transcribe/`,
Whisper large-v3; the Bangla came out only as rough English machine translation under music, so
nothing below is a quote and all of it is unconfirmed). What Sujan and the others are talking about:

- **The morning check is for the owner, not the bus.** "If the bus is damaged the owner will make
  trouble: why did you take a damaged bus out, why on that road"; damage found later is blamed on the
  driver. Matches `RESEARCH.md`: damage comes out of the crew.
- **The day.** The route is named as Azimpur; out at 6 in the morning and back at 1 at night (one
  reading of a garbled passage; a 19-hour day would fit the research's 12–17 and the video's
  "disciplined to his schedule"). The one fuel stop is "gas, oil and water": a CNG bus, which bears on
  the tester's fuel question above.
- **Police.** "A police post at every point of our road … at every point we get [stopped] … they give
  cases"; a phrase the model renders as "the land men getting paid" is most likely লাইনম্যান, the
  lineman. Sergeants, cases and the line, in the driver's own account.
- **On aggression.** "We are all brothers here … under a lot of pressure"; the rest is lost.
- **The depot lineman.** Buses are in bad shape, riders went to the metro, "if new buses come the
  people come back".
- **Commuters.** Students late for school, jobs at risk, "people cannot drive properly". A resident on
  the footbridges: people cross through traffic instead; "that is where the accidents come from".

A third video, a Kalbela TV report ([youtu.be/VFHF9mQhzYk](https://youtu.be/VFHF9mQhzYk), Bangla captions
from the owner, so these are real quotes): driver Monir Hossain's viral complaint and the reactions.
Nothing on the pack; what it gives is the crew's standing and the half-fare fight.

- স্টুডেন্টরা আমাদের গাড়িতে ২০ বছর যাবৎ হাফ ভাড়া দিয়ে যায়। তারা যখন প্রতিষ্ঠিত হয়, ডাক্তার হয়,
  ইঞ্জিনিয়ার হয়, ব্যারিস্টার হয়, তখন তো তারা আমাদের জন্য আর হাফ করে না — "students have ridden our
  buses at half fare for twenty years; when they are made, doctors, engineers, barristers, they do not
  do half for us" (Monir Hossain, driver).
- কোন নেতা না, কোন এমপি না, কোন মন্ত্রী না … পরিবহনে ড্রাইভার স্টাফদের কোন মূল্য নাই — "no leader, no
  MP, no minister … transport drivers and staff have no value" (a driver). Another: taken to Dhaka
  Medical with a Tk 10 ticket and told to wait outside; no healthcare, social security or pension.
- সবাই সব কর্মজীবী ছুটি পায়। ওইদিনও আমাদের কর্ম করে চলতে হয় — "every worker gets the (Eid) holiday;
  that day too we have to work" (a driver). No days off.
- রোড লঙ্ঘন করলে মামলাটা হইতো, ওটা সর্বোচ্চ ৫০ টাকা আছিল — "a traffic violation got a case, and that
  was at most Tk 50" (an owner, on how cases used to be priced; today's are in the thousands, so the
  case amounts in the game are the new regime, not an old one). No owner "can put a hand on his chest
  and say he is surviving."
- বাসের মধ্যে পাঁচ-দশ টাকার জন্য ঝগড়া লাগে … এক হাতে তালি কখনো বাজে না — "fights break out in the bus
  over five or ten taka … one hand never claps" (a student); সব বাসে কিন্তু স্টুডেন্টরা আসলে হাফ ভাড়া
  পাচ্ছে না — "not on every bus do students actually get the half fare".

For the game: the conductor's half-fare argument is a daily, political fight, not a gag; the crew's
between-shift scenes (illness, a hospital that makes them wait, no holiday) are from life; and the
owners remember Tk 50 cases, which is why a Tk 3,000 case feels like the state's doing to them.

A News Bangla piece found on the way ("the bus runs on the driver's daily lease",
[newsbangla24](https://www.newsbangla24.com/news/166932/), search snippet only, blocked from here):
driver and helper were paid Tk 300 a trip under the old per-trip system, Tk 1,500–2,000 on a good
day and nothing on a bad one; owners set daily targets of Tk 1,500–3,000 by route. Unconfirmed.

A fourth ([Inside the Life of a Bus Driver in Bangladesh](https://youtu.be/zIn31_xYoOM), DreamersEye,
English narration) is an **intercity** day, Rajshahi to Lalmonirhat and back, about 500 km, not Dhaka
city. Useful only as the comparator: driver Raju is on a fixed daily wage from the owner, about
US$ 12 (Tk 1,400–1,500) handed over by the supervisor at night, five days a week with two days off;
counter tickets, a crew of four (driver, a supervisor who checks tickets, two helpers); the limit is
80 km/h and he runs over 100 on a clear road; the hardest part of the day is the afternoon, "when he
gets sleepy and his eyelids grow heavy". So a long-distance driver earns roughly what a Dhaka crew
clears on a good day, with a wage, days off and no race: the city deposit system is the exception,
not the trade.

The France 24 clip ([youtu.be/SeQFgkNl7Ms](https://youtu.be/SeQFgkNl7Ms), April 2026) is one minute
of viral highway-race footage with a vlogger's comment: "when there are a lot of people on the bus the
driver tries to entertain the passengers by driving very fast"; a helper's pitch "get on, it's half
price" (companies competing on fare and journey time); buses in 12 % of the country's road accidents.
Nothing on dispatch. It confirms only that passengers egg the driver on, which `RESEARCH.md` has.

Independent of the video: the road transport minister names the same mechanism when announcing
the one-company-per-route reform, একই রুটে বিচ্ছিন্নভাবে একাধিক মালিকের বাস আর চলবে না … যাত্রী
তোলার প্রতিযোগিতা — "buses of several owners will no longer run separately on one route … the
competition to pick up passengers" ([Khobor Sangjog](https://www.khoborsangjog.com/capital/114956/),
[Bangla Tribune](https://www.banglatribune.com/others/capital-city/890873/)); and a Prothom Alo
op-ed, [Bus races, where lives are lost](https://en.prothomalo.com/opinion/op-ed/5l1t0dtpg7). The
pack release itself (two or three at once, no interval) has only the video and English rewrites
of it ([TBS](https://www.tbsnews.net/features/mad-bus-races-youtube-views-304855),
[bdnews24](https://bdnews24.com/bangladesh/a01a0cb5c7f7)); unconfirmed in Bangla. A company
time-checker fining early or late arrival was searched for (বাস টাইম চেকার জরিমানা) and not found;
the waybill checks that logged times and counts were scrapped in August 2022
([Dhaka Tribune Bangla](https://bangla.dhakatribune.com/bangladesh/2022/08/10/16601528202533)),
which leaves nothing holding the buses apart.

What the BUET waiting study (`RESEARCH.md`, "the full/empty dial": drivers wait to fill, 70 % of
it near the origin) means under this reading: the waiting is at the terminal and the first stands,
before and as the pack forms, not on the road with a pack behind you. Unconfirmed; the owner reads
it the same way.

### Who is in the pack: same banner, different owners

A Dhaka bus "company" is the route-permit holder and the livery. The buses under it belong to many
small owners; each pays the company a daily gate pass to run under the banner: কোম্পানির অধীন বাস
চালাতে দৈনিক ওয়েবিল বা গেটপাস (জিপি) চাঁদা দিতে হয় — "to run a bus under a company one pays a daily
waybill or gate-pass (GP) fee" ([Kaler Alo](https://kaleralo.com/385863/2026/); the Tk 5–15 thousand
figure there is for long-distance buses). The minister's reform text says the same of the problem:
একই রুটে বিচ্ছিন্নভাবে একাধিক মালিকের বাস — "several owners' buses, each on its own, on one route"
([Khobor Sangjog](https://www.khoborsangjog.com/capital/114956/)). So the three buses leaving together
wear the same name and are three businesses: three owners' deposits, three crews on commission. The
company earns its GP and the owners their deposit whichever bus wins; only the crews are in the race.
Buses of other companies share stretches of the road and take a slice as well, but the bus that
turns your day to nothing is the one in your own colours. In the game that is the two named crews of
the player's company (`RESEARCH.md`, "own company"), plus other companies' buses as passing traffic.

### The math that holds, passengers only

No fines, no scrapes. Three buses of one company leave Sayedabad together. A stop's crowd is the
minutes of people who gathered since the last bus of this route loaded there. At 2–6 s a head
(`RESEARCH.md`, boarding time), a bus takes about one person per 4 s of door time.

The rule: **what you can load at a stop is bounded by your lead over the bus behind you.** Load
longer than that and it draws level and passes you at the door, and from then on it is the one
loading. So a bus 20 s ahead can take about 5 people per stop; 60 s ahead, 15, which is a whole
stand; a bus 0 s ahead takes the one person the helper can grab on the roll.

| | lead over the bus behind | takes per stand (crowd 15) | takes per roadside wave (crowd 2) | riders a trip, 7 stands and 23 waves |
|---|---|---|---|---|
| first out of the pack, 30 s ahead | 30 s | 7–8 | 2 | about 100 (capped by room, then turnover in the core) |
| second, right behind | a few seconds | 1–2 | 0–1 | 20–30 |
| third | nothing to take | 0–1 | 0 | 5–10 |

Everyone's income is a function of one number, seconds of lead, and the pack's total is fixed by
the headway in front of the pack, so every second gained is a second taken from a colleague of the
same company. That is the engine, and it does not need accident arithmetic:

- A calm driver in a pack is third by construction and goes home with nothing. Not because of a
  fine, because of where the bus is.
- Nobody can wait at a stop: a 30 s dwell is 30 s of lead given away at the next stop.
- A pass is worth minutes and is only possible while the leader is loading, so the fight happens
  at the stops, among the people boarding; on the road the leader blocks.
- Blocking is as good as passing and costs nothing: hold the middle, let nobody by.
- Other companies on the same road take a slice of every headway, which is why the pack's pie is
  smaller than the route's demand.

Open numbers for the owner: how often a pack is released (the headway in front of the pack sets
the pie); how many stops a pass or a block typically swaps the order; whether a company ever fines
a crew for lagging, and if so how much.

**Against the sandbox.** The careful autopilot dwells up to 40 s and fishes; the Dhaka one leaves
when the kerb is bare. Under the pack model neither is right: the dwell is set by the gap behind,
and the rivals should leave the stand with the player, not 60–120 s apart. The headless report
should print seconds of lead and share of stops taken first; those two columns replace the scrape
and fine arithmetic as the verdict. Next build, after the getting-off design (report 1131).

## Sources

- [Amar Sangbad: the pink bus does not even cover its fuel ("if" 2–2.5 km/l, 16.8–21 l a trip)](https://www.amarsangbad.com/bangladesh/news/359731)
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
