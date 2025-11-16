using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using System;
using UnityEngine.Windows;

namespace KinematicCharacterController.Walkthrough.NoClipState
{
    public enum CharacterState
    {
        Default,
        NoClip,
    }

    public struct PlayerCharacterInputs
    {
        public float MoveAxisForward;
        public float MoveAxisRight;
        public Quaternion CameraRotation;
        public bool JumpDown;
        public bool JumpHeld;
        public bool CrouchDown;
        public bool CrouchUp;
        public bool CrouchHeld;
        public bool NoClipDown;
    }

    public class MyCharacterController : MonoBehaviour, ICharacterController
    {
        public KinematicCharacterMotor Motor;

        [Header("Stable Movement")]
        public float MaxStableMoveSpeed = 10f;
        public float StableMovementSharpness = 15;
        public float OrientationSharpness = 10;
        public float MaxStableDistanceFromLedge = 5f;
        [Range(0f, 180f)]
        public float MaxStableDenivelationAngle = 180f;

        [Header("Air Movement")]
        public float MaxAirMoveSpeed = 10f;
        public float AirAccelerationSpeed = 5f;
        public float Drag = 0.1f;

        [Header("Jumping")]
        public bool AllowJumpingWhenSliding = false;
        public bool AllowDoubleJump = false;
        public bool AllowWallJump = false;
        public float JumpSpeed = 10f;
        public float WallJumpSpeed = 12f;
        [Range(0f, 1f)]
        public float WallJumpInputInfluence = 0.3f;
        public float WallJumpControlRecoveryTime = 0.2f;
        public float JumpPreGroundingGraceTime = 0f;
        public float JumpPostGroundingGraceTime = 0f;

        [Header("Wall Detection")]
        [Tooltip("Minimum angle (in degrees) between surface normal and up vector to be considered a wall")]
        [Range(0f, 90f)]
        public float MinWallAngle = 45f;

        // Wall detection state (read-only, visible in inspector for debugging)
        [Tooltip("Is the character currently touching a wall?")]
        [SerializeField] private bool _isOnWall;
        [Tooltip("The normal vector of the wall being touched")]
        [SerializeField] private Vector3 _wallNormal;
        [Tooltip("Time the character has been on the current wall")]
        [SerializeField] private float _timeOnWall;

        // Public accessors
        public bool IsOnWall => _isOnWall;
        public Vector3 WallNormal => _wallNormal;
        public float TimeOnWall => _timeOnWall;

        // Wall collider (not serialized as Unity can't serialize Collider references properly in this context)
        private Collider _wallCollider;
        public Collider WallCollider => _wallCollider;

        [Header("NoClip")]
        public float NoClipMoveSpeed = 10f;
        public float NoClipSharpness = 15;

        [Header("Animation")]
        public Animator CharacterAnimator;
        private bool isFacingRight = true;
        [Tooltip("Minimum speed to transition from idle to walk animation")]
        public float WalkSpeedThreshold = 0.1f;

        [Header("Misc")]
        public List<Collider> IgnoredColliders = new List<Collider>();
        public bool OrientTowardsGravity = false;
        public Vector3 Gravity = new Vector3(0, -30f, 0);
        public Transform MeshRoot;

        public CharacterState CurrentCharacterState { get; private set; }

        private Collider[] _probedColliders = new Collider[8];
        private Vector3 _moveInputVector;
        private Vector3 _lookInputVector;
        private bool _jumpInputIsHeld = false;
        private bool _crouchInputIsHeld = false;
        private bool _jumpRequested = false;
        private bool _jumpConsumed = false;
        private bool _doubleJumpConsumed = false;
        private bool _jumpedThisFrame = false;
        private bool _canWallJump = false;
        private Vector3 _wallJumpNormal;
        private float _timeSinceJumpRequested = Mathf.Infinity;
        private float _timeSinceLastAbleToJump = 0f;
        private Vector3 _internalVelocityAdd = Vector3.zero;
        private bool _shouldBeCrouching = false;
        private bool _isCrouching = false;
        private float _wallJumpControlReduction = 0f;
        private bool _wallTouchedThisFrame = false;

