using UnityEngine;
using UnityEngine.Rendering;

namespace EmergencyVR.Patient.Presentation
{
    /// <summary>A small floor-only soft contact for moving bodies, alongside static baked shadows.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class CharacterContactShadow : MonoBehaviour
    {
        static Material sharedMaterial;
        static Texture2D sharedTexture;
        static int users;
        Renderer body, shadow;
        bool ownsSharedMaterial;
        readonly RaycastHit[] hits = new RaycastHit[16];
        public bool Visible => shadow != null && shadow.enabled;

        public static CharacterContactShadow Attach(GameObject owner, Renderer body)
        {
            var contact = owner.GetComponent<CharacterContactShadow>() ?? owner.AddComponent<CharacterContactShadow>();
            contact.body = body;
            return contact;
        }

        void Start()
        {
            if (body == null) { enabled = false; return; }
            if (sharedMaterial == null)
            {
                var shader = Resources.Load<Shader>("Visual/CharacterContactShadow");
                if (shader == null) throw new System.InvalidOperationException("Missing contact-shadow shader.");
                const int size = 64;
                sharedTexture = new Texture2D(size, size, TextureFormat.RGBA32, true, true) {
                    name = "Shared soft character contact", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float radius = new Vector2((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1).magnitude;
                        float alpha = Mathf.Pow(Mathf.Clamp01(1 - radius), 1.8f) * .38f;
                        pixels[y * size + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(alpha * 255));
                    }
                sharedTexture.SetPixels32(pixels);
                sharedTexture.Apply(true, true);
                sharedMaterial = new Material(shader) { name = "Shared character contact", enableInstancing = true };
                sharedMaterial.SetTexture("_BaseMap", sharedTexture);
            }
            users++;
            ownsSharedMaterial = true;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Soft character contact";
            quad.transform.SetParent(transform, false);
            var collider = quad.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            shadow = quad.GetComponent<Renderer>();
            shadow.sharedMaterial = sharedMaterial;
            shadow.shadowCastingMode = ShadowCastingMode.Off;
            shadow.receiveShadows = false;
            shadow.lightProbeUsage = LightProbeUsage.Off;
            shadow.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void LateUpdate()
        {
            if (body == null || shadow == null) return;
            var bounds = body.bounds;
            // All authored release environments share a level ground at y=0. Ignore furniture,
            // tools and patient colliders; a contact must never float on top of an unseen object.
            var origin = new Vector3(bounds.center.x, .2f, bounds.center.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, .6f, ~0, QueryTriggerInteraction.Ignore);
            float ground = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.attachedRigidbody != null ||
                    hit.normal.y < .98f || hit.point.y > .06f) continue;
                ground = Mathf.Max(ground, hit.point.y);
            }
            shadow.enabled = body.enabled && !body.forceRenderingOff && body.gameObject.activeInHierarchy && !float.IsNegativeInfinity(ground);
            if (!shadow.enabled) return;
            shadow.transform.SetPositionAndRotation(new Vector3(bounds.center.x, ground + .009f, bounds.center.z), Quaternion.Euler(90, 0, 0));
            shadow.transform.localScale = new Vector3(Mathf.Clamp(bounds.size.x + .35f, .6f, 2.4f), Mathf.Clamp(bounds.size.z + .35f, .6f, 2.4f), 1);
        }

        void OnDestroy()
        {
            if (!ownsSharedMaterial) return;
            if (--users > 0) return;
            Destroy(sharedMaterial); Destroy(sharedTexture);
            sharedMaterial = null; sharedTexture = null;
        }
    }
}
