# The rolling door: getting on and off a bus that doesn't stop

Research annex to `RESEARCH.md`, 3 Oct 2026, done in Bangla first (CLAUDE.md). How people get on
and off Dhaka buses that barely stop, where in the road it happens, why it looks so fluent, and how fast the bus is going. Most
Bangla news sites are blocked from the cloud sessions this was written in; points rest on
search-result snippets of the articles listed, and each says how sure it is.

## Why the bus doesn't stop (confirmed)

- **There is often no stop to stop at.** One audit: 41.3 % of authorised stops don't physically
  exist and only about a fifth work as stops (English press summary). Where stops exist, loading
  still happens in the road: স্টপেজ থাকলেও যাত্রী ওঠানামা করা হয় রাস্তা থেকে, "even where there is a
  stop, people get on and off in the road" (Bangla Tribune).
- **Every second stopped is money.** The zoma rewards trips; the bus behind takes the next crowd.
  A full stop also invites the sergeant (stopping in the road is the offence; a rolling bus is
  harder to book) and the bus behind's horn.
- **The driver pulls away early.** "ওস্তাদ, টান দিয়েন না" ("master, don't pull away") is the
  helper's own plea. A woman passenger: নামতে গেলেই চালক বাস টান দিচ্ছিলেন, পরে লাফিয়ে নামি —
  "every time I went to get off, the driver pulled the bus away; in the end I jumped" (Prothom Alo).

## How it is done (confirmed as practice)

- **The door is on the left, at the kerb.** People step off facing forward, **left foot first**.
  The helper calls it: "আগে বাম পা দ্যান, বাম পা দ্যান" — "left foot first, left foot first". The
  explanation given in Bangla explainers (Somoy News, Science Bee): the body keeps the bus's
  forward motion (জড়তা, inertia); stepping down with the foot on the side of travel lets the
  passenger run a step or two with the bus instead of being spun round. Boarding: right foot first,
  grabbing the rail.
- **The helper does the work.** He hangs out of the door gripping the frame, calls the stop to the
  driver (voice and slaps on the body, see `docs/STICKS_AND_SIGNALS.md`), takes a boarding
  passenger's hand or arm and pulls them up while the bus rolls, and pushes or guides alighting
  passengers off. Off and on happen at the same time, through the same door.
- **The driver only slows.** He brakes to a crawl at the crowd, and is already accelerating as the
  last person is on the step; the helper boards last, running and jumping onto the step.
- **Who it fails.** Women, the elderly, children, people with bags: they need the bus to stop and
  often don't get it. Reports of passengers jumping, being pushed, or the bus pulling away
  mid-step are common (Prothom Alo, Newsbangla24).

## Where the bus stops: the kerb, or wherever it is (confirmed, many reports)

The rule says the kerb; the street says wherever the people are.

- **The rule.** DMP road-safety guidance: বাস বে/নির্দিষ্ট স্থান ব্যতীত যত্রতত্র বাস থামিয়ে যাত্রী
  উঠানামা করাবেন না এবং গাড়ী থামানোর ক্ষেত্রে সর্বদা রাস্তার বাম ঘেঁসে থামাবেন — "do not stop to
  load anywhere but a bus bay or set place, and when stopping always keep to the left of the road".
  The Road Transport Act 2018 makes loading outside set places an offence.
- **The street: in the middle of the road, any lane.** মাঝ রাস্তায় থামে বাস, ঝুঁকিতে যাত্রী — "buses
  stop in the middle of the road, passengers at risk" (Rising BD). Buses stop in the middle at busy
  junctions when someone waves (English press: Gulistan, Sayedabad, Jatrabari, Gabtoli, Mohakhali,
  Farmgate, Karwan Bazar, Mirpur, Uttara named).
- **Onto the divider.** On the Dhaka–Chattogram highway (Cumilla) the bus stops mid-carriageway and
  people at the door যে যার মতো লাফিয়ে নামতে থাকেন সড়ক ডিভাইডারের ওপর — "jump down onto the road
  divider, each as best they can"; locals say dozens of buses a day drop people on the divider "or
  wherever they like". At the mouth of the Mohakhali flyover people **wait on the divider** and
  climb on when a bus stops beside it (সড়ক বিভাজকের ওপর দাঁড়িয়ে … বাস এসে থামলে উঠে পড়েন), though it
  is no stop (Prothom Alo).
