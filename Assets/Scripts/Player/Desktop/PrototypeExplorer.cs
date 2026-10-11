using UnityEngine;

namespace CampusEmergency.Prototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class PrototypeExplorer : MonoBehaviour, ICampusInteractor
    {
        [SerializeField] private Camera walkCamera;
        [SerializeField] private Camera overviewCamera;
        [SerializeField] private Transform spawnSecondFloor;
        [SerializeField] private Transform spawnFourthFloor;
        [SerializeField] private Transform assemblyPoint;
        [SerializeField] private GameObject roof;
        [SerializeField] private GameObject smokePreview;
        [SerializeField] private float walkSpeed = 2.6f;
        [SerializeField] private float sprintSpeed = 4.2f;
        [SerializeField] private float acceleration = 14f;
        [SerializeField] private float braking = 18f;
        [SerializeField] private float jumpHeight = .75f;
        [SerializeField] private float gravity = 20f;

        private CharacterController controller;
        private Transform currentSpawn;
        private bool walking;
        private bool reachedAssembly;
        private float pitch;
        private float verticalSpeed;
        private float orbitYaw = -43f;
        private float orbitPitch = 28f;
        private float orbitDistance = 58f;
        private string notice = string.Empty;
        private float noticeUntil;
        private CampusStudentAvatar avatar;
        private CampusFirstPersonArms firstPersonArms;
        private Vector3 horizontalVelocity;
        private float groundedGrace, jumpBuffer;
        private bool thirdPerson;
        private IPrototypeInteractable focused;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle badgeStyle;

        public CampusTowel HeldTowel { get; private set; }
        public Transform TowelHoldPoint => thirdPerson && avatar != null ? avatar.RightHandHoldPoint
            : firstPersonArms != null ? firstPersonArms.HoldPoint : walkCamera.transform;
        public bool IsGrounded => controller != null && controller.enabled && controller.isGrounded;
        public bool IsSprinting => horizontalVelocity.magnitude > walkSpeed + .15f;

        public void Configure(Camera firstPerson, Camera overview, Transform secondFloor, Transform fourthFloor,
            Transform assembly, GameObject roofObject, GameObject smokeObject)
        {
            walkCamera = firstPerson;
            overviewCamera = overview;
            spawnSecondFloor = secondFloor;
            spawnFourthFloor = fourthFloor;
            assemblyPoint = assembly;
            roof = roofObject;
            smokePreview = smokeObject;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            avatar = GetComponentInChildren<CampusStudentAvatar>();
            if (avatar != null)
            {
                avatar.InitializeRig();
                firstPersonArms = walkCamera.gameObject.AddComponent<CampusFirstPersonArms>();
                firstPersonArms.Initialize(avatar);
            }
            // Start at the same indoor spawn used by F4; Tab still opens the overview.
            EnterFloor(spawnFourthFloor);
        }

    }
}
