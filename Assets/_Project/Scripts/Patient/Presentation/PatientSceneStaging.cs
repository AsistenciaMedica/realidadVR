using System;
using EmergencyVR.Scenarios;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    // Furniture follows a selected, settled seated pose once. It never writes patient transforms,
    // medical state, interaction anchors or position-confirmation outcomes.
    [DefaultExecutionOrder(160)]
    public sealed class PatientSceneStaging : MonoBehaviour
    {
        ReviewCaseSession review;
        PatientVisualController visual;
        ArticulatedPatient body;
        Transform assembly;
        BoxCollider seat;
        readonly BoxCollider[] feet = new BoxCollider[2];
        readonly Transform[] legs = new Transform[4];
        readonly RaycastHit[] hits = new RaycastHit[32];
        Material upholstery, frame;
        Mesh sample;
        Vector3 previousPelvis, previousLeft, previousRight, stagedPelvis;
        float settledSeconds;
        bool observing, attempted;
        Collider floor;
        float floorHeight = float.NaN;
        string floorCandidates = "";

        public bool SupportVisible => assembly != null && assembly.gameObject.activeInHierarchy;
        public Transform SupportRoot => assembly;
        public BoxCollider Seat => seat;
        public int LayoutCount { get; private set; }
        public string LastValidationFailure { get; private set; } = "";
        public string LastSupportMeasurement { get; private set; } = "";

        public static PatientSceneStaging Attach(ReviewCaseSession owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            var existing = owner.GetComponent<PatientSceneStaging>();
            if (existing != null) return existing;
            var staging = owner.gameObject.AddComponent<PatientSceneStaging>();
            staging.review = owner;
            staging.visual = owner.Procedures.Visuals;
            staging.body = staging.visual.Rig.GetComponent<ArticulatedPatient>();
            owner.SelectionChanged += staging.Selected;
            owner.Manager.Changed += staging.StateChanged;
            staging.Selected();
            return staging;
        }

        void Selected()
        {
            Withdraw();
            LastValidationFailure = "";
            LastSupportMeasurement = "";
        }

        bool RequiresSeat() => review != null && body != null && body.enabled &&
            review.Selected.medical != null && review.Selected.medical.clinicalV2 == null &&
            !visual.ExternalBodyPresentation && visual.EffectivePosture == PatientPosture.Seated &&
            visual.VisualState.Consciousness != PatientConsciousness.Unresponsive &&
            visual.VisualState.Breathing != PatientBreathingMode.Absent &&
            visual.VisualState.Breathing != PatientBreathingMode.Agonal &&
            !visual.VisualState.Seizure && visual.CompressionDepthMetres <= .001f &&
            (visual.Rig.controlledRagdoll == null || !visual.Rig.controlledRagdoll.OwnsPose);

        void StateChanged() { if (!RequiresSeat()) Withdraw(); }

        void LateUpdate()
        {
            if (!RequiresSeat()) { Withdraw(); return; }
            if (body.pelvis == null || body.ankles == null || body.ankles.Length < 2 ||
                body.ankles[0] == null || body.ankles[1] == null) return;
            if (SupportVisible)
            {
                // An unexpected displacement invalidates furniture; never chase a falling/moving body.
                if (Vector3.Distance(stagedPelvis, body.pelvis.position) > .06f)
                {
                    assembly.gameObject.SetActive(false);
                    LastValidationFailure = "El paciente se ha desplazado fuera del asiento.";
                }
                return;
            }
            if (attempted || Time.deltaTime <= 0 || !body.gameObject.activeInHierarchy) return;
            var pelvis = body.pelvis.position;
            var left = body.ankles[0].position;
            var right = body.ankles[1].position;
            float movement = Mathf.Max(Vector3.Distance(pelvis, previousPelvis),
                Mathf.Max(Vector3.Distance(left, previousLeft), Vector3.Distance(right, previousRight)));
            settledSeconds = observing && movement / Time.deltaTime < .01f ? settledSeconds + Time.deltaTime : 0;
            observing = true;
            previousPelvis = pelvis; previousLeft = left; previousRight = right;
            if (settledSeconds < .15f) return;
            attempted = true;
            PlaceFurniture();
        }

        void Withdraw()
        {
            if (assembly != null) assembly.gameObject.SetActive(false);
            observing = attempted = false;
            settledSeconds = 0;
        }

        struct Geometry
        {
            public Vector3 pelvis, forward, leftSole, rightSole;
            public float seatTop;
        }

        bool Measure(out Geometry geometry)
        {
            geometry = default;
            var skin = visual.Rig.face;
            if (skin == null || skin.sharedMesh == null || body.calves == null || body.calves.Length < 2)
                return Fail("Faltan la malla o las rodillas para medir el asiento.");
            geometry.pelvis = body.pelvis.position;
            geometry.forward = Vector3.ProjectOnPlane((body.calves[0].position + body.calves[1].position) * .5f - geometry.pelvis, Vector3.up).normalized;
            if (geometry.forward.sqrMagnitude < .5f) return Fail("La postura no define una orientación sentada.");
            Vector3 lateral = Vector3.Cross(Vector3.up, geometry.forward);
            if (sample == null) sample = new Mesh { name = "Seated contact measurement" };
            skin.BakeMesh(sample);
            var vertices = sample.vertices;
            var weights = skin.sharedMesh.boneWeights;
            var bones = skin.bones;
            if (weights.Length != vertices.Length) return Fail("La malla no permite medir la suela por sus huesos.");
            var leftBones = new bool[bones.Length];
            var rightBones = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                leftBones[i] = bones[i] != null && (bones[i] == body.ankles[0] || bones[i].IsChildOf(body.ankles[0]));
                rightBones[i] = bones[i] != null && (bones[i] == body.ankles[1] || bones[i].IsChildOf(body.ankles[1]));
            }
            float bottom = float.PositiveInfinity, left = float.PositiveInfinity, right = float.PositiveInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                var point = skin.transform.TransformPoint(vertices[i]);
                var delta = point - geometry.pelvis;
                // Underside immediately beneath the pelvis, excluding the arms and descending shins.
                float along = Vector3.Dot(delta, geometry.forward);
                if (Mathf.Abs(Vector3.Dot(delta, lateral)) < .22f && along > -.18f && along < .08f &&
                    delta.y < -.035f && delta.y > -.24f) bottom = Mathf.Min(bottom, point.y);
                if (Influence(weights[i], leftBones) > .35f && point.y < left)
                { left = point.y; geometry.leftSole = point; }
                if (Influence(weights[i], rightBones) > .35f && point.y < right)
                { right = point.y; geometry.rightSole = point; }
            }
            if (float.IsInfinity(bottom) || float.IsInfinity(left) || float.IsInfinity(right))
                return Fail("No se identificaron contactos de pelvis y ambas suelas en la malla.");
            geometry.seatTop = bottom + .004f;
            return true;
        }

        static float Influence(BoneWeight weight, bool[] mask)
        {
            float Read(int index, float amount) => index >= 0 && index < mask.Length && mask[index] ? amount : 0;
            return Read(weight.boneIndex0, weight.weight0) + Read(weight.boneIndex1, weight.weight1) +
                Read(weight.boneIndex2, weight.weight2) + Read(weight.boneIndex3, weight.weight3);
        }

        void PlaceFurniture()
        {
            if (!Measure(out var geometry)) return;
            floor = null;
            floorHeight = float.NaN;
            floorCandidates = "";
            Physics.SyncTransforms();
            int count = Physics.RaycastNonAlloc(geometry.pelvis, Vector3.down, hits, 2f, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) { FailGeometry("Demasiadas superficies para identificar el suelo.", geometry); return; }
            float floorY = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.normal.y < .95f || hit.collider.transform.IsChildOf(visual.transform) ||
                    (assembly != null && hit.collider.transform.IsChildOf(assembly)) || hit.point.y >= geometry.seatTop - .15f) continue;
                floorCandidates += FormattableString.Invariant($" [{ColliderPath(hit.collider)} y={hit.point.y:F3}]");
                if (hit.point.y > floorY) { floorY = hit.point.y; floor = hit.collider; }
            }
            floorHeight = floor == null ? float.NaN : floorY;
            RecordMeasurement(geometry);
            if (floor == null) { FailGeometry("No se encontró suelo bajo el asiento.", geometry); return; }
            if (geometry.leftSole.y < floorY - .025f || geometry.rightSole.y < floorY - .025f)
            { FailGeometry("La postura introduce un pie bajo el suelo; requiere corregir la pose.", geometry); return; }
            if (geometry.leftSole.y > geometry.seatTop - .1f || geometry.rightSole.y > geometry.seatTop - .1f)
            { FailGeometry("La postura eleva los pies hasta el asiento; requiere corregir la pose.", geometry); return; }
            EnsureAssembly();
            assembly.SetPositionAndRotation(new Vector3(geometry.pelvis.x, floorY, geometry.pelvis.z), Quaternion.LookRotation(geometry.forward));
            float height = geometry.seatTop - floorY;
            SetBox(seat.transform, new Vector3(0, height - .04f, 0), new Vector3(.82f, .08f, .46f));
            for (int i = 0; i < legs.Length; i++)
                SetBox(legs[i], new Vector3(i % 2 == 0 ? -.34f : .34f, (height - .08f) * .5f, i < 2 ? -.16f : .16f),
                    new Vector3(.045f, height - .08f, .045f));
            PlaceFootrest(feet[0], geometry.leftSole, body.ankles[0].position, floorY);
            PlaceFootrest(feet[1], geometry.rightSole, body.ankles[1].position, floorY);
            stagedPelvis = geometry.pelvis;
            assembly.gameObject.SetActive(true);
            LayoutCount++;
            ValidateSupport();
        }

        void PlaceFootrest(BoxCollider footrest, Vector3 sole, Vector3 ankle, float floorY)
        {
            // A visible solid footrest reaches the actual shoe surface, not an assumed ankle offset.
            float height = sole.y - floorY;
            bool needed = height > .018f;
            footrest.gameObject.SetActive(needed);
            if (!needed) return;
            var centre = assembly.InverseTransformPoint(Vector3.Lerp(sole, ankle, .4f));
            centre.y = height * .5f;
            SetBox(footrest.transform, centre, new Vector3(.28f, height, .44f));
        }

        public bool ValidateSupport()
        {
            if (!SupportVisible || !RequiresSeat()) return Fail("El asiento no está activo para esta postura.");
            if (!Measure(out var geometry)) return false;
            RecordMeasurement(geometry);
            Physics.SyncTransforms();
            if (!seat.Raycast(new Ray(geometry.pelvis, Vector3.down), out var contact, .28f) ||
                contact.normal.y < .95f || Mathf.Abs(contact.point.y - geometry.seatTop) > .02f)
                return FailGeometry("El asiento no coincide con la superficie bajo la pelvis.", geometry);
            if (!FootSupported(geometry.leftSole, feet[0]) || !FootSupported(geometry.rightSole, feet[1]))
                return FailGeometry("Una suela no tiene apoyo visible en el suelo o reposapiés.", geometry);
            LastValidationFailure = "";
            return true;
        }

        bool FootSupported(Vector3 sole, Collider footrest)
        {
            var support = footrest.gameObject.activeInHierarchy ? footrest : floor;
            return support != null && support.Raycast(new Ray(sole + Vector3.up * .025f, Vector3.down), out var hit, .05f) && hit.normal.y > .95f;
        }

        bool Fail(string reason) { LastValidationFailure = reason; return false; }

        bool FailGeometry(string reason, Geometry geometry)
        {
            RecordMeasurement(geometry);
            return Fail(reason + " " + LastSupportMeasurement);
        }

        void RecordMeasurement(Geometry geometry)
        {
            // Metres in world space; invariant decimals keep CI reports comparable on every locale.
            LastSupportMeasurement = FormattableString.Invariant(
                $"case={review.Selected.medical?.id}; floorY={floorHeight:F3}; leftSoleY={geometry.leftSole.y:F3}; rightSoleY={geometry.rightSole.y:F3}; seatTopY={geometry.seatTop:F3}; pelvisY={geometry.pelvis.y:F3}; leftClearance={geometry.leftSole.y - floorHeight:F3}; rightClearance={geometry.rightSole.y - floorHeight:F3}; pelvisXZ=({geometry.pelvis.x:F3},{geometry.pelvis.z:F3}); floor={ColliderPath(floor)}; candidates={floorCandidates}");
        }

        static string ColliderPath(Collider collider)
        {
            if (collider == null) return "none";
            string path = collider.name;
            for (var parent = collider.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }

        void EnsureAssembly()
        {
            if (assembly != null) return;
            assembly = new GameObject("Seated patient support").transform;
            assembly.SetParent(transform, false);
            upholstery = Surface("Seated support upholstery", new Color(.055f, .18f, .20f));
            frame = Surface("Seated support frame", new Color(.22f, .26f, .28f));
            seat = Box("Patient bench seat", upholstery);
            for (int i = 0; i < legs.Length; i++) legs[i] = Box("Bench leg " + (i + 1), frame).transform;
            for (int i = 0; i < feet.Length; i++) feet[i] = Box("Patient footrest " + (i + 1), upholstery);
        }

        BoxCollider Box(string label, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = label;
            item.transform.SetParent(assembly, false);
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item.GetComponent<BoxCollider>();
        }

        static void SetBox(Transform item, Vector3 position, Vector3 size)
        { item.localPosition = position; item.localRotation = Quaternion.identity; item.localScale = size; }

        static Material Surface(string label, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = label };
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .2f);
            return material;
        }

        void OnDisable() { Withdraw(); }
        void OnDestroy()
        {
            if (review != null)
            {
                review.SelectionChanged -= Selected;
                if (review.Manager != null) review.Manager.Changed -= StateChanged;
            }
            if (assembly != null) Destroy(assembly.gameObject);
            if (upholstery != null) Destroy(upholstery);
            if (frame != null) Destroy(frame);
            if (sample != null) Destroy(sample);
        }
    }
}
