using UnityEngine;

namespace EmergencyVR.Environment.Presentation
{
    internal sealed partial class EnvironmentModuleBuilder
    {
        void Street()
        {
            // The full patient/tool area stays level with the other environments. The road is
            // beyond the curb on +X; no vehicles, street furniture or building walls enter it.
            g.Cluster("Safe pedestrian pavement");
            B(new Vector3(-1, -.10f, 8), new Vector3(10, .20f, 48), "displayStone", true);
            g.Cluster("Road and curbs");
            B(new Vector3(8.15f, -.25f, 8), new Vector3(8.1f, .22f, 48), "streetAsphalt", true);
            B(new Vector3(4.05f, -.04f, 8), new Vector3(.14f, .20f, 48), "stone", true);
            B(new Vector3(14.15f, -.10f, 8), new Vector3(3.8f, .20f, 48), "displayStone", true);
            B(new Vector3(12.18f, -.04f, 8), new Vector3(.14f, .20f, 48), "stone", true);
            for (int z = -14; z < 31; z += 5)
                B(new Vector3(8.15f, -.134f, z), new Vector3(.10f, .008f, 2.2f), "streetPaint");
            for (int stripe = 0; stripe < 7; stripe++)
                B(new Vector3(4.65f + stripe * 1.14f, -.132f, 11), new Vector3(.57f, .009f, 2.4f), "white");

            g.Cluster("Street frontage");
            for (int block = 0; block < 6; block++)
            {
                float z = -11 + block * 7.5f;
                float height = 6.4f + block % 3 * 1.25f;
                StreetFacade(new Vector3(-7.55f, 0, z), height, true, block);
                StreetFacade(new Vector3(17.65f, 0, z + 1.2f), height + .7f, false, block + 1);
            }

            g.Cluster("Pedestrian furniture");
            Bench(new Vector3(-4.7f, 0, 1.5f), 2.1f, 90);
            Bin(new Vector3(-4.9f, 0, 4.1f));
            foreach (float z in new[] { -7f, 7.5f, 21f })
            {
                // Bollards make the edge of the pedestrian area visible without obstructing care.
                R(new Vector3(3.64f, .40f, z), new Vector3(.16f, .80f, .16f), "navy");
                R(new Vector3(3.64f, .69f, z), new Vector3(.17f, .075f, .17f), "white");
                g.Solid("Pavement bollard", new Vector3(3.64f, .4f, z), new Vector3(.18f, .8f, .18f));
                StreetLamp(new Vector3(3.50f, 0, z - 1.6f));
                StreetTree(new Vector3(-4.9f, 0, z - 1.3f));
            }
            g.Cluster("Neighbourhood signage");
            B(new Vector3(-5.94f, 2.7f, 3.9f), new Vector3(.06f, .34f, 1.25f), "blue");
            g.Text("CALLE MAYOR  /  12", new Vector3(-5.89f, 2.7f, 3.9f), .022f, -90, true);
            g.Cluster("Parked cars beyond patient area");
            StreetCar(new Vector3(10.9f, -.14f, 19), "blue");
            StreetCar(new Vector3(10.9f, -.14f, -10), "white");
        }

