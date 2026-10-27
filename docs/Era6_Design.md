# Era 6 — implementation design

Working design for Era 6 (The Nuclear Age). It fills the gaps the bible leaves with decisions
taken with the project owner on 2026-10-27, and builds on the shared systems in
`docs/Eras3-5_Design.md` (inherited station, load areas, contracts and managers, research,
balances above the tabs, black start). The bible stays the canonical reference; where this file
and the bible differ, this file records the choice that was made and why.

All numbers live in `Assets/Scripts/Data/Era6Data.cs` and are written into ScriptableObjects by
`Era6AssetBuilder`. Tune them there.

## Decisions

| Question | Decision |
|---|---|
| How the fleet is run | Modes and ramp rates. Each unit on the bars is on **BASE** (flat out) or **AGC** (following the load). Units on AGC are loaded cheapest to run first (the merit order), each no faster than its ramp rate and never below its minimum stable output. A reactor can only run on BASE. A unit can be stood down to **STBY**. |
| Waiting | Short start-ups only. A unit takes game time to start from standby (gas turbine 3 min, pumped storage about 1 min, combined cycle 30 min, coal 1 h 30, a reactor 8 h, a mega-station 12 h). A tripped reactor can't restart for 24 h (the xenon has to clear). Building is instant once paid for; reactors are held back by research, a reputation licence (bible §9.9) and price. |
| Training (OTP) | Bought with research and cash, not the prestige loop's XP (prestige is still deferred). Learning *Operator training* introduces the OTP score and the drill. |
| Map | The regions of Britain on a national mimic board, connected like Era 5's cities. Reactive power stays in the background (no capacitor puzzle). |
| Era end | 1 TW delivered at once. Era 7 isn't built: its capstone shows the end-of-content card. |

## The concept: big machines are slow

* **Merit order.** Every unit has a running cost: coal about £45 a MWh (from the yard, at £130 a
  tonne), combined cycle £38, pumped storage £62, gas turbines £80, reactors £6 (£5 the
  mega-station). The inherited old fleet costs the player nothing but sits at £56 in the order,
  so cheaper new units are loaded first. A region pays £62.50 a MWh, so the reactors pay best
  and the peakers don't pay at all: they are there for the peak and the reserve.
* **Ramps and the governors.** The dispatch moves each unit on AGC no faster than its ramp rate
  (coal 1.5 % of its rating a game minute, combined cycle 5 %, gas turbines 25 %, pumped
  storage 60 %). On top of that, every unit that can follow the load gives a primary response
  at once (bible §4.3): a tenth of its rating for 0.2 Hz off. So a sudden change is held near
  fifty while the ramps catch up, and only one bigger than the fleet's tenth pulls the
  frequency away. A reactor gives no primary response.
* **BASE and AGC.** Without the *Automatic Generation Control* research, new units can only run
  on BASE, and the old fleet alone follows the load. That works while the swing between night
  and evening fits in the old fleet; it soon doesn't.
* **The night trough.** Units on BASE, plus the minimums of the units on AGC, must stay under the
  night's load. If they don't, the frequency rises. Over 50.5 Hz the desk warns; held over
  51 Hz for 10 seconds, overspeed protection trips the biggest unit on BASE. A tripped reactor
  holds for 24 hours.
* **The evening rise.** Units on standby must be started early enough to be on the bars for the
  peak. The status strip says when the peak will outrun what is running and starting, and
  which unit to start now.
* **Reserve against the margin** (bible Appendix A, Era 6). The reserve (spinning headroom plus
  fast standby units) should cover the biggest unit on the bars. If it doesn't, the ALARMS
  panel shows RESERVE LOW.
* **Maintenance.** It carries on from Era 4. A unit can be serviced from standby (and goes back
  there), and a reactor's service is its refuelling outage (three times as long).

## Automation (research)

* *Automatic Generation Control* unlocks the AGC mode.
* *Unit commitment computer* starts standby units in time for the peak, cheapest first, and
  stands the dearest down when the night would leave too much on the bars.
* *Outage planning* services units before they run overdue, one at a time, when the rest can
  cover them.

## Operator training and drills (bible §10.4, §11, §14.2.5)

* OTP = 30 for the training, +15 for each of the two levels of the *Control-room simulator*,
  and up to 40 from the last drill, fading to nothing over 10 game days.
* A drill (£25M) is a planned black start: the grid goes down on purpose with every customer
  warned. No repute is lost, the contracts wait where they are, and no step of the start
  sequence fails. How well it went (collapses, relay trips, time) sets its points.
* OTP only ever helps: up to 30 s more cascade warning, a clean-close window on the
  synchroscope up to 60 % wider, and up to half the start-sequence failures gone.
* In a blackout reactors scram: the triage lists them as unavailable ("SCRAM safe state") and
  they hold for 24 hours.

## Regions, units and the rest

* Fourteen regions from the Midlands (and South Wales) outwards: about 850 GW on average when
  all are lit, near 1.2 TW at the evening peak. Each hangs off a parent nearer the Midlands.
* Units: coal-fired station 10 GW £150M; gas turbine peaker 3 GW £45M (black start); pumped
  storage 12 GW £300M, three at most (black start); combined cycle 20 GW £200M; PWR 60 GW
  £1.5B (licence at repute 85); twin-reactor mega-station 200 GW £4B.
* R&D: the national research institute, £20M, 5,000 RP an hour (bible §13.1.2).
* *Economy 7* (1978) moves 30 % of the daily swing into the night: the trough fills for the
  reactors and the peak eases.
* **Pacing** (`PacingReport.Era6`): the greedy bot takes 55 h (bible: one to three weeks of real
  play), with no blackouts and brownouts about 1 % of the time. Its longest wait is 166 min,
  saving for the first £4B mega-station; most waits late in the era are 40–80 min. At £100 a
  MWh (and fuel to match) it took 33 h.
* The screen follows the mockup: a charcoal header bar, the FREQ CRT with RoCoF, DEMAND and
  GENERATION tiles, the DISPATCH board (CCGT-01 HUNTERSTON 18.4 AGC) with BASE · AGC · STBY on
  the unit card, the ALARMS panel, and the preparedness scores (MP, FRP, RP, OTP).
