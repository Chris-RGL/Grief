using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using KinematicCharacterController.Examples;
using System.Linq;

namespace KinematicCharacterController.Walkthrough.NoClipState
{
    public class MyPlayer : MonoBehaviour
    {
        public ExampleCharacterCamera OrbitCamera;
        public Transform CameraFollowPoint;
        public MyCharacterController Character;

        private const string MouseXInput = "Mouse X";
        private const string MouseYInput = "Mouse Y";
        private const string MouseScrollInput = "Mouse ScrollWheel";
        private const string HorizontalInput = "Horizontal";
        private const string VerticalInput = "Vertical";

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;

            // Tell camera to follow transform
            OrbitCamera.SetFollowTransform(CameraFollowPoint);

            // Ignore the character's collider(s) for camera obstruction checks
            OrbitCamera.IgnoredColliders.Clear();
            OrbitCamera.IgnoredColliders.AddRange(Character.GetComponentsInChildren<Collider>());
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
            }

            HandleCharacterInput();
        }

        private void LateUpdate()
        {
            HandleCameraInput();
        }

        private void HandleCameraInput()
        {
            // --- MODIFIED SECTION START ---
            // Create the look input vector for the camera.
            // By setting this to Vector3.zero, we explicitly disable mouse look.
            Vector3 lookInputVector = Vector3.zero;

            // The logic to prevent camera movement when the cursor is unlocked is now redundant for look,
            // but the original camera script might still be expecting this logic flow.
            // Since lookInputVector is already zero, we can simplify/remove the redundant check.

            // If you want to completely disable all look logic, including the cursor lock check, 
            // you can remove the entire original input block and just set lookInputVector to Vector3.zero.
            // --- MODIFIED SECTION END ---

            // Input for zooming the camera (disabled in WebGL because it can cause problems)
            float scrollInput = -Input.GetAxis(MouseScrollInput);
#if UNITY_WEBGL
    scrollInput = 0f;
#endif

            // Apply inputs to the camera
            OrbitCamera.UpdateWithInput(Time.deltaTime, scrollInput, lookInputVector);

            // Handle toggling zoom level
            if (Input.GetMouseButtonDown(1))
            {
                OrbitCamera.TargetDistance = (OrbitCamera.TargetDistance == 0f) ? OrbitCamera.DefaultDistance : 0f;
            }
        }

        private void HandleCharacterInput()
        {
            PlayerCharacterInputs characterInputs = new PlayerCharacterInputs();

            // Build the CharacterInputs struct
            characterInputs.MoveAxisForward = Input.GetAxisRaw(VerticalInput);
            characterInputs.MoveAxisRight = Input.GetAxisRaw(HorizontalInput);
            characterInputs.CameraRotation = OrbitCamera.Transform.rotation;
            characterInputs.JumpDown = Input.GetKeyDown(KeyCode.Space);
            characterInputs.JumpHeld = Input.GetKey(KeyCode.Space);
            characterInputs.CrouchDown = Input.GetKeyDown(KeyCode.C);
            characterInputs.CrouchUp = Input.GetKeyUp(KeyCode.C);
            characterInputs.CrouchHeld = Input.GetKey(KeyCode.C);
            characterInputs.NoClipDown = Input.GetKeyUp(KeyCode.Q);

            // Apply inputs to character
            Character.SetInputs(ref characterInputs);
        }
    }
}