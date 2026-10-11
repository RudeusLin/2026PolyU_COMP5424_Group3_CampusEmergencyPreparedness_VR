using UnityEngine;

namespace CampusEmergency.Prototype
{
    public sealed partial class PrototypeExplorer
    {
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
                MoveWithInput(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                    Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                    Input.GetKeyDown(KeyCode.Space), Time.deltaTime);
                UpdateFocus();
                if (Input.GetKeyDown(KeyCode.E) && focused != null)
                {
                    notice = focused.Interact(this);
                    noticeUntil = Time.time + 6f;
                }
            }
            else
            {
                focused = null;
                MoveWithInput(Vector2.zero, false, false, Time.deltaTime);
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
    }
}
