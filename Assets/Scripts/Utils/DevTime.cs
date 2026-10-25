namespace IdlePowerGen.Utils
{
    // The DEV strip's time acceleration: the sim tick and the game clock run this many times
    // faster than real time. Kept apart from Time.timeScale because Unity clamps a frame's
    // scaled delta to Time.maximumDeltaTime, which caps timeScale at about 20× at 60 fps,
    // and because UI tweens and the minigames should keep to real time.
    public static class DevTime
    {
        public const float MinMultiplier = 1f;
        public const float MaxMultiplier = 1000f;

        private static float _multiplier = 1f;

        public static float Multiplier
        {
            get => _multiplier;
            set => _multiplier = value < MinMultiplier ? MinMultiplier : value > MaxMultiplier ? MaxMultiplier : value;
        }
    }
}
