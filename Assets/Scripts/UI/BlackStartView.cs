using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Controllers;
using IdlePowerGen.Utils;
using Phase = IdlePowerGen.Controllers.BlackStartSession.Phase;
using Kind = IdlePowerGen.Controllers.BlackStartSequence.Kind;

namespace IdlePowerGen.UI
{
    // The black start (bible §10, §15): emergency lighting, the outage timer in red, and one
    // panel per phase, each played by hand: the diesel cranked in rhythm and fired on the
    // stroke, a hydro unit's gates eased up to speed, loads picked up against the island's
    // frequency, and the second unit's start sequence step by step. No cancel, no pause:
    // once it starts the player is committed (§15.3). Lists are built from one template.
    public class BlackStartView : MonoBehaviour
    {
        [SerializeField] private UnitGridController _grid;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _timerLabel;
        [SerializeField] private TMP_Text _phaseLabel;
        [SerializeField] private TMP_Text _situationLabel;
        [SerializeField] private TMP_Text _messageLabel;
        [SerializeField] private Transform _list;
        [SerializeField] private Button _itemTemplate;     // labels: Title, Detail
        [SerializeField] private Button _crank;            // the Era 1 crank, for the diesel
        [SerializeField] private TMP_Text _crankLabel;
        [SerializeField] private HoldButton _gates;        // held down: the oil pump
        [SerializeField] private TMP_Text _gatesLabel;
        [SerializeField] private Image _bar;
        [SerializeField] private TMP_Text _barLabel;
        [SerializeField] private Button _primary;
        [SerializeField] private TMP_Text _primaryLabel;
        [SerializeField] private Button _secondary;
        [SerializeField] private TMP_Text _secondaryLabel;
        [SerializeField] private Button _tertiary;
        [SerializeField] private TMP_Text _tertiaryLabel;
        [SerializeField] private SyncOverlayView _sync;
        [SerializeField] private BandGaugeView _gaugeA;
        [SerializeField] private BandGaugeView _gaugeB;
        [SerializeField] private Slider _lever;            // the hydro gates, or the steam valve
        [SerializeField] private TMP_Text _leverLabel;
        [SerializeField] private Button _fire;
        [SerializeField] private TMP_Text _fireLabel;
        [SerializeField] private Color _ready   = Color.green;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _muted   = Color.gray;
        [SerializeField] private Color _alarm   = Color.red;

        private readonly List<(Button button, TMP_Text title, TMP_Text detail)> _items = new();
        private BlackStartSession _session;

        private void Awake()
        {
            _panel.SetActive(false);
            _itemTemplate.gameObject.SetActive(false);
            // On the press: the crank is a rhythm and the Fire is a moment (the Era 1 crank's
            // turn, its sound and spin included: bible §10, "the player literally returns to
            // the hand-crank of Era 1").
            PressButton.On(_crank).Pressed += () => { _session?.Crank(); EventBus.Publish(new TapEvent(0, 0)); };
            PressButton.On(_fire).Pressed += () => _session?.Fire();
            _lever.onValueChanged.AddListener(Lever);
            _primary.onClick.AddListener(Primary);
            _secondary.onClick.AddListener(Secondary);
            _tertiary.onClick.AddListener(Tertiary);
        }

        private void Lever(float value)
        {
            if (_session == null) return;
            if (_session.Hydro != null && _session.Current == Phase.Cranking) _session.SetGates(value);
            else _session.SetSteamValve(value);
        }

