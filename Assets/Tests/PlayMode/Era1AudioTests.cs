using NUnit.Framework;
using UnityEngine;

namespace IdlePowerGen.Tests
{
    // Batch-mode runs have no audio device, so these pin the wiring rather than playback.
    public class Era1AudioTests : Era1SceneFixture
    {
        [Test]
        public void CrankButtonHasItsOwnAudioSource() =>
            Assert.That(GameObject.Find("CrankButton").GetComponent<AudioSource>(), Is.Not.Null);

        [Test]
        public void AmbienceLoopsAWorkshopClip()
        {
            var sources = GameObject.Find("_Audio").GetComponents<AudioSource>();
            Assert.That(System.Array.Exists(sources, s => s.loop && s.clip != null), Is.True);
        }

        [Test]
        public void SceneHasAnAudioListener() =>
            Assert.That(Object.FindAnyObjectByType<AudioListener>(), Is.Not.Null);
    }
}
