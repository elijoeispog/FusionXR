using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;

namespace Fusion.XR
{
    #if UNITY_EDITOR
    public class ReadOnlyAttribute : PropertyAttribute
    {

    }

    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property,
                                                GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position,
                                   SerializedProperty property,
                                   GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
    #endif

    public static class Extensions
    {
        public static Vector3 ClampVector(this Vector3 vector, float maxLength)
        {
            if (vector.magnitude < maxLength) return vector;

            return vector.normalized * maxLength;
        }

        public static void TryDestroyComponent<T>(this GameObject gameObject) where T : Component
        {
            if (gameObject.TryGetComponent<T>(out T t))
            {
                if(Application.isPlaying)
                {
                    Object.Destroy(t);
                }
                else
                {
                    Object.DestroyImmediate(t);
                }
            }
        }

        public static T GetComponentInAllChildren<T>(this Transform transform) where T : Component
        {
            if (transform.TryGetComponent<T>(out T t))
            {
                return t;
            }
            else
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);

                    var c = child.GetComponentInAllChildren<T>();

                    if(c != null)
                    {
                        return c;
                    }
                }
            }

            return null;
        }

        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            if(gameObject.TryGetComponent<T>(out T t))
            {
                return t;
            }
            else
            {
                return gameObject.AddComponent<T>();
            }
        }

        public static GameObject GetChildByName(this GameObject gameObject, string name, [Optional] bool recursive)
        {
            GameObject obj = null;

            if (recursive == true)
            {
                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    var child = gameObject.transform.GetChild(i);

                    if (child.name == name)
                    {
                        obj = child.gameObject;
                        break;
                    }

                    var possibleObj = child.gameObject.GetChildByName(name, true);

                    if (possibleObj)
                    {
                        obj = possibleObj;
                    }
                }
            }
            else
            {
                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    var child = gameObject.transform.GetChild(i);

                    if (child.name == name)
                    {
                        obj = child.gameObject;
                    }
                }
            }

            return obj;
        }
    }

    public static class Utils
    {
        #region Matching
        public static bool ObjectMatchesTags(GameObject obj, string[] tags)
        {
            //When no tag mask is set
            if (tags.Length == 0) return true;

            foreach (string tag in tags)
            {
                if (obj.tag == tag) return true;
            }

            return false;
        }

        public static bool ObjectMatchesLayermask(GameObject obj, LayerMask mask)
        {
            if (mask == ~0) return true;

            if (mask == (mask | (1 << obj.layer)))
            {
                return true;
            }
            else return false;
        }

        public static bool ObjectMatchesAttractType(GameObject obj, AttractType attractType)
        {
            //Dont attach hands
            if (obj.TryGetComponent<FusionXRHand>(out FusionXRHand hand))
            {
                return false;
            }

            if (attractType == AttractType.Grabbables)
            {
                return obj.TryGetComponent<IGrabbable>(out IGrabbable g);
            }
            else if (attractType == AttractType.Rigidbodys)
            {
                return obj.TryGetComponent<Rigidbody>(out Rigidbody r);
            }
            else //if (attractType == AttractType.AllCollisionObjects)
            {
                return true;
            }
        }
        #endregion

        #region ClosestObject

        public static Vector3 ClosestPointOnLine(Vector3 linePnt, Vector3 lineDir, float lineLength, Vector3 pnt)
        {
            lineDir.Normalize();
            var v = pnt - linePnt;
            var d = Vector3.Dot(v, lineDir);
            return linePnt + (lineDir * d).ClampVector(lineLength * 0.5f);
        }

        public static GrabPoint ClosestGrabPoint(IGrabbable grabbable, Vector3 point, Transform handTransform, Hand desiredHand)
        {
            GrabPoint closestGrabPoint = null;
            float distance = float.MaxValue;

            if (grabbable.grabPoints != null)
            {
                foreach (GrabPoint currentGrabPoint in grabbable.grabPoints)
                {
                    //TODO FIX
                    if (currentGrabPoint.IsGrabPossible(handTransform, desiredHand, grabbable.twoHandedMode)) //Check if the GrabPoint is for the correct Hand and if it isActive
                    {
                        if ((currentGrabPoint.transform.position - point).sqrMagnitude < distance) //Check if next Point is closer than last Point
                        {
                            closestGrabPoint = currentGrabPoint;
                            distance = (currentGrabPoint.transform.position - point).sqrMagnitude; //New (smaller) distance
                        }
                    }
                }
            }
            return closestGrabPoint;
        }

        public static GameObject ClosestGameObject(GameObject[] gameObjects, Vector3 pos)
        {
            if (gameObjects.Length == 0)
            {
                Debug.LogError("Given List was empty");
                return null;
            }

            float dist = Mathf.Infinity;
            GameObject closestObject = gameObjects[0];

            if (gameObjects.Length != 1)
            {
                foreach (GameObject obj in gameObjects)
                {
                    float currDist = (obj.transform.position - pos).sqrMagnitude;
                    if (currDist < dist)
                    {
                        dist = currDist;
                        closestObject = obj;
                    }
                }
            }

            return closestObject;
        }

        public static GameObject ClosestGameObject(List<GameObject> gameObjects, Vector3 pos)
        {
            if (gameObjects.Count == 0)
            {
                Debug.LogError("Given List was empty");
                return null;
            }

            float dist = Mathf.Infinity;
            GameObject closestObject = gameObjects[0];

            if (gameObjects.Count != 1)
            {
                foreach (GameObject obj in gameObjects)
                {
                    float currDist = (obj.transform.position - pos).sqrMagnitude;
                    if (currDist < dist)
                    {
                        dist = currDist;
                        closestObject = obj;
                    }
                }
            }

            return closestObject;
        }
        #endregion

        #region Drivers
        public static TrackDriver DriverFromEnum(TrackingMode trackingMode)
        {
            //Defaulting to Kinematic Driver
            TrackDriver driver = new KinematicDriver();

            switch (trackingMode)
            {
                case TrackingMode.Kinematic:
                    driver = new KinematicDriver();
                    break;
                case TrackingMode.Velocity:
                    driver = new VelocityDriver();
                    break;
                case TrackingMode.ActiveJoint:
                    driver = new ActiveJointDriver();
                    break;
                case TrackingMode.PassiveJoint:
                    driver = new PassiveJointDriver();
                    break;
                case TrackingMode.Force:
                    driver = new ForceDriver();
                    break;
                default:
                    Debug.LogError("No matching TrackDriver was setup for the given trackingMode enum, defaulting to a Kinematic Driver. Define a matching TrackDriver and declare it in Utilities.cs");
                    break;
            }

            return driver;
        }

        public static FingerDriver FingerDriverFromEnum(FingerTrackingMode fingerTrackingMode)
        {
            //Defaulting to Kinematic Driver
            FingerDriver driver = new KinematicFingerDriver();

            switch (fingerTrackingMode)
            {
                case FingerTrackingMode.Kinematic:
                    driver = new KinematicFingerDriver();
                    break;
                case FingerTrackingMode.CollisionTest:
                    driver = new CollisionTestDriver();
                    break;
                default:
                    Debug.LogError("No matching FingerDriver was setup for the given FingerTrackingMode enum, defaulting to a Kinematic Driver. Define a matching FingerDriver and declare it in Utilities.cs");
                    break;
            }

            return driver;
        }
        #endregion

        #region Collision
        public static Collider[] CheckBoxCollider(Transform transform, BoxCollider boxCollider)
        {
            Vector3 boxCenter = transform.TransformPoint(boxCollider.center);

            return Physics.OverlapBox(boxCenter, boxCollider.size / 2, transform.rotation);
        }
        #endregion

        public static Direction GetDirectionFromVector(Vector2 input)
        {
            var angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;

            var absAngle = Mathf.Abs(angle);

            if (absAngle < 45f)
                return Direction.East;
            if (absAngle > 135f)
                return Direction.West;

            return angle >= 0f ? Direction.North : Direction.South;
        }
    }

    [System.Serializable]
    public class PID
    {
        public float P, I, D;

        public PID(float P, float I, float D)
        {
            this.P = P;
            this.I = I;
            this.D = D;
        }

        Vector3 current;
        public Vector3 CalcVector(Vector3 setPoint, Vector3 actualPoint, float deltaTime)
        {
            current.Set(
                CalcScalar(setPoint.x, actualPoint.x, deltaTime),
                CalcScalar(setPoint.y, actualPoint.y, deltaTime),
                CalcScalar(setPoint.z, actualPoint.z, deltaTime));
            return current;
        }

        float present, derivative, lastError, integral;
        public float CalcScalar(float setPoint, float actual, float deltaTime)
        {
            present = setPoint - actual;
            integral += present * deltaTime;
            lastError = present;
            derivative = (present - lastError) / deltaTime;
            return present * P + integral * I + derivative * D;
        }
    }
}