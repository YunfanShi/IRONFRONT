using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Small world-space props share the soldier's primitive low-poly language.
    // They are visual only; gameplay collision stays on the projectile or actor.
    public static class PrototypeEquipmentVisual
    {
        public static GameObject BuildThrowable(Transform parent,
            PrototypeThrowableId kind, PrototypeRuntime game)
        {
            var root = new GameObject(kind + " grenade model");
            root.transform.SetParent(parent, false);
            Material body = kind == PrototypeThrowableId.Frag ?
                game.BlueHelmetMaterial : game.GlassMaterial;
            Material band = kind == PrototypeThrowableId.Frag ?
                game.BootMaterial : game.BlueTrimMaterial;
            Part("Canister", PrimitiveType.Cylinder, root.transform,
                Vector3.zero, new Vector3(.12f, .16f, .12f), body);
            for (int i = -1; i <= 1; i++)
                Part("Grip band", PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, i * .085f, 0f),
                    new Vector3(.125f, .013f, .125f), band);
            if (kind == PrototypeThrowableId.Frag)
            {
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * Mathf.PI / 3f;
                    GameObject rib = Part("Fragment rib", PrimitiveType.Cube,
                        root.transform, new Vector3(Mathf.Sin(angle) * .105f,
                            0f, Mathf.Cos(angle) * .105f),
                        new Vector3(.018f, .19f, .025f), game.ArmorMaterial);
                    rib.transform.localRotation = Quaternion.Euler(0f,
                        angle * Mathf.Rad2Deg, 0f);
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.PI * .5f;
                    Part("Smoke vent", PrimitiveType.Cube, root.transform,
                        new Vector3(Mathf.Sin(angle) * .122f, .10f,
                            Mathf.Cos(angle) * .122f),
                        new Vector3(.045f, .032f, .016f), game.GunMaterial)
                        .transform.localRotation = Quaternion.Euler(0f,
                            angle * Mathf.Rad2Deg, 0f);
                }
            }
            Part("Fuse cap", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, .18f, 0f), new Vector3(.072f, .036f, .072f),
                game.GunMaterial);
            Part("Safety lever", PrimitiveType.Cube, root.transform,
                new Vector3(.04f, .23f, 0f), new Vector3(.10f, .018f, .045f),
                game.BootMaterial);
            Part("Pull ring", PrimitiveType.Sphere, root.transform,
                new Vector3(.13f, .21f, 0f), new Vector3(.055f, .014f, .055f),
                game.LampMaterial);
            return root;
        }

        public static GameObject BuildRocket(Transform parent, PrototypeRuntime game)
        {
            var root = new GameObject("Anti-armor rocket model");
            root.transform.SetParent(parent, false);
            Part("Motor casing", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, 0f), new Vector3(.085f, .28f, .085f),
                game.ArmorMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part("Warhead", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0f, .30f), new Vector3(.19f, .19f, .27f),
                game.GunMaterial);
            Part("Warhead band", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, .17f), new Vector3(.098f, .024f, .098f),
                game.RedTrimMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                Part("Stabilizer fin", PrimitiveType.Cube, root.transform,
                    new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * .11f,
                        Mathf.Cos(angle * Mathf.Deg2Rad) * .11f, -.20f),
                    new Vector3(.025f, .20f, .20f), game.BootMaterial)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
            return root;
        }

        public static GameObject BuildLauncher(Transform parent, PrototypeRuntime game)
        {
            var root = new GameObject("Anti-armor launcher model");
            root.transform.SetParent(parent, false);
            Part("Launcher tube", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, .12f), new Vector3(.14f, .58f, .14f),
                game.ArmorMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part("Muzzle collar", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, .68f), new Vector3(.17f, .045f, .17f),
                game.GunMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part("Rear vent", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, -.47f), new Vector3(.16f, .065f, .16f),
                game.BootMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part("Firing grip", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.22f, .02f), new Vector3(.11f, .35f, .17f),
                game.BootMaterial);
            Part("Optical sight", PrimitiveType.Cube, root.transform,
                new Vector3(.15f, .19f, .17f), new Vector3(.12f, .12f, .34f),
                game.GunMaterial);
            Part("Sight glass", PrimitiveType.Cube, root.transform,
                new Vector3(.15f, .19f, .35f), new Vector3(.09f, .08f, .015f),
                game.VisorMaterial);
            return root;
        }

        public static void BuildClassGear(Transform soldier,
            PrototypeInfantryClass role, PrototypeRuntime game,
            PrototypeTeam team = PrototypeTeam.Blue)
        {
            Material accent = team == PrototypeTeam.Blue ?
                game.BlueTrimMaterial : game.RedTrimMaterial;
            if (role == PrototypeInfantryClass.Engineer)
            {
                GameObject launcher = BuildLauncher(soldier, game);
                launcher.transform.localPosition = new Vector3(-.37f, 1.16f, -.42f);
                launcher.transform.localRotation = Quaternion.Euler(-20f, 0f, -18f);
                launcher.transform.localScale = Vector3.one * .55f;
                Part("Repair tool", PrimitiveType.Cube, soldier,
                    new Vector3(.37f, .74f, -.18f), new Vector3(.14f, .38f, .13f),
                    game.LampMaterial);
            }
            else if (role == PrototypeInfantryClass.Medic)
            {
                Part("Medical kit", PrimitiveType.Cube, soldier,
                    new Vector3(-.40f, .77f, -.11f), new Vector3(.29f, .29f, .21f),
                    accent);
                Part("Medical cross vertical", PrimitiveType.Cube, soldier,
                    new Vector3(-.40f, .77f, .001f), new Vector3(.055f, .17f, .012f),
                    game.LampMaterial);
                Part("Medical cross horizontal", PrimitiveType.Cube, soldier,
                    new Vector3(-.40f, .77f, .003f), new Vector3(.17f, .055f, .012f),
                    game.LampMaterial);
            }
            else if (role == PrototypeInfantryClass.Recon)
            {
                Part("Respawn beacon casing", PrimitiveType.Cylinder, soldier,
                    new Vector3(.35f, .88f, -.29f), new Vector3(.12f, .31f, .12f),
                    game.GunMaterial);
                Part("Beacon antenna", PrimitiveType.Cylinder, soldier,
                    new Vector3(.35f, 1.31f, -.29f), new Vector3(.018f, .20f, .018f),
                    accent);
            }
            else
            {
                Part("Ammunition satchel", PrimitiveType.Cube, soldier,
                    new Vector3(.37f, .77f, -.16f), new Vector3(.32f, .27f, .22f),
                    game.ArmorMaterial);
                for (int i = 0; i < 3; i++)
                    Part("Magazine", PrimitiveType.Cube, soldier,
                        new Vector3(.24f + i * .12f, .81f, -.04f),
                        new Vector3(.085f, .18f, .055f), game.GunMaterial);
            }
        }

        private static GameObject Part(string name, PrimitiveType shape,
            Transform parent, Vector3 position, Vector3 scale, Material material) =>
            PrototypeSoldierVisual.Part(name, shape, parent, position, scale, material);
    }
}
