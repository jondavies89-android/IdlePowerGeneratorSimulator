using UnityEngine;

namespace IdlePowerGen.Utils
{
    // In-game time runs at GameTimeScale × real time (default 60: 1 real second = 1 game minute).
    public class GameClock : MonoBehaviour
    {
        public static GameClock Instance { get; private set; }

        [SerializeField] private float _gameTimeScale = 60f;

        public double GameTimeSeconds { get; private set; }

        // Convenience accessors
        public float HourOfDay  => (float)((GameTimeSeconds / 3600.0) % 24.0);
        public int   DayOfWeek  => (int)((GameTimeSeconds / 86400.0) % 7.0);

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            GameTimeSeconds += Time.deltaTime * _gameTimeScale;
        }

        public void SetGameTime(double seconds) => GameTimeSeconds = seconds;

        public float GameTimeScale => _gameTimeScale;
    }
}
