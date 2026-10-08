using UnityEngine;

namespace TwentyTons.Unity
{
    /// <summary>
    /// A camera behind and above the bus, smoothed so the heavy steering reads as the bus swinging,
    /// not the world. Placeholder for the real camera work; it only has to let the owner drive.
    /// </summary>
    public sealed class ChaseCamera : MonoBehaviour
    {
        public Transform Target;
        public float Behind = 16f;
        public float Up = 7f;
        public float LookAhead = 10f;
        public float Smoothing = 4f;       // higher follows tighter

        private void LateUpdate()
        {
            if (Target == null) return;
            Vector3 wanted = Target.position - Target.forward * Behind + Vector3.up * Up;
            transform.position = Vector3.Lerp(transform.position, wanted, 1f - Mathf.Exp(-Smoothing * Time.deltaTime));
            transform.LookAt(Target.position + Target.forward * LookAhead + Vector3.up * 1.5f);
        }
    }
}
