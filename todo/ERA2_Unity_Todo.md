# Era 2 Unity To-Do — "The Demonstration"
**Setting:** London, 9 October 1851 · 1 kW → 100 kW  
**New concept:** Demand isn't constant. Scheduled contracts vary by time of day.  
**Aesthetic:** Gas-lit demonstration office; Capacity · Purse · Repute; the contract progress bar as the spotlight mechanic.

Built from the bible (§2, §5.1.4–5.1.6, §9.2, Appendix A mockup). Where the bible disagrees with itself, the
choice made is noted. Numbers live in `Assets/Scripts/Data/Era2Data.cs`; `Era2AssetBuilder` writes the assets.

---

## 1. ScriptableObject Assets

### 1.1 Era Config — `Assets/ScriptableObjects/Eras/Era2Config.asset`

- [x] Era 2, "The Demonstration", 1851, London, 1 kW → 100 kW, joules + cash + reputation
- [x] Contract base rate **0.4 d per watt per game hour** (see §5.1)
- [x] Offline era factor 1.0, tau 4 h, *"Your apprentice kept the fires burning while you were away."*

### 1.2 Generators — `Assets/ScriptableObjects/Generators/Era2/`

Bible §5.1.5 machines, paid in **pennies** (bible §9.2: infrastructure always costs cash; §5.1.4's
"joules for equipment" is the older line). Stackable, ×1.15 a unit. Running costs are per game hour
(one real minute): wages from the purse, coal from the hopper.

| Asset | Machine | Cost (d) | Output | Running cost | Unlocked by |
|---|---|---|---|---|---|
| `Generator_SmallDynamo` | Apprentice on dynamo | 50 | 50 W | wage 1 d/hr | — |
| `Generator_Waterwheel` | Water wheel | 500 | 200 W | none | — |
| `Generator_SteamEngine` | Steam engine | 2,000 | 800 W | coal 1 d/hr | Watt's Engine Patent |
| `Generator_LargeSteamEngine` | Large steam engine | 10,000 | 4 kW | coal 4 d/hr | Boulton & Watt Partnership |

- [x] The Era 1 workshop (crank, flywheel, water wheel) keeps running and shows as "The old workshop"; tapping it turns the crank.
- [x] Unpaid apprentices down tools (offline) until there is an hour's wages in the purse.
- [ ] Seasonal water wheel (bible §5.1.5 "seasonally varying") — **not done**: seasons arrive with the annual demand curve, deferred to Era 3+ by `DemandModel`.

### 1.3 Upgrades — `Assets/ScriptableObjects/Upgrades/Era2/`

Paid in pennies; reputation is a gate, never spent.

| Upgrade | Cost (d) | Repute | Effect | Needs |
|---|---|---|---|---|
| Apprentice Training | 150 | — | apprentices ×2 | — |
| A Clerk at the Desk | 600 | 4 | books engagements the works can fully supply | — |
| Watt's Engine Patent | 1,000 | 3 | unlocks the steam engine | — |
| Telegraph Lines (2 levels, ×10) | 1,500 | 5 | +1 engagement at a time | — |
| Mill-race Weir | 2,000 | — | water wheels ×2 | — |
| Coal Merchant's Account | 4,000 | — | orders coal when the hopper is low | Watt |
| Steam Pressure Control | 6,000 | — | steam engines ×2 | Watt |
| Boulton & Watt Partnership | 25,000 | 10 | unlocks the large steam engine | Watt |
| Compound Cylinders | 120,000 | — | large steam engines ×2 | Boulton & Watt |
| Master Engineer | 300,000 | 18 | everything ×1.5 | — |
| Royal Warrant | 1,000,000 | 24 | everything ×2 | — |

New effects (appended to `UpgradeEffect`): `MultiplyGeneratorOutput` (per machine type, including ones bought later)
and `AddContractSlot`.

### 1.4 Contracts — `Assets/ScriptableObjects/Contracts/Era2/`

Each client asks during their window of the day, once a day. A booking runs for its hours from when it was
booked. Pay = hours × watts × 0.4 × multiplier; below 50 % supplied on average: −2 repute and pro-rated pay.
Output is shared oldest booking first, each taking at most what it asked for.

