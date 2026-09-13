using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace EmergencyVR.Editor.Environment
{
    // Geometry is authored in metres. Visual can be replaced without changing Physics or the root pivot.
    internal sealed partial class EnvironmentPrefabFactory
    {
        public const string Root = DemoProjectBuilder.Root + "/Art/Prefabs/Generated";
        public const string Materials = DemoProjectBuilder.Root + "/Art/Materials/Generated";
        public const string Meshes = DemoProjectBuilder.Root + "/Art/Meshes/Generated";
        const string Revision = "EmergencyVR.ProceduralVisuals.2";
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        Mesh cylinder;
        Transform visual;
        Transform physics;
        bool preservePhysics;
        readonly List<Mesh> temporaryMeshes = new List<Mesh>();

        public EnvironmentPrefabFactory()
        {
            foreach (var folder in new[] { Materials, Meshes, Root + "/Architecture", Root + "/Furniture", Root + "/Medical", Root + "/Props" })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            AddMaterial("M_Wall_Hospital", new Color(.82f, .85f, .83f));
            AddMaterial("M_Floor_Hospital", new Color(.48f, .53f, .53f), smooth:.16f);
            AddMaterial("M_Ceiling", new Color(.89f, .92f, .92f));
            AddMaterial("M_Metal", new Color(.56f, .6f, .61f), .35f, .36f);
            AddMaterial("M_PlasticWhite", new Color(.86f, .91f, .91f));
            AddMaterial("M_MedicalBlue", new Color(.23f, .37f, .44f));
            AddMaterial("M_DarkEquipment", new Color(.035f, .065f, .082f));
            // Opaque frosted glass: no transparency sorting or overdraw on mobile.
            AddMaterial("M_Glass", new Color(.49f, .63f, .65f), .05f, .35f);
            AddMaterial("M_Light", new Color(.86f, .91f, .9f), emission: new Color(.35f, .4f, .39f));
            AddMaterial("M_Screen", new Color(.016f, .025f, .03f), smooth:.12f);
            AddMaterial("M_WallLower", new Color(.64f,.71f,.71f));
            AddMaterial("M_Seam", new Color(.39f,.46f,.47f));
            AddMaterial("M_Teleport", new Color(.34f,.46f,.47f));
            AddMaterial("M_Display", new Color(.64f,.86f,.78f), emission:new Color(.2f,.36f,.29f));
            cylinder = AssetDatabase.LoadAssetAtPath<Mesh>(Meshes + "/Cylinder12.asset");
            if (cylinder == null)
            {
                cylinder = CreateCylinder();
                AssetDatabase.CreateAsset(cylinder, Meshes + "/Cylinder12.asset");
            }
        }

        void AddMaterial(string name, Color color, float metal = 0, float smooth = .2f, Color emission = default)
        {
            var path = Materials + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created || AssetImporter.GetAtPath(path).userData != Revision)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP/Lit is required. Existing packages/settings are not changed.");
                if(created) mat = new Material(shader) { name = name, enableInstancing = true };
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Metallic", metal);
                mat.SetFloat("_Smoothness", smooth);
                if (emission.maxColorComponent > 0)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission);
                }
                if(created) AssetDatabase.CreateAsset(mat, path);
                else EditorUtility.SetDirty(mat);
                var importer = AssetImporter.GetAtPath(path);
                importer.userData = Revision;
                importer.SaveAndReimport();
            }
            materials.Add(name, mat);
        }

        public GameObject Get(string category, string name)
        {
            string path = Root + "/" + category + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && AssetImporter.GetAtPath(path).userData == Revision) return existing;
            // Migrate the visual once per revision; keep the same root, Physics, components and GUID.
            var root = existing != null ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
            root.SetActive(false);
            try
            {
                var oldVisual = root.transform.Find("Visual");
                if(oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);
                visual = Child(root.transform, "Visual");
                physics = root.transform.Find("Physics");
                preservePhysics = physics != null;
                if(physics == null) physics = Child(root.transform, "Physics");
                switch (name)
                {
                    case "Floor": Box("Slab", new Vector3(0,-.1f,0), new Vector3(7,.2f,8), "M_Floor_Hospital"); Solid(Vector3.down * .1f, new Vector3(7,.2f,8)); break;
                    case "Ceiling": Box("Slab", Vector3.up * 3.06f, new Vector3(7,.12f,8), "M_Ceiling"); Solid(Vector3.up * 3.06f,new Vector3(7,.12f,8)); break;
                    case "Wall": Box("Wall", Vector3.zero, Vector3.one, "M_Wall_Hospital"); Solid(Vector3.zero,Vector3.one); break;
                    case "Skirting": Box("Trim", Vector3.zero, Vector3.one, "M_MedicalBlue"); break;
                    case "Door": Door(); break;
                    case "Window": Window(); break;
                    case "CeilingFixture": Box("Housing",Vector3.zero,new Vector3(1.1f,.08f,.42f),"M_Metal"); Box("Diffuser",new Vector3(0,-.046f,0),new Vector3(1.02f,.016f,.35f),"M_Light"); break;
                    case "HospitalBed": Bed(); break;
                    case "MedicalCabinet": Cabinet(); break;
                    case "MedicalCart": Cart(); break;
                    case "SideTable": Table(); break;
                    case "Stool": Stool(); break;
                    case "PatientMonitor": Monitor(); break;
                    case "IVStand": IVStand(); break;
                    case "OxygenTank": Oxygen(); break;
                    case "DefibrillatorPlaceholder": Defibrillator(); break;
                    case "Supplies": Supplies(); break;
                    case "WallServicePanel": ServicePanel(); break;
                    case "RoomDetails": RoomDetails(); break;
                    case "WallFinish": WallFinish(); break;
                    default: throw new ArgumentException("Unknown generated element: " + name);
                }
                Polish(name);
                CombineVisual(name);
                root.SetActive(true);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null) throw new IOException("Could not save " + path);
                var importer = AssetImporter.GetAtPath(path);
                importer.userData = Revision;
                importer.SaveAndReimport();
                return prefab;
            }
            finally
            {
                if(existing != null) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
                foreach(var mesh in temporaryMeshes) Object.DestroyImmediate(mesh);
                temporaryMeshes.Clear();
                preservePhysics = false;
            }
        }

        static Transform Child(Transform parent, string name)
        {
            var item = new GameObject(name).transform;
            item.SetParent(parent, false);
            return item;
        }

        void Box(string name, Vector3 pos, Vector3 size, string material)
        {
            if(name == "Mattress" || name == "Pillow" || name == "Housing" || name == "Body" || name == "Bag" || name.StartsWith("End_") || name.StartsWith("Drawer_"))
            {
                SoftBox(name,pos,size,material,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.2f);
                return;
            }
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.name = name;
            obj.transform.SetParent(visual, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = materials[material];
        }

        void Round(string name, Vector3 pos, Vector3 size, string material, bool wheel = false)
        {
            var obj = Child(visual, name).gameObject;
            obj.transform.localPosition = pos;
            obj.transform.localScale = size;
            if (wheel) obj.transform.localRotation = Quaternion.Euler(0,0,90);
            obj.AddComponent<MeshFilter>().sharedMesh = cylinder;
            obj.AddComponent<MeshRenderer>().sharedMaterial = materials[material];
        }

        void Solid(Vector3 center, Vector3 size)
        {
            if(preservePhysics) return;
            var collider = Child(physics, "Collider_" + physics.childCount).gameObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        void Wheels(float x, float z)
        {
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    Round("Wheel_" + i + "_" + j, new Vector3(i*x,.09f,j*z), new Vector3(.16f,.07f,.16f), "M_DarkEquipment", true);
        }

        void Bed()
        {
            Box("Frame",new Vector3(0,.52f,0),new Vector3(.88f,.13f,2.08f),"M_Metal");
            Box("Mattress",new Vector3(0,.675f,0),new Vector3(.86f,.15f,2.02f),"M_MedicalBlue");
            Box("Sheet",new Vector3(0,.755f,-.23f),new Vector3(.84f,.018f,1.46f),"M_PlasticWhite");
            Box("Pillow",new Vector3(0,.8f,.75f),new Vector3(.64f,.1f,.39f),"M_PlasticWhite");
            for(int i=-1;i<=1;i+=2)
            {
                Box("End_"+i,new Vector3(0,.7f,i*1.085f),new Vector3(.92f,.48f,.07f),"M_PlasticWhite");
                Box("EndAccent_"+i,new Vector3(0,.75f,i*1.127f),new Vector3(.68f,.2f,.012f),"M_MedicalBlue");
                Box("Rail_"+i,new Vector3(i*.49f,.9f,0),new Vector3(.035f,.04f,1.28f),"M_Metal");
                for(int j=-1;j<=1;j+=2)
                {
                    Box("RailPost_"+i+"_"+j,new Vector3(i*.49f,.76f,j*.59f),new Vector3(.03f,.28f,.03f),"M_Metal");
                    Box("Leg_"+i+"_"+j,new Vector3(i*.34f,.3f,j*.79f),new Vector3(.065f,.39f,.065f),"M_Metal");
                }
            }
            Wheels(.34f,.79f);
            Solid(new Vector3(0,.44f,0),new Vector3(1,.88f,2.26f));
        }

        void Cart()
        {
            Box("Body",new Vector3(0,.53f,0),new Vector3(.64f,.7f,.5f),"M_PlasticWhite");
            Box("Top",new Vector3(0,.92f,0),new Vector3(.73f,.07f,.58f),"M_Metal");
            for(int i=0;i<3;i++)
            {
                Box("Drawer_"+i,new Vector3(0,.35f+i*.18f,-.263f),new Vector3(.56f,.15f,.025f),"M_MedicalBlue");
                Box("Pull_"+i,new Vector3(0,.35f+i*.18f,-.296f),new Vector3(.24f,.025f,.04f),"M_Metal");
            }
            Wheels(.25f,.19f);
            Solid(new Vector3(0,.5f,0),new Vector3(.75f,1,.64f));
        }

        void Cabinet()
        {
            Box("Body",new Vector3(0,1.02f,0),new Vector3(1.15f,1.94f,.47f),"M_PlasticWhite");
            for(int i=-1;i<=1;i+=2)
            {
                Box("Door_"+i,new Vector3(i*.28f,1.04f,-.25f),new Vector3(.54f,1.82f,.04f),"M_Wall_Hospital");
                Box("Glass_"+i,new Vector3(i*.28f,1.49f,-.277f),new Vector3(.42f,.7f,.015f),"M_Glass");
                Box("Handle_"+i,new Vector3(i*.065f,1,-.3f),new Vector3(.025f,.22f,.055f),"M_Metal");
            }
            Solid(new Vector3(0,1,0),new Vector3(1.15f,2,.64f));
        }

        void Table()
        {
            Box("Top",new Vector3(0,.77f,0),new Vector3(1.2f,.06f,.75f),"M_PlasticWhite");
            for(int i=-1;i<=1;i+=2)
                for(int j=-1;j<=1;j+=2)
                    Box("Leg_"+i+"_"+j,new Vector3(i*.5f,.375f,j*.27f),new Vector3(.05f,.75f,.05f),"M_Metal");
            Solid(new Vector3(0,.4f,0),new Vector3(1.2f,.8f,.75f));
        }

        void Stool()
        {
            Round("Seat",new Vector3(0,.51f,0),new Vector3(.39f,.09f,.39f),"M_MedicalBlue");
            Round("Stem",new Vector3(0,.27f,0),new Vector3(.07f,.46f,.07f),"M_Metal");
            Box("FootX",new Vector3(0,.05f,0),new Vector3(.48f,.06f,.07f),"M_DarkEquipment");
            Box("FootZ",new Vector3(0,.05f,0),new Vector3(.07f,.06f,.48f),"M_DarkEquipment");
            Solid(new Vector3(0,.28f,0),new Vector3(.48f,.56f,.48f));
        }

        void Monitor()
        {
            Box("Base",new Vector3(0,.045f,0),new Vector3(.48f,.09f,.43f),"M_DarkEquipment");
            Box("Stand",new Vector3(0,.62f,.08f),new Vector3(.055f,1.16f,.055f),"M_Metal");
            Box("Housing",new Vector3(0,1.29f,0),new Vector3(.54f,.39f,.14f),"M_PlasticWhite");
            Box("Screen",new Vector3(-.035f,1.3f,-.078f),new Vector3(.4f,.29f,.017f),"M_Screen");
            // Decorative, deliberately no simulated clinical values.
            for(int i=0;i<3;i++) Box("DisplayRow_"+i,new Vector3(-.07f,1.38f-i*.08f,-.089f),new Vector3(.23f,.008f,.004f),"M_MedicalBlue");
            for(int i=0;i<3;i++) Round("Button_"+i,new Vector3(.22f,1.2f+i*.065f,-.08f),new Vector3(.027f,.027f,.027f),"M_MedicalBlue");
            Solid(new Vector3(0,.75f,0),new Vector3(.58f,1.5f,.48f));
        }

        void IVStand()
        {
            Box("BaseX",new Vector3(0,.05f,0),new Vector3(.5f,.06f,.07f),"M_DarkEquipment");
            Box("BaseZ",new Vector3(0,.05f,0),new Vector3(.07f,.06f,.5f),"M_DarkEquipment");
            Round("Pole",new Vector3(0,1.04f,0),new Vector3(.03f,2,.03f),"M_Metal");
            Box("Crossbar",new Vector3(0,1.97f,0),new Vector3(.4f,.025f,.025f),"M_Metal");
            for(int i=-1;i<=1;i+=2) Box("Hook_"+i,new Vector3(i*.19f,1.93f,0),new Vector3(.025f,.09f,.025f),"M_Metal");
            Box("Bag",new Vector3(-.16f,1.69f,0),new Vector3(.13f,.25f,.045f),"M_Glass");
            Solid(new Vector3(0,.055f,0),new Vector3(.5f,.11f,.5f));
            Solid(new Vector3(0,1.05f,0),new Vector3(.07f,2,.07f));
        }

        void Oxygen()
        {
            Round("Cylinder",new Vector3(0,.62f,0),new Vector3(.26f,.97f,.26f),"M_MedicalBlue");
            Round("Shoulder",new Vector3(0,1.14f,0),new Vector3(.18f,.1f,.18f),"M_Metal");
            Box("Valve",new Vector3(0,1.25f,0),new Vector3(.17f,.08f,.045f),"M_Metal");
            Box("Cradle",new Vector3(0,.11f,0),new Vector3(.38f,.1f,.34f),"M_DarkEquipment");
            Box("Support",new Vector3(0,.63f,.17f),new Vector3(.05f,1.1f,.05f),"M_Metal");
            Solid(new Vector3(0,.66f,0),new Vector3(.4f,1.32f,.4f));
        }

        void Defibrillator()
        {
            Box("Housing",new Vector3(0,.15f,0),new Vector3(.4f,.28f,.27f),"M_PlasticWhite");
            Box("Screen",new Vector3(-.06f,.16f,-.143f),new Vector3(.19f,.15f,.018f),"M_Screen");
            Box("Handle",new Vector3(0,.33f,0),new Vector3(.23f,.045f,.045f),"M_MedicalBlue");
            for(int i=-1;i<=1;i+=2) Box("HandlePost_"+i,new Vector3(i*.095f,.29f,0),new Vector3(.04f,.08f,.045f),"M_MedicalBlue");
            Box("Controls",new Vector3(.115f,.17f,-.147f),new Vector3(.08f,.11f,.023f),"M_MedicalBlue");
            Solid(new Vector3(0,.18f,0),new Vector3(.4f,.36f,.3f));
        }

        void Supplies()
        {
            Box("Tray",new Vector3(0,.025f,0),new Vector3(.33f,.05f,.22f),"M_Metal");
            Box("Dressings",new Vector3(-.06f,.07f,0),new Vector3(.14f,.06f,.12f),"M_PlasticWhite");
            Round("Bottle",new Vector3(.09f,.11f,0),new Vector3(.065f,.17f,.065f),"M_Glass");
            Round("Cap",new Vector3(.09f,.205f,0),new Vector3(.045f,.025f,.045f),"M_PlasticWhite");
        }

        void Door()
        {
            for(int i=-1;i<=1;i+=2)
            {
                Box("Jamb_"+i,new Vector3(i*.65f,1.07f,0),new Vector3(.1f,2.14f,.22f),"M_Metal");
                Solid(new Vector3(i*.65f,1.07f,0),new Vector3(.1f,2.14f,.22f));
            }
            Box("Header",new Vector3(0,2.17f,0),new Vector3(1.4f,.14f,.22f),"M_Metal");
            Solid(new Vector3(0,2.17f,0),new Vector3(1.4f,.14f,.22f));
            // Sliding door parked alongside the opening; clear width 1.2 m, height 2.1 m.
            Box("OpenLeaf",new Vector3(1.3f,1.04f,.15f),new Vector3(1.16f,2.08f,.065f),"M_PlasticWhite");
            Box("LeafGlass",new Vector3(1.3f,1.4f,.19f),new Vector3(.65f,.63f,.016f),"M_Glass");
            Box("LeafAccent",new Vector3(1.3f,.76f,.19f),new Vector3(1.05f,.18f,.016f),"M_MedicalBlue");
            Solid(new Vector3(1.3f,1.04f,.15f),new Vector3(1.16f,2.08f,.065f));
        }

        void Window()
        {
            Box("Glass",Vector3.zero,new Vector3(1.18f,.78f,.035f),"M_Glass");
            for(int i=-1;i<=1;i+=2)
            {
                Box("Vertical_"+i,new Vector3(i*.625f,0,0),new Vector3(.07f,.92f,.15f),"M_Metal");
                Box("Horizontal_"+i,new Vector3(0,i*.425f,0),new Vector3(1.32f,.07f,.15f),"M_Metal");
            }
            Solid(Vector3.zero,new Vector3(1.32f,.92f,.15f));
        }

        void ServicePanel()
        {
            Box("Backplate",Vector3.zero,new Vector3(1.45f,.24f,.09f),"M_PlasticWhite");
            for(int i=0;i<4;i++) Box("Socket_"+i,new Vector3(-.49f+i*.3f,0,-.054f),new Vector3(.12f,.13f,.025f),i<2?"M_MedicalBlue":"M_DarkEquipment");
        }

        void CombineVisual(string prefabName)
        {
            var filters = visual.GetComponentsInChildren<MeshFilter>(true);
            foreach(var group in filters.GroupBy(f => f.GetComponent<Renderer>().sharedMaterial))
            {
                var mesh = new Mesh { name = prefabName + "_" + group.Key.name };
                mesh.CombineMeshes(group.Select(f => new CombineInstance { mesh = f.sharedMesh,
                    transform = visual.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray(), true, true);
                // UVs for a future optional light bake; generation itself does not bake lighting.
                Unwrapping.GenerateSecondaryUVSet(mesh);
                string path = Meshes + "/" + mesh.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh,path);
                else { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh = existing; }
                var part = Child(visual,group.Key.name).gameObject;
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = group.Key;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(part, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            }
            foreach(var filter in filters) Object.DestroyImmediate(filter.gameObject);
        }

        static Mesh CreateCylinder()
        {
            const int sides = 12;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for(int i=0;i<sides;i++)
            {
                float a = i*Mathf.PI*2/sides, b = (i+1)*Mathf.PI*2/sides;
                var p = new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f);
                var q = new Vector3(Mathf.Cos(b)*.5f,0,Mathf.Sin(b)*.5f);
                int k=vertices.Count;
                vertices.AddRange(new[] { p-Vector3.up*.5f,p+Vector3.up*.5f,q+Vector3.up*.5f,q-Vector3.up*.5f,
                    Vector3.up*.5f,p+Vector3.up*.5f,q+Vector3.up*.5f,-Vector3.up*.5f,q-Vector3.up*.5f,p-Vector3.up*.5f });
                triangles.AddRange(new[] { k,k+1,k+2,k,k+2,k+3,k+4,k+6,k+5,k+7,k+9,k+8 });
            }
            var mesh = new Mesh { name = "Cylinder12" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
