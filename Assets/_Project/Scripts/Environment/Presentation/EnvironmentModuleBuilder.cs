using System.Collections.Generic;
using UnityEngine;

namespace EmergencyVR.Environment.Presentation
{
    /// <summary>Reusable structural/secondary props; hero furniture remains replaceable by licensed assets.</summary>
    internal sealed class EnvironmentModuleBuilder
    {
        readonly EnvironmentGeometry g;
        public EnvironmentModuleBuilder(Transform root, EnvironmentPalette palette, List<Mesh> meshes)
        { g = new EnvironmentGeometry(root, palette, meshes); }

        public void Build(string id)
        {
            if (id == "football") Football();
            else
            {
                Interior(id);
                if (id == "dental") Dental();
                else if (id == "gym") Gym();
                else Mall();
            }
            g.Cluster("Portable care station");
            // This is a floor-supported station for the reused cart/DEA, outside the patient and approach route.
            B(new Vector3(-1.8f, .006f, 3.75f), new Vector3(1.02f, .012f, 1.05f), id == "football" ? "rubber" : "stone");
            g.Text("DEA", new Vector3(-1.8f, 1.63f, 4.03f), .025f);
            T(new Vector3(-1.8f, .02f, 4.08f), new Vector3(-1.8f, 1.63f, 4.08f), .02f, "metal");
            B(new Vector3(-1.8f, 1.63f, 4.055f), new Vector3(.42f, .25f, .035f), "white");
            g.Finish();
        }

        void Interior(string id)
        {
            g.Cluster("Room shell");
            string floor = id == "gym" ? "rubber" : "stone";
            B(new Vector3(0, -.10f, 1), new Vector3(7, .2f, 8), floor, true);
            B(new Vector3(0, 1.55f, 5.06f), new Vector3(7.2f, 3.1f, .12f), "plaster", true);
            B(new Vector3(-3.56f, 1.55f, 1), new Vector3(.12f, 3.1f, 8), "plaster", true);
            B(new Vector3(3.56f, 1.55f, 1), new Vector3(.12f, 3.1f, 8), "plaster", true);
            B(new Vector3(0, 1.55f, -3.06f), new Vector3(7.2f, 3.1f, .12f), "plaster", true);
            B(new Vector3(0, 3.16f, 1), new Vector3(7.2f, .12f, 8.2f), id == "gym" ? "navy" : "white");
            foreach (float x in new[] { -3.47f, 3.47f })
            {
                B(new Vector3(x, .07f, 1), new Vector3(.04f, .14f, 8), "navy");
                B(new Vector3(x, 3.05f, 1), new Vector3(.06f, .12f, 8), "white");
            }
            foreach (float z in new[] { -2.97f, 4.97f })
                B(new Vector3(0, .07f, z), new Vector3(7, .14f, .04f), "navy");
            g.Cluster("Floor finish");
            // Welded clinical flooring and rubber flooring read as continuous surfaces, not a debug grid.
            if (id == "mall")
            {
                for (int i = -1; i <= 1; i++) B(new Vector3(i * 2, .003f, 1), new Vector3(.003f, .004f, 8), "plaster");
                for (int i = -1; i <= 2; i++) B(new Vector3(0, .003f, i * 2), new Vector3(7, .004f, .003f), "plaster");
            }
            g.Cluster("Entrance");
            B(new Vector3(0, 1.16f, -2.965f), new Vector3(1.30f, 2.32f, .04f), "navy");
            B(new Vector3(0, 1.14f, -2.925f), new Vector3(1.14f, 2.20f, .05f), "blue");
            B(new Vector3(0, 1.55f, -2.888f), new Vector3(.79f, .63f, .016f), "glass");
            T(new Vector3(.43f, .87f, -2.85f), new Vector3(.43f, 1.22f, -2.85f), .021f, "metal");
            B(new Vector3(0, .24f, -2.889f), new Vector3(1.08f, .31f, .014f), "metal");
            B(new Vector3(0, 2.51f, -2.94f), new Vector3(.66f, .2f, .04f), "green");
            g.Text("SALIDA", new Vector3(0, 2.51f, -2.905f), .016f, 180, true);
            g.Cluster("Ceiling fittings");
            for (int i = 0; i < 3; i++)
            {
                float z = -1.65f + i * 2.5f;
                B(new Vector3(0, 3.04f, z), new Vector3(id == "gym" ? 2.6f : 1.3f, .09f, .48f), "metal");
                B(new Vector3(0, 2.984f, z), new Vector3(id == "gym" ? 2.46f : 1.18f, .018f, .36f), "lamp");
            }
            for (int i = -2; i < 4; i++) B(new Vector3(0, 3.086f, i * 1.25f), new Vector3(7, .024f, .014f), "stone");
            // An opaque privacy window: no expensive transparency/recursive mirror rendering.
            g.Cluster("Interior privacy glazing");
            B(new Vector3(3.483f, 1.72f, -.95f), new Vector3(.035f, 1.17f, 1.9f), "metal");
            B(new Vector3(3.452f, 1.72f, -.95f), new Vector3(.023f, 1.04f, 1.77f), "glass");
            for (int i = 0; i < 3; i++) B(new Vector3(3.433f, 1.50f + i * .13f, -.95f), new Vector3(.014f, .035f, 1.77f), "white");
        }

