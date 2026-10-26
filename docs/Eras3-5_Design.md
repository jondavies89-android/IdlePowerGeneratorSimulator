# Eras 3–5 — implementation design

Working design for Eras 3 (The War of Currents), 4 (The Grid Awakens) and 5 (The Interconnected
Grid). It fills the gaps the bible leaves (its Open Questions list the Era 3 AC/DC mechanics and the
Era 4 synchronisation minigame) with decisions taken with the project owner on 2026-10-25. The
bible stays the canonical reference; where this file and the bible differ, this file records the
choice that was made and why.

All numbers below live in `Assets/Scripts/Data/Era3Data.cs`, `Era4Data.cs` and `Era5Data.cs` and are
written into ScriptableObjects by the era asset builders. Tune them there.

## Decisions

| Question | Decision |
|---|---|
| AC vs DC (bible open question) | DC first, AC unlocks. Era 3 opens on Edison-style DC mains that can only reach districts close to the station; researching transformers and buying Westinghouse alternators turns the network to AC, which reaches the whole town. The War of Currents is the era's arc, not a branching save. |
| Research | Small and era-gated. Research points (RP) come from R&D buildings (bible §13.1.2). Each era has a short list of research nodes that gate its key technology. Research is permanent across eras (bible §13). The full six-branch tree is deferred. |
| Era 4 sync minigame (bible open question) | Synchroscope. A needle sweeps at the slip between the incoming unit and the grid; the player nudges the unit's governor slower/faster and taps CLOSE near 12 o'clock. Out of phase slams the breaker: the unit trips and needs repair. |
| Scope | Maintenance & fuel scores (Era 4), automatic under-frequency load shedding / brownouts and the redundancy score (Era 5), the black-start minigame. Prestige (Engineering XP) is deferred. |
| Income | Both: connected districts / feeders / cities pay a tariff for every unit delivered, and a contract board of bigger clients carries on from Era 2. |
| Currency | Pounds from Era 3. Money is still held in pennies internally (decimal), so the Era 2 purse converts at 240 d = £1 by construction; only the display changes. |
| Era end | A goal bar toward the era's end output (10 MW, 1 GW, 50 GW delivered); in Era 2, lighting the Great Exhibition. Meeting it pops a panel saying what has been done, with "Begin the next era" and "Not yet"; until the player goes, a gold banner above the tab bar (on every page) begins the transition. The player always chooses when. |
| Carry-over | The previous era's works become one inherited asset at that era's theoretical maximum output (1 kW into Era 2, 100 kW into Era 3, 10 MW into Era 4, 1 GW into Era 5), whatever the player actually built. It is steady, never trips and isn't touched by upgrade multipliers. Upgrades that multiply output apply only to the era they belong to; research carries forward. |

## Shared systems

* **Inherited station.** `PowerGrid.InheritedWatts`, set from the previous era's `EraConfig.maxWatts`
  whenever the era changes or a save loads. The previous era's machines are retired at the change.
  Era 1's crank, flywheel and water wheel stop feeding the grid from Era 2 on.
* **Load areas.** Districts (Era 3), feeders (Era 4) and cities (Era 5) are the same thing: a place
  that draws power on the daily demand curve (bible §3.4), pays a tariff on what it receives, and
  costs money to connect. Saved by id.
* **Transmission network** (`Simulation/TransmissionNetwork`). Shares the available generation out
  among connected load areas and works out the losses. Each area has a loss coefficient
  `k` (per MW delivered): delivering `x` costs `x + k·x²` at the station, which is the I²R shape
  without showing the player an equation. Short of power, every area is dimmed by the same factor
  (solved exactly from the quadratic). Era 5 adds reactive power: current, and so losses and the
  voltage drop, grow with `√(P² + Q²)`, and voltage at a city falls with `(P·R + Q·X)`.
