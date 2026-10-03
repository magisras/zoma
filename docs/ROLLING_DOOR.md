# The rolling door: getting on and off a bus that doesn't stop

Research annex to `RESEARCH.md`, 3 Oct 2026, done in Bangla first (CLAUDE.md). How people get on
and off Dhaka buses that barely stop, why it looks so fluent, and how fast the bus is going. Most
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