        private void Update()
        {
            var session = _grid != null && _grid.IsCurrentEra ? _grid.BlackStart : null;
            if (session != _session) _session = session;
            bool show = _session != null;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;

            int t = (int)_session.OutageSeconds;
            _timerLabel.text = $"<mspace=0.6em>T+{t / 60:00}:{t % 60:00}</mspace>";
            _messageLabel.text = _session.LastMessage ?? "";
            _situationLabel.text = _session.Cause;

            // The crank's holder carries its caption too: hide the whole thing, not just the wheel.
            var crankHolder = _crank.transform.parent.gameObject;
            crankHolder.SetActive(false);
            _gates.gameObject.SetActive(false);
            _fire.gameObject.SetActive(false);
            _bar.transform.parent.parent.gameObject.SetActive(false);
            _primary.gameObject.SetActive(false);
            _secondary.gameObject.SetActive(false);
            _tertiary.gameObject.SetActive(false);
            _gaugeA.Hide();
            _gaugeB.Hide();
            bool lever = false;
            int items = 0;

            switch (_session.Current)
            {
                case Phase.Triage:
                    _phaseLabel.text = _session.Drill ? "Drill · triage · what survived" : "Triage · what survived";
                    foreach (var u in _session.StartUnits)
                        Item(items++, u.Name, $"{NumberFormatter.FormatWatts(u.Watts)} · {u.Note}", _ready, null);
                    // Bible §15.3: what was lost is shown too, with the reason.
                    foreach (var (name, reason) in _session.Unavailable.Take(4))
                        Item(items++, name, reason, _muted, null);
                    Bar(_session.PhaseSeconds / BlackStartSession.TriageSeconds, "the control room is taking stock");
                    break;

                case Phase.Cranking:
                    if (_session.Chosen == null)
                    {
                        _phaseLabel.text = "Cranking power · choose a black-start unit";
                        for (int i = 0; i < _session.StartUnits.Count; i++)
                        {
                            var u = _session.StartUnits[i];
                            int index = i;
                            Item(items++, u.Name, $"{NumberFormatter.FormatWatts(u.Watts)} · {u.Note}", _ready, () => _session.Choose(index));
                        }
                    }
                    else if (_session.Diesel != null)
                    {
                        _phaseLabel.text = $"Cranking power · {_session.Chosen.Name}";
                        crankHolder.SetActive(true);
                        Diesel(_session.Diesel);
                    }
                    else if (_session.Hydro != null)
                    {
                        _phaseLabel.text = $"Cranking power · {_session.Chosen.Name}";
                        lever = true;
                        Hydro(_session.Hydro);
                    }
                    break;

                case Phase.Island:
                case Phase.Restore:
                    Island(ref items);
                    break;

                case Phase.SecondUnit:
                    lever = SecondUnit(ref items);
                    break;

                case Phase.Synchronise:
                    _phaseLabel.text = "Synchronisation · match it to the island";
                    if (!_sync.IsOpen) _sync.OpenForBlackStart(_session);
                    break;

                case Phase.PostMortem:
                    _phaseLabel.text = _session.Drill ? "Drill · post-mortem" : "Post-mortem";
                    int mins = (int)_session.OutageSeconds;
                    Item(items++, "Outage", $"{mins} minutes dark", _caution, null);
                    Item(items++, "Customer-minutes lost", $"{_session.CustomerMinutes:N0} MW-minutes", _caution, null);
                    if (_session.Drill)
                        Item(items++, "Training", $"a drill, announced: no repute lost · {_session.DrillPerformance:0%} of the training a drill can give",
                             _ready, null);
                    else Item(items++, "Trust", $"{_session.TrustLost:0.#} repute lost", _caution, null);
                    foreach (var f in _session.Factors) Item(items++, "What went wrong", f, _muted, null);
                    Button(_primary, _primaryLabel, "Back to the desk", true);
                    break;
            }
            var leverHolder = _lever.transform.parent.gameObject;
            if (leverHolder.activeSelf != lever) leverHolder.SetActive(lever);
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].button.gameObject.activeSelf != (i < items)) _items[i].button.gameObject.SetActive(i < items);
        }

        // ---- Cranking ----

        private void Diesel(BlackStartCrank d)
        {
            bool stunned = d.Stunned > 0;
            _crankLabel.text = stunned ? "Kicked back · wait…" : d.Ready ? "Keep it turning" : "Turn the crank · keep it in the green";
            _gaugeA.Show("Crank speed", d.InGreen ? "in the green" : d.Speed > BlackStartCrank.GreenHigh ? "too fast" : "too slow",
                         (float)(d.Speed / BlackStartCrank.KickBack), d.InGreen ? _ready : d.Speed > BlackStartCrank.GreenHigh ? _alarm : _caution,
                         (float)(BlackStartCrank.GreenLow / BlackStartCrank.KickBack), (float)(BlackStartCrank.GreenHigh / BlackStartCrank.KickBack),
                         (float)(0.88 / BlackStartCrank.KickBack), 1f);
            // The piston sweeps across with the top in the middle: fire as it crosses.
            float sweep = (float)((d.Piston + 0.5) % 1.0);
            _gaugeB.Show("Piston", d.AtTop ? "at the top" : "", sweep, d.AtTop ? _ready : _muted,
                         (float)(0.5 - BlackStartCrank.FireWindow), (float)(0.5 + BlackStartCrank.FireWindow), asMarker: true);
            Bar(d.Primed / BlackStartCrank.PrimeSeconds, d.Ready ? "compression up · fire it as the piston tops" : "building compression");
            _fire.gameObject.SetActive(true);
            _fire.interactable = d.Ready;
            _fireLabel.text = d.Ready ? "Fire!" : "Fire · build compression first";
            _fireLabel.alpha = d.Ready ? 1f : 0.4f;
            if (d.Message != null) _messageLabel.text = d.Message;
        }

        private void Hydro(BlackStartGates g)
        {
            Lever(g.Opening, g.Tripped <= 0, $"Gate opening · {g.Opening:0%}" + (g.Tripped > 0 ? " · tripped, wait…" : ""));
            const float scale = 1.2f; // the gauge runs to 120 % of rated speed
            _gaugeA.Show("Turbine speed", $"{g.Speed:0%} of rated", (float)(g.Speed / scale),
                         g.AtRated ? _ready : g.Speed > BlackStartGates.RatedHigh ? _alarm : _caution,
                         (float)(BlackStartGates.RatedLow / scale), (float)(BlackStartGates.RatedHigh / scale),
                         (float)(BlackStartGates.Overspeed / scale), 1f);
            // The water behind the gates: the lag that makes the speed overshoot.
            _gaugeB.Show("Water through the turbine", $"{g.Flow:0%}", (float)g.Flow, _muted,
                         (float)(BlackStartGates.HoldOpening - 0.02), (float)(BlackStartGates.HoldOpening + 0.02));
            Bar(g.AtSpeed / BlackStartGates.HoldSeconds, g.AtRated ? "holding rated speed" : "open the gates, then ease off before rated speed");
            if (g.Message != null && g.Tripped > 0) _messageLabel.text = g.Message;
        }

        // Keeps the lever where the session has it: a trip shuts the gates, a retry closes the valve.
        private void Lever(double value, bool on, string label)
        {
            if (Mathf.Abs(_lever.value - (float)value) > 0.001f) _lever.SetValueWithoutNotify((float)value);
            _lever.interactable = on;
            _leverLabel.text = label;
        }

        // ---- The island and restoration ----

        private void Island(ref int items)
        {
            bool island = _session.Current == Phase.Island;
            _phaseLabel.text = island ? "Establishing the island · pick loads it can carry"
                                      : $"Restoration · {_session.Restored:0%} of the city back";
            Bar(_session.IslandLoad / System.Math.Max(1, _session.IslandCapacity),
                $"{NumberFormatter.FormatWatts(_session.IslandLoad)} of {NumberFormatter.FormatWatts(_session.IslandCapacity)} on the island");
            double hz = _session.IslandHz;
            float f = (float)((hz - BlackStartSession.CollapseHz) / BlackStartSession.DipHz);
            bool settled = f > 0.9f;
            _gaugeA.Show("Island frequency", $"{hz:0.00} Hz" + (settled ? " · steady" : " · recovering"), f,
                         settled ? _ready : f > 0.4f ? _caution : _alarm, 0.9f, 1f, 0f, 0.1f);

            foreach (var id in _session.LitBefore.Where(id => _session.ChunksOn(id) < BlackStartSession.Chunks)
                                                 .OrderBy(id => _session.RunningLoadOf(id)).Take(island ? 6 : 8))
            {
                string area = id;
                bool industrial = _session.IsIndustrial(id);
                double running = _session.RunningLoadOf(id) / BlackStartSession.Chunks;
                double inrush = running * (industrial ? BlackStartSession.IndustrialInrush : BlackStartSession.HouseholdInrush);
                bool fits = _session.Fits(id), now = _session.FitsNow(id);
                string verdict = now ? ""
                               : fits ? " · wait for the frequency to recover"
                               : island ? " · too much: the island would go down" : " · needs another unit first";
                Item(items++, $"{_grid.AreaName(id)} · substation {_session.ChunksOn(id) + 1} of {BlackStartSession.Chunks}",
                     $"{NumberFormatter.FormatWatts(running)} running · {NumberFormatter.FormatWatts(inrush)} to start" +
                     (industrial ? " · heavy inrush" : "") + verdict,
                     now ? _ready : _caution, () => _session.Energise(area));
            }
            if (island)
            {
                Button(_primary, _primaryLabel, _session.Energised.Count > 0 || !_session.CanHold
                           ? "The island is holding · start the next unit"
                           : "Nothing fits on it yet · start the next unit", _session.CanHold);
            }
            else
            {
                string finish = _session.Restored >= BlackStartSession.RestoreTarget
                    ? $"Back to normal · {_session.Restored:0%} restored"
                    : _session.OutOfOptions
                        ? $"Back to normal · {_session.Restored:0%} restored, the rest stays dark"
                        : $"Back to normal at 95% · {_session.Restored:0%} so far";
                Button(_primary, _primaryLabel, finish, _session.CanFinish);
                Button(_secondary, _secondaryLabel, _session.OldStationBack ? "Bring on the next unit" : "Bring the old station back", _session.CanBringOn);
            }
        }

        // ---- The second unit ----

        // Returns whether the lever is in use (warming the turbine).
        private bool SecondUnit(ref int items)
        {
            var seq = _session.Sequence;
            var step = _session.CurrentStep;
            _phaseLabel.text = $"Second unit · Unit {_session.SecondUnit?.Number} start sequence";
            for (int i = 0; i < _session.Steps.Count; i++)
            {
                string state = i < _session.StepIndex ? "done" : i == _session.StepIndex ? (_session.StepFailed ? "FAILED" : "now") : "waiting";
                Item(items++, _session.Steps[i].Name, state,
                     i < _session.StepIndex ? _ready : i == _session.StepIndex ? (_session.StepFailed ? _alarm : _caution) : _muted, null);
            }
            if (_session.StepFailed)
            {
                Button(_primary, _primaryLabel, "Retry · the step again", true);
                Button(_secondary, _secondaryLabel, "Bypass · skip it, may damage the unit", true);
                Button(_tertiary, _tertiaryLabel, "Abandon this unit", true);
                return false;
            }
            if (seq == null || step == null) return false;
            if (seq.Message != null) _messageLabel.text = seq.Message;

            switch (step.Kind)
            {
                case Kind.Breakers:
                    Bar((double)seq.BreakersClosed / BlackStartSequence.BreakerOrder.Length, "close the breakers · supply before load");
                    foreach (var b in seq.BreakersShown)
                    {
                        string breaker = b;
                        bool closed = seq.IsClosed(b);
                        Item(items++, $"{b} breaker", closed ? "closed" : "open · tap to close", closed ? _ready : _caution,
                             closed ? null : () => _session.CloseAuxiliary(breaker));
                    }
                    return false;

                case Kind.OilPump:
                    _gates.gameObject.SetActive(true);
                    _gatesLabel.text = _gates.Held ? "Pumping…" : "Hold to run the oil pump";
                    _session.HoldPump(_gates.Held);
                    bool inBand = seq.Pressure >= BlackStartSequence.OilLow && seq.Pressure <= BlackStartSequence.OilHigh;
                    _gaugeA.Show("Lube oil pressure", inBand ? "in the band" : seq.Pressure > BlackStartSequence.OilHigh ? "too high" : "low",
                                 (float)seq.Pressure, inBand ? _ready : seq.Pressure > BlackStartSequence.OilHigh ? _alarm : _caution,
                                 (float)BlackStartSequence.OilLow, (float)BlackStartSequence.OilHigh, 0.92f, 1f);
                    Bar(seq.OilTime / BlackStartSequence.OilSeconds, "hold the pressure in the band");
                    return false;

                default:
                    Lever(seq.Valve, true, $"Steam valve · {seq.Valve:0%} open" +
                                                (seq.Valve > BlackStartSequence.SafeOpening ? " · faster than the casing likes" : ""));
                    _gaugeA.Show("Turbine temperature", $"{seq.Temperature:0%}", (float)seq.Temperature, _caution);
                    _gaugeB.Show("Casing stress", seq.Stress > 0.6 ? "easing off now!" : seq.Stress > 0 ? "building" : "none",
                                 (float)seq.Stress, seq.Stress > 0.6 ? _alarm : seq.Stress > 0 ? _caution : _ready, 0f, 0f, 0.6f, 1f);
                    Bar(seq.Temperature, "warm it through · half open never stresses it");
                    return true;
            }
        }

        // ---- Shared ----

        private readonly List<System.Action> _actions = new();

        private void Item(int i, string title, string detail, Color colour, System.Action action)
        {
            while (_items.Count <= i)
            {
                var b = Instantiate(_itemTemplate, _list);
                int index = _items.Count;
                b.onClick.AddListener(() => { if (index < _actions.Count) _actions[index]?.Invoke(); });
                var labels = b.GetComponentsInChildren<TMP_Text>();
                _items.Add((b, labels[0], labels[1]));
                _actions.Add(null);
            }
            var item = _items[i];
            item.title.text = title;
            item.detail.text = detail;
            item.detail.color = colour;
            item.button.interactable = action != null;
            _actions[i] = action;
        }

        private void Bar(double fraction, string label)
        {
            _bar.transform.parent.parent.gameObject.SetActive(true);
            _bar.fillAmount = Mathf.Clamp01((float)fraction);
            _barLabel.text = label;
        }

        private static void Button(Button b, TMP_Text label, string text, bool on)
        {
            b.gameObject.SetActive(true);
            label.text = text;
            b.interactable = on;
            // The button's tint alone hardly shows on the emergency lighting's dark panels.
            label.alpha = on ? 1f : 0.4f;
        }

        private void Primary()
        {
            if (_session == null) return;
            switch (_session.Current)
            {
                case Phase.Island:     _session.IslandHolding(); break;
                case Phase.Restore:    _session.FinishRestoration(); break;
                case Phase.SecondUnit: _session.Retry(); break;
                case Phase.PostMortem: _session.Close(); break;
            }
        }

        private void Secondary()
        {
            if (_session == null) return;
            if (_session.Current == Phase.Restore) _session.BringOnNext();
            else if (_session.Current == Phase.SecondUnit) _session.Bypass();
        }

        private void Tertiary()
        {
            if (_session?.Current == Phase.SecondUnit) _session.Abandon();
        }
    }
}
