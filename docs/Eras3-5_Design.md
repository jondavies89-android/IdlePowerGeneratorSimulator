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
| Era end | A goal bar toward the era's end output (10 MW, 1 GW, 50 GW delivered). Reaching it offers a capstone event that closes the era. |
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
  served first, from what reaches customers after losses.
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

## Era 4 — The Grid Awakens

Central Electric Station, 14th June 1928. 10 MW → 1 GW. One new concept: the grid has a heartbeat.

* Control desk: frequency gauge, load, a monospaced unit board (ON / RMP / OFF / SYNC / FAULT),
  reserve. A bought unit has to be synchronised before it carries load.
* Under 49.5 Hz the board goes amber; under 49.0 Hz for long a cascade warning counts down
  (bible §10.2) with one-tap remedies (start a reserve unit, shed a feeder). Running out of it, or
  falling below 48.0 Hz, is a blackout and the black-start minigame.
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

## Dev tools

A collapsible DEV strip at the top of the screen: jump to the start of any era (replacing the save)
and a time-acceleration slider (1×–1000×) that runs the sim and the clock faster.
