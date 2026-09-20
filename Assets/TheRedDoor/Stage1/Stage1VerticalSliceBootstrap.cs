using UnityEngine;

namespace TheRedDoor.Stage1
{
    public sealed class Stage1VerticalSliceBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 72;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Physics.defaultContactOffset = 0.01f;
        }
    }
}
