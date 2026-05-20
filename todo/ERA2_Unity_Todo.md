# Era 2 Unity To-Do — "The Demonstration"
**Setting:** Tuesday evening, 9 October 1851 · 1 kW → 100 kW  
**New concept:** Demand isn't constant. Scheduled contracts vary by time of day.  
**Aesthetic:** Gas-lit demonstration office; two currencies + reputation stars; contract progress bar as spotlight mechanic.

---

## Prerequisites

- [ ] Era 1 is fully playable and the Era 1 Unity checklist is complete
- [ ] `Era1_Workshop` scene, all Manager GameObjects, and catalogues are working

---

## 1. ScriptableObject Assets

### 1.1 Era Config

- [ ] `Assets/ScriptableObjects/Eras/Era2Config.asset`
  - Era Number: **2**, Era Name: **"The Demonstration"**
  - Flavor Year: **1851**, Flavor Location: **"London"**
  - Min Watts: **1000**, Max Watts: **100000**
  - Use Joules: ✓, Use Cash: ✓ (pennies), Use Reputation: ✓
  - Contract Base Rate: **0.001** (pennies per W·s)
  - Offline Era Factor: **1.0**, Offline Tau Hours: **4**
  - Offline Flavor Text: *"Your apprentice kept the fires burning while you were away."*

### 1.2 Generator Definitions

All values are in `Assets/Scripts/Data/Era2Data.cs`.  
Fuel consumption > 0 means the generator will trip when fuel reaches 0 — this is the Era 2 coal hopper warning.

| Asset name                   | Type                | Base Power | Base Cost (J) | Fuel/sec |
|------------------------------|---------------------|------------|---------------|----------|
| `Generator_SmallDynamo`      | SmallDynamo         | 10 W       | 50            | 0        |
| `Generator_GasLampGenerator` | GasLampGenerator    | 100 W      | 500           | 0.0002   |
| `Generator_CoalFiredBoiler`  | CoalFiredBoiler     | 500 W      | 2500          | 0.0005   |
| `Generator_SteamEngine`      | SteamEngine         | 1 000 W    | 12 500        | 0.001    |
| `Generator_LargeSteamEngine` | LargeSteamEngine    | 5 000 W    | 62 500        | 0.002    |

- [ ] Create all five assets in `Assets/ScriptableObjects/Generators/`
- [ ] Add them to the `_generatorDefinitions` array on **GameManager**

### 1.3 Upgrade Definitions

Era 2 upgrades are paid in **pennies** (the `costPennies` field). All values are in `Assets/Scripts/Data/Era2Data.cs`.

**Generator unlocks** (effect `UnlockGenerator`, `unlocksId` = enum name):

| Asset name                    | ID                          | Cost (d) | Unlocks ID         | Prereq                      |
|-------------------------------|-----------------------------|----------|--------------------|-----------------------------|
| `Upgrade_UnlockGasLamp`       | `era2_unlock_gas_lamp`      | 5        | `GasLampGenerator` | —                           |
| `Upgrade_UnlockCoalBoiler`    | `era2_unlock_coal_boiler`   | 25       | `CoalFiredBoiler`  | `era2_unlock_gas_lamp`      |
| `Upgrade_UnlockSteamEngine`   | `era2_unlock_steam_engine`  | 125      | `SteamEngine`      | `era2_unlock_coal_boiler`   |
| `Upgrade_UnlockLargeSteam`    | `era2_unlock_large_steam`   | 625      | `LargeSteamEngine` | `era2_unlock_steam_engine`  |

**Efficiency upgrades** (effect `MultiplyAllOutput`):

| Asset name                       | ID                            | Cost (d) | Multiplier | Prereq                      |
|----------------------------------|-------------------------------|----------|------------|-----------------------------|
| `Upgrade_ApprenticeTraining`     | `era2_apprentice_training`    | 10       | 1.10       | —                           |
| `Upgrade_ReinforcedBoilers`      | `era2_reinforced_boilers`     | 50       | 1.15       | `era2_unlock_coal_boiler`   |
| `Upgrade_PressureControl`        | `era2_pressure_control`       | 250      | 1.25       | `era2_unlock_steam_engine`  |
| `Upgrade_MasterEngineer`         | `era2_master_engineer`        | 1250     | 1.50       | `era2_pressure_control`     |

