using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The contract managers hired this era, one card each: which client it books (step
    // through them with the arrows, or leave it to pick the best-paying one the works can
    // supply), on or off, and what it's doing. Switched off, its Start button books the same
    // client by hand at full pay. Hidden until the first manager is hired.
    public class ContractManagersView : MonoBehaviour
    {
        [SerializeField] private Controllers.ContractDesk _desk;
        [SerializeField] private GameObject _rowTemplate;  // children: Name, Toggle, Prev, Choice, Next, Status, Start
        [SerializeField] private Transform _content;
        [SerializeField] private TMP_Text _headerLabel;
        [SerializeField] private Color _text    = Color.white;
        [SerializeField] private Color _muted   = Color.gray;
        [SerializeField] private Color _healthy = Color.green;
        [SerializeField] private Color _caution = Color.yellow;

        private sealed class Row
        {
            public GameObject Root;
            public TMP_Text Name, Choice, Status, ToggleLabel;
            public Button Toggle, Prev, Next, Start;
        }

        private readonly List<Row> _rows = new();
        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;

        private void Awake() => _rowTemplate.SetActive(false);

        private Row Make(int index)
        {
            var go = Instantiate(_rowTemplate, _content);
            go.name = $"Manager_{index + 1}";
            TMP_Text Label(string path) => go.transform.Find(path).GetComponent<TMP_Text>();
            Button Btn(string path) => go.transform.Find(path).GetComponent<Button>();
            var row = new Row
            {
                Root = go,
                Name = Label("Top/Name"), ToggleLabel = Label("Top/Toggle/Label"), Toggle = Btn("Top/Toggle"),
                Prev = Btn("Choose/Prev"), Choice = Label("Choose/Choice"), Next = Btn("Choose/Next"),
                Status = Label("Bottom/Status"), Start = Btn("Bottom/Start"),
            };
            row.Toggle.onClick.AddListener(() => { _desk.ToggleManager(index); _since = RefreshInterval; });
            row.Prev.onClick.AddListener(() => { _desk.CycleManager(index, -1); _since = RefreshInterval; });
            row.Next.onClick.AddListener(() => { _desk.CycleManager(index, +1); _since = RefreshInterval; });
            row.Start.onClick.AddListener(() => { _desk.StartManually(index); _since = RefreshInterval; });
            return row;
        }

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_desk == null) return;

            int hired = _desk.IsCurrentEra ? _desk.ManagersHired : 0;
            if (_headerLabel != null)
            {
                if (_headerLabel.gameObject.activeSelf != hired > 0) _headerLabel.gameObject.SetActive(hired > 0);
                _headerLabel.text = $"Contract managers · {hired}";
            }
            while (_rows.Count < hired) _rows.Add(Make(_rows.Count));
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool show = i < hired;
                if (row.Root.activeSelf != show) row.Root.SetActive(show);
                if (show) Fill(row, i);
            }
        }

        private void Fill(Row row, int i)
        {
            var m = _desk.Manager(i);
            var target = _desk.ManagerTarget(i);
            row.Name.text = $"Manager {i + 1}";
            row.ToggleLabel.text = m.On ? "On" : "Off";
            row.ToggleLabel.color = m.On ? _healthy : _muted;
            row.Choice.text = string.IsNullOrEmpty(m.ContractId) ? "The best paying it can supply"
                            : target != null ? target.clientName : "—";
            bool canChoose = _desk.ManagerChoices.Count > 0;
            row.Prev.interactable = row.Next.interactable = canChoose;

            var (status, warn) = Status(i, m, target);
            row.Status.text = status;
            row.Status.color = warn ? _caution : _muted;

            bool manual = !m.On;
            if (row.Start.gameObject.activeSelf != manual) row.Start.gameObject.SetActive(manual);
            if (manual)
                row.Start.interactable = target != null && _desk.HasFreeSlot && _desk.IsOffered(target);
        }

        private (string, bool) Status(int i, Economy.ContractManager m, Models.ContractDefinition target)
        {
            var running = _desk.ManagerRunning(i);
            if (running != null)
                return ($"Running {running.Definition.clientName} · " +
                        $"{Managers.Money.Format(running.CommissionPaid, _desk.EraNumber)} commission so far", false);
            double spare = _desk.SpareForContracts;
            if (!m.On)
            {
                if (target == null) return ("Off. Nothing it could book in full right now.", false);
                if (spare < target.requiredWatts)
                    return ($"Off. Start {target.clientName} yourself, though you'd be " +
                            $"{NumberFormatter.FormatWatts(target.requiredWatts - System.Math.Max(0, spare))} short.", true);
                return ($"Off. Start {target.clientName} yourself and keep the whole fee.", false);
            }
            if (target == null)
                return ("Waiting: nothing the works can supply in full yet.", true);
            if (!_desk.IsOffered(target))
                return ($"Waiting: {target.clientName} is already running.", false);
            if (!_desk.HasFreeSlot)
                return ("Waiting for a free contract slot.", true);
            if (spare < target.requiredWatts)
                return ($"Waiting for {NumberFormatter.FormatWatts(target.requiredWatts - System.Math.Max(0, spare))} more spare power.", true);
            return ($"Booking {target.clientName}.", false);
        }
    }
}
