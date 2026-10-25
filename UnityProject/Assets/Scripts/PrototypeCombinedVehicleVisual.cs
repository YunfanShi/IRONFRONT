using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // The main vehicle silhouettes reproduced with the Unity prototype's
    // low-poly materials. Only the hull collider participates in gameplay.
    public sealed class PrototypeCombinedVehicleVisual
    {
        public Transform Turret, Barrel, Muzzle, GunnerTurret, GunnerBarrel, GunnerMuzzle,
            Airframe, Rotor, TailRotor, Radar;
        public BoxCollider Hull;
        public Renderer[] Renderers;
        public TextMesh Instruments;
        public Transform MotorcycleBody, MotorcycleSteering, MotorcycleFrontWheel, MotorcycleRearWheel,
            MotorcycleDriverSeat, MotorcyclePassengerSeat, MotorcycleDriverEye, MotorcyclePassengerEye,
            MotorcycleLeftGrip, MotorcycleRightGrip, MotorcycleLeftPeg, MotorcycleRightPeg,
            MotorcyclePassengerLeftGrip, MotorcyclePassengerRightGrip,
            MotorcyclePassengerLeftPeg, MotorcyclePassengerRightPeg;
        private PrototypeRuntime game;
        private Transform root;
        private Material body, trim;
        private GameObject artRoot;
        private Renderer[] retainedRenderers;
        public bool HasAuthoredArt => artRoot != null;
        public float WheelSteering;
        public PrototypeVehicleArtRig ArtRig { get; private set; }

        public void Build(Transform parent, PrototypeRuntime runtime, PrototypeTeam team,
            PrototypeVehicleKind kind)
        {
            root = parent; game = runtime;
            body = team == PrototypeTeam.Blue ? game.BlueMaterial : game.RedMaterial;
            trim = team == PrototypeTeam.Blue ? game.BlueTrimMaterial : game.RedTrimMaterial;
            Hull = parent.gameObject.AddComponent<BoxCollider>();
            if (kind == PrototypeVehicleKind.Motorcycle) BuildMotorcycle();
            else if (PrototypeVehicleDefinition.Get(kind).Aircraft)
                BuildAircraft(kind);
            else BuildArmor(kind == PrototypeVehicleKind.AntiAir);
            if (kind != PrototypeVehicleKind.Jet && kind != PrototypeVehicleKind.Motorcycle)
                BuildGunner(kind == PrototypeVehicleKind.Helicopter);
            Renderers = parent.GetComponentsInChildren<Renderer>();
            TryAttachAuthoredArt(team, kind);
        }

        private void TryAttachAuthoredArt(PrototypeTeam team, PrototypeVehicleKind kind)
        {
            string vehicle = kind == PrototypeVehicleKind.Helicopter ? "H8" : kind == PrototypeVehicleKind.Ifv ? "V12" : kind == PrototypeVehicleKind.Motorcycle ? "M2" : kind == PrototypeVehicleKind.ReconHelicopter ? "R3" : kind == PrototypeVehicleKind.Jet ? "J20" : "A40";
            string side = team == PrototypeTeam.Blue ? "Blue" : "Red";
            GameObject prefab = Resources.Load<GameObject>("Vehicles/" + vehicle + "_" + side);
            if (prefab == null) return;
            GameObject instance = Object.Instantiate(prefab, root, false);
            instance.name = vehicle + " authored art";
            PrototypeVehicleArtRig rig = instance.GetComponent<PrototypeVehicleArtRig>();
            if (rig == null || !rig.Bind(this, kind))
            {
                Object.Destroy(instance);
                Debug.LogWarning(vehicle + " art prefab has incomplete pivot bindings; using prototype visuals.");
                return;
            }

            // The independent crew gun retains its gameplay-controlled visual.
            // The rest of the prototype primitives are replaced by the FBX art.
            retainedRenderers = GunnerTurret != null ?
                GunnerTurret.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            foreach (Renderer renderer in Renderers)
                if (renderer != null && System.Array.IndexOf(retainedRenderers, renderer) < 0)
                    renderer.enabled = false;
            artRoot = instance; ArtRig = rig;
            Renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (kind == PrototypeVehicleKind.Ifv)
            {
                rig.CalibrateWeapon(Turret, Barrel, Muzzle);
                Hull.center = rig.HullCenter; Hull.size = rig.HullSize;
                GunnerTurret.localPosition = new Vector3(.52f, 2.72f, 0f);
                GunnerTurret.localScale = Vector3.one * .45f;
            }
            if (kind == PrototypeVehicleKind.Motorcycle) CalibrateMotorcycle(rig);
            if (kind == PrototypeVehicleKind.ReconHelicopter || kind == PrototypeVehicleKind.Jet)
            {
                Turret.SetParent(Airframe, false);
                rig.CalibrateWeapon(Turret, Barrel, Muzzle);
                Hull.center = rig.HullCenter; Hull.size = rig.HullSize;
                if (GunnerTurret != null) { GunnerTurret.localPosition = new Vector3(.65f, 1.35f, 1.25f); GunnerTurret.localScale = Vector3.one * .4f; }
            }
            CalibrateAuthoredArt(kind);
        }

        private void CalibrateMotorcycle(PrototypeVehicleArtRig rig)
        {
            Hull.center = rig.HullCenter; Hull.size = rig.HullSize;
            MotorcycleSteering.localPosition = rig.SteeringAnchor;
            Transform[] targets = { MotorcycleDriverSeat, MotorcyclePassengerSeat, MotorcycleDriverEye, MotorcyclePassengerEye,
                MotorcycleLeftGrip, MotorcycleRightGrip, MotorcycleLeftPeg, MotorcycleRightPeg,
                MotorcyclePassengerLeftGrip, MotorcyclePassengerRightGrip, MotorcyclePassengerLeftPeg, MotorcyclePassengerRightPeg };
            for (int i = 0; i < targets.Length; i++)
            {
                // Imported FBX handedness can reverse authored Left/Right
                // labels. Rider targets use vehicle-local X, never crossed limbs.
                int sourceIndex = i;
                if (i >= 4)
                {
                    bool left = (i & 1) == 0;
                    if (left ? rig.ContactAnchors[i].x > 0 : rig.ContactAnchors[i].x < 0)
                        sourceIndex = (i & 1) == 0 ? i + 1 : i - 1;
                }
                Vector3 point = rig.ContactAnchors[sourceIndex];
                targets[i].localPosition = targets[i].parent == MotorcycleSteering ? point - rig.SteeringAnchor : point;
            }
        }

        private void CalibrateAuthoredArt(PrototypeVehicleKind kind)
        {
            if (kind == PrototypeVehicleKind.Helicopter)
            {
                // The H8's tail-to-nose body is 19.2 m, while the prototype
                // collision box was only 7.4 m long. The main rotor is visual
                // geometry; the hull box covers the central fuselage and tail.
                Hull.center = new Vector3(0f, 3.35f, -1.22f);
                Hull.size = new Vector3(4.2f, 6.7f, 19.2f);
                // Imported H8 ChinGun tip: (0, 1.43, 8.385) in Unity metres.
                Muzzle.localPosition = new Vector3(0f, -.47f, 8.485f);
                // Keep the separate player-controlled belly MG beneath the
                // cabin, away from the imported fixed chin cannon.
                GunnerTurret.localPosition = new Vector3(0f, 2.3f, 2.5f);
            }
            else if (kind == PrototypeVehicleKind.AntiAir)
            {
                Hull.center = new Vector3(0f, 1.81f, .06f);
                Hull.size = new Vector3(4.7f, 3.62f, 7.1f);
                // The A40's four real barrel tips are about 2.7 m ahead of
                // the elevation pivot; the prototype bore was 5.68 m away.
                Muzzle.localPosition = new Vector3(-.57f, -.24f, 2.70f);
            }
        }

        public void SetVisible(bool visible)
        {
            if (artRoot != null)
            {
                artRoot.SetActive(visible);
                foreach (Renderer renderer in retainedRenderers)
                    if (renderer != null) renderer.enabled = visible;
                return;
            }
            if (Renderers == null) return;
            foreach (Renderer renderer in Renderers)
                if (renderer != null) renderer.enabled = visible;
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

        private void BuildGunner(bool downward)
        {
            GunnerTurret = Pivot(downward ? "Restricted belly machine gun" : "Independent crew MG", root,
                downward ? new Vector3(0f, .53f, 1.25f) : new Vector3(.77f, 3.2f, -.25f));
            Part("MG ring", Vector3.zero, new Vector3(.5f, .08f, .5f),
                game.ArmorMaterial, GunnerTurret, PrimitiveType.Cylinder);
            GunnerBarrel = Pivot("Elevating crew MG", GunnerTurret, new Vector3(0f, .18f, 0f));
            Part("MG receiver", Vector3.zero, new Vector3(.35f, .3f, .6f), game.GunMaterial, GunnerBarrel);
            Part("MG barrel", new Vector3(0f, 0f, .95f), new Vector3(.14f, .14f, 1.5f),
                game.GunMaterial, GunnerBarrel);
            GunnerMuzzle = Pivot("Crew MG bore", GunnerBarrel, new Vector3(0f, 0f, 1.74f));
            if (downward)
            {
                Part("Belly gimbal fairing", new Vector3(0f, .22f, 0f), new Vector3(.7f, .43f, .7f),
                    game.ArmorMaterial, GunnerTurret, PrimitiveType.Sphere);
                GunnerBarrel.localRotation = Quaternion.Euler(45f, 0f, 0f);
            }
        }

        private void BuildAircraft(PrototypeVehicleKind kind)
        {
            bool jet = kind == PrototypeVehicleKind.Jet;
            bool recon = kind == PrototypeVehicleKind.ReconHelicopter;
            Hull.center = new Vector3(0f, 1.8f, 0f);
            Hull.size = recon ? new Vector3(2.15f, 2.6f, 6.2f) : new Vector3(2.4f, 2.6f, 7.4f);
            Airframe = Pivot(jet ? "Fighter airframe" : recon ? "Light reconnaissance airframe" :
                "Armored attack airframe", root, Vector3.zero);
            Part("Fuselage", new Vector3(0f, 1.72f, 0f),
                recon ? new Vector3(2.1f, 1.9f, 5.6f) : new Vector3(2.2f, 2.02f, 6.7f), body, Airframe,
                recon ? PrimitiveType.Sphere : PrimitiveType.Cube);
            Part(recon ? "Panoramic observation bubble" : "Raised armored cockpit canopy",
                new Vector3(0f, recon ? 2.25f : 2.5f, recon ? 1.8f : 2.02f),
                recon ? new Vector3(1.96f, 1.5f, 2.65f) : new Vector3(1.82f, 1.05f, 2.1f),
                game.GlassMaterial, Airframe, recon ? PrimitiveType.Sphere : PrimitiveType.Cube);
            if (!recon) Part("Nose fairing", new Vector3(0f, 1.6f, 3.4f),
                new Vector3(1.5f, 1.2f, 1.45f), body, Airframe);
            Part("Tail boom", new Vector3(0f, 1.55f, -4.3f),
                new Vector3(recon ? .43f : .9f, recon ? .43f : .65f, 4.8f), game.ArmorMaterial, Airframe);
            Part("Tail stabilizer", new Vector3(0f, 1.92f, -4.9f),
                new Vector3(3.07f, .15f, 1.53f), trim, Airframe);
            Part("Vertical tail", new Vector3(0f, 2.58f, -4.9f),
                new Vector3(.15f, 1.92f, 1.53f), trim, Airframe);
            bool fixedCannon = kind == PrototypeVehicleKind.Helicopter;
            Turret = Pivot(fixedCannon ? "Fixed aircraft forward weapon" : "Aircraft forward weapon",
                fixedCannon ? Airframe : root, new Vector3(0f, 1.9f, -.1f));
            Barrel = Pivot(fixedCannon ? "Fixed attack cannon mount" : "Aircraft gun elevation", Turret, Vector3.zero);
            Part(jet ? "25mm nose cannon" : recon ? "Recon nose machine gun" : "70mm attack cannon",
                new Vector3(0f, recon ? -.55f : 0f, recon ? 3.25f : 4.24f),
                new Vector3(recon ? .09f : .19f, recon ? .09f : .19f, recon ? 1.65f : 2.88f), game.GunMaterial, Barrel);
            // A muzzle pivot's forward axis is the visible barrel's exact bore.
            Muzzle = Pivot("Aircraft weapon bore", Barrel, new Vector3(0f, recon ? -.55f : 0f, recon ? 4.12f : 5.72f));
            foreach (int side in new[] { -1, 1 })
            {
                if (!recon) Part(jet ? "Missile pylon" : "Armored weapon stub wing", new Vector3(side * 1.4f, 1.22f, -.5f),
                    new Vector3(.7f, .2f, 2.2f), game.GunMaterial, Airframe);
                if (jet) Part("Missile tube", new Vector3(side * 1.4f, 1.03f, .05f),
                    new Vector3(.25f, .25f, 2.3f), game.ArmorMaterial, Airframe);
                else if (!recon)
                {
                    Part("Attack weapon pod armor", new Vector3(side * 1.7f, 1.09f, .18f),
                        new Vector3(.76f, .54f, 1.8f), body, Airframe);
                    foreach (float x in new[] { -.18f, .18f })
                        foreach (float y in new[] { -.13f, .13f })
                            Part("Attack pod visible bore", new Vector3(side * 1.7f + x, 1.09f + y, 1.09f),
                                new Vector3(.16f, .16f, .035f), game.GunMaterial, Airframe, PrimitiveType.Sphere);
                }
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
                Part("Main rotor blade X", Vector3.zero, new Vector3(recon ? 10.9f : 13.4f, .1f, recon ? .18f : .3f),
                    game.GunMaterial, Rotor);
                Part("Main rotor blade Z", Vector3.zero, new Vector3(recon ? .18f : .3f, .1f, recon ? 10.9f : 13.4f),
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
                    Part(recon ? "Recon visibility stripe" : "Attack reinforced door armor",
                        new Vector3(side * 1.15f, 1.25f, -.5f), new Vector3(.07f, recon ? .22f : .7f, 2f), trim, Airframe);
                    Part("Crew seat cushion", new Vector3(side * .58f, 1.05f, .8f),
                        new Vector3(.54f, .16f, .73f), game.BootMaterial, Airframe);
                    if (!recon || side < 0) Part("Rear passenger cushion", new Vector3(side * .58f, 1.05f, -1.3f),
                        new Vector3(.54f, .16f, .73f), game.BootMaterial, Airframe);
                }
                TailRotor = Pivot("Powered tail rotor", Airframe, new Vector3(.56f, 2.25f, -5.4f));
                Part("Tail rotor vertical blade", Vector3.zero, new Vector3(.09f, 1.86f, .17f), game.GunMaterial, TailRotor);
                Part("Tail rotor horizontal blade", Vector3.zero, new Vector3(.09f, .17f, 1.86f), game.GunMaterial, TailRotor);
                if (recon)
                {
                    Radar = Pivot("Recon rotating radar mast", Airframe, new Vector3(0f, 2.85f, -1.65f));
                    Part("Recon radar antenna", Vector3.zero, new Vector3(1.35f, .12f, .65f), trim, Radar);
                    Part("Recon electro optical sensor", new Vector3(0f, 1f, 2.43f),
                        new Vector3(.54f, .54f, .54f), game.ArmorMaterial, Airframe, PrimitiveType.Sphere);
                    Part("Recon sensor lens", new Vector3(0f, 1f, 2.69f), new Vector3(.25f, .25f, .04f), game.GlassMaterial, Airframe);
                }
                else foreach (int side in new[] { -1, 1 })
                    Part("Attack turbine housing", new Vector3(side * .75f, 2.85f, -.85f),
                        new Vector3(.85f, .65f, 2.45f), game.ArmorMaterial, Airframe);
            }
            BuildInstrument(new Vector3(0f, 2.04f, 3.2f));
        }

        private void BuildMotorcycle()
        {
            Hull.center = new Vector3(0f, .85f, 0f);
            Hull.size = new Vector3(.78f, 1.6f, 2.55f);
            MotorcycleBody = Pivot("Motorcycle visual chassis", root, Vector3.zero);
            MotorcycleSteering = Pivot("Steering stem", MotorcycleBody, new Vector3(0f, 1.18f, .76f));
            MotorcycleRearWheel = BuildMotorcycleWheel(MotorcycleBody, new Vector3(0f, .46f, -.89f), "Rear");
            MotorcycleFrontWheel = BuildMotorcycleWheel(MotorcycleSteering, new Vector3(0f, -.72f, .12f), "Front");
            Part("Engine block", new Vector3(0f, .64f, -.02f), new Vector3(.37f, .33f, .40f), game.GunMaterial, MotorcycleBody);
            for (int fin = 0; fin < 5; fin++)
                Part("Engine cooling fin", new Vector3(0f, .65f + fin * .038f, .055f),
                    new Vector3(.41f, .018f, .28f), game.BootMaterial, MotorcycleBody);
            foreach (float side in new[] { -1f, 1f })
            {
                BikeTube("Tubular main frame", MotorcycleBody, new Vector3(side * .18f, .65f, -.55f),
                    new Vector3(side * .15f, 1.12f, .59f), .045f, trim);
                BikeTube("Rear swingarm", MotorcycleBody, new Vector3(side * .16f, .48f, -.05f),
                    new Vector3(side * .15f, .46f, -.89f), .05f, game.GunMaterial);
                BikeTube("Rear shock", MotorcycleBody, new Vector3(side * .22f, .53f, -.83f),
                    new Vector3(side * .22f, 1.01f, -.50f), .037f, game.GunMaterial);
                BikeTube("Front suspension", MotorcycleSteering, new Vector3(side * .14f, .07f, -.025f),
                    new Vector3(side * .14f, -.72f, .12f), .043f, game.GunMaterial);
                Part("Fork dust sleeve", new Vector3(side * .14f, -.34f, .054f),
                    new Vector3(.115f, .34f, .115f), game.BootMaterial, MotorcycleSteering);
                BikeTube("Handlebar rise", MotorcycleSteering, new Vector3(side * .12f, .10f, 0f),
                    new Vector3(side * .30f, .19f, -.065f), .025f, game.GunMaterial);
                Part("Grip rubber", new Vector3(side * .39f, .19f, -.08f),
                    new Vector3(.22f, .066f, .066f), game.BootMaterial, MotorcycleSteering);
                Part("Brake lever", new Vector3(side * .36f, .17f, -.15f),
                    new Vector3(.19f, .018f, .024f), game.GunMaterial, MotorcycleSteering);
                Part("Switch housing", new Vector3(side * .27f, .19f, -.08f),
                    new Vector3(.09f, .10f, .105f), game.GunMaterial, MotorcycleSteering);
                Part("Driver footpeg", new Vector3(side * .33f, .36f, .18f),
                    new Vector3(.23f, .045f, .12f), game.BootMaterial, MotorcycleBody);
                Part("Passenger footpeg", new Vector3(side * .34f, .47f, -.55f),
                    new Vector3(.20f, .045f, .12f), game.BootMaterial, MotorcycleBody);
                BikeTube("Passenger grab rail", MotorcycleBody, new Vector3(side * .29f, 1.10f, -.45f),
                    new Vector3(side * .29f, 1.10f, -.81f), .022f, game.GunMaterial);
            }
            Part("Driver saddle", new Vector3(0f, .98f, .02f), new Vector3(.46f, .12f, .56f), game.BootMaterial, MotorcycleBody);
            Part("Passenger saddle", new Vector3(0f, 1.04f, -.55f), new Vector3(.43f, .13f, .56f), game.BootMaterial, MotorcycleBody);
            Part("Fuel tank", new Vector3(0f, 1.05f, .45f), new Vector3(.47f, .33f, .57f), body, MotorcycleBody, PrimitiveType.Sphere);
            Part("Fuel cap", new Vector3(0f, 1.22f, .40f), new Vector3(.12f, .012f, .12f), game.GunMaterial, MotorcycleBody, PrimitiveType.Cylinder);
            Part("Front mudguard", new Vector3(0f, -.185f, .12f), new Vector3(.27f, .08f, .66f), body, MotorcycleSteering);
            Part("Rear mudguard", new Vector3(0f, .985f, -.91f), new Vector3(.28f, .07f, .57f), body, MotorcycleBody);
            Part("Exhaust silencer", new Vector3(.27f, .59f, -.62f), new Vector3(.13f, .38f, .13f), game.GunMaterial,
                MotorcycleBody, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(72f, 0f, 0f);
            Part("Headlight shroud", new Vector3(0f, .05f, .13f), new Vector3(.25f, .23f, .15f), body, MotorcycleSteering);
            Part("Headlight lens", new Vector3(0f, .05f, .215f), new Vector3(.19f, .16f, .024f), game.LampMaterial, MotorcycleSteering);
            Part("Tail light", new Vector3(0f, 1.07f, -.875f), new Vector3(.19f, .06f, .035f), game.RedMaterial, MotorcycleBody);
            Part("Handlebar brace", new Vector3(0f, .19f, -.08f), new Vector3(.59f, .04f, .045f), trim, MotorcycleSteering);
            foreach (float side in new[] { -1f, 1f })
                BikeTube("Control cable", MotorcycleSteering, new Vector3(side * .25f, .17f, -.07f),
                    new Vector3(side * .08f, -.04f, .08f), .007f, game.BootMaterial);
            Transform panel = Pivot("Motorcycle instrument panel", MotorcycleSteering, new Vector3(0f, .25f, .06f));
            panel.localRotation = Quaternion.Euler(25f, 0f, 0f);
            Part("Instrument backing", Vector3.zero, new Vector3(.30f, .15f, .06f), game.GunMaterial, panel);
            Instruments = new GameObject("Motorcycle speed display").AddComponent<TextMesh>();
            Instruments.transform.SetParent(panel, false);
            Instruments.transform.localPosition = new Vector3(0f, 0f, -.038f);
            Instruments.transform.localRotation = Quaternion.identity;
            Instruments.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Instruments.fontSize = 48; Instruments.fontStyle = FontStyle.Bold;
            Instruments.font.RequestCharactersInTexture("0123456789 KM/H\nHP", Instruments.fontSize, Instruments.fontStyle);
            Instruments.GetComponent<Renderer>().sharedMaterial = Instruments.font.material;
            Instruments.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Instruments.GetComponent<Renderer>().receiveShadows = false;
            Instruments.characterSize = .011f;
            Instruments.color = new Color(.64f, .96f, .80f);
            Instruments.anchor = TextAnchor.MiddleCenter; Instruments.alignment = TextAlignment.Center;
            Instruments.lineSpacing = .9f;
            MotorcycleDriverSeat = Pivot("Driver saddle contact", MotorcycleBody, new Vector3(0f, 1.04f, .015f));
            MotorcyclePassengerSeat = Pivot("Passenger saddle contact", MotorcycleBody, new Vector3(0f, 1.105f, -.56f));
            MotorcycleDriverEye = Pivot("Driver eye point", MotorcycleBody, new Vector3(0f, 1.77f, -.07f));
            MotorcyclePassengerEye = Pivot("Passenger eye point", MotorcycleBody, new Vector3(0f, 1.97f, -.54f));
            MotorcycleLeftGrip = Pivot("Left hand grip target", MotorcycleSteering, new Vector3(-.39f, .19f, -.08f));
            MotorcycleRightGrip = Pivot("Right hand grip target", MotorcycleSteering, new Vector3(.39f, .19f, -.08f));
            MotorcycleLeftGrip.localRotation = Quaternion.Euler(0f, 90f, 0f);
            MotorcycleRightGrip.localRotation = Quaternion.Euler(0f, -90f, 0f);
            MotorcycleLeftPeg = Pivot("Left boot peg target", MotorcycleBody, new Vector3(-.33f, .485f, .115f));
            MotorcycleRightPeg = Pivot("Right boot peg target", MotorcycleBody, new Vector3(.33f, .485f, .115f));
            MotorcyclePassengerLeftGrip = Pivot("Passenger left grab target", MotorcycleBody, new Vector3(-.29f, 1.10f, -.57f));
            MotorcyclePassengerRightGrip = Pivot("Passenger right grab target", MotorcycleBody, new Vector3(.29f, 1.10f, -.57f));
            MotorcyclePassengerLeftGrip.localRotation = MotorcyclePassengerRightGrip.localRotation = Quaternion.Euler(0f, 90f, 0f);
            MotorcyclePassengerLeftPeg = Pivot("Passenger left peg target", MotorcycleBody, new Vector3(-.34f, .595f, -.615f));
            MotorcyclePassengerRightPeg = Pivot("Passenger right peg target", MotorcycleBody, new Vector3(.34f, .595f, -.615f));
        }

        private Transform BuildMotorcycleWheel(Transform parent, Vector3 at, string label)
        {
            Transform wheel = Pivot(label + " wheel axle", parent, at);
            Part(label + " tire", Vector3.zero, new Vector3(.92f, .105f, .92f), game.BootMaterial,
                wheel, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(label + " rim", Vector3.zero, new Vector3(.65f, .11f, .65f), game.GunMaterial,
                wheel, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(label + " hub", Vector3.zero, new Vector3(.15f, .14f, .15f), trim,
                wheel, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (int spoke = 0; spoke < 8; spoke++)
            {
                float angle = spoke * Mathf.PI / 4f;
                Vector3 radial = new Vector3(0f, Mathf.Sin(angle), Mathf.Cos(angle));
                foreach (float side in new[] { -1f, 1f })
                    BikeTube(label + " spoke", wheel, new Vector3(side * .118f, 0f, 0f) + radial * .08f,
                        new Vector3(side * .118f, 0f, 0f) + radial * .30f, .008f, trim);
            }
            for (int tread = 0; tread < 20; tread++)
            {
                float angle = tread * Mathf.PI / 10f;
                GameObject block = Part(label + " tire tread", new Vector3(0f, Mathf.Sin(angle) * .46f, Mathf.Cos(angle) * .46f),
                    new Vector3(.23f, .055f, .07f), game.BootMaterial, wheel);
                block.transform.localRotation = Quaternion.Euler(-angle * Mathf.Rad2Deg, 0f, 0f);
            }
            return wheel;
        }

        private void BikeTube(string name, Transform parent, Vector3 from, Vector3 to, float radius, Material material)
        {
            Vector3 delta = to - from;
            GameObject tube = Part(name, (from + to) * .5f, new Vector3(radius * 2f, delta.magnitude * .5f, radius * 2f),
                material, parent, PrimitiveType.Cylinder);
            tube.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        }

        public void UpdateMotorcycle(float speed, float steering, float dt)
        {
            if (MotorcycleBody == null) return;
            float blend = 1f - Mathf.Exp(-Mathf.Max(0f, dt) * 10f);
            MotorcycleSteering.localRotation = Quaternion.Slerp(MotorcycleSteering.localRotation,
                Quaternion.Euler(0f, Mathf.Clamp(steering, -1f, 1f) * 23f, 0f), blend);
            MotorcycleBody.localRotation = Quaternion.Slerp(MotorcycleBody.localRotation,
                Quaternion.Euler(0f, 0f, -steering * Mathf.Clamp01(Mathf.Abs(speed) / 12f) * 7f), blend);
            float rotation = speed / .46f * Mathf.Rad2Deg * dt;
            MotorcycleFrontWheel.Rotate(rotation, 0f, 0f, Space.Self);
            MotorcycleRearWheel.Rotate(rotation, 0f, 0f, Space.Self);
        }

        public void ResetMotorcycle()
        {
            if (MotorcycleBody == null) return;
            MotorcycleBody.localRotation = MotorcycleSteering.localRotation = Quaternion.identity;
            MotorcycleFrontWheel.localRotation = MotorcycleRearWheel.localRotation = Quaternion.identity;
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
