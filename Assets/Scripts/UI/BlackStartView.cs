using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;
using Phase = IdlePowerGen.Controllers.BlackStartSession.Phase;

namespace IdlePowerGen.UI
{
    // The black start (bible §10, §15): emergency lighting, the outage timer in red, and one
    // panel per phase. No cancel, no pause: once it starts the player is committed (§15.3).
    // Lists (start units, loads to energise) are built from one button template.
    public class BlackStartView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _timerLabel;
        [SerializeField] private TMP_Text _phaseLabel;
        [SerializeField] private TMP_Text _situationLabel;
        [SerializeField] private TMP_Text _messageLabel;
        [SerializeField] private Transform _list;
        [SerializeField] private Button _itemTemplate;     // labels: Title, Detail
        [SerializeField] private Button _crank;            // the Era 1 crank, for the diesel
        [SerializeField] private TMP_Text _crankLabel;
        [SerializeField] private HoldButton _gates;        // held open, for hydro
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
        [SerializeField] private Color _ready   = Color.green;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _muted   = Color.gray;

        private readonly List<(Button button, TMP_Text title, TMP_Text detail)> _items = new();
        private Controllers.BlackStartSession _session;

        private void Awake()
        {
            _panel.SetActive(false);
            _itemTemplate.gameObject.SetActive(false);
            // The Era 1 crank's turn, its sound and spin included (bible §10: "the player
            // literally returns to the hand-crank of Era 1").
            _crank.onClick.AddListener(() => { _session?.Crank(); EventBus.Publish(new TapEvent(0, 0)); });
            _primary.onClick.AddListener(Primary);
            _secondary.onClick.AddListener(Secondary);
            _tertiary.onClick.AddListener(Tertiary);
        }