        void Dental()
        {
            g.Cluster("Dental chair - replacement mount");
            var c = new Vector3(1.55f, 0, 2.1f);
            S(c + new Vector3(0, .08f, 0), new Vector3(.88f, .16f, 1.33f), "white");
            R(c + new Vector3(0, .39f, -.06f), new Vector3(.24f, .55f, .24f), "metal");
            R(c + new Vector3(0, .58f, -.06f), new Vector3(.36f, .22f, .36f), "white");
            Soft(c + new Vector3(0, .77f, -.10f), new Vector3(.74f, .20f, 1.41f), "white");
            Soft(c + new Vector3(0, .875f, -.33f), new Vector3(.66f, .09f, .65f), "blue");
            Soft(c + new Vector3(0, .871f, .32f), new Vector3(.63f, .10f, .80f), "blue");
            Soft(c + new Vector3(0, .919f, .81f), new Vector3(.34f, .11f, .31f), "blue");
            Soft(c + new Vector3(0, .72f, -.89f), new Vector3(.55f, .10f, .59f), "blue", Quaternion.Euler(-16, 0, 0));
            Soft(c + new Vector3(0, .61f, -1.13f), new Vector3(.59f, .08f, .20f), "rubber");
            for (int side = -1; side <= 1; side += 2)
            {
                T(c + new Vector3(side * .34f, .70f, -.18f), c + new Vector3(side * .42f, .91f, -.18f), .027f, "metal");
                Soft(c + new Vector3(side * .43f, .94f, -.15f), new Vector3(.095f, .08f, .49f), "blue");
            }
            g.Solid("Dental chair support", c + new Vector3(0, .46f, -.08f), new Vector3(.76f, .87f, 1.66f));
            Soft(c + new Vector3(-.58f, .055f, -.70f), new Vector3(.19f, .11f, .27f), "stone");

            g.Cluster("Articulated examination lamp");
            R(new Vector3(3.22f, 2.35f, 2.9f), new Vector3(.10f, .55f, .10f), "white");
            T(new Vector3(3.22f, 2.62f, 2.9f), new Vector3(2.6f, 2.60f, 2.9f), .045f, "white");
            T(new Vector3(2.6f, 2.60f, 2.9f), new Vector3(1.85f, 2.23f, 2.6f), .043f, "white");
            R(new Vector3(2.6f, 2.60f, 2.9f), Vector3.one * .16f, "metal");
            Soft(new Vector3(1.85f, 2.19f, 2.6f), new Vector3(.42f, .12f, .25f), "white", Quaternion.Euler(8, 0, 0));
            Soft(new Vector3(1.85f, 2.119f, 2.6f), new Vector3(.31f, .027f, .17f), "lamp", Quaternion.Euler(8, 0, 0));
            foreach (float x in new[] { 1.60f, 2.10f }) T(new Vector3(x, 2.14f, 2.51f), new Vector3(x, 2.14f, 2.69f), .021f, "metal");

            g.Cluster("Sink and storage");
            Soft(new Vector3(-3.11f, .44f, 1.9f), new Vector3(.63f, .84f, 2.55f), "white");
            B(new Vector3(-3.07f, .90f, 1.9f), new Vector3(.73f, .08f, 2.64f), "stone");
            B(new Vector3(-3.14f, 1.07f, 1.9f), new Vector3(.04f, .28f, 2.6f), "glass");
            for (int i = 0; i < 4; i++)
            {
                float z = .96f + i * .62f;
                B(new Vector3(-2.784f, .45f, z), new Vector3(.017f, .70f, .58f), "plaster");
                T(new Vector3(-2.75f, .71f, z - .09f), new Vector3(-2.75f, .71f, z + .09f), .012f, "metal");
            }
            S(new Vector3(-3.06f, .938f, 2.55f), new Vector3(.53f, .04f, .57f), "metal");
            S(new Vector3(-3.06f, .962f, 2.55f), new Vector3(.40f, .014f, .43f), "navy");
            T(new Vector3(-3.33f, .95f, 2.55f), new Vector3(-3.33f, 1.19f, 2.55f), .024f, "metal");
            T(new Vector3(-3.33f, 1.19f, 2.55f), new Vector3(-3.10f, 1.19f, 2.55f), .024f, "metal");
            Bottle(new Vector3(-3.10f, .946f, 1.13f), "white");
            Soft(new Vector3(-3.10f, .986f, 1.5f), new Vector3(.37f, .07f, .27f), "blue");
            g.Solid("Sink cabinets", new Vector3(-3.08f, .47f, 1.9f), new Vector3(.78f, .94f, 2.66f));
            Bin(new Vector3(-3.02f, 0, -.1f));
            g.BrandLockup(new Vector3(.7f, 2.61f, 4.91f), 1.8f);
            g.Text("CLÍNICA DENTAL", new Vector3(.7f, 2.30f, 4.91f), .020f);
            g.Text("HIGIENE DE MANOS", new Vector3(-3.46f, 1.71f, 2.6f), .012f, -90);
            g.Text("01", new Vector3(3.42f, 2.50f, 1.3f), .045f, 90);
        }

