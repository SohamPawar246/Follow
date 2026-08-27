using UnityEngine;

namespace Follow.UI
{
    /// <summary>
    /// Drifts the menu camera through the forest vignette. Replaces a pre-rendered video
    /// clip: no file size, no re-encoding, and it picks up any art change for free.
    /// </summary>
    public class MenuCameraOrbit : MonoBehaviour
    {
        public Transform focus;
        public float radius = 11f;
        public float height = 4.4f;
        public float degreesPerSecond = 1.6f;
        public float bobHeight = 0.35f;
        public float bobSeconds = 9f;
        public float startAngle = 34f;

        float _angle;

        void Start() { _angle = startAngle; }

        void LateUpdate()
        {
            _angle += degreesPerSecond * Time.deltaTime;
            float rad = _angle * Mathf.Deg2Rad;

            Vector3 centre = focus != null ? focus.position : Vector3.zero;
            float bob = Mathf.Sin(Time.time / Mathf.Max(0.1f, bobSeconds) * Mathf.PI * 2f) * bobHeight;

            transform.position = centre + new Vector3(Mathf.Sin(rad) * radius, height + bob, Mathf.Cos(rad) * radius);
            transform.rotation = Quaternion.LookRotation(centre + Vector3.up * 1.1f - transform.position, Vector3.up);
        }
    }
}
