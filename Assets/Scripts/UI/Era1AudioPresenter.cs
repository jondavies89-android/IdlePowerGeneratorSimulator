using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // Era 1 sound (todo §8): a ratchet click per crank turn, a chime per purchase, a stinger
    // per milestone, and a low fire-and-workshop loop underneath. Browsers start audio only
    // after the first tap; Unity resumes the ambience then on its own.
    public class Era1AudioPresenter : MonoBehaviour
    {
        [SerializeField] private AudioSource _crankSource;
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _ambienceSource;
        [SerializeField] private AudioClip _crankClip;
        [SerializeField] private AudioClip _upgradeClip;
        [SerializeField] private AudioClip _milestoneClip;

        private void Awake()
        {
            EventBus.Subscribe<TapEvent>(OnTap);
            EventBus.Subscribe<UpgradePurchasedEvent>(OnUpgrade);
            EventBus.Subscribe<GeneratorPurchasedEvent>(OnGenerator);
            EventBus.Subscribe<MilestoneReachedEvent>(OnMilestone);
        }

        private void Start()
        {
            if (_ambienceSource != null && !_ambienceSource.isPlaying) _ambienceSource.Play();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TapEvent>(OnTap);
            EventBus.Unsubscribe<UpgradePurchasedEvent>(OnUpgrade);
            EventBus.Unsubscribe<GeneratorPurchasedEvent>(OnGenerator);
            EventBus.Unsubscribe<MilestoneReachedEvent>(OnMilestone);
        }

        private void OnTap(TapEvent _)
        {
            // Slight pitch drift so rapid cranking doesn't sound like a machine gun.
            // Presentation-only randomness, so UnityEngine.Random rather than a seeded sim RNG.
            _crankSource.pitch = Random.Range(0.92f, 1.08f);
            _crankSource.PlayOneShot(_crankClip);
        }

        private void OnUpgrade(UpgradePurchasedEvent _)     => _sfxSource.PlayOneShot(_upgradeClip);
        private void OnGenerator(GeneratorPurchasedEvent _) => _sfxSource.PlayOneShot(_upgradeClip, 0.6f);
        private void OnMilestone(MilestoneReachedEvent _)   => _sfxSource.PlayOneShot(_milestoneClip);
    }
}