        void Gym()
        {
            g.Cluster("Gym feature wall");
            B(new Vector3(0, 1.48f, 4.969f), new Vector3(7, 2.3f, .04f), "navy");
            B(new Vector3(0, .46f, 4.939f), new Vector3(7, .08f, .025f), "accent");
            g.BrandLockup(new Vector3(.2f, 2.45f, 4.925f), 2.2f);
            g.Text("FUERZA   ·   MOVILIDAD   ·   BIENESTAR", new Vector3(.2f, 2.12f, 4.925f), .012f, light: true);
            // Recessed opaque mirror finish conveys the wall without a second render camera on Quest.
            B(new Vector3(-3.475f, 1.58f, .8f), new Vector3(.04f, 1.5f, 3.7f), "metal");
            B(new Vector3(-3.445f, 1.58f, .8f), new Vector3(.019f, 1.40f, 3.60f), "glass");

            g.Cluster("Weight bench and free weights");
            Vector3 c = new Vector3(-2.17f, 0, 1.05f);
            B(c + Vector3.up * .012f, new Vector3(1.24f, .025f, 2.15f), "blue");
            foreach (float z in new[] { -.5f, .5f })
            {
                T(c + new Vector3(-.32f, .07f, z), c + new Vector3(.32f, .07f, z), .044f, "metal");
                T(c + new Vector3(0, .07f, z), c + new Vector3(0, .40f, z), .05f, "metal");
            }
            T(c + new Vector3(0, .30f, -.56f), c + new Vector3(0, .30f, .56f), .052f, "metal");
            Soft(c + new Vector3(0, .48f, -.36f), new Vector3(.42f, .12f, .47f), "navy");
            Soft(c + new Vector3(0, .59f, .24f), new Vector3(.43f, .12f, .72f), "navy", Quaternion.Euler(-16, 0, 0));
            g.Solid("Weight bench", c + new Vector3(0, .32f, 0), new Vector3(.7f, .65f, 1.45f));
            Dumbbell(c + new Vector3(.49f, .085f, -.8f), .22f);
            Soft(c + new Vector3(0, .68f, .47f), new Vector3(.45f, .037f, .18f), "white", Quaternion.Euler(-16, 0, 0));
            Bottle(new Vector3(-2.9f, .025f, .15f), "blue");

            g.Cluster("Dumbbell rack");
            foreach (float x in new[] { 1.32f, 2.70f })
            {
                T(new Vector3(x, .05f, 4.07f), new Vector3(x, .99f, 4.42f), .04f, "metal");
                T(new Vector3(x, .05f, 4.85f), new Vector3(x, .99f, 4.42f), .04f, "metal");
            }
            foreach (float y in new[] { .58f, .91f })
            {
                B(new Vector3(2.0f, y, 4.4f), new Vector3(1.54f, .05f, .34f), "metal");
                for (int i = 0; i < 4; i++) Dumbbell(new Vector3(1.45f + .36f * i, y + .10f, 4.4f), .21f);
            }
            g.Solid("Dumbbell rack", new Vector3(2.0f, .53f, 4.43f), new Vector3(1.65f, 1.06f, .8f));

            g.Cluster("Cardio equipment - replacement mount");
            var t = new Vector3(2.68f, 0, -.56f);
            Soft(t + new Vector3(0, .13f, 0), new Vector3(.85f, .24f, 1.73f), "metal");
            B(t + new Vector3(0, .265f, -.12f), new Vector3(.63f, .024f, 1.30f), "rubber");
            for (int i = 0; i < 8; i++) B(t + new Vector3(0, .28f, -.66f + i * .16f), new Vector3(.6f, .004f, .009f), "stone");
            foreach (float x in new[] { -.36f, .36f })
            {
                T(t + new Vector3(x, .20f, .53f), t + new Vector3(x, 1.14f, .64f), .038f, "metal");
                T(t + new Vector3(x, 1.05f, .55f), t + new Vector3(x, 1.05f, -.22f), .027f, "rubber");
            }
            Soft(t + new Vector3(0, 1.15f, .65f), new Vector3(.74f, .15f, .31f), "navy", Quaternion.Euler(-12, 0, 0));
            B(t + new Vector3(0, 1.229f, .61f), new Vector3(.27f, .012f, .14f), "glass", rotation: Quaternion.Euler(-12, 0, 0));
            g.Solid("Treadmill", t + new Vector3(0, .52f, 0), new Vector3(.92f, 1.04f, 1.8f));
            g.Cluster("Recovery corner");
            Bench(new Vector3(-2.7f, 0, -1.84f), .95f, 90);
            g.Text("HIDRATACIÓN", new Vector3(-3.47f, 2.26f, -1.8f), .015f, -90);
            g.Text("ZONA DE ENTRENAMIENTO", new Vector3(3.44f, 2.65f, 2.2f), .019f, 90);
        }

