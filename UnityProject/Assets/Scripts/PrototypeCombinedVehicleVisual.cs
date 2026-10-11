using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // The main vehicle silhouettes reproduced with the Unity prototype's
    // low-poly materials. Only the hull collider participates in gameplay.
    public sealed class PrototypeCombinedVehicleVisual
    {
        public Transform Turret, Barrel, Muzzle, GunnerTurret, GunnerBarrel, GunnerMuzzle,
            Airframe, Rotor, Radar;
        public BoxCollider Hull;
        public Renderer[] Renderers;
        public TextMesh Instruments;
        private PrototypeRuntime game;
        private Transform root;
        private Material body, trim;

        public void Build(Transform parent, PrototypeRuntime runtime, PrototypeTeam team,
            PrototypeVehicleKind kind)
        {
            root = parent; game = runtime;
            body = team == PrototypeTeam.Blue ? game.BlueMaterial : game.RedMaterial;
            trim = team == PrototypeTeam.Blue ? game.BlueTrimMaterial : game.RedTrimMaterial;
            Hull = parent.gameObject.AddComponent<BoxCollider>();
            if (kind == PrototypeVehicleKind.Motorcycle) BuildMotorcycle();
            else if (kind == PrototypeVehicleKind.Helicopter || kind == PrototypeVehicleKind.Jet)
                BuildAircraft(kind == PrototypeVehicleKind.Jet);
            else BuildArmor(kind == PrototypeVehicleKind.AntiAir);
            if (kind != PrototypeVehicleKind.Jet && kind != PrototypeVehicleKind.Motorcycle)
                BuildGunner();
            Renderers = parent.GetComponentsInChildren<Renderer>();
        }

        private Transform Pivot(string name, Transform parent, Vector3 at)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false); pivot.localPosition = at; return pivot;
        }

        private GameObject Part(string name, Vector3 at, Vector3 size, Material material,
            Transform parent = null, PrimitiveType shape = PrimitiveType.Cube)
        {
            GameObject item = GameObject.CreatePrimitive(shape);
            item.name = name; item.transform.SetParent(parent ?? root, false);
            item.transform.localPosition = at; item.transform.localScale = size;
            Collider collider = item.GetComponent<Collider>();
            collider.enabled = false; Object.Destroy(collider);
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private void BuildArmor(bool antiAir)
        {
            Hull.center = new Vector3(0f, 1.3f, 0f);
            Hull.size = new Vector3(4.7f, 2.4f, 7.1f);
            Part("Eight-wheel armored chassis", new Vector3(0f, .73f, 0f),
                new Vector3(4.5f, .72f, 7.1f), game.ArmorMaterial);
            Part("Layered upper armor", new Vector3(0f, 1.32f, .05f),
                new Vector3(4.17f, 1f, 6.2f), body);
            GameObject nose = Part("Sloped IFV nose", new Vector3(0f, 1.71f, 2.4f),
                new Vector3(4f, .74f, 2.06f), body);
            nose.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Part("Deck trim", new Vector3(0f, 1.84f, -.2f),
                new Vector3(4.4f, .2f, 5.5f), trim);
            foreach (int side in new[] { -1, 1 })
            {
                Part("Side skirt", new Vector3(side * 2.3f, 1f, 0f),
                    new Vector3(.2f, .75f, 6.24f), body);
                foreach (float z in new[] { -2.45f, -.83f, .83f, 2.45f })
                {
                    GameObject wheel = Part("IFV road wheel", new Vector3(side * 2.1f, .65f, z),
                        new Vector3(1.27f, .23f, 1.27f), game.BootMaterial,
                        root, PrimitiveType.Cylinder);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    GameObject hub = Part("Road wheel hub", new Vector3(side * 2.25f, .65f, z),
                        new Vector3(.5f, .08f, .5f), trim, root, PrimitiveType.Cylinder);
                    hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
                Part("Headlamp", new Vector3(side * 1.4f, 1.52f, 3.3f),
                    new Vector3(.4f, .28f, .13f), game.LampMaterial);
                Part("Crew seat", new Vector3(side * .75f, 1.25f, -1.55f),
                    new Vector3(.58f, .16f, .74f), game.BootMaterial);
                Part("Crew backrest", new Vector3(side * .75f, 1.68f, -1.9f),
                    new Vector3(.58f, .66f, .12f), game.BootMaterial);
            }
            Turret = Pivot(antiAir ? "A40 turret" : "V12 turret", root, new Vector3(0f, 2f, -.1f));
            Part("Turret armor", new Vector3(0f, .33f, 0f),
                new Vector3(2.93f, .75f, 2.55f), body, Turret);
            Part("Optic housing", new Vector3(.58f, .88f, .4f),
                new Vector3(.84f, .27f, .7f), game.GlassMaterial, Turret);
            Part("Commander hatch", new Vector3(-.56f, .91f, -.3f),
                new Vector3(1.17f, .44f, 1f), game.ArmorMaterial, Turret);
            Barrel = Pivot("Elevating main weapon", Turret, new Vector3(0f, .48f, 0f));
            if (antiAir)
            {
                foreach (float x in new[] { -.53f, .53f })
                    foreach (float y in new[] { -.14f, .14f })
                        Part("35mm air cannon", new Vector3(x, y, 3.19f),
                            new Vector3(.11f, .11f, 5f), game.GunMaterial, Barrel);
                Radar = Pivot("Rotating AA radar", Turret, new Vector3(0f, 1.45f, -.55f));
                GameObject dish = Part("Radar array", Vector3.zero,
                    new Vector3(1.83f, .15f, 1.05f), game.GlassMaterial, Radar);
                dish.transform.localRotation = Quaternion.Euler(46f, 0f, 0f);
            }
            else
            {
                Part("30mm barrel", new Vector3(0f, 0f, 3.21f),
                    new Vector3(.25f, .25f, 4.56f), game.GunMaterial, Barrel);
                Part("Muzzle brake", new Vector3(0f, 0f, 5.42f),
                    new Vector3(.43f, .4f, .52f), game.ArmorMaterial, Barrel);
                Part("Coaxial machine gun", new Vector3(.43f, 0f, 2.25f),
                    new Vector3(.1f, .1f, 1.92f), game.GunMaterial, Barrel);
            }
            Muzzle = Pivot("Main bore", Barrel, new Vector3(antiAir ? -.53f : 0f, 0f, 5.68f));
            Part("Turret antenna", new Vector3(-.9f, 1.9f, -.74f),
                new Vector3(.035f, 2.5f, .035f), game.GunMaterial, Turret);
            BuildInstrument(new Vector3(.5f, 2.75f, .82f));
        }

        private void BuildGunner()
        {
            GunnerTurret = Pivot("Independent crew MG", root, new Vector3(.77f, 3.2f, -.25f));
            Part("MG ring", Vector3.zero, new Vector3(.5f, .08f, .5f),
                game.ArmorMaterial, GunnerTurret, PrimitiveType.Cylinder);
            GunnerBarrel = Pivot("Elevating crew MG", GunnerTurret, new Vector3(0f, .18f, 0f));
            Part("MG receiver", Vector3.zero, new Vector3(.35f, .3f, .6f), game.GunMaterial, GunnerBarrel);
            Part("MG barrel", new Vector3(0f, 0f, .95f), new Vector3(.14f, .14f, 1.5f),
                game.GunMaterial, GunnerBarrel);
            GunnerMuzzle = Pivot("Crew MG bore", GunnerBarrel, new Vector3(0f, 0f, 1.74f));
        }

        private void BuildAircraft(bool jet)
        {
            Hull.center = new Vector3(0f, 1.8f, 0f);
            Hull.size = new Vector3(2.4f, 2.6f, 7.4f);
            Airframe = Pivot(jet ? "Fighter airframe" : "Rotorcraft airframe", root, Vector3.zero);
            Part("Fuselage", new Vector3(0f, 1.72f, 0f),
                new Vector3(2.2f, 2.02f, 6.7f), body, Airframe);
            Part("Raised cockpit canopy", new Vector3(0f, 2.5f, 2.02f),
                new Vector3(1.82f, 1.05f, 2.1f), game.GlassMaterial, Airframe);
            Part("Nose fairing", new Vector3(0f, 1.6f, 3.4f),
                new Vector3(1.5f, 1.2f, 1.45f), body, Airframe);
            Part("Tail boom", new Vector3(0f, 1.55f, -4.3f),
                new Vector3(.9f, .65f, 4.8f), game.ArmorMaterial, Airframe);
            Part("Tail stabilizer", new Vector3(0f, 1.92f, -4.9f),
                new Vector3(3.07f, .15f, 1.53f), trim, Airframe);
            Part("Vertical tail", new Vector3(0f, 2.58f, -4.9f),
                new Vector3(.15f, 1.92f, 1.53f), trim, Airframe);
            Turret = Pivot("Aircraft forward weapon", root, new Vector3(0f, 1.9f, -.1f));
            Barrel = Pivot("Aircraft gun elevation", Turret, Vector3.zero);
            Part(jet ? "25mm nose cannon" : "70mm rocket cannon", new Vector3(0f, 0f, 4.24f),
                new Vector3(.15f, .15f, 2.88f), game.GunMaterial, Barrel);
            Muzzle = Pivot("Aircraft weapon bore", Barrel, new Vector3(0f, 0f, 5.72f));
            foreach (int side in new[] { -1, 1 })
            {
                Part("Missile pylon", new Vector3(side * 1.4f, 1.22f, -.5f),
                    new Vector3(.7f, .2f, 2.2f), game.GunMaterial, Airframe);
                Part("Missile tube", new Vector3(side * 1.4f, 1.03f, .05f),
                    new Vector3(.25f, .25f, 2.3f), game.ArmorMaterial, Airframe);
                if (jet) Part("Pilot side glazing", new Vector3(side * 1.12f, 2.25f, 2.05f),
                    new Vector3(.05f, .68f, 1.72f), game.GlassMaterial, Airframe);
            }
            if (jet)
            {
                GameObject wing = Part("Swept fighter wings", new Vector3(0f, 1.34f, -.76f),
                    new Vector3(12.5f, .18f, 3.45f), body, Airframe);
                wing.transform.localRotation = Quaternion.Euler(0f, 6f, 0f);
                Part("Jet exhaust", new Vector3(0f, 1.43f, -3.6f),
                    new Vector3(1.34f, .63f, 1.72f), game.GunMaterial, Airframe);
                foreach (int side in new[] { -1, 1 })
                    Part("Main landing gear", new Vector3(side * .95f, .45f, -.75f),
                        new Vector3(.26f, .7f, .7f), game.BootMaterial, Airframe);
                Part("Nose landing gear", new Vector3(0f, .35f, 2.42f),
                    new Vector3(.22f, .5f, .6f), game.BootMaterial, Airframe);
            }
            else
            {
                Rotor = Pivot("Powered main rotor", Airframe, new Vector3(0f, 3.65f, -.1f));
                Part("Main rotor blade X", Vector3.zero, new Vector3(13.4f, .1f, .23f),
                    game.GunMaterial, Rotor);
                Part("Main rotor blade Z", Vector3.zero, new Vector3(.23f, .1f, 13.4f),
                    game.GunMaterial, Rotor);
                Part("Rotor mast", new Vector3(0f, 3.2f, -.1f), new Vector3(.25f, .8f, .25f),
                    game.GunMaterial, Airframe);
                foreach (int side in new[] { -1, 1 })
                {
                    Part("Landing skid", new Vector3(side * 1.45f, .18f, 0f),
                        new Vector3(.15f, .15f, 4.8f), game.GunMaterial, Airframe);
                    foreach (int end in new[] { -1, 1 })
                        Part("Skid support", new Vector3(side * 1.1f, .64f, end * 1.45f),
                            new Vector3(.15f, 1f, .15f), game.GunMaterial, Airframe);
                    Part("Door side window", new Vector3(side * 1.12f, 2.08f, .3f),
                        new Vector3(.04f, .76f, 1.75f), game.GlassMaterial, Airframe);
                }
            }
            BuildInstrument(new Vector3(0f, 2.04f, 3.2f));
        }

        private void BuildMotorcycle()
        {
            Hull.center = new Vector3(0f, .85f, 0f);
            Hull.size = new Vector3(.78f, 1.6f, 2.55f);
            foreach (int end in new[] { -1, 1 })
            {
                GameObject wheel = Part("Motorcycle wheel", new Vector3(0f, .46f, end * .87f),
                    new Vector3(.91f, .11f, .91f), game.BootMaterial, root, PrimitiveType.Cylinder);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            Part("Motorcycle frame", new Vector3(0f, .78f, 0f), new Vector3(.3f, .53f, 1.43f), body);
            Part("Two-person saddle", new Vector3(0f, 1.1f, -.25f), new Vector3(.48f, .14f, 1.13f), game.BootMaterial);
            Part("Fuel tank", new Vector3(0f, 1.11f, .45f), new Vector3(.43f, .36f, .58f), body);
            Part("Fork", new Vector3(0f, .86f, .81f), new Vector3(.08f, .9f, .09f), game.GunMaterial);
            Part("Handlebars", new Vector3(0f, 1.4f, .83f), new Vector3(.87f, .06f, .07f), trim);
            Part("Headlight", new Vector3(0f, 1.16f, .98f), new Vector3(.24f, .2f, .15f), game.LampMaterial);
            BuildInstrument(new Vector3(0f, 1.30f, .73f));
        }

        private void BuildInstrument(Vector3 at)
        {
            // A small physical display stays below the seat's unobstructed
            // sightline; it also makes pilot/driver stations distinguishable.
            Transform panel = Pivot("Seat instrument panel", root, at);
            Part("Instrument backing", Vector3.zero, new Vector3(.8f, .32f, .06f), game.GunMaterial, panel);
            Instruments = new GameObject("Speed armor altitude display").AddComponent<TextMesh>();
            Instruments.transform.SetParent(panel, false);
            Instruments.transform.localPosition = new Vector3(-.34f, .09f, -.04f);
            Instruments.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Instruments.characterSize = .023f; Instruments.fontSize = 32;
            Instruments.color = new Color(.64f, .89f, .77f);
            Instruments.anchor = TextAnchor.UpperLeft;
        }
    }
}
