# CLAUDE.md

Project briefing for Claude Code and other AI agents working on this repository.

## What this project is

An incremental idle game called **Idle Power Generator Simulator**, in the vein of *Antimatter Dimensions* and *Exponential Idle*. The player runs a power station, scaling from a hand-cranked generator in 1820 (Era 1) to a planetary fusion grid in 2080 (Era 11).

The full design is in `docs/design_bible.docx` — a living document covering era progression, mechanics, formulas, UI grammar, and the eleven-era visual mockup gallery. **Read it before doing substantive design or implementation work.** Most "how should this work?" questions are answered there.

## Stack & target platform

- **Engine**: Unity (LTS — currently 2022 LTS or later)
- **Language**: C# (`.NET Standard 2.1`)
- **Target**: Android mobile, portrait orientation, minimum 380dp width
- **Tertiary**: iOS later; desktop never

## Repository structure

```
/Assets/
  /Scripts/
    /Simulation/      # Headless sim — frequency, dispatch, dynamics, demand
    /Economy/         # Cash, RP, OXP, reputation, prestige currencies
    /Contracts/       # Contract lifecycle and dispatch
    /Era/             # Era state, transitions, unlocked mechanics
    /Persistence/     # Save/load, versioned migrations, offline catch-up
    /UI/              # MonoBehaviours, per-era views, presenters
  /ScriptableObjects/ # Generator definitions, upgrade defs, era configs
  /Prefabs/
  /Scenes/
/Packages/
/docs/
  design_bible.docx   # The canonical design reference
  README.md           # Player-facing project description
```

If this structure doesn't exist yet, follow it when creating new files. If a meaningfully different structure is already in place, follow what's there and update this file.

## Architecture principles

- **Sim is headless and deterministic.** The `Simulation/` assembly never references `UnityEngine.UI` or any presentation type. It must be runnable in tests with no scene loaded. UI subscribes to sim events; sim never reaches into UI.
- **Event bus between sim and UI.** Faults, dispatch actions, contract resolutions, era transitions — all flow through a typed pub/sub. Adding a new event type goes in `Simulation/Events/` and is consumed by relevant UI presenters.
- **ScriptableObjects for content, code for systems.** Generator capacities, upgrade costs, era configs, contract templates — all live as `ScriptableObject` assets so designers can tune without recompiling. Behaviour logic lives in plain C# classes.
- **Sim tick: 1 Hz. UI tick: 60 Hz.** Grid dynamics need 1-second resolution; UI smooths over that. Don't increase sim tick without a real reason — it's a performance budget choice for low-end Android.
- **Determinism + seeded RNG.** Every random concern (weather, faults, market noise) has its own `System.Random` seeded from a per-session seed. This is non-negotiable for save reproducibility.

## Numerical conventions

- `double` for power, energy, time, frequency — never `float`. Late-game numbers exceed `float` precision.
- `decimal` for money the player sees. Eliminates rounding artefacts in UI displays.
- For numbers above ~`1e15` W (Era 9+), use **BreakInfinity.cs** (the same library Antimatter Dimensions uses — open source, MIT-licensed). Don't write a custom BigNumber.
- Display rule: 3 significant figures, largest applicable SI prefix. `1,247,000 W` → `"1.25 MW"`. Animate prefix transitions; never strip them.

## Save format & versioning

- JSON or MessagePack, gzip-compressed.
- **Every save has a `version: N` field from day one.** Add a migration function for every schema change. Migrations chain from save version to current.
- Saves include: full sim state, era progress, owned upgrades (bit field), active contracts, RNG seed, lifetime stats.
- Offline progression uses tiered fidelity — see `docs/design_bible.docx` §7. Don't try to fast-forward the full sim at 1 Hz across hours of absence; use the macroscopic integral approach for absences > 6 hours.

## What to do

