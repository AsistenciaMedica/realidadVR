using System.Collections.Generic;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    // Owned procedural evaluation proxy. This is NOT the production PatientMale/PatientFemale asset.
    // Smooth geometry makes motion and physical attachment reviewable while a licensed human is missing.
    public sealed class PatientAnatomicalProxy : MonoBehaviour
    {
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Material> materials=new List<Material>();
        public static PatientRigAdapter Create(Transform parent, Vector3 centre, Material source)
        {
            var go=new GameObject("Patient anatomical proxy - NEEDS_ASSET");
            go.transform.SetParent(parent,false);go.transform.localPosition=centre;
            var generator=go.AddComponent<PatientAnatomicalProxy>();
            return generator.Build(source);
        }
        PatientRigAdapter Build(Material source)
        {
            var rig=gameObject.AddComponent<PatientRigAdapter>();rig.provisionalAsset=true;
            var skin=Material(source,"Proxy skin - no human texture asset",rig.skinColor,.27f);
            var clothing=Material(source,"Matte patient clothing",new Color(.08f,.13f,.18f),.14f);
            var hair=Material(source,"Short hair proxy",new Color(.055f,.037f,.027f),.13f);
            var sclera=Material(source,"Sclera",new Color(.77f,.78f,.73f),.42f);
            var iris=Material(source,"Iris",new Color(.055f,.075f,.065f),.46f);
            var lip=Material(source,"Lips",new Color(.37f,.19f,.15f),.27f);
            rig.poseRoot=Child(transform,"Assisted posture pivot",Vector3.zero);
            rig.chestMotion=Child(rig.poseRoot,"Chest deformation pivot",Vector3.zero);
            rig.abdomenMotion=Child(rig.poseRoot,"Abdominal breathing pivot",Vector3.zero);
            var skinParts=new List<Renderer>();
            skinParts.Add(MeshPart(rig.chestMotion,"Thorax",skin,new[]{ E(new Vector3(0,0,.22f),new Vector3(.213f,.137f,.29f)), E(new Vector3(0,0,.454f),new Vector3(.07f,.073f,.12f)) }));
            skinParts.Add(MeshPart(rig.abdomenMotion,"Abdomen",skin,new[]{ E(new Vector3(0,-.015f,-.09f),new Vector3(.173f,.112f,.24f)) }));
            MeshPart(rig.poseRoot,"Trouser waistband",clothing,new[]{ E(new Vector3(0,-.012f,-.285f),new Vector3(.177f,.113f,.15f)) });
            rig.upperLegs=new Transform[2];rig.lowerLegs=new Transform[2];rig.feet=new Transform[2];
            for(int leg=0;leg<2;leg++)
            {
                int side=leg==0?-1:1;
                rig.upperLegs[leg]=Child(rig.poseRoot,"Upper leg pose",new Vector3(side*.09f,-.03f,-.29f));
                MeshPart(rig.upperLegs[leg],"Trouser thigh",clothing,new[]{E(new Vector3(0,0,-.16f),new Vector3(.086f,.092f,.226f))});
                rig.lowerLegs[leg]=Child(rig.upperLegs[leg],"Knee pose",new Vector3(side*.008f,-.01f,-.345f));
                MeshPart(rig.lowerLegs[leg],"Trouser calf",clothing,new[]{E(new Vector3(0,0,-.114f),new Vector3(.062f,.073f,.19f))});
                rig.feet[leg]=Child(rig.lowerLegs[leg],"Ankle pose",new Vector3(0,.05f,-.29f));
                MeshPart(rig.feet[leg],"Footwear",clothing,new[]{E(Vector3.zero,new Vector3(.07f,.112f,.13f))});
            }
            var arms=new List<Ellipsoid>();
            foreach(int side in new[]{-1,1})
            {
                arms.Add(E(new Vector3(side*.215f,-.02f,.31f),new Vector3(.077f,.08f,.127f)));
                arms.Add(E(new Vector3(side*.255f,-.04f,.14f),new Vector3(.064f,.066f,.205f)));
                arms.Add(E(new Vector3(side*.295f,-.05f,-.125f),new Vector3(.043f,.05f,.185f)));
                arms.Add(E(new Vector3(side*.30f,-.039f,-.318f),new Vector3(.042f,.025f,.065f)));
                for(int finger=0;finger<4;finger++) arms.Add(E(new Vector3(side*(.273f+finger*.018f),-.037f,-.392f+(finger==3?.014f:0)),new Vector3(.008f,.01f,.045f-(finger==3?.009f:0))));
                arms.Add(E(new Vector3(side*.253f,-.024f,-.325f),new Vector3(.013f,.014f,.046f)));
            }
            skinParts.Add(MeshPart(rig.poseRoot,"Arms and hands",skin,arms.ToArray()));
            rig.head=Child(rig.poseRoot,"Head motion pivot",new Vector3(0,.012f,.66f));
            skinParts.Add(MeshPart(rig.head,"Face and ears proxy",skin,new[]{ E(Vector3.zero,new Vector3(.105f,.116f,.142f)),
                E(new Vector3(-.109f,-.004f,.004f),new Vector3(.019f,.031f,.04f)),E(new Vector3(.109f,-.004f,.004f),new Vector3(.019f,.031f,.04f)),
                E(new Vector3(0,.108f,-.012f),new Vector3(.014f,.016f,.037f)),E(new Vector3(0,.12f,-.035f),new Vector3(.018f,.013f,.014f)) }));
            MeshPart(rig.head,"Short hair cap",hair,new[]{ E(new Vector3(0,-.022f,.079f),new Vector3(.108f,.105f,.076f)), E(new Vector3(0,-.095f,.005f),new Vector3(.084f,.027f,.111f)) });
            foreach(int side in new[]{-1,1})
            {
                var eye=Child(rig.head,side<0?"Left eye":"Right eye",new Vector3(side*.034f,.103f,.034f));
                MeshPart(eye,"Eye",sclera,new[]{ E(Vector3.zero,new Vector3(.022f,.007f,.010f)) });
                MeshPart(eye,"Iris",iris,new[]{ E(new Vector3(0,.006f,0),new Vector3(.007f,.0015f,.0075f)) });
                var lid=Child(rig.head,"Upper eyelid",new Vector3(side*.034f,.108f,.043f));
                skinParts.Add(MeshPart(lid,"Eyelid",skin,new[]{ E(Vector3.zero,new Vector3(.024f,.005f,.0075f)) }));
                MeshPart(rig.head,"Brow",hair,new[]{ E(new Vector3(side*.034f,.098f,.062f),new Vector3(.025f,.002f,.0035f)) });
                if(side<0) { rig.leftEye=eye;rig.leftLid=lid; } else { rig.rightEye=eye;rig.rightLid=lid; }
            }
            rig.jaw=Child(rig.head,"Mouth movement",new Vector3(0,.104f,-.069f));
            MeshPart(rig.jaw,"Lips",lip,new[]{ E(Vector3.zero,new Vector3(.027f,.003f,.004f)),E(new Vector3(0,-.001f,-.006f),new Vector3(.024f,.003f,.004f)) });
            rig.chestAnchor=Child(rig.chestMotion,"CPR sternum contact",new Vector3(0,.136f,.22f));
            rig.aedRightPadAnchor=Child(rig.chestMotion,"AED right upper chest",new Vector3(-.105f,.115f,.365f));
            rig.aedLeftPadAnchor=Child(rig.chestMotion,"AED left lateral chest",new Vector3(.177f,.073f,.09f));
            rig.aedLeftPadAnchor.localRotation=Quaternion.FromToRotation(Vector3.up,new Vector3(.7f,.7f,0).normalized);
            rig.upperArmAnchor=Child(rig.poseRoot,"Blood pressure upper arm",new Vector3(-.255f,-.04f,.15f));
            rig.fingerAnchor=Child(rig.poseRoot,"Pulse oximeter index finger",new Vector3(-.29f,-.023f,-.406f));
            rig.thighAnchor=Child(rig.upperLegs[1],"Training autoinjector lateral thigh",new Vector3(.075f,.037f,-.16f));
            rig.thighAnchor.localRotation=Quaternion.FromToRotation(Vector3.up,new Vector3(.85f,.45f,0).normalized);
            rig.woundAnchor=Child(rig.poseRoot,"Configurable wound site - proxy upper arm",new Vector3(-.255f,.029f,.15f));
            rig.chinAnchor=Child(rig.head,"Airway chin contact",new Vector3(0,.086f,-.11f));
            rig.skinRenderers=skinParts.ToArray();ConfigureContactVolumes(rig);return rig;
        }
        public static void ConfigureContactVolumes(PatientRigAdapter rig)
        {
            // Include clothing and limbs; DEA no-contact checks must not only test the CPR sternum.
            var volumes=new List<PatientContactVolume> {
                new PatientContactVolume("thorax",rig.chestMotion,new Vector3(0,0,.22f),new Vector3(.43f,.28f,.58f)),
                new PatientContactVolume("abdomen and pelvis",rig.poseRoot,new Vector3(0,-.015f,-.12f),new Vector3(.36f,.24f,.56f)),
                new PatientContactVolume("head",rig.head,Vector3.zero,new Vector3(.24f,.235f,.29f)) };
            for(int side=-1;side<=1;side+=2)
            {
                volumes.Add(new PatientContactVolume(side<0?"right upper arm":"left upper arm",rig.poseRoot,
                    new Vector3(side*.245f,-.035f,.22f),new Vector3(.145f,.145f,.4f)));
                volumes.Add(new PatientContactVolume(side<0?"right forearm and hand":"left forearm and hand",rig.poseRoot,
                    new Vector3(side*.292f,-.04f,-.21f),new Vector3(.115f,.105f,.5f)));
                int leg=side<0?0:1;
                if(rig.upperLegs.Length>leg)volumes.Add(new PatientContactVolume(side<0?"right thigh":"left thigh",rig.upperLegs[leg],
                    new Vector3(0,0,-.16f),new Vector3(.18f,.195f,.47f)));
                if(rig.lowerLegs.Length>leg)volumes.Add(new PatientContactVolume(side<0?"right lower leg and foot":"left lower leg and foot",rig.lowerLegs[leg],
                    new Vector3(0,.04f,-.16f),new Vector3(.15f,.255f,.52f)));
            }
            rig.contactVolumes=volumes.ToArray();
        }
        static Transform Child(Transform parent,string name,Vector3 position)
        {
            var node=new GameObject(name).transform;node.SetParent(parent,false);node.localPosition=position;return node;
        }
        Material Material(Material source,string label,Color color,float smoothness)
        {
            var material=source!=null?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name=label;material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",smoothness);
            material.SetColor("_EmissionColor",Color.black);material.DisableKeyword("_EMISSION");material.enableInstancing=true;
            materials.Add(material);return material;
        }
        readonly struct Ellipsoid { public readonly Vector3 centre,radius; public Ellipsoid(Vector3 c,Vector3 r) { centre=c;radius=r; } }
        static Ellipsoid E(Vector3 position,Vector3 radius) => new Ellipsoid(position,radius);
        Renderer MeshPart(Transform parent,string label,Material material,Ellipsoid[] ellipsoids)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            const int rings=10, segments=16;
            foreach(var ellipsoid in ellipsoids)
            {
                int offset=vertices.Count;
                for(int ring=0;ring<=rings;ring++) for(int segment=0;segment<=segments;segment++)
                {
                    float latitude=Mathf.PI*ring/rings,longitude=Mathf.PI*2*segment/segments;
                    var unit=new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),Mathf.Sin(latitude)*Mathf.Sin(longitude),Mathf.Cos(latitude));
                    vertices.Add(ellipsoid.centre+Vector3.Scale(unit,ellipsoid.radius));
                    normals.Add(new Vector3(unit.x/ellipsoid.radius.x,unit.y/ellipsoid.radius.y,unit.z/ellipsoid.radius.z).normalized);
                    uv.Add(new Vector2((float)segment/segments,(float)ring/rings));
                    if(ring<rings && segment<segments)
                    {
                        int a=offset+ring*(segments+1)+segment,b=a+segments+1;
                        triangles.Add(a);triangles.Add(b);triangles.Add(a+1);
                        triangles.Add(a+1);triangles.Add(b);triangles.Add(b+1);
                    }
                }
            }
            var mesh=new Mesh{name=label};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();meshes.Add(mesh);
            var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=true;return renderer;
        }
        void OnDestroy()
        {
            foreach(var mesh in meshes) if(mesh!=null) Destroy(mesh);
            foreach(var material in materials) if(material!=null) Destroy(material);
        }
    }
}