| Client | Watts | Hours | Asks | Repute needed | +Repute | Pay × |
|---|---|---|---|---|---|---|
| Street Lamp Row | 500 W | 5 | 17:00–21:00 | 0 | 1 | 1.0 |
| Whitmore's Bakery | 800 W | 3 | 03:00–06:00 | 0 | 1 | 1.0 |
| The Grand Hotel | 1.2 kW | 8 | all day | 0 | 1 | 1.0 |
| City Print Works | 3 kW | 8 | 06:00–10:00 | 3 | 1.5 | 1.0 |
| Theatre Royal, Drury Lane | 6 kW | 4 | 17:00–20:00 | 6 | 2 | 1.0 |
| ★ Royal Society demonstration | 10 kW | 2 | 18:00–20:00 | 10 | 3 | 1.5 |
| Spitalfields Silk Mill | 20 kW | 10 | 05:00–08:00 | 14 | 2 | 1.0 |
| Great Western Railway | 40 kW | 12 | all day | 18 | 3 | 1.0 |
| Shopfronts of the Strand | 60 kW | 5 | 16:00–19:00 | 22 | 3 | 1.0 |
| ★ The Great Exhibition | 100 kW | 8 | 08:00–10:00 | 28 | 5 | 1.6 |

★ = `ContractTier.Reputation`. Completing the Great Exhibition ends Era 2.

---

## 2. Scene

- [x] Option B: one scene, `Era1Screen` and `Era2Screen` swapped by `EraScreenSwitcher` on `CurrentEra`
- [x] `Era2WorksController` (board, bookings, wages, coal, clerk, merchant) and `Era2GeneratorController` on `_Controllers`
- [x] Built by `Era2ScreenBuilder`, called from `Era1SceneBuilder.Build`

## 3. Screen (bible Appendix A mockup)

- [x] Header: "— THE DEMONSTRATION OFFICE —", date and part of day ("Thursday evening, 9th October 1851")
- [x] Capacity · Purse · Repute panel; repute as five stars (one per 6 repute), part stars filled
- [x] Supply line: customers' draw against output, green / amber (short) / red (below half)
- [x] Day bar: 24 h with daylight shaded and a marker at the hour, clock beside it
- [x] Engagements: client, story, start → end, need, a bar of the hours passing coloured by supply so far
      (green ≥ 80 %, amber ≥ 50 %, red below), time left, "holding" / "% supplied", pay and repute
- [x] The board: clients asking now (tap to book; warns if the works would be short), clients later today
      dimmed with their hours, and the next client the repute ladder would open
- [x] Generators with wages / coal in coral, output, and "Build" price; running costs total
- [x] Coal hopper: level bar, hours left, amber below 20 %, red below 5 %, **order N d** button
- [x] Improvements (Era 2 upgrades only)
- [x] Weather glass (tap: "A barometer. Might the weather affect demand someday?") and the gas-lamp footer
- [x] Toasts: engagement paid / failed, coal out, apprentices downed tools, offline pay
- [x] Title card on 1 → 2: "The Demonstration — 9th October 1851 · Your first customer has arrived"
- [x] End card on the Great Exhibition: "Era 3 is still being built" with keep going / start again
- [ ] Gas-lamp flicker, coin sound and stings on contract results (optional, todo §5.2/§7)

## 4. Saves (v4)

- [x] Era 2 start day, coal hopper level, contracts booked per day; v3 → v4 migration
- [x] Offline (medium/long absence): the game clock waits, so bookings resume; customers drawing power at save
      time keep paying through the offline curve; no wages or coal charged (bible §10.5: nothing goes wrong offline)
- [x] Short absences tick the sim with the clock moving, so contracts end on time

## 5. Decisions to review

1. **Contract pay rate.** The bible's 0.001 d per W·s (per W·h in code) couldn't pay an apprentice's wage.
   0.4 d per W per game hour: a pacing sim (greedy bookings, best watts per penny) finishes the era in ~3 h 10 m.
2. **Machines cost pennies**, not joules (bible §9.2 over §5.1.4). Joules still accrue but buy nothing in Era 2.
3. **The date.** The bible says *Tuesday* 9 October 1851; the calendar says Thursday. The header uses the calendar.
4. **The coal hopper is shared** (as in the mockup) rather than per engine, and sized to a day's burn of every
   engine owned, so an order lasts a game day (24 real minutes) at any fleet size.
5. **Contract roster** runs to 100 kW so the era can reach its end wattage; the todo's original five stopped at 5 kW.
