using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdlePowerGen.UI
{
    // A row of tabs along the bottom of a screen, within thumb reach, each showing one page.
    // Hidden pages are faded out rather than deactivated, so their views keep running (lists
    // stay current, cards keep their subscriptions) and switching back is instant.
    public class TabBarView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup[] _pages;
        [SerializeField] private Button[] _tabs;
        [SerializeField] private Image[] _tabBackgrounds;
        [SerializeField] private TMP_Text[] _tabLabels;
        [SerializeField] private Image[] _badges;
        [SerializeField] private Color _selected   = new(0.14f, 0.09f, 0.06f);
        [SerializeField] private Color _unselected = new(0.11f, 0.07f, 0.05f);
        [SerializeField] private Color _selectedText   = Color.white;
        [SerializeField] private Color _unselectedText = Color.gray;

        public int Current { get; private set; } = -1;

        private void Awake()
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                _tabs[i].onClick.AddListener(() => Show(index));
            }
            Show(0);
        }

        public void Show(int index)
        {
            if (index < 0 || index >= _pages.Length) return;
            Current = index;
            for (int i = 0; i < _pages.Length; i++)
            {
                bool on = i == index;
                _pages[i].alpha = on ? 1f : 0f;
                _pages[i].interactable = on;
                _pages[i].blocksRaycasts = on;
                if (i < _tabBackgrounds.Length) _tabBackgrounds[i].color = on ? _selected : _unselected;
                if (i < _tabLabels.Length) _tabLabels[i].color = on ? _selectedText : _unselectedText;
            }
        }

        // A dot on a tab: something there wants the player. Never on the tab already open.
        public void SetBadge(int index, bool on, Color colour)
        {
            if (index < 0 || index >= _badges.Length) return;
            bool show = on && index != Current;
            if (_badges[index].gameObject.activeSelf != show) _badges[index].gameObject.SetActive(show);
            _badges[index].color = colour;
        }
    }
}