- **Read the bible before designing anything substantive.** It encodes hard-won decisions across many sessions. The Open Questions section lists what's deliberately undecided.
- **Match the era-shifting UI grammar.** Each era has a distinct aesthetic; see the mockup gallery (Appendix A). Don't introduce a new era's UI in another era's visual language.
- **Preserve colour grammar across eras.** Green = healthy, amber = caution, red = alarm, gold = achievement. Specific shades evolve; meaning stays constant.
- **Use plain English in player-facing UI.** "Reserve below margin" not "RESERVE_VIOLATION". The game has historical voice; numbers wrapped in stories.
- **Surface warnings with one-tap remediations.** Every alert should offer an immediate path forward. See the coal-hopper pattern in Era 2.
- **Generous tap targets on mobile.** Minimum 44dp height, 8dp vertical spacing. Cost labels and card body should both be tappable, doing the same thing.

## What not to do

- **Don't hardcode era-specific values in shared code.** Generator capex, demand multipliers, fuel costs — these go in ScriptableObjects, not constants.
- **Don't simulate offline faults.** Players should never lose progress to something that happened while they slept. The offline catch-up either auto-resolves benignly or snapshots and offers retroactive replay. See bible §10.5.
- **Don't expose raw equation language in the UI.** The player should never see "swing equation", "RoCoF" until Era 6 at earliest, and even then in plain terms. Mechanics arrive through felt experience, not jargon.
- **Don't introduce multiple new mechanics in one era.** The "one new concept per era" rule is the spine of the onboarding design. If a feature seems to require two new concepts simultaneously, split it across two eras or fold one into existing mechanics.
- **Don't add tutorials before failure.** The Preparedness Dashboard scores appear *after* the player has felt the consequence (a generator fails → MP is introduced). Don't surface a score before it has a story.
- **Don't use `Resources.Load` for content.** Use Addressables or direct `ScriptableObject` references. Saves us pain when builds get large.
- **Don't write custom number-formatting code without checking the existing helper.** There's exactly one place SI-prefix formatting should live; multiple implementations drift.

## Code quality

A linter and formatter (Roslyn analyzers, `.editorconfig`) handle most style concerns. Trust them. Don't pad CLAUDE.md with style rules that tooling enforces.

What tooling can't enforce, and matters:

- **Naming**: `Era1WorkshopView` not `WorkshopUI` — era prefixes on era-specific types make the codebase navigable. Shared types have no era prefix.
- **Comments**: Explain *why*, not *what*. The code shows what. If a constant is `0.93`, a comment should say *why* 0.93 — not "the variance factor".
- **One assertion per test.** Tests should fail with a specific cause. Sim tests in particular should pin one behaviour each.

## Commands

```bash
# Unity build (CLI — adjust path to Unity install)
Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod BuildScript.BuildAndroid

# Run sim tests (headless, no Unity required)
dotnet test tests/Simulation.Tests/

# Run all tests in Unity Test Runner
Unity -batchmode -quit -projectPath . -runTests -testPlatform PlayMode
```

If the project hasn't been set up yet, ignore these — they're the expected commands once the build is wired.

## Working on the design

The design bible (`docs/design_bible.docx`) is a Word document built from a JavaScript source script that lives in `docs/build_bible.js`. When updating the bible:

1. Edit `docs/build_bible.js`, not the .docx directly.
2. Bump the version label and add a changelog entry.
3. Run `node docs/build_bible.js` to regenerate the .docx.
4. Commit both the script and the regenerated .docx.

The bible currently has an Appendix A of era mockups rendered from HTML via Playwright (`docs/render_mockups.py`). Don't hand-edit the embedded PNGs; regenerate from source.

## When stuck

- Design question: check the bible first. If unanswered, the Open Questions section may list it as deferred.
- Implementation question: ask before building. Half of a feature in the wrong direction is worse than no feature.
- Numerical question: check §6 of the bible (Numerical Foundations) and §13 (Research) for cost-curve conventions. Don't invent new growth ratios.
- Don't speculatively refactor systems you weren't asked to touch.
