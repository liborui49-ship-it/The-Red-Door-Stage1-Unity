using UnityEngine;

namespace TheRedDoor.Stage1
{
    public sealed class Stage1RedDoorController : MonoBehaviour
    {
        [SerializeField] private Transform viewer;
        [SerializeField, Min(0.5f)] private float triggerDistance = 1.25f;
        [SerializeField, Range(-140f, 140f)] private float openAngle = -95f;
        [SerializeField, Min(0.1f)] private float transitionDuration = 0.9f;
        [SerializeField] private bool openOnce = true;
        [SerializeField] private Collider blockingCollider;

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private float transition;
        private bool open;
        private bool hasOpened;

        public bool IsOpen => open;
        public float TriggerDistance => triggerDistance;
        public float OpenAngle => openAngle;
        public Collider BlockingCollider => blockingCollider;

        public void Configure(
            Transform viewerTransform,
            float distance,
            float angle,
            float duration,
            bool remainOpen,
            Collider doorCollider)
        {
            viewer = viewerTransform;
            triggerDistance = distance;
            openAngle = angle;
            transitionDuration = duration;
            openOnce = remainOpen;
            blockingCollider = doorCollider;
        }

        private void Awake()
        {
            closedRotation = transform.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
            transition = 0f;
        }

        private void Update()
        {
            if (viewer == null && Camera.main != null)
                viewer = Camera.main.transform;

            if (viewer != null && (!openOnce || !hasOpened))
            {
                Vector3 offset = viewer.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= triggerDistance * triggerDistance)
                    Open();
            }

            float target = open ? 1f : 0f;
            transition = Mathf.MoveTowards(
                transition,
                target,
                Time.deltaTime / Mathf.Max(0.01f, transitionDuration));
            float eased = transition * transition * (3f - 2f * transition);
            transform.localRotation = Quaternion.Slerp(closedRotation, openRotation, eased);

            if (blockingCollider != null)
                blockingCollider.enabled = !open || transition < 0.85f;
        }

        public void Open()
        {
            open = true;
            hasOpened = true;
        }

        public void Close()
        {
            if (openOnce && hasOpened)
                return;
            open = false;
        }

        public void Toggle()
        {
            if (open)
                Close();
            else
                Open();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.1f, 0.08f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, triggerDistance);
        }
    }
}
