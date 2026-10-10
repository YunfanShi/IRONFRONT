using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // The browser game's layered low-poly infantry is the visual reference. The
    // gameplay capsule stays on the parent, so these parts never change hit rules.
    public sealed class PrototypeSoldierVisual : MonoBehaviour
    {
        private GameObject model;
        private Transform leftArm;
        private Transform rightArm;
        private Transform leftLeg;
        private Transform rightLeg;
        private PrototypeBot bot;
        private Vector3 lastPosition;
        private float gait;
        private float motion;
        private float baseHeight;

        public void Initialize(PrototypeRuntime game, PrototypeTeam team,
            PrototypeInfantryClass role, float feetOffset)
        {
            bot = GetComponent<PrototypeBot>();
            baseHeight = feetOffset;
            model = new GameObject("Armored infantry visual");
            model.transform.SetParent(transform, false);
            model.transform.localPosition = new Vector3(0f, feetOffset, 0f);
            Material cloth = team == PrototypeTeam.Blue ?
                game.BlueClothMaterial : game.RedClothMaterial;
            Material helmet = team == PrototypeTeam.Blue ?
                game.BlueHelmetMaterial : game.RedHelmetMaterial;
            Material trim = team == PrototypeTeam.Blue ?
                game.BlueTrimMaterial : game.RedTrimMaterial;

            Part("Uniform", PrimitiveType.Cylinder, model.transform,
                new Vector3(0f, 1.18f, 0f), new Vector3(.34f, .44f, .25f), cloth);
            Part("Vest", PrimitiveType.Cylinder, model.transform,
                new Vector3(0f, 1.18f, .035f), new Vector3(.37f, .35f, .29f), game.ArmorMaterial);
            Part("Front plate", PrimitiveType.Cube, model.transform,
                new Vector3(0f, 1.22f, .285f), new Vector3(.47f, .43f, .095f), game.ArmorMaterial);
            Part("Backpack", PrimitiveType.Cube, model.transform,
                new Vector3(0f, 1.18f, -.30f), new Vector3(.48f, .48f, .22f), helmet);
            Part("Webbing belt", PrimitiveType.Cube, model.transform,
                new Vector3(0f, .83f, .035f), new Vector3(.62f, .11f, .39f), game.BootMaterial);
            for (int i = -1; i <= 1; i++)
                Part("Magazine pouch", PrimitiveType.Cube, model.transform,
                    new Vector3(i * .17f, .91f, .36f), new Vector3(.13f, .20f, .10f), trim);
            foreach (float side in new[] { -1f, 1f })
            {
                Part("Shoulder pad", PrimitiveType.Sphere, model.transform,
                    new Vector3(side * .39f, 1.48f, 0f),
                    new Vector3(.28f, .20f, .31f), helmet);
                Part("Side pouch", PrimitiveType.Cube, model.transform,
                    new Vector3(side * .39f, .90f, -.08f),
                    new Vector3(.16f, .25f, .17f), trim);
            }

            Part("Face", PrimitiveType.Sphere, model.transform,
                new Vector3(0f, 1.69f, .035f), new Vector3(.37f, .43f, .36f), game.SkinMaterial);
            Part("Helmet shell", PrimitiveType.Sphere, model.transform,
                new Vector3(0f, 1.80f, -.015f), new Vector3(.51f, .32f, .49f), helmet);
            Part("Helmet brow", PrimitiveType.Cube, model.transform,
                new Vector3(0f, 1.76f, .245f), new Vector3(.49f, .075f, .15f), helmet);
            Part("Visor", PrimitiveType.Cube, model.transform,
                new Vector3(0f, 1.68f, .232f), new Vector3(.39f, .095f, .055f), game.VisorMaterial);
            Part("Chin guard", PrimitiveType.Cube, model.transform,
                new Vector3(0f, 1.51f, .195f), new Vector3(.37f, .10f, .11f), trim);
            foreach (float side in new[] { -1f, 1f })
                Part("Ear guard", PrimitiveType.Cube, model.transform,
                    new Vector3(side * .27f, 1.67f, 0f),
                    new Vector3(.10f, .17f, .22f), helmet);

            leftArm = Pivot("Left arm", new Vector3(-.39f, 1.46f, 0f));
            rightArm = Pivot("Right arm", new Vector3(.39f, 1.46f, 0f));
            leftLeg = Pivot("Left leg", new Vector3(-.18f, .83f, 0f));
            rightLeg = Pivot("Right leg", new Vector3(.18f, .83f, 0f));
            Arm(leftArm, cloth, trim, game.BootMaterial);
            Arm(rightArm, cloth, trim, game.BootMaterial);
            Leg(leftLeg, cloth, game.ArmorMaterial, game.BootMaterial);
            Leg(rightLeg, cloth, game.ArmorMaterial, game.BootMaterial);

            var weaponMount = new GameObject("Held weapon");
            weaponMount.transform.SetParent(model.transform, false);
            weaponMount.transform.localPosition = new Vector3(.12f, 1.20f, .40f);
            weaponMount.transform.localScale = Vector3.one * .65f;
            PrototypeWeaponVisual.Build(weaponMount.transform, role, game);
            if (role == PrototypeInfantryClass.Medic)
                Part("Medical insignia", PrimitiveType.Cube, model.transform,
                    new Vector3(-.38f, 1.44f, .17f), new Vector3(.09f, .12f, .025f),
                    game.LampMaterial);
            lastPosition = transform.position;
        }

        public void SetVisible(bool visible)
        {
            if (model == null || model.activeSelf == visible) return;
            model.SetActive(visible);
            lastPosition = transform.position;
            motion = 0f;
        }

        private void LateUpdate()
        {
            if (model == null || !model.activeSelf) return;
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            float speed = delta.magnitude > 3f ? 0f :
                delta.magnitude / Mathf.Max(Time.deltaTime, .001f);
            motion = Mathf.MoveTowards(motion, Mathf.Clamp01(speed / 5.5f),
                Time.deltaTime * 5f);
            gait += Time.deltaTime * (2f + motion * 11f);
            float swing = Mathf.Sin(gait) * motion;
            leftLeg.localRotation = Quaternion.Euler(swing * 26f, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(-swing * 26f, 0f, 0f);
            leftArm.localRotation = Quaternion.Euler(-25f - swing * 12f, 0f, -8f);
            rightArm.localRotation = Quaternion.Euler(-28f + swing * 12f, 0f, 8f);
            bool crouching = bot != null && (bot.TacticalState ==
                PrototypeTacticalState.SeekCover || bot.TacticalState ==
                PrototypeTacticalState.InCover);
            model.transform.localPosition = new Vector3(0f,
                baseHeight - (crouching ? .12f : 0f) +
                Mathf.Abs(Mathf.Sin(gait)) * motion * .035f, 0f);
        }

        private Transform Pivot(string label, Vector3 position)
        {
            var pivot = new GameObject(label).transform;
            pivot.SetParent(model.transform, false);
            pivot.localPosition = position;
            return pivot;
        }

        private static void Arm(Transform pivot, Material cloth, Material trim, Material gloves)
        {
            Part("Sleeve", PrimitiveType.Cylinder, pivot,
                new Vector3(0f, -.23f, .02f), new Vector3(.14f, .29f, .14f), cloth);
            Part("Elbow guard", PrimitiveType.Sphere, pivot,
                new Vector3(0f, -.45f, .09f), new Vector3(.22f, .19f, .23f), trim);
            Part("Glove", PrimitiveType.Cube, pivot,
                new Vector3(0f, -.56f, .16f), new Vector3(.20f, .17f, .21f), gloves);
        }

        private static void Leg(Transform pivot, Material cloth, Material armor, Material boots)
        {
            Part("Trouser", PrimitiveType.Cylinder, pivot,
                new Vector3(0f, -.27f, 0f), new Vector3(.17f, .30f, .17f), cloth);
            Part("Knee pad", PrimitiveType.Sphere, pivot,
                new Vector3(0f, -.46f, .10f), new Vector3(.23f, .16f, .18f), armor);
            Part("Boot", PrimitiveType.Cube, pivot,
                new Vector3(0f, -.70f, .09f), new Vector3(.29f, .25f, .40f), boots);
        }

        internal static GameObject Part(string label, PrimitiveType shape, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = label;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }
    }

    // Distinct silhouettes for the four equipped primaries. The parent controls
    // camera placement; this geometry has no collider or combat authority.
    public static class PrototypeWeaponVisual
    {
        public static GameObject Build(Transform parent, PrototypeInfantryClass role,
            PrototypeRuntime game, bool firstPerson = false,
            PrototypeTeam team = PrototypeTeam.Blue)
        {
            var root = new GameObject(role + " weapon model");
            root.transform.SetParent(parent, false);
            bool compact = role == PrototypeInfantryClass.Medic;
            bool longRifle = role == PrototypeInfantryClass.Recon;
            float barrel = compact ? .29f : longRifle ? .88f : .57f;
            float receiver = compact ? .38f : .55f;
            Material steel = game.GunMaterial;
            Material furniture = game.ArmorMaterial;
            Material trim = game.BootMaterial;
            PrototypeSoldierVisual.Part("Receiver", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0f, .05f), new Vector3(.18f, .17f, receiver), steel);
            PrototypeSoldierVisual.Part("Handguard", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.01f, .31f), new Vector3(.20f, .15f, .40f), furniture);
            PrototypeSoldierVisual.Part("Barrel", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, .025f, .48f + barrel * .5f),
                new Vector3(.045f, barrel * .5f, .045f), steel)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PrototypeSoldierVisual.Part("Muzzle brake", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, .025f, .47f + barrel),
                new Vector3(.067f, .055f, .067f), trim)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PrototypeSoldierVisual.Part("Magazine", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.20f, .04f),
                new Vector3(.15f, compact ? .28f : .31f, .16f), steel)
                .transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            PrototypeSoldierVisual.Part("Pistol grip", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.19f, -.19f), new Vector3(.13f, .27f, .15f), trim)
                .transform.localRotation = Quaternion.Euler(13f, 0f, 0f);
            PrototypeSoldierVisual.Part("Stock", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.02f, -.41f), new Vector3(.16f, .17f, .39f), furniture);
            PrototypeSoldierVisual.Part("Top rail", PrimitiveType.Cube, root.transform,
                new Vector3(0f, .11f, .04f), new Vector3(.13f, .035f, .47f), trim);
            if (longRifle)
            {
                GameObject scope = PrototypeSoldierVisual.Part("Scope", PrimitiveType.Cylinder,
                    root.transform, new Vector3(0f, .22f, .07f),
                    new Vector3(.06f, .25f, .06f), trim);
                scope.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                PrototypeSoldierVisual.Part("Scope lens", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, .22f, .32f), new Vector3(.09f, .09f, .025f),
                    game.GlassMaterial);
            }
            else
            {
                PrototypeSoldierVisual.Part("Reflex sight", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, .19f, .04f), new Vector3(.16f, .14f, .07f), trim);
                PrototypeSoldierVisual.Part("Sight glass", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, .19f, .08f), new Vector3(.11f, .08f, .015f),
                    game.GlassMaterial);
            }
            if (firstPerson)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = side < 0 ? .31f : -.17f;
                    PrototypeSoldierVisual.Part("Gloved hand", PrimitiveType.Sphere,
                        root.transform, new Vector3(side * .15f, -.18f, z),
                        new Vector3(.20f, .19f, .27f), game.BootMaterial);
                    PrototypeSoldierVisual.Part("Sleeve", PrimitiveType.Cylinder,
                        root.transform, new Vector3(side * .24f, -.30f, z - .15f),
                        new Vector3(.11f, .31f, .11f), team == PrototypeTeam.Blue ?
                        game.BlueClothMaterial : game.RedClothMaterial)
                        .transform.localRotation = Quaternion.Euler(75f, 0f, side * 12f);
                }
            }
            return root;
        }
    }
}