**Prestige upgrades** — pennies + reputation gate (set `requiredReputation` field):

| Asset name              | ID                     | Cost (d) | Req Reputation | Effect             | Effect Value | Unlocks ID                       |
|-------------------------|------------------------|----------|----------------|--------------------|--------------|----------------------------------|
| `Upgrade_TelegraphLines`| `era2_telegraph_lines` | 500      | 10             | UnlockFeature      | 0            | `feature_extra_contract_slot`    |
| `Upgrade_RoyalWarrant`  | `era2_royal_warrant`   | 5000     | 25             | MultiplyAllOutput  | 2.0          | —                                |

> Reputation is a gate, not a cost — `requiredReputation` must be met but is **not deducted** on purchase.

### 1.4 Contract Definitions

All values are in `Assets/Scripts/Data/Era2Data.cs`.

| Asset name                  | ID                 | Client                  | Required W | Hours | Pay Mult | Rep | Tier     | Hours Active |
|-----------------------------|--------------------|-------------------------|------------|-------|----------|-----|----------|--------------|
| `Contract_StreetLamp`       | `c2_street_lamp`   | Street Lamp Row         | 50         | 4     | 1.0      | 0.5 | Standard | 18:00–22:00  |
| `Contract_Bakery`           | `c2_bakery`        | Whitmore's Bakery       | 200        | 2     | 1.0      | 0.5 | Standard | 05:00–08:00  |
| `Contract_PrintWorks`       | `c2_print_works`   | City Print Works        | 500        | 8     | 1.1      | 1.0 | Standard | 06:00–18:00  |
| `Contract_Hotel`            | `c2_hotel`         | The Grand Hotel         | 1 000      | 12    | 1.2      | 1.5 | Standard | 00:00–24:00  |
| `Contract_GreatExhibition`  | `c2_exhibition`    | The Great Exhibition    | 5 000      | 6     | 1.5      | 5.0 | Prestige | 09:00–17:00  |

- [ ] Create all five assets in `Assets/ScriptableObjects/Contracts/`
- [ ] Set **Fail Threshold = 0.5** on all (below 50 % average output = failed contract)
- [ ] Set **Reputation On Fail = -2** on all

---

## 2. Scene Setup

### 2.1 Option A — Separate scene

- [ ] Duplicate `Era1_Workshop` as `Era2_Demonstration`
- [ ] Replace background art, font, and palette (see §7 below)
- [ ] Load this scene on `EraAdvancedEvent` where `ToEra == 2`

### 2.2 Option B — Same scene, swap UI panels

- [ ] Add an `Era2Panel` root GameObject; activate/deactivate in `EraManager` based on `CurrentEra`

> Recommendation: start with Option B (single scene) until you have a working Era 2 loop; split into separate scenes later.

### 2.3 New Manager / Controller

- [ ] Add `Era2GeneratorController` component to the scene
- [ ] Add a **Contract Board** UI controller (new MonoBehaviour) that reads `GameManager.Contracts.ActiveContracts` and available `ContractDefinition` assets

---

## 3. Dual-Currency UI

Era 2 introduces pennies and reputation alongside joules.

### 3.1 Currency Bar (always visible)

```
[ ⚡ 1.24 kJ ]   [ £ 0.00 ]   [ ★ 0 Rep ]
```

- [ ] Three TMP labels in a horizontal layout group at top of screen
- [ ] All three subscribe to `CurrencyChangedEvent` and update on change
- [ ] **Pennies** use `NumberFormatter.FormatMoney(ledger.Pennies)`
- [ ] **Reputation** — integer display; show star icon beside value
- [ ] Animate value changes with a brief punch scale (DOTween) for dopamine on earn

