using UnityEngine;
using UnityEngine.InputSystem;

namespace TwentyTons.Unity
{
    /// <summary>
    /// Three ways to look at the street, cycled with C: the chase camera behind the bus; the driver's
    /// seat, right-hand side, since Bangladesh drives on the left, which rides the body's every lean
    /// and dive; and the helper's spot on the door step, looking out and forward, for Act 1. The
    /// chase view smooths the body's motion; the two seats are bolted to it.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public enum View { Chase, Driver, Door }
        public Transform Target;
        public View Current = View.Chase;
        public float ChaseBehind = 16f, ChaseUp = 7f, ChaseLookAhead = 10f, ChaseSmoothing = 4f;
        public Vector3 DriverSeat = new Vector3(0.85f, 2.35f, 4.3f);     // right of centre, eye height, behind the windscreen
        public Vector3 DoorStep = new Vector3(-1.35f, 1.9f, 1.6f);      // hanging out of the left door
        public float DriverFov = 70f, DoorFov = 80f, ChaseFov = 60f;

        private Camera _cam;

        private void Awake() { _cam = GetComponent<Camera>(); }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k != null && k.cKey.wasPressedThisFrame) Current = (View)(((int)Current + 1) % 3);
        }

        private void LateUpdate()
        {
            if (Target == null) return;
            switch (Current)
            {
                case View.Driver:
                    transform.position = Target.TransformPoint(DriverSeat);
                    transform.rotation = Target.rotation;
                    if (_cam != null) _cam.fieldOfView = DriverFov;
                    break;
                case View.Door:
                    transform.position = Target.TransformPoint(DoorStep);
                    transform.rotation = Target.rotation * Quaternion.Euler(0f, -35f, 0f);
                    if (_cam != null) _cam.fieldOfView = DoorFov;
                    break;
                default:
                    // Behind and above, on the road plane: the body may lean, the camera does not.
                    Vector3 flatForward = Vector3.ProjectOnPlane(Target.forward, Vector3.up).normalized;
                    Vector3 wanted = Target.position - flatForward * ChaseBehind + Vector3.up * ChaseUp;
                    transform.position = Vector3.Lerp(transform.position, wanted, 1f - Mathf.Exp(-ChaseSmoothing * Time.deltaTime));
                    transform.LookAt(Target.position + flatForward * ChaseLookAhead + Vector3.up * 1.5f);
                    if (_cam != null) _cam.fieldOfView = ChaseFov;
                    break;
            }
        }
    }
}
