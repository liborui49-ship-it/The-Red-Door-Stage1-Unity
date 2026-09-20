using UnityEngine;

namespace TheRedDoor.Stage1
{
    public sealed class Quest3SmokeTestBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 72;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
