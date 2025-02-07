using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations;

namespace Fusion.XR
{
    public class LocoSphereMover : Movement
    {
        private PhysicsBody body;

        [Header("Torques and Accelerations")]
        [SerializeField]
        private Rigidbody LocoSphere;

        [SerializeField]
        private ForceMode forceMode = ForceMode.VelocityChange;

        [SerializeField]
        private float torque = 5f;

        [SerializeField] [Range(0.1f, 1f)]
        private float accelerationTime = 0.5f;

        [SerializeField]
        private AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0, 0, 0.5f, 1);

        [SerializeField] [Range(0.1f, 1f)]
        private float decelerationTime = 0.33f;

        [SerializeField]
        private float deceleration = 0.1f;

        [Header("Crouch and Jump")]
        [SerializeField]
        private InputAction jump;

        [SerializeField] [ReadOnly]
        private PlayerState playerState = PlayerState.Standing;

        [SerializeField]
        private float jumpForce = 1000f;

        [SerializeField]
        private float crouchHeight = 1.3f;

        #region Private Vars
        private Vector3 currentMove;
        private Vector3 torqueVec;
        private Vector3 vel;

        private bool isMoving;

        private float currentTorque;

        private float timeSinceMoveStarted = 0;
        private float timeSinceMoveEnded = 0;
        #endregion

        private void Awake()
        {
            body = GetComponent<PhysicsBody>();

            jump.Enable();
            jump.started += OnCrouch;
            jump.canceled += OnJump;
        }

        private void FixedUpdate()
        {
            LocoSphere.freezeRotation = false;

            isMoving = Vector3.ProjectOnPlane(currentMove, Vector3.up).sqrMagnitude > 0.1f;

            //currentTorque = UpdateTorqueAcceleration();

            if (isMoving)
            {
                LocoSphere.angularVelocity = LocoSphere.angularVelocity.ClampVector(playerSpeed / body.LocoSphereCollider.radius);

                currentTorque = UpdateTorqueAcceleration();

                ApplyTorque();
            }
            else
            {
                currentTorque = 0;
                LocoSphere.angularVelocity *= deceleration;

                if(LocoSphere.angularVelocity.sqrMagnitude < 1f)
                {
                    LocoSphere.freezeRotation = true;
                }
            }
        }

        public override void Move(Vector3 direction)
        {
            currentMove = direction;
        }

        #region Jumping & Crouching

        public void OnCrouch(InputAction.CallbackContext obj) { OnCrouch(); }

        public void OnJump(InputAction.CallbackContext obj) { OnJump(); }

        public void OnCrouch()
        {
            body.StartCrouch();
        }

        public void OnJump()
        {
            body.StartJump(jumpForce);
        }

        #endregion

        #region Torque
        private float UpdateTorqueAcceleration()
        {
            timeSinceMoveStarted += Time.fixedDeltaTime / accelerationTime;
            timeSinceMoveEnded = 0;

            return accelerationCurve.Evaluate(timeSinceMoveStarted) * torque;
        }

        private void ApplyTorque()
        {
            if (currentTorque > 0)
            {
                torqueVec = Vector3.Cross(currentMove, Vector3.down);

                LocoSphere.AddTorque(torqueVec * currentTorque, forceMode);
            }
        } 
        #endregion
    } 
}