        void Mall()
        {
            // Compact interior is a recognisable corridor segment, not a claim to a complete shopping centre asset.
            g.Cluster("Mall corridor inlay");
            foreach (float x in new[] { -1.25f, 1.25f }) B(new Vector3(x, .006f, 1), new Vector3(.035f, .007f, 8), "wood");
            g.Cluster("Shopfronts");
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 3.46f;
                foreach (float z in new[] { -.6f, 3.13f })
                {
                    B(new Vector3(x, 1.4f, z), new Vector3(.07f, 2.35f, 2.92f), "navy");
                    B(new Vector3(x - side * .05f, 1.33f, z), new Vector3(.034f, 1.87f, 2.61f), "glass");
                    foreach (float offset in new[] { -1.25f, 0, 1.25f }) B(new Vector3(x - side * .08f, 1.31f, z + offset), new Vector3(.035f, 1.97f, .046f), "metal");
                    B(new Vector3(x - side * .08f, 2.36f, z), new Vector3(.06f, .33f, 2.75f), "wood");
                    g.Text(z < 1 ? "ATELIER" : "CASA  /  OBJETOS", new Vector3(x - side * .13f, 2.36f, z), .022f, side < 0 ? -90 : 90, true);
                    B(new Vector3(x - side * .08f, .30f, z), new Vector3(.035f, .22f, 2.73f), "stone");
                    // Opaque display panels and shelving sit behind facade framing rather than pretending to be transparent glass.
                    B(new Vector3(x - side * .085f, 1.13f, z + .58f), new Vector3(.035f, .59f, .72f), "blue");
                    g.Text("NUEVA\nCOLECCIÓN", new Vector3(x - side * .11f, 1.13f, z + .58f), .012f, side < 0 ? -90 : 90, true);
                }
            }
            g.Cluster("Public seating and planting");
            Bench(new Vector3(-2.59f, 0, .30f), 1.5f, 90);
            Planter(new Vector3(-2.62f, 0, 1.66f));
            Planter(new Vector3(-2.62f, 0, -1.13f));
            Bin(new Vector3(-2.87f, 0, 2.53f));
            g.Cluster("Wayfinding and end arcade");
            B(new Vector3(.25f, 1.6f, 4.965f), new Vector3(3.22f, 2.62f, .035f), "glass");
            foreach (float x in new[] { -1.35f, .25f, 1.85f }) B(new Vector3(x, 1.55f, 4.926f), new Vector3(.07f, 2.72f, .05f), "white");
            B(new Vector3(.25f, 2.76f, 4.88f), new Vector3(3.3f, .42f, .06f), "navy");
            g.Text("GALERÍA CENTRAL", new Vector3(.25f, 2.77f, 4.833f), .028f, light: true);
            B(new Vector3(.05f, 2.62f, .26f), new Vector3(2.18f, .31f, .075f), "navy");
            T(new Vector3(-.83f, 2.80f, .26f), new Vector3(-.83f, 3.06f, .26f), .012f, "metal");
            T(new Vector3(.9f, 2.80f, .26f), new Vector3(.9f, 3.06f, .26f), .012f, "metal");
            g.Text("SALIDA  ←    SERVICIOS  →", new Vector3(.05f, 2.62f, .205f), .016f, light: true);
            g.Text("INFORMACIÓN", new Vector3(2.8f, 1.86f, 4.90f), .016f);
            B(new Vector3(2.78f, 1.19f, 4.87f), new Vector3(.65f, .93f, .06f), "white");
            B(new Vector3(2.78f, 1.24f, 4.828f), new Vector3(.50f, .58f, .024f), "blue");
            g.Text("01  ·  TIENDAS\n02  ·  SERVICIOS\n03  ·  SALIDA", new Vector3(2.78f, 1.24f, 4.807f), .010f, light: true);
        }

