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
            GameTimeSeconds += Time.deltaTime * GameTimeScale;
        }

        public void SetGameTime(double seconds) => GameTimeSeconds = seconds;

        // Game seconds per real second right now, boost included.
        public float GameTimeScale => _gameTimeScale * SpeedMultiplier;

        // Game seconds per real second without any boost: the pace of what shouldn't speed up
        // with the clock (the street's trade, wages, coal, offline pay).
        public float BaseTimeScale => _gameTimeScale;

        // Set by the 2× boost, which runs the whole clock faster so contracts end on time.
        public float SpeedMultiplier { get; set; } = 1f;
    }
}
