using UnityEngine;

namespace CampusEmergency.Prototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypeExplorer : MonoBehaviour
    {
        [SerializeField] private Camera walkCamera;
        [SerializeField] private Camera overviewCamera;
        [SerializeField] private Transform spawnSecondFloor;
        [SerializeField] private Transform spawnFourthFloor;
        [SerializeField] private Transform assemblyPoint;
        [SerializeField] private GameObject roof;
        [SerializeField] private GameObject smokePreview;

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
        private bool thirdPerson;
        private IPrototypeInteractable focused;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle badgeStyle;

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
            currentSpawn = spawnFourthFloor;
            SetOverview();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2)) EnterFloor(spawnSecondFloor);
            if (Input.GetKeyDown(KeyCode.F4)) EnterFloor(spawnFourthFloor);
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (walking) SetOverview(); else EnterFloor(currentSpawn);
            }
            if (Input.GetKeyDown(KeyCode.C) && !walking) roof.SetActive(!roof.activeSelf);
            if (Input.GetKeyDown(KeyCode.H)) smokePreview.SetActive(!smokePreview.activeSelf);

            if (!walking)
            {
                UpdateOverview();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape)) ReleaseCursor();
            if (Input.GetMouseButtonDown(0)) Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
            if (Input.GetKeyDown(KeyCode.R)) EnterFloor(currentSpawn);
            if (Input.GetKeyDown(KeyCode.V) && avatar != null) ToggleCameraView();

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                transform.Rotate(0f, Input.GetAxis("Mouse X") * 1.8f, 0f);
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 1.8f, -80f, 80f);
                walkCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                Vector3 direction = transform.right * Input.GetAxisRaw("Horizontal")
                    + transform.forward * Input.GetAxisRaw("Vertical");
                float speed = Input.GetKey(KeyCode.LeftShift) ? 4.2f : 2.6f;
                MoveCharacter(direction, speed, Time.deltaTime);
                UpdateFocus();
                if (Input.GetKeyDown(KeyCode.E) && focused != null)
                {
                    notice = focused.Interact();
                    noticeUntil = Time.time + 6f;
                }
            }
            else
            {
                focused = null;
                if (avatar != null) avatar.Animate(Vector3.zero, controller.isGrounded, Time.deltaTime);
            }
            UpdateWalkCamera();

            if (transform.position.y < -5f) EnterFloor(currentSpawn);
            Vector3 groundOffset = transform.position - assemblyPoint.position;
            groundOffset.y = 0f;
            if (!reachedAssembly && transform.position.y < 1f && groundOffset.magnitude < 2.4f)
            {
                reachedAssembly = true;
                notice = "ASSEMBLY POINT REACHED\nYou have completed the prototype walkthrough.";
                noticeUntil = Time.time + 8f;
            }
        }

        public void MoveCharacter(Vector3 direction, float speed, float deltaTime)
        {
            Vector3 before = transform.position;
            verticalSpeed = controller.isGrounded && verticalSpeed < 0f ? -2f : verticalSpeed - 20f * deltaTime;
            controller.Move((direction.normalized * speed + Vector3.up * verticalSpeed) * deltaTime);
            if (avatar != null && deltaTime > 0)
                avatar.Animate(transform.InverseTransformDirection((transform.position - before) / deltaTime),
                    controller.isGrounded, deltaTime);
        }

        public void ToggleCameraView()
        {
            thirdPerson = !thirdPerson;
            if (walking) UpdateWalkCamera();
        }

        private void UpdateWalkCamera()
        {
            if (!thirdPerson)
            {
                walkCamera.transform.localPosition = new Vector3(0, 1.65f, 0);
                if (avatar != null) avatar.SetVisible(false);
                return;
            }
            Vector3 pivot = transform.position + Vector3.up * 1.5f;
            Vector3 offset = transform.rotation * Quaternion.Euler(pitch, 0, 0) * new Vector3(.32f, .15f, -2.6f);
            float distance = offset.magnitude;
            if (Physics.SphereCast(pivot, .14f, offset.normalized, out RaycastHit hit, distance,
                ~0, QueryTriggerInteraction.Ignore) && hit.collider != controller)
                distance = Mathf.Max(.1f, hit.distance - .08f);
            walkCamera.transform.position = pivot + offset.normalized * distance;
            if (avatar != null) avatar.SetVisible(distance > .55f);
        }

        private void UpdateFocus()
        {
            focused = null;
            // Interaction distance stays anchored to the student in either camera mode.
            Vector3 eye = transform.position + Vector3.up * 1.65f;
            if (!Physics.Raycast(eye, walkCamera.transform.forward, out RaycastHit hit, 2.6f)) return;
            foreach (MonoBehaviour component in hit.collider.GetComponentsInParent<MonoBehaviour>())
            {
                if (component is IPrototypeInteractable interactable)
                {
                    focused = interactable;
                    break;
                }
            }
        }

        private void UpdateOverview()
        {
            if (Input.GetMouseButton(1))
            {
                orbitYaw += Input.GetAxis("Mouse X") * 3f;
                orbitPitch = Mathf.Clamp(orbitPitch - Input.GetAxis("Mouse Y") * 2f, 10f, 75f);
            }
            orbitDistance = Mathf.Clamp(orbitDistance - Input.mouseScrollDelta.y * 3f, 25f, 85f);
            Vector3 target = new Vector3(0f, 6f, -3f);
            overviewCamera.transform.position = target + Quaternion.Euler(orbitPitch, orbitYaw, 0f) * Vector3.back * orbitDistance;
            overviewCamera.transform.LookAt(target);
        }

        private void EnterFloor(Transform spawn)
        {
            currentSpawn = spawn;
            controller.enabled = false;
            transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            pitch = 0f;
            verticalSpeed = 0f;
            walkCamera.transform.localRotation = Quaternion.identity;
            controller.enabled = true;
            walking = true;
            reachedAssembly = false;
            walkCamera.enabled = true;
            overviewCamera.enabled = false;
            roof.SetActive(true);
            UpdateWalkCamera();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            notice = "Follow EXIT signs to a stairwell. Press E to open the stair doors.";
            noticeUntil = Time.time + 5f;
        }

        private void SetOverview()
        {
            walking = false;
            focused = null;
            controller.enabled = false;
            walkCamera.enabled = false;
            overviewCamera.enabled = true;
            if (avatar != null) avatar.SetVisible(true);
            ReleaseCursor();
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDisable()
        {
            ReleaseCursor();
        }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
                titleStyle.normal.textColor = Color.white;
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
                textStyle.normal.textColor = new Color(.82f, .9f, .92f);
                badgeStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
                badgeStyle.normal.textColor = new Color(.45f, .94f, .77f);
            }

            Color previous = GUI.color;
            GUI.color = new Color(.03f, .12f, .16f, .92f);
            GUI.DrawTexture(new Rect(20, 20, 350, walking ? 122 : 193), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(38, 32, 315, 22), "HARBOURVIEW UNIVERSITY", badgeStyle);
            GUI.Label(new Rect(38, 56, 320, 34), "Teaching Block A", titleStyle);
            GUI.Label(new Rect(38, 94, 310, 38), walking
                ? "WASD move / Mouse look / E interact\nV camera / Tab overview / R restart"
                : "4 floors / 2 stairwells / 1 lift\nRight-drag orbit / Scroll zoom / C cutaway", textStyle);
            if (!walking)
            {
                if (GUI.Button(new Rect(38, 147, 147, 34), "Enter fourth floor [F4]")) EnterFloor(spawnFourthFloor);
                if (GUI.Button(new Rect(194, 147, 147, 34), "Enter second floor [F2]")) EnterFloor(spawnSecondFloor);
            }
            GUI.Label(new Rect(22, Screen.height - 38, 600, 25), "ENVIRONMENT PROTOTYPE  /  F2 & F4 start points  /  H smoke visual only", textStyle);

            if (walking)
            {
                GUI.Label(new Rect(Screen.width / 2f - 4, Screen.height / 2f - 12, 12, 24), "+");
                int floor = Mathf.Clamp(Mathf.FloorToInt((transform.position.y + .15f) / 3.6f) + 1, 1, 4);
                GUI.Label(new Rect(Screen.width - 165, 24, 145, 25), "CURRENT FLOOR  " + floor.ToString("00"), badgeStyle);
                if (focused != null) GUI.Label(new Rect(Screen.width / 2f - 150, Screen.height / 2f + 25, 300, 28), focused.Prompt, badgeStyle);
                if (Time.time < noticeUntil && !string.IsNullOrEmpty(notice))
                {
                    GUI.color = new Color(.03f, .12f, .16f, .92f);
                    GUI.DrawTexture(new Rect(Screen.width / 2f - 260, Screen.height - 130, 520, 76), Texture2D.whiteTexture);
                    GUI.color = previous;
                    GUI.Label(new Rect(Screen.width / 2f - 244, Screen.height - 118, 488, 65), notice, textStyle);
                }
            }
        }
    }
}
