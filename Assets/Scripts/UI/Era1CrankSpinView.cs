using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // Turns the crank wheel. A tap swings it round once; once there is a flywheel, the
    // wheel keeps turning at a speed that follows the flywheel's spin, so the player sees
    // the charge they put in winding down.
    public class Era1CrankSpinView : MonoBehaviour
    {
        // A tap starts at 1440°/s and slows at 4/s: 1440 / 4 = 360°, one full turn.
        [SerializeField] private float _tapKickDegreesPerSecond = 1440f;
        [SerializeField] private float _tapSlowdownPerSecond    = 4f;

        // Speed with the flywheel full to capacity: two turns a second.
        [SerializeField] private float _fullFlywheelDegreesPerSecond = 720f;

        private float _tapSpeed;
        private float _angle;

        private void OnEnable()  => EventBus.Subscribe<TapEvent>(OnTap);
        private void OnDisable() => EventBus.Unsubscribe<TapEvent>(OnTap);

        private void OnTap(TapEvent _) => _tapSpeed = Mathf.Max(_tapSpeed, _tapKickDegreesPerSecond);

        private void Update()
        {
            float dt = Time.deltaTime;
            _tapSpeed *= Mathf.Exp(-_tapSlowdownPerSecond * dt);

            float speed = Mathf.Max(_tapSpeed, FlywheelSpeed());
            // Clockwise, the way a right hand turns a crank.
            _angle = Mathf.Repeat(_angle - speed * dt, 360f);
            transform.localRotation = Quaternion.Euler(0, 0, _angle);
        }

        private float FlywheelSpeed()
        {
            var sim = Simulation.SimulationTick.Instance;
            if (sim == null || sim.Flywheel.CapacityWatts <= 0) return 0f;
            double fraction = sim.Flywheel.SpinAt(sim.SecondsSinceTick) / sim.Flywheel.CapacityWatts;
            return (float)fraction * _fullFlywheelDegreesPerSecond;
        }

        public float CurrentAngle => _angle;
    }
}
