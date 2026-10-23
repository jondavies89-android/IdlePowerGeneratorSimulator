using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdlePowerGen.Tests
{
    public class Era1SceneSmokeTests : Era1SceneFixture
    {
        // Three sim ticks with nothing logged at error level is the Play Mode equivalent of
        // "Console is clean" for this scene.
        [UnityTest]
        public IEnumerator SceneRunsThreeTicksWithoutErrors()
        {
            int ticks = 0;
            void Count(SimTickEvent _) => ticks++;
            Utils.EventBus.Subscribe<SimTickEvent>(Count);

            yield return new WaitForSeconds(3.2f);

            Utils.EventBus.Unsubscribe<SimTickEvent>(Count);
            Assert.That(ticks, Is.GreaterThanOrEqualTo(3));
        }
    }
}