- **Two lanes wide.** Where buses gather they stand side by side: বাসগুলো দুই লেনজুড়ে সারিবদ্ধভাবে
  দাঁড়িয়ে থাকে — "buses stand in rows across two lanes", with the lineman deciding which bus stands
  where (Prothom Alo). A bus that arrives second stops **outside** the first one, so its door opens
  onto the second lane, and its passengers walk between moving traffic and the first bus.
- **Between two buses.** That gap is where people die: দুই বাসের মাঝখানে চাপা পড়ে পথচারী নিহত —
  "pedestrian crushed to death between two buses" (Dhaka Tribune Bangla); the same at Chankharpul
  (two racing buses) and in Chattogram (বাসের অপেক্ষায় থাকা বিল্লালের প্রাণ গেল দুই বাসের চাপায়, "Billal,
  waiting for a bus, killed between two buses", ITV).
- **Why not the kerb.** The left edge is taken: rickshaws, legunas and CNGs waiting there, hawkers
  out over the footpath and into the road (সড়কের তিন লেনই হকারের, "all three lanes are the hawkers'",
  Bangladesh Pratidin), parked buses at the stand. Pulling in to the kerb also costs seconds and
  lets the bus behind pass. And the crowd does not wait on the footpath: people walk out into the
  road toward the bus they want.
- **How often.** Prothom Alo rode one Shikor Paribahan bus from Mirpur 11 to Zero Point:
  ১২ কিলোমিটার পথে ২৩ স্থানে যাত্রী তুলল বাসটি — "the bus took on passengers at 23 places in
  12 km", about one stop every 500 m, at Kamarpara, Farmgate, Karwan Bazar, Bangla Motor, Shahbag,
  the Press Club corner and more. When it sees traffic police, the bus slows and lets people on and
  off while still moving rather than stopping in front of them.

**For the simulation**: the demand zone today sits at the kerb and the bus "works it" within 20 m
under 3 m/s with the walk to the bus costed per metre (`WalkToBusSecondsPerMetre`). That already
allows a mid-road stop. What the street adds: the crowd walks out to meet the bus (the kerb is not
where they stand), a second bus stops outside the first and loads across it, people get off onto
whatever is beside the door (the divider, the second lane), and the space between two loading
buses is the most dangerous place on the road.

## When people pay (confirmed for local buses; the door dispute is reported often)

- **Local buses: in the ride, to the conductor.** On a local (লোকাল) bus nobody pays at the door
  going in. The conductor works the aisle after people are aboard, asks where they're getting off,
  and names the fare by distance (minimum Tk 10). On counter and "sitting service" buses (সিটিং
  সার্ভিস) a ticket is bought at the counter before boarding; those are the exception on a local
  route.
- **The dispute happens on the way out.** Fares are argued, and the argument is settled at the
  door. Reports: a passenger from Savar asked for Tk 80 paid the old Tk 70 and got off after a
  shouting match (কথাকাটাকাটি); ভাড়া নিয়ে বচসা, ছুরি হাতে যাত্রীকে তাড়া করলেন হেলপার — "a quarrel
  over the fare, the helper chased the passenger with a knife" (Daily Star Bangla). Students
  claim half fare; the conductor argues.
- **Unconfirmed, but it follows from the above:** whoever gets off before the conductor reached
  them pays what they hand over at the door, or nothing if they jump. A rider carried past their
  place has a reason to pay less and the crew little leverage to argue on a moving bus.

**For the simulation**: today the whole fare is added when a rider steps on
(`Boarding.Step`, `load.FaresTk += boarded.FareTk`). The street says the money is not in the bag
until the conductor has collected it, and the last chance is the door. That gives "carried past"
and "jumped off" a cost without any UI: the fare in the ledger is simply smaller.

## How fast (not measured anywhere I could find: estimate)

No source gives a speed. What the reports allow us to say:

- People step off and run a step or two with the bus. A person can land and run out a step at
  roughly **jogging pace, 2–3 m/s (7–11 km/h)**; much faster than that and they fall.
- Boarding with the helper's pull works at about **walking-to-jogging pace, 1–2.5 m/s
  (4–9 km/h)**; the helper himself jumps on at more.
- When it goes wrong it is braking or pulling away, not steady speed: "ব্রেক করা মাত্রই চলন্ত বাস
  থেকে পড়ে যাত্রী নিহত" — "a passenger fell from the moving bus and died the moment it braked"
  (Bangla Tribune); helpers die the same way (রাস্তায় যাত্রী দেখে হঠাৎ ব্রেক করলেন বাসচালক, ছিটকে
  পড়ে হেলপার নিহত — "the driver braked hard on seeing a passenger in the road; the helper was
  thrown off and killed", RTV).

These are estimates from how people move, not measurements. A phone video of a few stops (count
the frames from the kerb line) would pin them down.

## Against the game's numbers today (`TuningTable`, Passengers section)

| Field | Game | This research |
|---|---|---|
| `DoorSpeedMs` (above this, boarding is "rolling") | 3 m/s (10.8 km/h) | rolling boarding is the norm at walking to jogging pace; 3 m/s is the top of it, not the start |
| `JumpSpeedMs` (above this, nobody gets on or off) | 6 m/s (21.6 km/h) | looks high: above ~3 m/s people can't run it out; helpers alone jump on faster |
| `InjurySpeedMs` (a fall above this is an injury) | 3 m/s | matches the jogging limit |
| `MovingDoorTimeFactor` (rolling is quicker) | 0.7 | right direction: the helper's pull and on-and-off at once make it quick |
| left foot first, the helper's hand | not modelled | a passenger's fall chance could drop with the helper at the door and rise when the driver pulls away mid-step |

The second row is a suggestion, not a measured fact: lowering the jump speed to about 4 m/s
(14 km/h) would match the reports better. The biggest missing piece is the moment of risk:
falls happen when the **speed changes** (the driver pulls away, or brakes) with someone on the
step, more than at a steady crawl.

## Sources

- [Daily Star Bangla: quarrel over the fare, helper chases a passenger with a knife](https://bangla.thedailystar.net/news/bangladesh/news-462446)
- [bdnews24 Bangla: fares raised 'as they like' before the new list](https://bangla.bdnews24.com/bangladesh/d6fab6ebac1f)
- [Rupali Bangladesh: arguments over higher fares, passengers pay the old fare](https://www.rupalibangladesh.com/national-news/news/167998)
- [bdnews24 Bangla: illegal 'sitting service'](https://bangla.bdnews24.com/janadurbhog/article1583466.bdnews)
- [Daily Star: Service 'local', fare special](https://www.thedailystar.net/backpage/service-local-fare-special-1392046)
- [Rising BD: "মাঝ রাস্তায় থামে বাস, ঝুঁকিতে যাত্রী"](https://www.risingbd.com/bangladesh/news/604683)
- [Prothom Alo: loading at the mouth of the Mohakhali flyover](https://www.prothomalo.com/bangladesh/%E0%A6%AE%E0%A6%B9%E0%A6%BE%E0%A6%96%E0%A6%BE%E0%A6%B2%E0%A7%80-%E0%A6%89%E0%A7%9C%E0%A6%BE%E0%A6%B2%E0%A6%B8%E0%A7%9C%E0%A6%95%E0%A7%87%E0%A6%B0-%E0%A6%AE%E0%A7%81%E0%A6%96%E0%A7%87-%E0%A6%AC%E0%A7%8D%E0%A6%AF%E0%A6%B8%E0%A7%8D%E0%A6%A4-%E0%A6%B8%E0%A7%9C%E0%A6%95%E0%A7%87-%E0%A6%AF%E0%A6%BE%E0%A6%A4%E0%A7%8D%E0%A6%B0%E0%A7%80-%E0%A6%93%E0%A6%A0%E0%A6%BE%E2%80%93%E0%A6%A8%E0%A6%BE%E0%A6%AE%E0%A6%BE)
- [Prothom Alo: "১২ কিলোমিটার পথে ২৩ স্থানে যাত্রী তুলল বাসটি"](https://www.prothomalo.com/bangladesh/capital/cvkqsgoznt)
- [Prothom Alo: buses stop anywhere, people cross at risk](https://www.prothomalo.com/bangladesh/%E0%A6%AF%E0%A6%A4%E0%A7%8D%E0%A6%B0%E0%A6%A4%E0%A6%A4%E0%A7%8D%E0%A6%B0-%E0%A6%A5%E0%A6%BE%E0%A6%AE%E0%A6%9B%E0%A7%87-%E0%A6%AC%E0%A6%BE%E0%A6%B8-%E0%A6%9D%E0%A7%81%E0%A6%81%E0%A6%95%E0%A6%BF-%E0%A6%A8%E0%A6%BF%E0%A7%9F%E0%A7%87-%E0%A6%B0%E0%A6%BE%E0%A6%B8%E0%A7%8D%E0%A6%A4%E0%A6%BE-%E0%A6%AA%E0%A6%BE%E0%A6%B0%E0%A6%BE%E0%A6%AA%E0%A6%BE%E0%A6%B0)
- [Prothom Alo: why people pay to climb over the road divider](https://www.prothomalo.com/bangladesh/district/fqketwthzh)
- [Banglanews24: the whole road is a bus station](https://www.banglanews24.com/national/news/bd/565226.details)
- [Dhaka Tribune Bangla: pedestrian crushed between two buses](https://bangla.dhakatribune.com/bangladesh/98894/)
- [Daily Star Bangla: helper crushed between two buses](https://bangla.thedailystar.net/news/bangladesh/accident-fire/news-503001)
- [ITV: Billal, waiting for a bus, killed between two buses](https://www.itvbd.com/country/chittagong/268394/)
- [Bangladesh Pratidin: all three lanes are the hawkers'](https://www.bd-pratidin.com/city-news/2026/05/15/1251182)
- [DMP: road safety guidance](https://dmp.gov.bd/road-safety/)
- [Daily New Nation: buses stop anywhere, park everywhere](https://dailynewnation.com/news/855003)
- [Bangla Tribune: loading in the road though stops exist](https://www.banglatribune.com/amp/c/414571/)
- [Prothom Alo: "নামতে গেলেই চালক বাস টান দিচ্ছিলেন, পরে লাফিয়ে নামেন"](https://www.prothomalo.com/bangladesh/capital/le26byz3e1)
- [Prothom Alo: "ওস্তাদ, টান দিয়েন না"](https://www.prothomalo.com/bangladesh/district/h4macl378t)
- [Somoy News: why get off a bus left foot first](https://www.somoynews.tv/news/2024-01-18/DWVJVYEW)
- [Science Bee: why the left foot when getting off a moving bus](https://www.sciencebee.com.bd/qna/131/)
- [Prothom Alo: 20 tips for taking the bus](https://www.prothomalo.com/lifestyle/4ylbaizog8)
- [Prothom Alo: "বাসে উঠে দাঁড়িয়ে থাকতে হয়, হেলপার গায়ে হাত দেয়"](https://www.prothomalo.com/bangladesh/district/k5y70u2usb)
- [Bangla Tribune: passenger dies falling from a bus as it braked](https://www.banglatribune.com/country/dhaka/948917/)
- [RTV: driver brakes for a passenger, helper thrown off and killed](https://rtvonline.com/country/379491)
- [Banglanews24: helper thrown from moving bus while taking on passengers](https://www.banglanews24.com/saradesh/news/bd/1652380.details)
- [Newsbangla24: woman pushed off a bus, driver and helper remanded](https://www.newsbangla24.com/national/128661/)
- [bdnews24: reckless driving, weak oversight (helpers, missing stops)](https://bdnews24.com/bangladesh/f04cfdfed70c)
