using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR
{
    public interface IGrabbable
    {
        Transform Transform { get; }

        GameObject GameObject { get; }

        TwoHandedModes twoHandedMode { get; }

        bool isGrabbed { get; }

        GrabPoint[] grabPoints { get; }

        List<FusionXRHand> attachedHands { get; }

        void Grab(FusionXRHand hand, TrackingMode mode, TrackingBase trackingBase);

        void Release(FusionXRHand hand);

        GrabPoint GetClosestGrabPoint(Vector3 point, Transform handTransform, Hand desiredHand);
    }
}
