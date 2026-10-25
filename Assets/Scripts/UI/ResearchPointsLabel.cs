using TMPro;
using UnityEngine;

namespace IdlePowerGen.UI
{
    // The research points held, as the Era 4 mockup's RESEARCH box shows them: "2,840 RP".
    public class ResearchPointsLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        private void Update()
        {
            var gm = Managers.GameManager.Instance;
            if (gm != null) _label.text = $"{gm.Ledger.ResearchPoints:N0} RP";
        }
    }
}