        private void Update()
        {
            var session = _grid != null && _grid.IsCurrentEra ? _grid.BlackStart : null;
            if (session != _session) _session = session;
            bool show = _session != null;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;

            if (_gates.Held) _session.HoldGates(Time.unscaledDeltaTime);

            int t = (int)_session.OutageSeconds;
            _timerLabel.text = $"<mspace=0.6em>T+{t / 60:00}:{t % 60:00}</mspace>";
            _messageLabel.text = _session.LastMessage ?? "";
            _situationLabel.text = _session.Cause;

            _crank.gameObject.SetActive(false);
            _gates.gameObject.SetActive(false);
            _bar.transform.parent.parent.gameObject.SetActive(false);
            _primary.gameObject.SetActive(false);
            _secondary.gameObject.SetActive(false);
            _tertiary.gameObject.SetActive(false);
            int items = 0;

            switch (_session.Current)
            {
                case Phase.Triage:
                    _phaseLabel.text = "Triage · what survived";
                    foreach (var u in _session.StartUnits)
                        Item(items++, u.Name, $"{NumberFormatter.FormatWatts(u.Watts)} · {u.Note}", _ready, null);
                    Bar(_session.PhaseSeconds / Controllers.BlackStartSession.TriageSeconds, "the control room is taking stock");
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
                    else if (_session.Chosen.Crank)
                    {
                        _phaseLabel.text = $"Cranking power · {_session.Chosen.Name}";
                        _crank.gameObject.SetActive(true);
                        _crankLabel.text = $"Turn the crank · {_session.Taps} of {_session.TapsNeeded}";
                    }
                    else
                    {
                        _phaseLabel.text = $"Cranking power · {_session.Chosen.Name}";
                        _gates.gameObject.SetActive(true);
                        _gatesLabel.text = "Hold the gates open";
                        Bar(_session.HeldSeconds / Controllers.BlackStartSession.HydroHoldSeconds, "spinning up");
                    }
                    break;

                case Phase.Island:
                case Phase.Restore:
                    bool island = _session.Current == Phase.Island;
                    _phaseLabel.text = island ? "Establishing the island · pick loads it can carry"
                                              : $"Restoration · {_session.Restored:0%} of the city back";
                    Bar(_session.IslandLoad / System.Math.Max(1, _session.IslandCapacity),
                        $"{NumberFormatter.FormatWatts(_session.IslandLoad)} of {NumberFormatter.FormatWatts(_session.IslandCapacity)} on the island");
                    foreach (var id in _session.LitBefore.Where(id => _session.ChunksOn(id) < Controllers.BlackStartSession.Chunks)
                                                         .OrderBy(id => _session.RunningLoadOf(id)).Take(island ? 6 : 8))
                    {
                        string area = id;
                        bool industrial = _session.IsIndustrial(id);
                        double running = _session.RunningLoadOf(id) / Controllers.BlackStartSession.Chunks;
                        double inrush = running * (industrial ? Controllers.BlackStartSession.IndustrialInrush
                                                              : Controllers.BlackStartSession.HouseholdInrush);
                        bool fits = _session.IslandLoad + inrush <= _session.IslandCapacity;
                        Item(items++, $"{_grid.AreaName(id)} · substation {_session.ChunksOn(id) + 1} of {Controllers.BlackStartSession.Chunks}",
                             $"{NumberFormatter.FormatWatts(running)} running · {NumberFormatter.FormatWatts(inrush)} to start" +
                             (industrial ? " · heavy inrush" : ""), fits ? _ready : _caution, () => _session.Energise(area));
                    }
                    if (island)
                    {
                        Button(_primary, _primaryLabel, "The island is holding · start the next unit", _session.Energised.Count > 0);
                    }
                    else
                    {
                        Button(_primary, _primaryLabel, "Back to normal · 95% restored", _session.CanFinish);
                        Button(_secondary, _secondaryLabel, _session.OldStationBack ? "Bring on the next unit" : "Bring the old station back", _session.CanBringOn);
                    }
                    break;

                case Phase.SecondUnit:
                    _phaseLabel.text = $"Second unit · Unit {_session.SecondUnit?.Number} start sequence";
                    for (int i = 0; i < _session.Steps.Count; i++)
                    {
                        var s = _session.Steps[i];
                        string state = i < _session.StepIndex ? "done"
                                     : i == _session.StepIndex ? (_session.StepFailed ? "FAILED" : $"{System.Math.Max(0, s.Seconds - _session.StepElapsed):0} min")
                                     : "waiting";
                        Item(items++, s.Name, state, i < _session.StepIndex ? _ready : i == _session.StepIndex && _session.StepFailed ? _caution : _muted, null);
                    }
                    Item(items++, "Ready to synchronise", _session.StepIndex >= _session.Steps.Count ? "now" : "waiting", _muted, null);
                    if (_session.StepFailed)
                    {
                        Button(_primary, _primaryLabel, "Retry · a minute lost", true);
                        Button(_secondary, _secondaryLabel, "Bypass · quicker, may damage it", true);
                        Button(_tertiary, _tertiaryLabel, "Abandon this unit", true);
                    }
                    break;

                case Phase.Synchronise:
                    _phaseLabel.text = "Synchronisation · match it to the island";
                    if (!_sync.IsOpen) _sync.OpenForBlackStart(_session);
                    break;

                case Phase.PostMortem:
                    _phaseLabel.text = "Post-mortem";
                    int mins = (int)_session.OutageSeconds;
                    Item(items++, "Outage", $"{mins} minutes dark", _caution, null);
                    Item(items++, "Customer-minutes lost", $"{_session.CustomerMinutes:N0} MW-minutes", _caution, null);
                    Item(items++, "Trust", $"{_session.TrustLost:0.#} repute lost", _caution, null);
                    foreach (var f in _session.Factors) Item(items++, "What went wrong", f, _muted, null);
                    Button(_primary, _primaryLabel, "Back to the desk", true);
                    break;
            }
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].button.gameObject.activeSelf != (i < items)) _items[i].button.gameObject.SetActive(i < items);
        }

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
