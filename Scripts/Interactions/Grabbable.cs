using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR
{
    public class Grabbable : MonoBehaviour, IGrabbable
    {
        public GrabbableType grabableType = GrabbableType.Interactables;

        #region IGrabbable Implementation
        public Transform Transform { get { return transform; } }
        public GameObject GameObject { get { return gameObject; } }

        public TwoHandedMode twoHandedMode = TwoHandedMode.SwitchHand;

        public bool isGrabbed { get; protected set; }

        [SerializeField] private GrabPoint[] grabPoints;

        public List<FusionXRHand> attachedHands { get; private set; } = new List<FusionXRHand>();
        #endregion

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
                gameObject.layer = LayerMask.NameToLayer(grabableType.ToString());
            }
            catch
            {
                Debug.LogError("Layers need to be setup correctly!");
            }

            rb = GetComponent<Rigidbody>();
        }

        public virtual void FixedUpdate()
        {
            if (!isGrabbed)
                return;

            //Reset Target Position and Rotation
            Quaternion targetRotation = Quaternion.identity;
            Vector3 targetPosition = Vector3.zero;

            int handsCount = attachedHands.Count;

            if (handsCount == 1) //If there is one hand grabbing
            {
                //Get GrabPoint Offsets
                Vector3 offsetPos = attachedHands[0].grabPosition.localPosition;
                Quaternion offsetRot = attachedHands[0].grabPosition.localRotation;

                //Delta Vector/Quaternion from Grabable (+ offset) to hand
                targetPosition = attachedHands[0].targetPosition - transform.TransformVector(offsetPos);
                targetRotation = attachedHands[0].targetRotation * Quaternion.Inverse(offsetRot);

                //Apply Target Transformation to hand
                attachedHands[0].grabbedTrackDriver.UpdateTrack(targetPosition, targetRotation);
            }
            else //If there is two hands grabbing 
            {
                Vector3[] posTargets = new Vector3[handsCount];
                Quaternion[] rotTargets = new Quaternion[handsCount];

                for (int i = 0; i < handsCount; i++)
                {
                    //Get GrabPoint Offsets
                    Vector3 offsetPos = attachedHands[i].grabPosition.localPosition;
                    Quaternion offsetRot = attachedHands[i].grabPosition.localRotation;

                    //Delta Vector/Quaternion from Grabable (+ offset) to hand
                    posTargets[i] = attachedHands[i].targetPosition - transform.TransformVector(offsetPos);
                    rotTargets[i] = attachedHands[i].targetRotation * Quaternion.Inverse(offsetRot);
                }

                //Average target transformation
                targetPosition = Vector3.Lerp(posTargets[0], posTargets[1], 0.5f);
                targetRotation = Quaternion.Lerp(rotTargets[0], rotTargets[1], 0.5f);

                //Apply Target Transformation to hands
                attachedHands[0].grabbedTrackDriver.UpdateTrack(targetPosition, targetRotation);
                attachedHands[1].grabbedTrackDriver.UpdateTrack(targetPosition, targetRotation);
            }
        }

        #endregion

        #region Events
        public virtual void Grab(FusionXRHand hand, TrackingMode mode, TrackingBase trackingBase)
        {
            ///Manage new hand first (so the last driver gets removed before a new one is added)
            ManageNewHand(hand, attachedHands, twoHandedMode);

            ///Setup and Start Track Driver
            hand.grabbedTrackDriver = Utils.DriverFromEnum(mode);
            hand.grabbedTrackDriver.StartTrack(transform, trackingBase);

            EnableOrDisableCollisions(gameObject, hand, true);

            ///This needs to be called at the end, if not the releasing Hand will set "isGrabbed" to false and it will stay that way
            isGrabbed = true;
        }

        public virtual void Release(FusionXRHand hand)
        {
            EnableOrDisableCollisions(gameObject, hand, false);

            RemoveHand(hand);

            //If the releasing hand was the last one grabbing the object, end the tracking/trackDriver
            hand.grabbedTrackDriver.EndTrack();
        }

        #endregion

        #region Functions

        //For returning the transform and the GrabPoint
        public Transform GetClosestGrabPoint(Vector3 point, Transform handTransform, Hand desiredHand, out GrabPoint grabPoint)
        {
            grabPoint = Utils.ClosestGrabPoint(grabPoints, point, handTransform, desiredHand);

            if (grabPoint != null)
            {
                grabPoint = grabPoint.GetAligned();
                grabPoint.BlockGrabPoint();
                return grabPoint.transform;
            }
            else
            {
                return null;
            }
        }

        public static void ManageNewHand(FusionXRHand hand, List<FusionXRHand> currentHands, TwoHandedMode mode)
        {
            if (mode == TwoHandedMode.SwitchHand)   //Case: Switch Hands (Release the other hand)
            {
                //The order of these operations is critical, if the next hand is added before the last one released the "if" will fail
                if (currentHands.Count > 0)
                {
                    //This will also call the release function on this grabable, with this structure the hand can also be forced to release whatever it is holding
                    currentHands[0].Release();
                }

                currentHands.Add(hand);
            }
            else if (mode == TwoHandedMode.Average) //Case: Averaging Between Hands;
            {
                currentHands.Add(hand);
            }
        }

        void RemoveHand(FusionXRHand hand)
        {
            attachedHands.Remove(hand);

            if (attachedHands.Count == 0)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.interpolation = RigidbodyInterpolation.None;
                isGrabbed = false;
            }
        }

        public static void EnableOrDisableCollisions(GameObject obj, FusionXRHand hand, bool disable)
        {
            foreach (Collider coll in obj.GetComponents<Collider>())
            {
                Physics.IgnoreCollision(hand.GetComponent<Collider>(), coll, disable);
            }
        }

        #endregion
    }
}
