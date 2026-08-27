using UnityEngine;

namespace Follow.Game
{
    /// <summary>
    /// Fixed high angle in the Cult of the Lamb register: a narrow field of view from a
    /// long way back, which flattens the scene into a diorama without the sorting
    /// problems of a true orthographic camera, and keeps depth of field usable.
    /// </summary>
    public class TopDownCamera : MonoBehaviour
    {
        public Transform target;

        [Header("Rig (GDD: camera and perspective)")]
        public float pitch = 52f;
        public float yaw = 45f;
        public float distance = 24f;
        public float fieldOfView = 40f;

        [Header("Feel")]
        public float followDamping = 0.18f;
        [Tooltip("How far the camera leans toward where the player is heading.")]
        public float leadAmount = 2.4f;
        public float heightOffset = 1.4f;

        Camera _cam;
        Vector3 _velocity;
        float _zoom = 1f;
        Vector3 _smoothedLead;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam != null) _cam.fieldOfView = fieldOfView;
            ApplyRotation();
            if (target != null) transform.position = DesiredPosition(target.position);
        }

        void ApplyRotation() => transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 DesiredPosition(Vector3 focus)
        {
            Vector3 back = Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
            return focus + Vector3.up * heightOffset * _zoom + back * distance * _zoom;
        }

        /// <summary>
        /// How close the rig sits is a taste question - how big the surveyor and the dog
        /// read on screen is exactly the sort of thing people disagree about - so the
        /// options screen owns it and this just follows.
        /// </summary>
        void PullZoom(float dt)
        {
            float want = Follow.Core.GameSettings.Zoom;
            _zoom = Mathf.Abs(_zoom - want) < 0.001f
                ? want
                : Mathf.Lerp(_zoom, want, 1f - Mathf.Exp(-dt / 0.25f));
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            PullZoom(dt);

            Vector3 lead = Vector3.zero;
            var cc = target.GetComponent<CharacterController>();
            if (cc != null)
            {
                Vector3 v = cc.velocity;
                v.y = 0f;
                lead = Vector3.ClampMagnitude(v, 6f) / 6f * leadAmount;
            }
            _smoothedLead = Vector3.Lerp(_smoothedLead, lead, 1f - Mathf.Exp(-dt / 0.35f));

            Vector3 desired = DesiredPosition(target.position + _smoothedLead);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, followDamping);

            if (_cam != null) _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fieldOfView, 1f - Mathf.Exp(-dt / 0.3f));
            ApplyRotation();
        }

        void OnValidate()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam != null) _cam.fieldOfView = fieldOfView;
            ApplyRotation();
        }
    }
}
