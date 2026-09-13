using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EmergencyVR.Editor.Environment
{
    internal sealed partial class EnvironmentPrefabFactory
    {
        // All detail is static geometry, combined by material. No Update, canvases, textures or new packages.
        void Polish(string name)
        {
            switch(name)
            {
                case "Floor":
                    for(int i=-1;i<=1;i++) Box("FloorJointX"+i,new Vector3(i*2,.001f,0),new Vector3(.004f,.002f,8),"M_WallLower");
                    for(int i=-1;i<=1;i++) Box("FloorJointZ"+i,new Vector3(0,.001f,i*2),new Vector3(7,.002f,.004f),"M_WallLower");
                    break;
                case "Ceiling":
                    for(int i=-2;i<=2;i++) Box("CeilingGridX"+i,new Vector3(i*1.2f,2.994f,0),new Vector3(.014f,.008f,8),"M_WallLower");
                    for(int i=-3;i<=3;i++) Box("CeilingGridZ"+i,new Vector3(0,2.994f,i*1.2f),new Vector3(7,.008f,.014f),"M_WallLower");
                    break;
                case "HospitalBed": PolishBed(); break;
                case "PatientMonitor": PolishMonitor(); break;
                case "IVStand": PolishIV(); break;
                case "OxygenTank": PolishOxygen(); break;
                case "MedicalCart": PolishCart(); break;
                case "MedicalCabinet": PolishCabinet(); break;
                case "DefibrillatorPlaceholder": PolishDefibrillator(); break;
                case "Door":
                    Box("Track",new Vector3(.65f,2.28f,0),new Vector3(2.7f,.08f,.11f),"M_PlasticWhite");
                    Box("KickPlate",new Vector3(1.3f,.23f,.191f),new Vector3(1.08f,.34f,.012f),"M_Metal");
                    Tube("DoorPull",.018f,"M_Metal",new Vector3(.82f,.85f,.22f),new Vector3(.82f,.85f,.28f),new Vector3(.82f,1.23f,.28f),new Vector3(.82f,1.23f,.22f));
                    Text("DoorLabel","URGENCIAS",new Vector3(1.61f,1.97f,.19f),.01f,"M_MedicalBlue",180);
                    break;
                case "Window":
                    Box("Sill",new Vector3(0,-.47f,-.025f),new Vector3(1.41f,.04f,.24f),"M_PlasticWhite");
                    for(int i=0;i<3;i++) Box("PrivacyStripe"+i,new Vector3(0,-.22f+i*.1f,-.02f),new Vector3(1.18f,.025f,.006f),"M_PlasticWhite");
                    break;
                case "WallServicePanel":
                    for(int i=0;i<4;i++)
                    {
                        var x=-.49f+i*.3f;
                        Round("Outlet"+i,new Vector3(x,0,-.073f),new Vector3(.06f,.035f,.06f),"M_DarkEquipment");
                        Text("OutletLabel"+i,i<2?"O2":"AC",new Vector3(x-.025f,.105f,-.051f),.004f,"M_MedicalBlue");
                    }
                    break;
                case "Supplies":
                    TrayRim("Tray",Vector3.zero,.33f,.22f);
                    Text("PackLabel","STERILE",new Vector3(-.12f,.084f,-.062f),.003f,"M_MedicalBlue");
                    break;
            }
        }

        void PolishBed()
        {
            SoftBox("Chassis",new Vector3(0,.22f,0),new Vector3(.67f,.12f,1.33f),"M_PlasticWhite",.045f);
            Round("Lift",new Vector3(0,.36f,0),new Vector3(.17f,.28f,.17f),"M_Metal");
            SoftBox("MattressPiping",new Vector3(0,.607f,0),new Vector3(.872f,.025f,2.03f),"M_PlasticWhite",.01f);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<3;i++)
                    Tube("GuardBar"+side+"_"+i,.023f,"M_Metal",new Vector3(side*.49f,.6f,-.45f+i*.45f),new Vector3(side*.49f,.88f,-.45f+i*.45f));
                Tube("GuardReturn"+side,.027f,"M_Metal",new Vector3(side*.49f,.65f,-.64f),new Vector3(side*.49f,.9f,-.64f),new Vector3(side*.49f,.92f,-.58f),new Vector3(side*.49f,.92f,.58f),new Vector3(side*.49f,.9f,.64f),new Vector3(side*.49f,.65f,.64f));
                for(int end=-1;end<=1;end+=2)
                {
                    Box("CasterFork"+side+end,new Vector3(side*.34f,.155f,end*.79f),new Vector3(.08f,.12f,.05f),"M_Metal");
                    Round("WheelHub"+side+end,new Vector3(side*.385f,.09f,end*.79f),new Vector3(.07f,.015f,.07f),"M_Metal",true);
                    Box("Brake"+side+end,new Vector3(side*.34f,.16f,end*.85f),new Vector3(.09f,.02f,.08f),"M_MedicalBlue");
                    SoftBox("BoardGrip"+side+end,new Vector3(side*.28f,.96f,end*1.086f),new Vector3(.19f,.055f,.074f),"M_MedicalBlue",.018f);
                }
            }
            Tube("LiftBrace",.045f,"M_Metal",new Vector3(-.26f,.27f,-.49f),new Vector3(.26f,.5f,.49f));
            Text("BedAssetTag","ER 01",new Vector3(-.14f,.77f,-1.136f),.009f,"M_PlasticWhite");
        }

        void PolishMonitor()
        {
            for(int i=0;i<3;i++) RemovePart("DisplayRow_"+i);
            SoftBox("Bezel",new Vector3(-.035f,1.3f,-.076f),new Vector3(.444f,.331f,.026f),"M_DarkEquipment",.016f);
            // Inset screen surface and fixed illustrative values. No link to PatientState or case scoring.
            Box("DisplayGlass",new Vector3(-.035f,1.3f,-.093f),new Vector3(.401f,.288f,.005f),"M_Screen");
            Text("Vitals","HR 78\nSpO2 97%\nBP 120/80",new Vector3(-.219f,1.406f,-.097f),.0064f,"M_Display");
            Text("MonitorLabel","DEMO",new Vector3(.14f,1.453f,-.076f),.003f,"M_MedicalBlue");
            Round("StatusLED",new Vector3(.222f,1.407f,-.084f),new Vector3(.019f,.02f,.019f),"M_Display");
            Tube("CarryHandle",.022f,"M_Metal",new Vector3(-.14f,1.46f,.02f),new Vector3(-.14f,1.54f,.02f),new Vector3(.14f,1.54f,.02f),new Vector3(.14f,1.46f,.02f));
            Round("AdjustmentKnob",new Vector3(.045f,.96f,.08f),new Vector3(.07f,.04f,.07f),"M_DarkEquipment",true);
            Tube("MonitorCable",.008f,"M_DarkEquipment",new Vector3(0,1.21f,.1f),new Vector3(.1f,.99f,.16f),new Vector3(.12f,.51f,.14f),new Vector3(0,.12f,.06f));
        }

        void PolishIV()
        {
            RemovePart("BaseX"); RemovePart("BaseZ");
            for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*2/5;
                var end=new Vector3(Mathf.Cos(a)*.21f,.08f,Mathf.Sin(a)*.21f);
                Tube("Foot"+i,.033f,"M_Metal",new Vector3(0,.13f,0),end);
                Round("Caster"+i,end-Vector3.up*.035f,new Vector3(.065f,.025f,.065f),"M_DarkEquipment",true);
            }
            Round("PoleCollar",new Vector3(0,1.1f,0),new Vector3(.046f,.08f,.046f),"M_DarkEquipment");
            for(int i=-1;i<=1;i+=2)
                Tube("CurvedHook"+i,.012f,"M_Metal",new Vector3(i*.19f,1.98f,0),new Vector3(i*.23f,1.98f,0),new Vector3(i*.245f,1.945f,0),new Vector3(i*.23f,1.915f,0),new Vector3(i*.2f,1.915f,0));
            Box("BagLabel",new Vector3(-.16f,1.71f,-.026f),new Vector3(.094f,.115f,.004f),"M_PlasticWhite");
            Text("SalineLabel","NaCl",new Vector3(-.199f,1.737f,-.03f),.0032f,"M_MedicalBlue");
            Round("DripChamber",new Vector3(-.16f,1.505f,0),new Vector3(.024f,.065f,.024f),"M_Glass");
            Tube("FluidLine",.006f,"M_Glass",new Vector3(-.16f,1.47f,0),new Vector3(-.19f,1.22f,-.05f),new Vector3(-.36f,1.08f,-.02f),new Vector3(-.62f,.93f,.17f),new Vector3(-.84f,.9f,.2f));
        }

        void PolishOxygen()
        {
            RemovePart("Shoulder");
            Round("ShoulderLow",new Vector3(0,1.107f,0),new Vector3(.23f,.055f,.23f),"M_PlasticWhite");
            Round("ShoulderTop",new Vector3(0,1.15f,0),new Vector3(.18f,.04f,.18f),"M_PlasticWhite");
            Round("Neck",new Vector3(0,1.194f,0),new Vector3(.09f,.055f,.09f),"M_Metal");
            Round("ValveWheel",new Vector3(0,1.3f,0),new Vector3(.14f,.026f,.14f),"M_DarkEquipment");
            Box("Regulator",new Vector3(.12f,1.235f,-.03f),new Vector3(.08f,.09f,.07f),"M_Metal");
            Tube("GaugeAxis",.075f,"M_DarkEquipment",new Vector3(.105f,1.285f,-.075f),new Vector3(.105f,1.285f,-.099f));
            Tube("GaugeFace",.059f,"M_PlasticWhite",new Vector3(.105f,1.285f,-.1f),new Vector3(.105f,1.285f,-.104f));
            Tube("GaugeNeedle",.004f,"M_DarkEquipment",new Vector3(.105f,1.285f,-.107f),new Vector3(.089f,1.302f,-.107f));
            Box("CylinderLabel",new Vector3(0,.82f,-.132f),new Vector3(.15f,.21f,.008f),"M_PlasticWhite");
            Text("OxygenLabel","O2",new Vector3(-.047f,.864f,-.138f),.009f,"M_MedicalBlue");
            Tube("OxygenHose",.008f,"M_DarkEquipment",new Vector3(.15f,1.2f,-.05f),new Vector3(.2f,.97f,-.07f),new Vector3(.2f,.36f,0),new Vector3(.04f,.21f,0));
        }

        void PolishCart()
        {
            TrayRim("TopTray",new Vector3(0,.957f,0),.7f,.55f);
            for(int i=0;i<3;i++) Text("DrawerLabel"+i,"0"+(i+1),new Vector3(-.24f,.397f+i*.18f,-.28f),.004f,"M_PlasticWhite");
            Tube("PushHandle",.023f,"M_Metal",new Vector3(-.31f,.88f,.24f),new Vector3(-.31f,1.045f,.24f),new Vector3(.31f,1.045f,.24f),new Vector3(.31f,.88f,.24f));
            Box("SupplyPack",new Vector3(.23f,.994f,.07f),new Vector3(.1f,.07f,.18f),"M_Wall_Hospital");
            Box("PackBand",new Vector3(.23f,1.032f,.07f),new Vector3(.035f,.004f,.18f),"M_MedicalBlue");
            for(int i=-1;i<=1;i+=2) Box("Bumper"+i,new Vector3(i*.337f,.25f,0),new Vector3(.035f,.05f,.5f),"M_DarkEquipment");
        }

        void PolishCabinet()
        {
            RemovePart("Body");
            Box("CabinetBack",new Vector3(0,1.02f,.22f),new Vector3(1.15f,1.94f,.04f),"M_WallLower");
            Box("CabinetTop",new Vector3(0,1.98f,0),new Vector3(1.15f,.05f,.47f),"M_PlasticWhite");
            for(int side=-1;side<=1;side+=2)
            {
                RemovePart("Door_"+side); RemovePart("Glass_"+side);
                Box("CabinetSide"+side,new Vector3(side*.553f,1.02f,0),new Vector3(.045f,1.94f,.47f),"M_PlasticWhite");
                SoftBox("LowerDoor"+side,new Vector3(side*.28f,.52f,-.25f),new Vector3(.54f,.89f,.04f),"M_PlasticWhite",.013f);
                // Recessed open upper compartments, framed like sliding glazed storage.
                Box("UpperFrame"+side,new Vector3(side*.55f,1.48f,-.253f),new Vector3(.045f,.98f,.045f),"M_Metal");
                Box("GlassLip"+side,new Vector3(side*.28f,1.03f,-.252f),new Vector3(.51f,.1f,.012f),"M_Glass");
            }
            Box("CenterMullion",new Vector3(0,1.48f,-.252f),new Vector3(.035f,.98f,.035f),"M_Metal");
            for(int shelf=0;shelf<3;shelf++)
            {
                float y=.98f+shelf*.31f;
                Box("Shelf"+shelf,new Vector3(0,y,0),new Vector3(1.06f,.025f,.43f),"M_PlasticWhite");
                for(int i=0;i<3;i++)
                {
                    var p=new Vector3(-.38f+i*.36f,y+.103f,.025f);
                    Box("Stock"+shelf+i,p,new Vector3(.25f,.18f,.22f),"M_Wall_Hospital");
                    Box("StockBand"+shelf+i,p+new Vector3(0,0,-.112f),new Vector3(.19f,.04f,.006f),"M_MedicalBlue");
                }
            }
        }

        void PolishDefibrillator()
        {
            SoftBox("DefibBezel",new Vector3(-.06f,.16f,-.142f),new Vector3(.218f,.18f,.018f),"M_DarkEquipment",.01f);
            Box("DefibDisplay",new Vector3(-.06f,.16f,-.154f),new Vector3(.18f,.14f,.006f),"M_Screen");
            Text("DefibText","DEA\nDEMO",new Vector3(-.137f,.2f,-.159f),.005f,"M_Display");
            for(int i=0;i<3;i++) Round("DefibButton"+i,new Vector3(.115f,.22f-i*.05f,-.162f),new Vector3(.03f,.024f,.03f),i==0?"M_Display":"M_PlasticWhite");
            for(int side=-1;side<=1;side+=2)
            {
                SoftBox("PaddlePad"+side,new Vector3(side*.25f,.09f,0),new Vector3(.085f,.11f,.17f),"M_DarkEquipment",.021f);
                SoftBox("PaddleHandle"+side,new Vector3(side*.25f,.17f,0),new Vector3(.04f,.09f,.13f),"M_MedicalBlue",.016f);
                Tube("PaddleCable"+side,.008f,"M_DarkEquipment",new Vector3(side*.25f,.1f,.06f),new Vector3(side*.3f,.05f,.18f),new Vector3(side*.27f,.02f,.22f),new Vector3(side*.14f,.04f,.21f),new Vector3(side*.14f,.19f,.135f));
            }
        }

        void WallFinish()
        {
            Box("BackWainscot",new Vector3(0,.51f,4.987f),new Vector3(7,.7f,.012f),"M_WallLower");
            Box("BackBumper",new Vector3(0,.89f,4.97f),new Vector3(7,.06f,.035f),"M_PlasticWhite");
            for(int side=-1;side<=1;side+=2)
            {
                Box("SideWainscot"+side,new Vector3(side*3.487f,.51f,1),new Vector3(.012f,.7f,8),"M_WallLower");
                Box("SideBumper"+side,new Vector3(side*3.471f,.89f,1),new Vector3(.035f,.06f,8),"M_PlasticWhite");
                Box("FrontWainscot"+side,new Vector3(side*2.05f,.51f,-2.987f),new Vector3(2.9f,.7f,.012f),"M_WallLower");
                Box("CeilingCove"+side,new Vector3(side*3.467f,2.96f,1),new Vector3(.06f,.07f,8),"M_PlasticWhite");
            }
            Box("BackCove",new Vector3(0,2.96f,4.966f),new Vector3(7,.07f,.06f),"M_PlasticWhite");
        }

        void RoomDetails()
        {
            // Positions are in room coordinates; small props are combined into one reusable detail prefab.
            SoftBox("Bin",new Vector3(-3.06f,.29f,3.82f),new Vector3(.34f,.53f,.34f),"M_PlasticWhite",.065f);
            SoftBox("BinLid",new Vector3(-3.06f,.57f,3.82f),new Vector3(.37f,.065f,.37f),"M_MedicalBlue",.023f);
            Box("BinPedal",new Vector3(-3.06f,.055f,3.61f),new Vector3(.16f,.025f,.09f),"M_DarkEquipment");
            Solid(new Vector3(-3.06f,.32f,3.82f),new Vector3(.4f,.64f,.44f));
            SoftBox("Dispenser",new Vector3(-.95f,1.34f,4.88f),new Vector3(.2f,.32f,.18f),"M_PlasticWhite",.032f);
            Box("DispenserWindow",new Vector3(-.95f,1.35f,4.784f),new Vector3(.065f,.15f,.007f),"M_Glass");
            Box("DispenserLever",new Vector3(-.95f,1.17f,4.74f),new Vector3(.1f,.025f,.15f),"M_Metal");
            // Clock, deliberately static decorative hands.
            Tube("ClockRim",.33f,"M_MedicalBlue",new Vector3(-.7f,2.46f,4.955f),new Vector3(-.7f,2.46f,4.9f));
            Tube("ClockFace",.294f,"M_PlasticWhite",new Vector3(-.7f,2.46f,4.897f),new Vector3(-.7f,2.46f,4.89f));
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;
                var d=new Vector3(Mathf.Sin(a),Mathf.Cos(a),0);
                Tube("ClockTick"+i,.006f,"M_MedicalBlue",new Vector3(-.7f,2.46f,4.885f)+d*.12f,new Vector3(-.7f,2.46f,4.885f)+d*.134f);
            }
            Tube("ClockMinute",.008f,"M_DarkEquipment",new Vector3(-.7f,2.46f,4.88f),new Vector3(-.62f,2.54f,4.88f));
            Tube("ClockHour",.011f,"M_DarkEquipment",new Vector3(-.7f,2.46f,4.878f),new Vector3(-.764f,2.49f,4.878f));
            Box("BaySign",new Vector3(1.55f,2.3f,4.96f),new Vector3(1.17f,.29f,.026f),"M_MedicalBlue");
            Text("BayTitle","URGENCIAS 01",new Vector3(1.043f,2.364f,4.943f),.014f,"M_PlasticWhite");
            Box("ExitSign",new Vector3(0,2.56f,-2.963f),new Vector3(.83f,.24f,.03f),"M_MedicalBlue");
            Text("ExitText","SALIDA",new Vector3(.31f,2.63f,-2.943f),.017f,"M_Display",180);
            Box("HygieneSign",new Vector3(-.95f,1.69f,4.96f),new Vector3(.35f,.16f,.015f),"M_WallLower");
            Text("HygieneText","HIGIENE",new Vector3(-1.09f,1.72f,4.949f),.0065f,"M_MedicalBlue");
        }

        void RemovePart(string name)
        {
            var part=visual.Find(name);
            if(part!=null) Object.DestroyImmediate(part.gameObject);
        }

        void TrayRim(string name,Vector3 center,float width,float depth)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Box(name+"RimX"+side,center+new Vector3(side*(width*.5f-.01f),.04f,0),new Vector3(.015f,.035f,depth),"M_Metal");
                Box(name+"RimZ"+side,center+new Vector3(0,.04f,side*(depth*.5f-.01f)),new Vector3(width,.035f,.015f),"M_Metal");
            }
        }

        void Tube(string name,float diameter,string material,params Vector3[] points)
        {
            for(int i=1;i<points.Length;i++)
            {
                var delta=points[i]-points[i-1];
                Round(name+"_"+i,(points[i]+points[i-1])*.5f,new Vector3(diameter,delta.magnitude,diameter),material);
                visual.GetChild(visual.childCount-1).localRotation=Quaternion.FromToRotation(Vector3.up,delta.normalized);
            }
        }

        void SoftBox(string name,Vector3 pos,Vector3 size,string material,float radius)
        {
            var vertices=new List<Vector3>(); var indices=new List<int>();
            var half=size*.5f; radius=Mathf.Min(radius,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.9f);
            var inner=half-Vector3.one*radius;
            var normals=new[] { Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back };
            foreach(var normal in normals)
            {
                var u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up;
                var v=Vector3.Cross(normal,u);
                float hu=Vector3.Scale(u,half).magnitude,hv=Vector3.Scale(v,half).magnitude,hn=Vector3.Scale(normal,half).magnitude;
                float[] xs={-hu,-hu+radius,hu-radius,hu},ys={-hv,-hv+radius,hv-radius,hv};
                int first=vertices.Count;
                for(int y=0;y<4;y++) for(int x=0;x<4;x++)
                {
                    var p=normal*hn+u*xs[x]+v*ys[y];
                    var q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                    vertices.Add(q+(p-q).normalized*radius);
                }
                for(int y=0;y<3;y++) for(int x=0;x<3;x++)
                {
                    int a=first+y*4+x;
                    indices.AddRange(new[] {a,a+1,a+5,a,a+5,a+4});
                }
            }
            var mesh=new Mesh { name=name }; mesh.SetVertices(vertices); mesh.SetTriangles(indices,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            MeshPart(name,mesh,pos,material);
        }

        void MeshPart(string name,Mesh mesh,Vector3 position,string material,float yaw=0)
        {
            temporaryMeshes.Add(mesh);
            var part=Child(visual,name).gameObject;
            part.transform.localPosition=position; part.transform.localRotation=Quaternion.Euler(0,yaw,0);
            part.AddComponent<MeshFilter>().sharedMesh=mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial=materials[material];
        }

        void Text(string name,string text,Vector3 topLeft,float pixel,string material,float yaw=0)
        {
            // Compact procedural 5x7 lettering, one quad per contiguous row segment, batched with the visual.
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            int column=0,line=0;
            foreach(char raw in text)
            {
                if(raw=='\n') { column=0; line++; continue; }
                string rows;
                if(!Glyphs.TryGetValue(char.ToUpperInvariant(raw),out rows)) { column++; continue; }
                var bits=rows.Split('/');
                for(int y=0;y<7;y++)
                    for(int x=0;x<5;x++)
                    {
                        if(bits[y][x]!='1') continue;
                        int end=x+1; while(end<5 && bits[y][end]=='1') end++;
                        float left=(column*6+x)*pixel,right=(column*6+end)*pixel,top=-(line*10+y)*pixel,bottom=top-pixel;
                        int k=vertices.Count;
                        vertices.AddRange(new[] {new Vector3(left,top,0),new Vector3(right,top,0),new Vector3(right,bottom,0),new Vector3(left,bottom,0)});
                        triangles.AddRange(new[] {k,k+1,k+2,k,k+2,k+3}); x=end-1;
                    }
                column++;
            }
            var mesh=new Mesh {name=name}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            MeshPart(name,mesh,topLeft,material,yaw);
        }

        static readonly Dictionary<char,string> Glyphs=new Dictionary<char,string>
        {
            {'0',"01110/10001/10011/10101/11001/10001/01110"},{'1',"00100/01100/00100/00100/00100/00100/01110"},
            {'2',"01110/10001/00001/00010/00100/01000/11111"},{'3',"11110/00001/00001/01110/00001/00001/11110"},
            {'4',"00010/00110/01010/10010/11111/00010/00010"},{'5',"11111/10000/10000/11110/00001/00001/11110"},
            {'6',"01110/10000/10000/11110/10001/10001/01110"},{'7',"11111/00001/00010/00100/01000/01000/01000"},
            {'8',"01110/10001/10001/01110/10001/10001/01110"},{'9',"01110/10001/10001/01111/00001/00001/01110"},
            {'A',"01110/10001/10001/11111/10001/10001/10001"},{'B',"11110/10001/10001/11110/10001/10001/11110"},
            {'C',"01111/10000/10000/10000/10000/10000/01111"},{'D',"11110/10001/10001/10001/10001/10001/11110"},
            {'E',"11111/10000/10000/11110/10000/10000/11111"},{'G',"01111/10000/10000/10111/10001/10001/01111"},
            {'H',"10001/10001/10001/11111/10001/10001/10001"},{'I',"11111/00100/00100/00100/00100/00100/11111"},
            {'L',"10000/10000/10000/10000/10000/10000/11111"},{'M',"10001/11011/10101/10101/10001/10001/10001"},
            {'N',"10001/11001/11001/10101/10011/10011/10001"},{'O',"01110/10001/10001/10001/10001/10001/01110"},
            {'P',"11110/10001/10001/11110/10000/10000/10000"},{'R',"11110/10001/10001/11110/10100/10010/10001"},
            {'S',"01111/10000/10000/01110/00001/00001/11110"},{'T',"11111/00100/00100/00100/00100/00100/00100"},
            {'U',"10001/10001/10001/10001/10001/10001/01110"},{'%',"11001/11010/00010/00100/01000/01011/10011"},
            {'/',"00001/00010/00010/00100/01000/01000/10000"}
        };
    }
}