### 3.2 Era 2 "first penny" moment

- [ ] When `CurrentEra` advances to 2, show a full-screen overlay:  
  *"A gentleman pays you one penny. A new age begins."*  
- [ ] Animate the `£` symbol appearing in the currency bar

---

## 4. Generator Purchase Panel

The crank is still available but generators dominate by design.

```
GeneratorPanel
├── SmallDynamo row
│   ├── Icon + Name
│   ├── "10 W passive"
│   ├── "Owned: 0"
│   ├── Cost label  (Era2GeneratorController.NextCost)
│   └── Buy button
├── GasLampGenerator row
...
```

- [ ] One row prefab, instantiate for each `GeneratorDefinition` with `minEra <= 2`
- [ ] Buy button → `Era2GeneratorController.TryBuy(def)`
- [ ] Cost label updates after each purchase (cost grows × 1.15 per unit owned)
- [ ] Rows with fuel consumption show a small **fuel bar** beneath the icon

### 4.1 Coal Hopper Warning

When any fuelled generator's `FuelLevel < 0.2`:

- [ ] Fuel bar turns amber
- [ ] At `FuelLevel < 0.05`: fuel bar turns red; show toast "⚠ COAL-3 LOW FUEL"
- [ ] When generator trips from fuel exhaustion: `GeneratorTripEvent` fires — show  
  `"[GeneratorName] OFFLINE — FUEL EXHAUSTED"` in a red banner
- [ ] Add a **Refuel button** that spends joules: `generator.Refuel(1.0)` (restores to full)

---

## 5. Contract Board Panel

This is the Era 2 spotlight mechanic — the first time-pressure-with-stakes element.

```
ContractBoard
├── ContractSlot_1  (active contract progress)
├── ContractSlot_2
├── ContractSlot_3
└── AvailableContracts
    ├── ContractOffer row × N  (filtered by current time of day)
    └── "No contracts available right now" placeholder
```

### 5.1 Contract Offer Row

Each row shows:
- [ ] Client name
- [ ] Required watts (formatted)
- [ ] Duration in in-game hours
- [ ] Estimated pay (pre-calculated: `durationHours × requiredWatts × contractBaseRate × payMult`)
- [ ] Time window (e.g. "Available 18:00–22:00")
- [ ] Prestige badge if `tier == Prestige`
- [ ] **Accept** button → `GameManager.Contracts.TryAccept(def, gameTime)`
- [ ] Row hidden when outside `availableStartHour–availableEndHour` (compare `GameClock.HourOfDay`)

### 5.2 Active Contract Slot

Once accepted:
- [ ] Client name + progress bar (0 → 100 %)
- [ ] Progress = `ContractInstance.FulfillmentRatio() × 100`
- [ ] Bar colour: green ≥ 80 %, amber 50–79 %, red < 50 %
- [ ] Time remaining label (EndGameTime − current game time)
- [ ] On contract resolve:
  - Success → brief green flash + coin sound + reputation tick-up animation
  - Fail → brief red flash + negative sting + rep tick-down

### 5.3 Contract Clock Widget

Players need to understand *when* contracts are available.

- [ ] Small 24-hour clock face (or horizontal bar from 0→24 h) showing current `GameClock.HourOfDay`
- [ ] Colour bands on the bar for each contract's active window
- [ ] Updates every UI tick (60 Hz)

---

## 6. Apprentice Wage Display

The design bible notes: *"The apprentice's wage shown in coral teaches that passive income is no longer free."*

- [ ] In the financial summary panel (collapsed by default, tap to expand):
  - Passive generators' per-second output in W
  - Apprentice wage: small ongoing penny drain (design value: e.g. 0.0001 pennies/s — tune in playtest)
  - Net income per second in pennies
- [ ] Show wage as a red/coral outgoing line item to draw attention

---

## 7. Gas-Lit Aesthetic

Appendix A Era 2 mockup reference.

