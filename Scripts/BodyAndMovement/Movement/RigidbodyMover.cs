using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR
{
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyMover : Movement
    {
        [HideInInspector]
        public Rigidbody rb;

        new bool usesGravity => false;

        Vector3 vel;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public override void Move(Vector3 direction)
        {
            vel = direction; // Vector3.ProjectOnPlane(direction, Vector3.up).normalized;

            vel.y = rb.velocity.y;

            CurrentVelocity = rb.velocity;

            rb.velocity = vel;
        }
    }
}
