using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR
{
    public enum TwoHandedMode
    {
        SwitchHand = 0,
        Average = 1,
        //AttachHand = 2
    }

    public class Grabable : MonoBehaviour
    {
        public TwoHandedMode twoHandedMode = TwoHandedMode.SwitchHand;
        public float releaseThreshold = 0.4f;

        [HideInInspector] public bool isGrabbed;
        [SerializeField] private GrabPoint[] grabPoints;

        [HideInInspector] public List<FusionXRHand> attachedHands = new List<FusionXRHand>();

        private GrabMode grabMode;
        private Rigidbody rb;

        //If 2 Handed:
        private Vector3 posOffset;
        private Vector3 rotOffset;

        private Vector3 refVel;

        #region UnityFunctions

        public virtual void Start()
        {
            try
            {
                gameObject.layer = LayerMask.NameToLayer("Interactables");
            }
            catch
            {
                Debug.LogError("Layers need to be setup correctly!");
            }

            rb = GetComponent<Rigidbody>();
            gameObject.tag = "Grabable";
        }

        public virtual void FixedUpdate()
        {
            if (!isGrabbed)
                return;

            if (grabMode == GrabMode.Joint) //Calculations not needed when using Joints
                return;

            Vector3 avgPos = Vector3.zero;
            Quaternion avgRot = Quaternion.identity;
            Vector3 offsetPos = Vector3.zero;

            if (attachedHands.Count > 1)
            {
                Vector3[] handsPosOffset = new Vector3[2];
                Quaternion[] handsRotOffset = new Quaternion[2];

                handsPosOffset[0] = attachedHands[0].grabSpot.localPosition;
                handsPosOffset[1] = attachedHands[1].grabSpot.localPosition;

                handsRotOffset[0] = attachedHands[0].rotWithOffset * Quaternion.Inverse(attachedHands[0].grabSpot.localRotation);
                handsRotOffset[1] = attachedHands[1].rotWithOffset * Quaternion.Inverse(attachedHands[1].grabSpot.localRotation);

                avgRot = Quaternion.Lerp(handsRotOffset[0], handsRotOffset[1], 0.5f);
                avgPos = Vector3.Lerp(attachedHands[0].posWithOffset, attachedHands[1].posWithOffset, 0.5f);

                offsetPos = Vector3.Lerp(handsPosOffset[0], handsPosOffset[1], .5f);
            }
            else
            {
                if(attachedHands[0].hand == Hand.Right)
                {
                    avgRot = attachedHands[0].rotWithOffset * Quaternion.Inverse(attachedHands[0].grabSpot.localRotation);
                }
                else
                {
                    avgRot = attachedHands[0].rotWithOffset * Quaternion.Inverse(attachedHands[0].grabSpot.localRotation * Quaternion.Euler(0, 0, 180)); //Left Hand needs to be rotated
                }
                avgPos = attachedHands[0].posWithOffset;
                offsetPos = attachedHands[0].grabSpot.localPosition;
            }

            Vector3 targetPos = avgPos - transform.TransformPoint(offsetPos);

            switch (grabMode)
            {
                case GrabMode.Kinematic:
                    TrackPositionKinematic(targetPos);
                    TrackRotationKinematic(avgRot);
                    break;
                case GrabMode.Velocity:
                    TrackPositionVelocity(targetPos);
                    TrackRotationVelocity(avgRot);
                    break;
                case GrabMode.Joint:
                    //Joint needs no tracking done
                    break;
            }

            return;
        }

        #endregion

        #region Events
        public void Grab(FusionXRHand hand, GrabMode mode) 
        {
            grabMode = mode;

            if (twoHandedMode == TwoHandedMode.SwitchHand)   //Case: Switch Hands (Release the other hand)
            {
                //The order of these operations is critical, if the next hand is added before the last one released the "if" will fail
                if (attachedHands.Count > 0)
                    attachedHands[0].Release();

                attachedHands.Add(hand);
            }
            else if(twoHandedMode == TwoHandedMode.Average) //Case: Averaging Between Hands;
            {
                attachedHands.Add(hand);
            }

            foreach (Collider coll in GetComponents<Collider>())
            {
                Physics.IgnoreCollision(hand.GetComponent<Collider>(), coll, true);
            }

            isGrabbed = true; //This needs to be called at the end, if not the releasing Hand will set "isGrabbed" to false and it will stay that way

            if (grabMode == GrabMode.Joint)
                AttachJoint(hand);
        }

        public void Release(FusionXRHand hand)
        {
            foreach (Collider coll in GetComponents<Collider>())
            {
                Physics.IgnoreCollision(hand.GetComponent<Collider>(), coll, false);
            }

            attachedHands.Remove(hand);

            if(attachedHands.Count == 0)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.interpolation = RigidbodyInterpolation.None;
                isGrabbed = false;
            }

            if (grabMode == GrabMode.Joint)
                ReleaseJoint(hand);
        }

        #endregion

        #region Functions

        public bool TryGetClosestGrapPoint(Vector3 point, Hand desiredHand, out Transform GrapPoint)
        {
            GrapPoint = ClosestGrabPoint(grabPoints, point, desiredHand);

            return GrapPoint != null;
        }

        Transform ClosestGrabPoint(GrabPoint[] grabPoints, Vector3 point, Hand desiredHand)
        {
            Transform closestGrabPoint = null;
            float distance = float.MaxValue;

            if (grabPoints != null)
            {
                foreach (GrabPoint currentGrabPoint in grabPoints)
                {
                    if (currentGrabPoint.CorrectHand(desiredHand) && currentGrabPoint.isActive) //Check if the GrapPoint is for the correct Hand and if it isActive
                    {
                        if ((currentGrabPoint.transform.position - point).sqrMagnitude < distance) //Check if next Point is closer than last Point
                        {
                            closestGrabPoint = currentGrabPoint.transform;
                            distance = (currentGrabPoint.transform.position - point).sqrMagnitude; //New (smaller) distance
                        }
                    }
                }
            }
            return closestGrabPoint;
        }

        #endregion

        #region Tracking Functions
        void TrackPositionVelocity(Vector3 targetPos)
        {
            targetPos *= 60f;

            if (float.IsNaN(targetPos.x) == false)
            {
                targetPos = Vector3.MoveTowards(rb.velocity, targetPos, 20f);

                rb.velocity = targetPos;
            }
        }

        void TrackRotationVelocity(Quaternion targetRot)
        {
            Quaternion deltaRotation = targetRot * Quaternion.Inverse(transform.rotation);

            deltaRotation.ToAngleAxis(out var angle, out var axis);

            if (angle > 180f)
            {
                angle -= 360;
            }

            if (angle != 0 && float.IsNaN(axis.x) == false && float.IsInfinity(axis.x) == false)
            {
                Vector3 angularTarget = axis * (Mathf.Deg2Rad * angle * 20);

                rb.angularVelocity = Vector3.MoveTowards(rb.angularVelocity, angularTarget, 30f);
            }
        }

        void TrackPositionKinematic(Vector3 targetPos)
        {
            transform.position = targetPos;
        }

        void TrackRotationKinematic(Quaternion targetRot)
        {
            transform.rotation = targetRot;
        }

        void AttachJoint(FusionXRHand hand)
        {
            //Rotate Grabable
            //transform.rotation = hand.rotWithOffset * Quaternion.Inverse(hand.grabSpot.localRotation);
            if (attachedHands[0].hand == Hand.Right)
            {
                transform.rotation = hand.rotWithOffset * Quaternion.Inverse(hand.grabSpot.localRotation);
            }
            else
            {
                transform.rotation = hand.rotWithOffset * Quaternion.Inverse(hand.grabSpot.localRotation * Quaternion.Euler(0, 0, 180)); //Left Hand needs to be rotated
            }

            //Setup Joint
            Joint attachedJoint = hand.gameObject.AddComponent<ConfigurableJoint>();
            attachedJoint.connectedBody = rb;

            attachedJoint.autoConfigureConnectedAnchor = false;

            attachedJoint.anchor = hand.transform.InverseTransformPoint(hand.palm.position);
            attachedJoint.connectedAnchor = transform.InverseTransformPoint(hand.grabSpot.position);

            if(hand.gameObject.TryGetComponent(out ConfigurableJoint configurableJoint))
            {
                configurableJoint.xMotion = configurableJoint.yMotion = configurableJoint.zMotion = ConfigurableJointMotion.Locked;

                configurableJoint.rotationDriveMode = RotationDriveMode.Slerp;

                var slerpDrive = new JointDrive();
                slerpDrive.positionSpring = 1000;
                slerpDrive.positionDamper = 75;
                slerpDrive.maximumForce = 500;

                configurableJoint.slerpDrive = slerpDrive;
            }
        }

        void TrackJointRotation(Quaternion targetRot)
        {
            foreach (FusionXRHand hand in attachedHands)
            {
                if (hand.gameObject.TryGetComponent(out ConfigurableJoint configurableJoint))
                {
                    configurableJoint.targetRotation = hand.rotWithOffset * Quaternion.Inverse(hand.grabSpot.localRotation);
                }
            }
        }

        void ReleaseJoint(FusionXRHand hand)
        {
            Joint jointToRemove = hand.gameObject.GetComponent<Joint>();

            Destroy(jointToRemove);
        }

        #endregion

        #region Deprecated Functions
        public Vector3 CalculateOffset(FusionXRHand hand)
        {
            Vector3 offset = Vector3.zero;

            if (grabPoints.Length > 0)
            {
                Transform grabPoint = ClosestGrabPoint(grabPoints, hand.transform.position, hand.hand);

                offset = grabPoint.position - transform.position;
            }
            else
            {
                offset = transform.position - hand.transform.position;
            }

            return offset;
        }

        public Quaternion CalculateRotationOffset(FusionXRHand hand)
        {
            Quaternion rotationOffset;

            if (grabPoints.Length > 0)
            {
                Transform grabPoint = ClosestGrabPoint(grabPoints, hand.transform.position, hand.hand);

                rotationOffset = grabPoint.rotation;
            }
            else
            {
                rotationOffset = hand.transform.rotation * Quaternion.Inverse(transform.rotation);
            }

            return rotationOffset;
        }


        #endregion
    }
}