* **Units and frequency** (`Simulation/GridDispatch`, Eras 4–5). Each unit has a capacity, a
  governor time constant and an inertia. Online units share the load in proportion to capacity and
  chase it with a lag; frequency follows the swing equation (bible §4.1) with load damping, and a
  frequency-bias term brings it back to 50 Hz when there is reserve. Short of capacity, frequency
  settles where load self-regulation balances the deficit: about −1 Hz for 5 % short.
* **Contracts.** The Era 2 contract board, generalised (`ContractDesk`). From Era 3 contracts are
  served first, from what reaches customers after losses. Every client the player's repute has
  opened can be booked at any time there's a free slot, and again as soon as it finishes. Clients
  used to ask only in their own hours of the day, once a day; that was meant for immersion but
  played as long stretches of watching the clock with nothing to do (2026-10-26).
* **Contract managers** (Eras 2–5, replacing Era 2's clerk). Each era's tree has a levelled
  "Contract manager", one per contract slot (up to four). A manager is switched on or off and set
  to a client, or to "the best paying it can supply". On, it books its client whenever there's a
  free slot and the works can supply the whole of it, never a contract that would fail, and keeps
  10 % of every payment from it. Off, its card has a Start button that books the same client by
  hand at full pay. Managers belong to their era, like the slots.
* **Research.** `CurrencyLedger.ResearchPoints`; research nodes are `UpgradeDefinition`s with a
  `costResearch` and `isResearch`, shown on a Research tab. R&D buildings are levelled upgrades that
  add RP per game hour.

## Era 3 — The War of Currents

Westgate, 7th April 1893. 100 kW → 10 MW. One new concept: distance has a cost.

* Town map of 18 districts on a 6 × 3 grid with the station at the left. Distance is in blocks
  (Manhattan distance). Three districts are lit at the start.
* Loss coefficient by mains: DC 110 V 0.8, three-wire DC 220 V 0.2, AC 2 kV 0.01, AC 6.6 kV 0.001
  (fraction lost per MW-block); heavier copper on one district halves it, twice. DC can only reach
  2 blocks.
* Machines (stackable, cost × 1.15ⁿ): Jumbo dynamo £200 · 20 kW; Parsons turbo-generator £1,500 ·
  300 kW (research Steam turbine); Coal-fired station £8,000 · 1.5 MW (Water-tube boilers);
  City power station £40,000 · 6 MW (Condensing turbines). Bible §9.7 anchors.
* R&D: Apprentice's workshop £500 → 5 RP per game hour (bible §13.1.2).
* Research: Transformer principles 80 RP (mockup), Steam turbine, Water-tube boilers,
  Electricity meters, High-voltage mains, Condensing turbines.
* The era ends when 10 MW is delivered at once: the Westgate Electrical Exhibition.
* Active play, real time and only while the player is here (2026-10-26):
  * **Stoking the boilers** (Town page): each stoke raises the steam pressure, which falls on its
    own; held in the green band (60–85 %) the works earns twice as much. Over-stoked, the safety
    valve lifts and stoking stops for 4 s. Money only.
  * **The bench galvanometer** (Research tab): a needle swings across a scale; a reading taken
    with it inside the marked band earns research — 5 % of an hour of the works' R&D, at least
    1 RP, up to double on a run of good readings. A miss ends the run. It brings the 80 RP to
    transformers (the gate to AC) within minutes of active play.

## Era 4 — The Grid Awakens

Central Electric Station, 14th June 1928. 10 MW → 1 GW. One new concept: the grid has a heartbeat.

* Control desk: frequency gauge, load, a monospaced unit board (ON / RMP / OFF / SYNC / FAULT),
  reserve. A bought unit has to be synchronised before it carries load.
* A status strip above the tabs (Eras 4–5) shows the frequency, the load against the units
  online, and the one thing most worth knowing: short now, the evening peak in the next six game
  hours outrunning what is online, an overdue unit the rest couldn't cover, coal running out,
  feeders still dropped. The cascade warning sits under it, so neither is hidden by another tab.
  The feeder list says when connecting one would overload the grid now or at the evening peak.
* Under 49.5 Hz the board goes amber; under 49.0 Hz a cascade warning counts down 90 s
  (bible §10.2) with one-tap remedies (start a reserve unit, drop the biggest feeder). Below
  48.0 Hz, while the load is still more than the units can carry, the countdown runs six times as
  fast: a big overload leaves about 15 s to act, never a blackout with no warning. Running out of
  it is a blackout and the black-start minigame.
* Maintenance: units run up service hours; past their interval they can fail (online only — never
  offline, CLAUDE.md). The first failure introduces Maintenance Preparedness (bible §14.2.1).
* Fuel: a coal stockpile burned by coal units. Running dry trips them; the first time introduces
  Fuel Reserve Preparedness and the fuel manager (target days of cover, bible §14.2.2).
* R&D: Engineering office £20,000 → 50 RP per game hour.

## Era 5 — The Interconnected Grid

Midlands region, 12th November 1952. 1 GW → 50 GW. One new concept: power isn't just power.

* Mimic board of cities joined by lines. Each city draws reactive power from its industry
  (Leicester's cotton mills worst); low power factor raises losses, eats generator capacity and
  sags voltage at the far end. Capacitor banks are the affordable fix.
* Headline stats: demand, frequency, power factor, voltage.
* Automatic under-frequency load shedding (bible §4.4): 49.5 / 49.2 / 48.8 Hz shed 5 / 10 / 15 %
  of load; the first brownout introduces Redundancy Preparedness and fitting units for black start.
* R&D: R&D laboratory £500,000 → 500 RP per game hour.

## Black start (bible §10)

Triage → cranking (tap the Era 1 crank) → establish an island (pick loads under capacity, mind
industrial inrush) → second unit's start sequence (step failures driven by the Maintenance and
Fuel scores) → synchronise (the synchroscope again) → restoration to 95 % of the load → post-mortem.
No way out once it starts (bible §15.3); a blackout that would happen offline is never simulated.

A grid that went dark for want of units (or because one failed, or the coal ran out) can't get
back to 95 %. Once there is no unit left to bring on and no substation that fits, restoration can
be finished anyway; the grid restarts carrying only what its units can (with a 5 % margin), and the
rest stays dropped, named in the post-mortem, to be picked up once there's reserve. Without that
the black start had no end.

## Pacing

`PacingReport` (explicit PlayMode tests, `-testFilter IdlePowerGen.Tests.PacingReport`) plays each
era from its start with a greedy bot and logs how long it took. Real players take longer (they
aren't perfect, and they're away), so the bot should land at or below the bible's lower bound.

| Era | Bible (real play) | Bot | Longest wait | Notes |
|---|---|---|---|---|
| 2 | 2–8 h | 2.1 h | 4.0 min | contract rate 0.4 → 0.3 when clients became bookable at any hour; the bot hires managers and pays their commission |
| 3 | 8–24 h | 4.2 h | 22 min | 4.7 h while clients kept their hours |
| 4 | 1–3 days | 11.0 h | 51 min | |
| 5 | 3–10 days | 23.9 h | 74 min | brownouts ~20 % of the time: the bot books every contract it can and overconnects |

Tuning that came out of it:

* A newly connected area (and a contract's load) comes on over 30 real seconds: switched in at
  once, a big works crashed the frequency before a governor could answer.
* Load the operators expect is scheduled: units follow it directly, and the governors (with their
  lag) answer only surprises — a unit tripping, the noise. Without that, the evening rise alone
  outran the governors.
* A grid that starts (era opening, save loading, black start finished) starts already carrying
  its load.
* In a black start each area comes back a substation (a quarter) at a time, and the old station
  is the first unit brought back in restoration; otherwise the diesel couldn't pick up even the
  smallest district.

## Dev tools

A collapsible DEV strip at the top of the screen: jump to the start of any era (replacing the save)
and a time-acceleration slider (1×–1000×) that runs the sim and the clock faster. In Eras 4–5,
a button blacks the grid out at once, to test the black start.