        void Football()
        {
            g.Cluster("Pitch and treatment sideline");
            B(new Vector3(0, -.10f, 10), new Vector3(36, .20f, 52), "grass", true);
            // A clear outdoor training pitch with long sight lines and full-height goals; no indoor walls/ceiling.
            for (int i = 0; i < 13; i++)
                if (i % 2 == 0) B(new Vector3(0, .002f, -14 + i * 4), new Vector3(31, .004f, 4), "green");
            foreach (float x in new[] { -15f, 15f }) B(new Vector3(x, .010f, 10), new Vector3(.085f, .009f, 48), "white");
            foreach (float z in new[] { -14f, 10f, 34f }) B(new Vector3(0, .011f, z), new Vector3(30, .009f, .085f), "white");
            Ring(new Vector3(0, .018f, 10), 5, .035f, "white", 48);
            foreach (float end in new[] { -14f, 34f })
            {
                float direction = end < 0 ? 1 : -1;
                B(new Vector3(0, .013f, end + direction * 5.5f), new Vector3(14, .009f, .08f), "white");
                foreach (float x in new[] { -7f, 7f }) B(new Vector3(x, .013f, end + direction * 2.75f), new Vector3(.08f, .009f, 5.5f), "white");
            }
            Goal(new Vector3(0, 0, 34), 0);
            Goal(new Vector3(0, 0, -14), 180);
            g.Cluster("Near training equipment");
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3(-.5f + i * .58f, 0, -.5f);
                Soft(c + Vector3.up * .017f, new Vector3(.25f, .035f, .25f), "accent");
                g.Shape("cone", c + Vector3.up * .16f, new Vector3(.15f, .29f, .15f), "accent");
                R(c + Vector3.up * .17f, new Vector3(.088f, .045f, .088f), "white");
            }
            Ball(new Vector3(2.8f, .115f, -.30f));
            Ball(new Vector3(-2.7f, .115f, 2.6f));
            g.Cluster("Team bench");
            Bench(new Vector3(-5.1f, 0, 2.1f), 3.8f, 90);
            foreach (float z in new[] { .2f, 4f })
            {
                T(new Vector3(-5.68f, 0, z), new Vector3(-5.68f, 2.02f, z), .045f, "metal");
                T(new Vector3(-5.68f, 2.02f, z), new Vector3(-4.65f, 2.17f, z), .04f, "metal");
            }
            B(new Vector3(-5.18f, 2.16f, 2.1f), new Vector3(1.29f, .07f, 4.13f), "blue");
            B(new Vector3(-5.69f, 1.35f, 2.1f), new Vector3(.045f, 1.37f, 4.03f), "glass");
            Bottle(new Vector3(-4.87f, .48f, .7f), "blue");
            Soft(new Vector3(-4.94f, .50f, 1.5f), new Vector3(.25f, .045f, .45f), "white");
            Soft(new Vector3(-4.62f, .22f, 4.6f), new Vector3(.55f, .42f, .42f), "blue");
            Soft(new Vector3(-4.62f, .448f, 4.6f), new Vector3(.57f, .055f, .43f), "white");
            g.Solid("Team cooler", new Vector3(-4.62f, .24f, 4.6f), new Vector3(.6f, .48f, .46f));
            g.Cluster("Pitch perimeter");
            foreach (float x in new[] { -17.6f, 17.6f })
            {
                for (int z = -16; z <= 36; z += 4) T(new Vector3(x, 0, z), new Vector3(x, 1.35f, z), .035f, "metal");
                foreach (float y in new[] { .4f, 1.2f }) T(new Vector3(x, y, -16), new Vector3(x, y, 36), .028f, "metal");
                g.Solid("Perimeter", new Vector3(x, .8f, 10), new Vector3(.12f, 1.6f, 52));
            }
            foreach (float z in new[] { -15.75f, 35.75f })
            {
                for (int x = -16; x <= 16; x += 4) T(new Vector3(x, 0, z), new Vector3(x, 1.35f, z), .035f, "metal");
                foreach (float y in new[] { .4f, 1.2f }) T(new Vector3(-17.6f, y, z), new Vector3(17.6f, y, z), .028f, "metal");
                g.Solid("End perimeter", new Vector3(0, .8f, z), new Vector3(35.4f, 1.6f, .12f));
            }
            g.Cluster("Far club facilities");
            B(new Vector3(-11, .75f, 33.8f), new Vector3(8, 1.5f, 1.7f), "stone");
            B(new Vector3(-11, 1.58f, 33.8f), new Vector3(8.5f, .16f, 2.1f), "navy");
            g.BrandLockup(new Vector3(-11, 1.04f, 32.90f), 3.1f);
            g.Text("CAMPO DE ENTRENAMIENTO", new Vector3(-11, .53f, 32.90f), .027f, light: true);
            // Low draw-cost stepped seating across the far edge gives scale without an NPC crowd.
            for (int i = 0; i < 3; i++)
            {
                B(new Vector3(10.8f, .20f + i * .3f, 33 + i * .5f), new Vector3(7.5f, .3f, .60f), "stone");
                for (int seat = 0; seat < 10; seat++) Soft(new Vector3(7.55f + seat * .72f, .378f + i * .3f, 33 + i * .5f), new Vector3(.50f, .065f, .39f), "blue");
            }
        }

        void Goal(Vector3 origin, float yaw)
        {
            g.Cluster("Goal " + yaw);
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float y, float z) => origin + q * new Vector3(x, y, z);
            foreach (float x in new[] { -3.66f, 3.66f })
            {
                T(P(x, 0, 0), P(x, 2.44f, 0), .055f, "white");
                T(P(x, 2.44f, 0), P(x, .08f, 1.35f), .032f, "metal");
                T(P(x, .08f, 0), P(x, .08f, 1.35f), .032f, "metal");
            }
            T(P(-3.66f, 2.44f, 0), P(3.66f, 2.44f, 0), .055f, "white");
            T(P(-3.66f, .08f, 1.35f), P(3.66f, .08f, 1.35f), .032f, "metal");
            // Fixed sparse net topology, not physics chains, alpha cards or per-frame splines.
            for (int i = 0; i <= 14; i++) { float x = -3.66f + i * (7.32f / 14); T(P(x, 2.39f, .04f), P(x, .09f, 1.32f), .007f, "white"); }
            for (int i = 1; i < 7; i++) { float t = i / 7f; T(P(-3.65f, 2.39f * (1 - t), 1.32f * t), P(3.65f, 2.39f * (1 - t), 1.32f * t), .007f, "white"); }
            foreach (float x in new[] { -3.66f, 3.66f }) g.Solid("Goal post", P(x, 1.22f, 0), new Vector3(.14f, 2.44f, .14f));
        }

        void Bench(Vector3 c, float length, float yaw = 0)
        {
            var rotation = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float y, float z) => c + rotation * new Vector3(x, y, z);
            foreach (float x in new[] { -length * .34f, length * .34f })
            {
                T(P(x, .04f, -.20f), P(x, .44f, -.20f), .031f, "metal");
                T(P(x, .04f, .20f), P(x, .94f, .20f), .031f, "metal");
                T(P(x, .43f, -.23f), P(x, .43f, .23f), .031f, "metal");
            }
            for (int i = 0; i < 4; i++) Soft(P(0, .465f, -.185f + i * .12f), new Vector3(length, .05f, .10f), "wood", rotation);
            for (int i = 0; i < 3; i++) Soft(P(0, .64f + i * .13f, .225f), new Vector3(length, .10f, .045f), "wood", rotation);
            g.Solid("Bench", P(0, .46f, 0), new Vector3(length, .92f, .53f), rotation);
        }

        void Dumbbell(Vector3 c, float width)
        {
            T(c + Vector3.left * width * .65f, c + Vector3.right * width * .65f, .014f, "metal");
            foreach (float side in new[] { -1f, 1f })
            {
                g.Shape("cylinder", c + Vector3.right * width * .48f * side, new Vector3(.15f, .071f, .15f), "rubber", Quaternion.Euler(0, 0, 90));
                g.Shape("cylinder", c + Vector3.right * width * .68f * side, new Vector3(.052f, .008f, .052f), "metal", Quaternion.Euler(0, 0, 90));
            }
        }
        void Bottle(Vector3 p, string color)
        {
            R(p + Vector3.up * .11f, new Vector3(.075f, .22f, .075f), color);
            R(p + Vector3.up * .24f, new Vector3(.037f, .04f, .037f), "navy");
            R(p + Vector3.up * .12f, new Vector3(.078f, .08f, .078f), "white");
        }
        void Bin(Vector3 c)
        {
            Soft(c + Vector3.up * .29f, new Vector3(.34f, .55f, .34f), "white");
            Soft(c + Vector3.up * .582f, new Vector3(.37f, .05f, .37f), "navy");
            Soft(c + new Vector3(0, .045f, -.18f), new Vector3(.13f, .035f, .10f), "metal");
            g.Solid("Bin", c + Vector3.up * .3f, new Vector3(.39f, .6f, .43f));
        }
        void Planter(Vector3 c)
        {
            R(c + Vector3.up * .26f, new Vector3(.54f, .52f, .54f), "white");
            R(c + Vector3.up * .526f, new Vector3(.48f, .015f, .48f), "wood");
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2 / 7;
                var tip = c + new Vector3(Mathf.Cos(angle) * .34f, .94f + (i % 3) * .13f, Mathf.Sin(angle) * .34f);
                T(c + Vector3.up * .52f, tip, .011f, "green");
                S(tip, new Vector3(.17f, .40f, .065f), "green", Quaternion.Euler(24, -angle * Mathf.Rad2Deg, -23));
            }
            g.Solid("Planter", c + Vector3.up * .27f, new Vector3(.56f, .54f, .56f));
        }
        void Ball(Vector3 c)
        {
            S(c, Vector3.one * .23f, "white");
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2 / 5;
                var offset = new Vector3(Mathf.Cos(angle) * .107f, .008f, Mathf.Sin(angle) * .107f);
                S(c + offset, new Vector3(.065f, .068f, .020f), "navy", Quaternion.LookRotation(offset.normalized));
            }
            S(c + Vector3.up * .108f, new Vector3(.075f, .017f, .075f), "navy");
        }
        void Ring(Vector3 c, float radius, float thickness, string material, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                T(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius, c + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius, thickness, material);
            }
        }
        void B(Vector3 p, Vector3 scale, string mat, bool solid = false, Quaternion rotation = default)
        { g.Shape("box", p, scale, mat, rotation); if (solid) g.Solid(mat, p, scale, rotation); }
        void Soft(Vector3 p, Vector3 scale, string mat, Quaternion rotation = default) => g.Shape("soft", p, scale, mat, rotation);
        void S(Vector3 p, Vector3 scale, string mat, Quaternion rotation = default) => g.Shape("sphere", p, scale, mat, rotation);
        void R(Vector3 p, Vector3 scale, string mat) => g.Shape("cylinder", p, scale, mat);
        void T(Vector3 a, Vector3 b, float radius, string mat) => g.Tube(a, b, radius, mat);
    }
}