        void StreetFacade(Vector3 origin, float height, bool near, int variant)
        {
            float face = origin.x + (near ? 1.55f : -1.55f);
            float front = near ? .035f : -.035f;
            B(origin + Vector3.up * height * .5f, new Vector3(3.1f, height, 7.15f), variant % 2 == 0 ? "plaster" : "streetBrick", true);
            B(new Vector3(face, .27f, origin.z), new Vector3(.12f, .54f, 7.17f), "stone");
            B(new Vector3(face, height - .18f, origin.z), new Vector3(.19f, .28f, 7.25f), "white");
            // Opaque glazing keeps the existing inexpensive material and batching strategy.
            for (int floor = 1; floor < 3; floor++)
                foreach (float offset in new[] { -2.3f, 0, 2.3f })
                {
                    float y = floor == 0 ? 1.45f : 3.65f + (floor - 1) * 1.8f;
                    var window = new Vector3(face + front, y, origin.z + offset);
                    B(window, new Vector3(.055f, 1.38f, 1.39f), "white");
                    B(window + new Vector3(front, 0, 0), new Vector3(.06f, 1.22f, 1.23f), "glass");
                    B(window + new Vector3(front * 2, 0, 0), new Vector3(.03f, 1.22f, .045f), "navy");
                }
            // Distinct entrances and shop windows give the street a human scale. All relief
            // stays against the facade, outside the learner, witness and equipment area.
            float outward = near ? 1 : -1;
            var door = new Vector3(face + front, 1.06f, origin.z);
            B(door, new Vector3(.09f, 2.12f, 1.18f), "white");
            B(door + Vector3.right * front * 2, new Vector3(.06f, 1.98f, 1.04f), "glass");
            B(door + new Vector3(outward * .12f, -.05f, .35f), new Vector3(.07f, .32f, .035f), "metal");
            foreach (float offset in new[] { -2.28f, 2.28f })
            {
                var display = new Vector3(face + front, 1.2f, origin.z + offset);
                B(display, new Vector3(.09f, 1.9f, 2.25f), "white");
                B(display + Vector3.right * front * 2, new Vector3(.06f, 1.73f, 2.08f), "glass");
                B(display + new Vector3(outward * .12f, -.40f, 0), new Vector3(.035f, .035f, 2.08f), "metal");
            }
            string trim = variant % 3 == 0 ? "blue" : variant % 3 == 1 ? "wood" : "green";
            B(new Vector3(face + outward * .3f, 2.38f, origin.z), new Vector3(.72f, .12f, 6.5f), trim);
            B(new Vector3(face + outward * .64f, 2.27f, origin.z), new Vector3(.06f, .20f, 6.5f), trim);
            // Limit readable signage to the central frontage: distant windows remain batched.
            if (origin.z > -5 && origin.z < 15)
            {
                B(new Vector3(face + outward * .10f, 2.82f, origin.z), new Vector3(.12f, .49f, 4.5f), trim);
                string shop = variant % 3 == 0 ? "CAFETERIA" : variant % 3 == 1 ? "LIBRERIA" : "MERCADO";
                g.Text(shop, new Vector3(face + outward * .17f, 2.82f, origin.z), .085f, near ? -90 : 90, true);
            }
            // Narrow upper sills and rails break up the repeated flat glazing.
            foreach (float offset in new[] { -2.3f, 2.3f })
            {
                var sill = new Vector3(face + outward * .18f, 2.91f, origin.z + offset);
                B(sill, new Vector3(.45f, .08f, 1.58f), "stone");
                B(sill + new Vector3(outward * .19f, .43f, 0), new Vector3(.035f, .035f, 1.58f), "metal");
                for (int rail = -2; rail <= 2; rail++)
                    B(sill + new Vector3(outward * .19f, .23f, rail * .32f), new Vector3(.025f, .40f, .025f), "metal");
            }
        }

        void StreetLamp(Vector3 position)
        {
            T(position, position + Vector3.up * 4.6f, .055f, "metal");
            T(position + Vector3.up * 4.6f, position + new Vector3(.55f, 4.65f, 0), .04f, "metal");
            Soft(position + new Vector3(.55f, 4.6f, 0), new Vector3(.42f, .1f, .23f), "navy");
            B(position + new Vector3(.55f, 4.54f, 0), new Vector3(.32f, .012f, .18f), "white");
            g.Solid("Streetlamp", position + Vector3.up * 1.5f, new Vector3(.13f, 3, .13f));
        }

        void StreetTree(Vector3 position)
        {
            B(position + Vector3.up * .09f, new Vector3(.9f, .18f, .9f), "stone", true);
            T(position + Vector3.up * .1f, position + Vector3.up * 2.5f, .12f, "wood");
            S(position + Vector3.up * 3.1f, new Vector3(2.0f, 2.4f, 2.2f), "green");
            S(position + new Vector3(.7f, 3, .5f), new Vector3(1.4f, 1.8f, 1.5f), "green");
            g.Solid("Street tree trunk", position + Vector3.up, new Vector3(.26f, 2, .26f));
        }

        void StreetCar(Vector3 position, string paint)
        {
            Soft(position + Vector3.up * .66f, new Vector3(1.72f, .65f, 3.7f), paint);
            Soft(position + new Vector3(0, 1.18f, -.17f), new Vector3(1.51f, .65f, 2.15f), "glass");
            B(position + new Vector3(0, 1.52f, -.17f), new Vector3(1.48f, .07f, 1.85f), paint);
            foreach (float x in new[] { -.85f, .85f })
                foreach (float z in new[] { -1.17f, 1.17f })
                    g.Shape("cylinder", position + new Vector3(x, .34f, z), new Vector3(.59f, .19f, .59f), "rubber", Quaternion.Euler(0, 0, 90));
            g.Solid("Parked vehicle", position + Vector3.up * .76f, new Vector3(1.8f, 1.52f, 3.75f));
        }
    }
}
