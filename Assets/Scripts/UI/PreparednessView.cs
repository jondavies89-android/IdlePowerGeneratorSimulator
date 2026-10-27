using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The Preparedness Dashboard (bible §11, §14). It starts empty: each score joins it after
    // the failure that gives it a story (CLAUDE.md: no tutorials before failure), pulsing for
    // a while, with the first thing to do about it. Maintenance and fuel in Era 4, redundancy
    // in Era 5.
    public class PreparednessView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private GameObject _panel;
        [SerializeField] private GameObject[] _rows;      // MP, FRP, RP
        [SerializeField] private TMP_Text[] _values;
        [SerializeField] private Image[] _fills;
        [SerializeField] private Button[] _actions;
        [SerializeField] private TMP_Text[] _actionLabels;
        [SerializeField] private GameObject _introPanel;
        [SerializeField] private TMP_Text _introTitle;
        [SerializeField] private TMP_Text _introStory;
        [SerializeField] private Button _introButton;
        [SerializeField] private Color _healthy = Color.green;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _alarm   = Color.red;

        public static readonly string[] Scores =
            { Data.Era4Data.MaintenanceScore, Data.Era4Data.FuelScore, Data.Era5Data.RedundancyScore, Data.Era6Data.TrainingScore };
        private readonly Dictionary<string, float> _pulse = new();

        private Controllers.Era6NationalGridController National => _grid as Controllers.Era6NationalGridController;

        private void Awake()
        {
            EventBus.Subscribe<ScoreIntroducedEvent>(OnIntroduced);
            _introPanel.SetActive(false);
            _introButton.onClick.AddListener(() => _introPanel.SetActive(false));
            _actions[0].onClick.AddListener(() => _grid.ServiceOverdue());
            _actions[1].onClick.AddListener(() => _grid.OrderCoal());
            _actions[2].onClick.AddListener(() => _grid.FitBlackStart());
            if (_actions.Length > 3) _actions[3].onClick.AddListener(() => National?.StartDrill());
        }

        private void OnDestroy() => EventBus.Unsubscribe<ScoreIntroducedEvent>(OnIntroduced);

        private void OnIntroduced(ScoreIntroducedEvent e)
        {
            if (_grid == null || !_grid.IsCurrentEra) return;
            _pulse[e.Score] = 8f;
            _introTitle.text = $"New on the dashboard: {e.Title}";
            _introStory.text = e.Story;
            _introPanel.SetActive(true);
        }

        private void Update()
        {
            if (_grid == null) return;
            bool any = false;
            for (int i = 0; i < Scores.Length && i < _rows.Length; i++)
            {
                // Training belongs to Era 6's drills: it shows only on the screen that can act on it.
                bool on = Controllers.UnitGridController.IsIntroduced(Scores[i]) && (i != 3 || National != null);
                any |= on;
                if (_rows[i].activeSelf != on) _rows[i].SetActive(on);
                if (!on) continue;
                double score = i == 0 ? _grid.MaintenanceScore : i == 1 ? _grid.FuelScore
                             : i == 2 ? _grid.RedundancyScore : _grid.TrainingScore;
                _values[i].text = $"{score:0}";
                _fills[i].fillAmount = (float)(score / 100);
                Color c = score >= 70 ? _healthy : score >= 40 ? _caution : _alarm;
                _fills[i].color = c;
                if (_pulse.TryGetValue(Scores[i], out float p) && p > 0)
                {
                    _pulse[Scores[i]] = p - Time.unscaledDeltaTime;
                    float glow = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                    _values[i].color = Color.Lerp(c, Color.white, glow * 0.6f);
                }
                else _values[i].color = c;

                switch (i)
                {
                    case 0:
                        int overdue = 0; foreach (var _ in _grid.OverdueUnits) overdue++;
                        _actionLabels[0].text = overdue > 0 ? $"Service {overdue} overdue · {Managers.Money.Format(_grid.ServiceOverdueCost)}"
                                                            : "Every unit in date";
                        _actions[0].interactable = overdue > 0;
                        break;
                    case 1:
                        _actionLabels[1].text = $"{_grid.CoalDaysOfCover:0.#} days of coal · order {Managers.Money.Format(_grid.ManualOrderCost)}";
                        _actions[1].interactable = _grid.HasCoalUnits;
                        break;
                    case 2:
                        var next = _grid.NextToFit;
                        _actionLabels[2].text = next != null
                            ? $"Fit {_grid.BoardName(next) ?? $"Unit {next.Number}"} for black start · {Managers.Money.Format(_grid.FitCost(next))}"
                            : "Every unit that can be is fitted";
                        _actions[2].interactable = next != null;
                        break;
                    case 3:
                        _actionLabels[3].text = $"Schedule a black-start drill · {Managers.Money.Format(National.DrillCost)}";
                        _actions[3].interactable = National.CanDrill;
                        break;
                }
            }
            if (_panel.activeSelf != any) _panel.SetActive(any);
        }
    }
}