        private void Start()
        {
            // Assign to motor
            Motor.CharacterController = this;

            // Handle initial state
            TransitionToState(CharacterState.Default);

            // animator setup
            CharacterAnimator = GetComponent<Animator>();
        }

        /// <summary>
        /// Handles movement state transitions and enter/exit callbacks
        /// </summary>
        public void TransitionToState(CharacterState newState)
        {
            CharacterState tmpInitialState = CurrentCharacterState;
            OnStateExit(tmpInitialState, newState);
            CurrentCharacterState = newState;
            OnStateEnter(newState, tmpInitialState);
        }

        /// <summary>
        /// Event when entering a state
        /// </summary>
        public void OnStateEnter(CharacterState state, CharacterState fromState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    {
                        break;
                    }
                case CharacterState.NoClip:
                    {
                        Motor.SetCapsuleCollisionsActivation(false);
                        Motor.SetMovementCollisionsSolvingActivation(false);
                        Motor.SetGroundSolvingActivation(false);
                        break;
                    }
            }
        }

        /// <summary>
        /// Event when exiting a state
        /// </summary>
        public void OnStateExit(CharacterState state, CharacterState toState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    {
                        break;
                    }
                case CharacterState.NoClip:
                    {
                        Motor.SetCapsuleCollisionsActivation(true);
                        Motor.SetMovementCollisionsSolvingActivation(true);
                        Motor.SetGroundSolvingActivation(true);
                        break;
                    }
            }
        }

        /// <summary>
        /// This is called every frame by MyPlayer in order to tell the character what its inputs are
        /// </summary>
        public void SetInputs(ref PlayerCharacterInputs inputs)
        {
            // Handle state transition from input
            if (inputs.NoClipDown)
            {
                if (CurrentCharacterState == CharacterState.Default)
                {
                    TransitionToState(CharacterState.NoClip);
                }
                else if (CurrentCharacterState == CharacterState.NoClip)
                {
                    TransitionToState(CharacterState.Default);
                }
            }

            _jumpInputIsHeld = inputs.JumpHeld;
            _crouchInputIsHeld = inputs.CrouchHeld;

            // Clamp input
            Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

            // Calculate camera direction and rotation on the character plane
            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
            if (cameraPlanarDirection.sqrMagnitude == 0f)
            {
                cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
            }
            Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);

            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        // Move and look inputs
                        _moveInputVector = cameraPlanarRotation * moveInputVector;
                        _lookInputVector = cameraPlanarDirection;

                        // Jumping input
                        if (inputs.JumpDown)
                        {
                            _timeSinceJumpRequested = 0f;
                            _jumpRequested = true;
                        }

                        // Crouching input
                        if (inputs.CrouchDown)
                        {
                            _shouldBeCrouching = true;

                            if (!_isCrouching)
                            {
                                _isCrouching = true;
                                Motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
                                MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
                            }
                        }
                        else if (inputs.CrouchUp)
                        {
                            _shouldBeCrouching = false;
                        }
                        break;
                    }
                case CharacterState.NoClip:
                    {
                        _moveInputVector = cameraPlanarRotation * moveInputVector;
                        break;
                    }
            }
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is called before the character begins its movement update
        /// </summary>
        public void BeforeCharacterUpdate(float deltaTime)
        {
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its rotation should be right now. 
        /// This is the ONLY place where you should set the character's rotation
        /// </summary>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        if (_lookInputVector != Vector3.zero && OrientationSharpness > 0f)
                        {
                            // Smoothly interpolate from current to target look direction
                            Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

                            // Set the current rotation (which will be used by the motor)
                            currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
                        }
                        if (OrientTowardsGravity)
                        {
                            // Rotate from current up to invert gravity
                            currentRotation = Quaternion.FromToRotation((currentRotation * Vector3.up), -Gravity) * currentRotation;
                        }
                        break;
                    }
                case CharacterState.NoClip:
                    {
                        if (_lookInputVector != Vector3.zero && OrientationSharpness > 0f)
                        {
                            Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;
                            currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its velocity should be right now. 
        /// This is the ONLY place where you can set the character's velocity
        /// </summary>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        Vector3 targetMovementVelocity = Vector3.zero;
                        if (Motor.GroundingStatus.IsStableOnGround)
                        {
                            // Reorient velocity on slope
                            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, Motor.GroundingStatus.GroundNormal) * currentVelocity.magnitude;

                            // Calculate target velocity
                            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
                            Vector3 reorientedInput = Vector3.Cross(Motor.GroundingStatus.GroundNormal, inputRight).normalized * _moveInputVector.magnitude;
                            targetMovementVelocity = reorientedInput * MaxStableMoveSpeed;

                            // Smooth movement Velocity
                            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1 - Mathf.Exp(-StableMovementSharpness * deltaTime));
                        }
                        else
                        {
                            // Add move input
                            if (_moveInputVector.sqrMagnitude > 0f)
                            {
                                // Apply reduced control during wall jump
                                float controlInfluence = 1f - _wallJumpControlReduction;
                                targetMovementVelocity = _moveInputVector * MaxAirMoveSpeed * controlInfluence;

                                // Prevent climbing on un-stable slopes with air movement
                                if (Motor.GroundingStatus.FoundAnyGround)
                                {
                                    Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal), Motor.CharacterUp).normalized;
                                    targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity, perpenticularObstructionNormal);
                                }

                                Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, Gravity);
                                currentVelocity += velocityDiff * AirAccelerationSpeed * deltaTime;
                            }

                            // Gravity
                            currentVelocity += Gravity * deltaTime;

                            // Drag
                            currentVelocity *= (1f / (1f + (Drag * deltaTime)));

                            // Recover wall jump control over time
                            if (_wallJumpControlReduction > 0f)
                            {
                                _wallJumpControlReduction -= deltaTime / WallJumpControlRecoveryTime;
                                _wallJumpControlReduction = Mathf.Max(0f, _wallJumpControlReduction);
                            }
                        }

                        // Handle jumping
                        {
                            _jumpedThisFrame = false;
                            _timeSinceJumpRequested += deltaTime;
                            if (_jumpRequested)
                            {
                                // Handle double jump
                                if (AllowDoubleJump)
                                {
                                    if (_jumpConsumed && !_doubleJumpConsumed && (AllowJumpingWhenSliding ? !Motor.GroundingStatus.FoundAnyGround : !Motor.GroundingStatus.IsStableOnGround))
                                    {
                                        Motor.ForceUnground(0.1f);

                                        // Add to the return velocity and reset jump state
                                        currentVelocity += (Motor.CharacterUp * JumpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);

                                        _jumpRequested = false;
                                        _doubleJumpConsumed = true;
                                        _jumpedThisFrame = true;
                                    }
                                }

                                // See if we actually are allowed to jump
                                if (_canWallJump ||
                                    (!_jumpConsumed && ((AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround) || _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime)))
                                {
                                    // Calculate jump direction before ungrounding
                                    Vector3 jumpDirection = Motor.CharacterUp;
                                    float jumpPower = JumpSpeed;

                                    if (_canWallJump)
                                    {
                                        // Blend wall normal with upward direction
                                        Vector3 wallPushDirection = _wallJumpNormal.normalized;
                                        jumpDirection = (wallPushDirection + Motor.CharacterUp).normalized;
                                        jumpPower = WallJumpSpeed;

                                        // Set control reduction (will gradually recover)
                                        _wallJumpControlReduction = 1f;

                                        // Clear existing velocity and apply wall jump
                                        currentVelocity = jumpDirection * jumpPower;
                                    }
                                    else if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                                    {
                                        jumpDirection = Motor.GroundingStatus.GroundNormal;
                                    }

                                    if (!_canWallJump)
                                    {
                                        // Makes the character skip ground probing/snapping on its next update. 
                                        // If this line weren't here, the character would remain snapped to the ground when trying to jump. Try commenting this line out and see.
                                        Motor.ForceUnground(0.1f);

                                        // Add to the return velocity and reset jump state
                                        currentVelocity += (jumpDirection * jumpPower) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                                    }

                                    _jumpRequested = false;
                                    _jumpConsumed = true;
                                    _jumpedThisFrame = true;
                                }
                            }

                            // Reset wall jump
                            _canWallJump = false;
                        }

                        // Take into account additive velocity
                        if (_internalVelocityAdd.sqrMagnitude > 0f)
                        {
                            currentVelocity += _internalVelocityAdd;
                            _internalVelocityAdd = Vector3.zero;
                        }
                        break;
                    }
                case CharacterState.NoClip:
                    {
                        float verticalInput = 0f + (_jumpInputIsHeld ? 1f : 0f) + (_crouchInputIsHeld ? -1f : 0f);

                        // Smoothly interpolate to target velocity
                        Vector3 targetMovementVelocity = (_moveInputVector + (Motor.CharacterUp * verticalInput)).normalized * NoClipMoveSpeed;
                        currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1 - Mathf.Exp(-NoClipSharpness * deltaTime));
                        break;
                    }
            }
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is called after the character has finished its movement update
        /// </summary>
        public void AfterCharacterUpdate(float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        // Handle jump-related values
                        {
                            // Handle jumping pre-ground grace period
                            if (_jumpRequested && _timeSinceJumpRequested > JumpPreGroundingGraceTime)
                            {
                                _jumpRequested = false;
                            }

                            if (AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround)
                            {
                                // If we're on a ground surface, reset jumping values
                                if (!_jumpedThisFrame)
                                {
                                    _doubleJumpConsumed = false;
                                    _jumpConsumed = false;
                                }
                                _timeSinceLastAbleToJump = 0f;
                            }
                            else
                            {
                                // Keep track of time since we were last able to jump (for grace period)
                                _timeSinceLastAbleToJump += deltaTime;
                            }
                        }

                        // Handle wall detection state updates
                        {
                            // If we're on stable ground, we're not on a wall
                            if (Motor.GroundingStatus.IsStableOnGround)
                            {
                                ResetWallDetection();
                            }
                            // If we were on a wall but didn't touch it this frame, we've left the wall
                            else if (_isOnWall && !_wallTouchedThisFrame)
                            {
                                ResetWallDetection();
                            }
                            // If we're still on a wall, increment the timer
                            else if (_isOnWall)
                            {
                                _timeOnWall += deltaTime;
                            }

                            // Reset the flag for next frame
                            _wallTouchedThisFrame = false;
                        }

                        // Handle uncrouching
                        if (_isCrouching && !_shouldBeCrouching)
                        {
                            // Do an overlap test with the character's standing height to see if there are any obstructions
                            Motor.SetCapsuleDimensions(0.5f, 2f, 1f);
                            if (Motor.CharacterOverlap(
                                Motor.TransientPosition,
                                Motor.TransientRotation,
                                _probedColliders,
                                Motor.CollidableLayers,
                                QueryTriggerInteraction.Ignore) > 0)
                            {
                                // If obstructions, just stick to crouching dimensions
                                Motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
                            }
                            else
                            {
                                // If no obstructions, uncrouch
                                MeshRoot.localScale = new Vector3(1f, 1f, 1f);
                                _isCrouching = false;
                            }
                        }
                        break;
                    }
            }

            // Update animations every frame
            UpdateAnimations(deltaTime);
        }

        /// <summary>
        /// Updates the animator with current character state and movement data
        /// </summary>
        private void UpdateAnimations(float deltaTime)
        {
            if (CharacterAnimator != null)
            {
                // Get input magnitude for blend trees
                float inputMagnitude = _moveInputVector.magnitude;

                // This is the local "sideways" movement we need for flipping
                float localMoveX = 0f;

                // Set velocities
                if (inputMagnitude > 0.01f)
                {
                    Vector3 localMove = transform.InverseTransformDirection(_moveInputVector);
                    localMoveX = localMove.x; // Store the local x value

                    CharacterAnimator.SetFloat("xVelocity", Math.Abs(localMove.x));
                }
                else
                {
                    CharacterAnimator.SetFloat("xVelocity", 0f);
                }

                float verticalVelocity = Motor.Velocity.y;
                CharacterAnimator.SetFloat("yVelocity", verticalVelocity);

                // Call FlipSprite AFTER we calculate localMoveX
                FlipSprite(localMoveX);

                // Set jumping state
                CharacterAnimator.SetBool("IsJumping", !Motor.GroundingStatus.IsStableOnGround);

                // Set wall detection state
                CharacterAnimator.SetBool("IsOnWall", IsOnWall);

                // Force immediate update if we just left the wall
                if (!IsOnWall && CharacterAnimator.GetBool("IsOnWall"))
                {
                    CharacterAnimator.Update(0f);
                }
            }
        }

        void FlipSprite(float hInput)
        {
            // Add a small deadzone to prevent flipping when idle
            if (Mathf.Abs(hInput) < 0.1f) return;

            if (isFacingRight && hInput < 0f || !isFacingRight && hInput > 0f)
            {
                isFacingRight = !isFacingRight;

                // Now this will flip the "Square" object you assigned
                Vector3 ls = MeshRoot.localScale;
                ls.x *= -1f;
                MeshRoot.localScale = ls;
            }
        }

        /// <summary>
        /// Helper method to determine if a surface normal qualifies as a wall
        /// </summary>
        private bool IsWall(Vector3 surfaceNormal)
        {
            // Calculate the angle between the surface normal and the character's up direction
            float angle = Vector3.Angle(surfaceNormal, Motor.CharacterUp);

            // If the angle is greater than the minimum wall angle, it's a wall
            return angle >= MinWallAngle;
        }

        /// <summary>
        /// Resets all wall detection state
        /// </summary>
        private void ResetWallDetection()
        {
            _isOnWall = false;
            _wallNormal = Vector3.zero;
            _wallCollider = null;
            _timeOnWall = 0f;
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {
            if (IgnoredColliders.Contains(coll))
            {
                return false;
            }
            return true;
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        // Detect if we're hitting a wall
                        // A wall is defined as: not stable on ground, hit is not stable, and meets minimum wall angle
                        if (!Motor.GroundingStatus.IsStableOnGround && !hitStabilityReport.IsStable && IsWall(hitNormal))
                        {
                            _isOnWall = true;
                            _wallNormal = hitNormal;
                            _wallTouchedThisFrame = true;  // Mark that we touched a wall this frame

                            // Reset timer if this is a new wall (different collider)
                            if (_wallCollider != hitCollider)
                            {
                                _timeOnWall = 0f;
                            }

                            _wallCollider = hitCollider;

                            // Existing wall jump logic
                            if (AllowWallJump)
                            {
                                _canWallJump = true;
                                _wallJumpNormal = hitNormal;
                            }
                        }
                        // NOTE: Removed the else block that was resetting wall detection
                        // Wall detection should only be reset when grounded (handled in UpdateVelocity)
                        // or when explicitly leaving the wall, not just because we hit something else
                        break;
                    }
            }
        }

        public void AddVelocity(Vector3 velocity)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        _internalVelocityAdd += velocity;
                        break;
                    }
            }
        }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void PostGroundingUpdate(float deltaTime)
        {
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }
    }
}