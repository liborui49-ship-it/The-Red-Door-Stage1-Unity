using UnityEngine;
using UnityEngine.InputSystem;

namespace TheRedDoor.Stage1
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class Stage1LocomotionController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.2f;
        [SerializeField, Range(15f, 90f)] private float snapTurnAngle = 30f;
        [SerializeField, Min(0f)] private float gravity = 9.81f;
        [SerializeField, Range(0.8f, 1.4f)] private float minimumHeight = 1f;
        [SerializeField, Range(1.5f, 2.2f)] private float maximumHeight = 2f;

        private CharacterController characterController;
        private InputAction moveAction;
        private InputAction turnAction;
        private float verticalVelocity;
        private bool turnReady = true;

        public Camera ViewCamera => viewCamera;
        public float MoveSpeed => moveSpeed;
        public float SnapTurnAngle => snapTurnAngle;

        public void Configure(Camera camera, float speed, float turnAngle)
        {
            viewCamera = camera;
            moveSpeed = speed;
            snapTurnAngle = turnAngle;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (viewCamera == null)
                viewCamera = Camera.main;

            moveAction = new InputAction("Stage1 Move", InputActionType.Value);
            moveAction.AddBinding("<XRController>{LeftHand}/primary2DAxis");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            turnAction = new InputAction("Stage1 Snap Turn", InputActionType.Value);
            turnAction.AddBinding("<XRController>{RightHand}/primary2DAxis");
            turnAction.AddCompositeBinding("2DVector")
                .With("Left", "<Keyboard>/q")
                .With("Right", "<Keyboard>/e");
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            turnAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            turnAction?.Disable();
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            turnAction?.Dispose();
        }

        private void Update()
        {
            if (viewCamera == null)
                return;

            SynchronizeCapsuleToHead();
            ApplyMovement();
            ApplySnapTurn();
        }

        private void SynchronizeCapsuleToHead()
        {
            Vector3 localHead = transform.InverseTransformPoint(viewCamera.transform.position);
            float height = Mathf.Clamp(localHead.y, minimumHeight, maximumHeight);
            characterController.height = height;
            characterController.center = new Vector3(
                localHead.x,
                height * 0.5f,
                localHead.z);
        }

        private void ApplyMovement()
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            Vector3 forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up).normalized;
            Vector3 horizontal = (forward * input.y + right * input.x) * moveSpeed;

            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -0.5f;
            else
                verticalVelocity -= gravity * Time.deltaTime;

            Vector3 motion = horizontal + Vector3.up * verticalVelocity;
            characterController.Move(motion * Time.deltaTime);
        }

        private void ApplySnapTurn()
        {
            float input = turnAction.ReadValue<Vector2>().x;
            if (Mathf.Abs(input) < 0.25f)
            {
                turnReady = true;
                return;
            }

            if (!turnReady || Mathf.Abs(input) < 0.7f)
                return;

            turnReady = false;
            float angle = Mathf.Sign(input) * snapTurnAngle;
            Vector3 pivot = viewCamera.transform.position;
            pivot.y = transform.position.y;
            transform.RotateAround(pivot, Vector3.up, angle);
        }
    }
}
