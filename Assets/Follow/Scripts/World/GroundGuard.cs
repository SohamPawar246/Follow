using UnityEngine;

namespace Follow.World
{
    /// <summary>
    /// Catches anything that has fallen through the world.
    ///
    /// Chunks are built a frame or two ahead of where you are walking, and a scene loaded
    /// before its ground exists leaves the character controller in mid-air. Rather than
    /// hope that never happens, this notices the fall and sets the actor back on the
    /// height field - which is the same surface the ground mesh is built from, so it puts
    /// them exactly where the ground is about to appear.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class GroundGuard : MonoBehaviour
    {
        [Tooltip("Metres below the height field before we call it a fall.")]
        public float tolerance = 6f;

        CharacterController _cc;

        void Awake() { _cc = GetComponent<CharacterController>(); }

        void LateUpdate()
        {
            var p = transform.position;
            float ground = WorldComposer.Height(p.x, p.z);
            if (p.y >= ground - tolerance) return;

            // Disabling the controller first, or Move will fight the teleport.
            bool had = _cc != null && _cc.enabled;
            if (had) _cc.enabled = false;
            transform.position = new Vector3(p.x, ground + 0.4f, p.z);
            if (had) _cc.enabled = true;
        }
    }
}
