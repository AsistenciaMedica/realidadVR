using UnityEngine;

namespace EmergencyVR.XR
{
    /// <summary>
    /// One invisible, static ground slab under every training environment (top face at y = 0).
    /// The procedural environments only give colliders to props and perimeters, so without this the
    /// doctor could walk off the original 8 × 8 m room floor or fall through the football pitch.
    /// Shared by the Windows controller and the Quest rig; it has no renderer and costs one box collider.
    /// </summary>
    public static class WalkableGround
    {
        public const string ObjectName = "VITAL walkable ground";
        public static readonly Vector3 Centre = new Vector3(0, -.05f, 10);
        public static readonly Vector3 Size = new Vector3(80, .1f, 80);

        public static BoxCollider Ensure()
        {
            var existing = GameObject.Find(ObjectName);
            if (existing != null && existing.TryGetComponent<BoxCollider>(out var found)) return found;
            var ground = new GameObject(ObjectName, typeof(BoxCollider));
            ground.transform.position = Centre;
            var box = ground.GetComponent<BoxCollider>();
            box.size = Size;
            return box;
        }
    }
}