- [ ] **Background colour**: `#1C1508` → slightly warmer/lighter than Era 1
- [ ] **Primary text**: `#EDD9A3` (warm parchment)
- [ ] **Penny / cash accent**: `#D4A017` (old gold coin)
- [ ] **Reputation stars**: `#C0A060` (aged brass)
- [ ] **Contract progress bar fill**: `#4CAF50` / `#FF9800` / `#F44336` (green/amber/red)
- [ ] **Warning / alarm**: `#E53935` coral-red
- [ ] Font: retain Georgia/Bodoni from Era 1 but slightly larger UI elements (prosperity aesthetic)
- [ ] Gas-lamp flicker particle effect on background (optional, low-priority)

---

## 8. Era Transition Animation

Fires on `EraAdvancedEvent(from:1, to:2)`:

- [ ] Slow fade to sepia → brief black → fade in to gas-lit palette
- [ ] Title card: *"The Demonstration — 9th October 1851"*
- [ ] Subtitle: *"Your first customer has arrived."*
- [ ] Duration: ~3 seconds; skip button for subsequent playthroughs

---

## 9. Demand Visibility (Tutorial Moment)

Era 2's new concept is "demand isn't constant." The player must *see* demand to learn.

- [ ] Add a **Demand Indicator** to the HUD: `"Demand: 1.24 kW"` — reads `SimulationTick.Instance.Grid.TotalDemandWatts`
- [ ] Add an **Output Indicator**: `"Output: 0.82 kW"` — reads `TotalOutputWatts`
- [ ] When output < demand: output label turns red; show tooltip *"Insufficient power — contracts at risk"*
- [ ] First time output < demand: show tutorial overlay explaining the shortfall

---

## 10. Demand Model Wiring

The `DemandModel` already implements the daily curve. You need to:

- [ ] Set `SimulationTick.Instance.Demand.BaseDemandWatts = Era2Data.BaseDemandWatts` (1 000 W)
  when `EraAdvancedEvent` fires for Era 2
- [ ] (Optional) Create a `DemandModelConfig` MonoBehaviour that reads this value from the
  `Era2Config` ScriptableObject so it's tuneable without code changes

---

## 11. Weather Glass (Forecasting Hint)

Appendix A notes: *"The weather glass hint plants the seed of forecasting."*

This is a non-functional Easter egg in Era 2 — it becomes mechanical in Era 3.

- [ ] Add a small barometer/weather glass sprite to the UI corner
- [ ] Tooltip: *"A barometer. Might the weather affect demand someday?"*

---

## 12. Testing Checklist

- [ ] Era 1 → Era 2 transition fires correctly on first contract completion
- [ ] Dual-currency bar shows joules + pennies + reputation
- [ ] All five contract types appear at the correct in-game hours
- [ ] Accepting a contract records output correctly; progress bar updates each second
- [ ] Contract completes → pennies paid correctly: `durationHours × requiredWatts × 0.001 × payMult`
- [ ] Contract fails (output below 50 %) → -2 reputation applied; partial pay issued
- [ ] Prestige contract (Great Exhibition) pays 1.5× base rate
- [ ] CoalFiredBoiler fuel drains; warning at 20 %; trips at 0 %; refuel restores it
- [ ] Generator count and contract state survive save / load cycle
- [ ] Offline joules awarded correctly for medium absence (5 min–6 h), using era factor 1.0 and tau 4 h
- [ ] Game runs at stable 60 fps on emulated low-end device with all 5 generator types active

---

## 13. What the Code Does NOT Include (Needs Unity Work)

- Apprentice wage as an ongoing economy drain — add a small `Update` deduction in a new `ExpenseManager`
- ContractBoard filtering by time of day — loop over `ContractDefinition` assets and check `GameClock.HourOfDay`
- All prefabs, sprites, audio clips
- Era transition animation sequence
- Demand model activation on `EraAdvancedEvent`
- Weather glass sprite / tooltip
- Inspector references (drag new generator/contract assets into GameManager catalogue arrays)
