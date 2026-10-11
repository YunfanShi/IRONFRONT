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
        private bool downed;

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
            PrototypeEquipmentVisual.BuildClassGear(model.transform, role, game, team);
            lastPosition = transform.position;
        }

        public void SetVisible(bool visible)
        {
            if (model == null || model.activeSelf == visible) return;
            model.SetActive(visible);
            lastPosition = transform.position;
            motion = 0f;
        }

        public void SetDowned(bool value)
        {
            downed = value;
            if (model == null) return;
            model.transform.localRotation = value ?
                Quaternion.Euler(82f, 0f, 0f) : Quaternion.identity;
            model.transform.localPosition = new Vector3(0f,
                baseHeight + (value ? .15f : 0f), 0f);
        }

        private void LateUpdate()
        {
            if (model == null || !model.activeSelf) return;
            if (downed)
            {
                lastPosition = transform.position;
                motion = 0f;
                return;
            }
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
                PrototypeTacticalState.InCover || bot.TacticalState ==
                PrototypeTacticalState.Revive);
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
            return Build(parent, PrototypeInfantryRoles.DefaultWeapon(role), game,
                firstPerson, team);
        }

        public static GameObject Build(Transform parent, PrototypeWeaponId weaponId,
            PrototypeRuntime game, bool firstPerson = false,
            PrototypeTeam team = PrototypeTeam.Blue)
        {
            var root = new GameObject(weaponId + " weapon model");
            root.transform.SetParent(parent, false);
            bool pistol = weaponId == PrototypeWeaponId.Pistol;
            if (pistol)
            {
                BuildPistol(root.transform, game);
                if (firstPerson) AddHands(root.transform, game, team);
                return root;
            }
            bool compact = pistol || weaponId == PrototypeWeaponId.Smg;
            bool longRifle = weaponId == PrototypeWeaponId.Marksman ||
                weaponId == PrototypeWeaponId.Sniper;
            float barrel = pistol ? .13f : compact ? .29f :
                weaponId == PrototypeWeaponId.Sniper ? 1.03f : longRifle ? .88f : .57f;
            float receiver = pistol ? .25f : compact ? .38f : .55f;
            Material steel = game.GunMaterial;
            Material furniture = game.ArmorMaterial;
            Material trim = game.BootMaterial;
            PrototypeSoldierVisual.Part("Receiver", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0f, .05f), new Vector3(.17f, .145f, receiver), steel);
            PrototypeSoldierVisual.Part("Upper receiver", PrimitiveType.Cube, root.transform,
                new Vector3(0f, .075f, .06f), new Vector3(.15f, .055f, receiver * .91f), trim);
            for (int side = -1; side <= 1; side += 2)
            {
                PrototypeSoldierVisual.Part("Receiver side panel", PrimitiveType.Cube,
                    root.transform, new Vector3(side * .088f, -.012f, .075f),
                    new Vector3(.012f, .082f, receiver * .70f), furniture);
                PrototypeSoldierVisual.Part("Handguard side vent", PrimitiveType.Cube,
                    root.transform, new Vector3(side * .105f, .015f, .34f),
                    new Vector3(.009f, .025f, .25f), trim);
            }
            PrototypeSoldierVisual.Part("Handguard", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.01f, .33f), new Vector3(.19f, .125f, .39f), furniture);
            PrototypeSoldierVisual.Part("Lower handguard", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.085f, .33f), new Vector3(.15f, .04f, .34f), trim);
            PrototypeSoldierVisual.Part("Barrel", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, .025f, .48f + barrel * .5f),
                new Vector3(.045f, barrel * .5f, .045f), steel)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PrototypeSoldierVisual.Part("Muzzle brake", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, .025f, .47f + barrel),
                new Vector3(.055f, .055f, .055f), trim)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PrototypeSoldierVisual.Part("Magazine", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.20f, .04f),
                new Vector3(.15f, compact ? .28f : .31f, .16f), steel)
                .transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            PrototypeSoldierVisual.Part("Pistol grip", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.19f, -.19f), new Vector3(.13f, .27f, .15f), trim)
                .transform.localRotation = Quaternion.Euler(13f, 0f, 0f);
            PrototypeSoldierVisual.Part("Trigger guard", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.13f, -.07f), new Vector3(.025f, .018f, .17f), steel);
            PrototypeSoldierVisual.Part("Charging handle", PrimitiveType.Cube,
                root.transform, new Vector3(.11f, .07f, -.09f),
                new Vector3(.10f, .025f, .055f), trim);
            // The buttstock sits behind the eye in first person; rendering it
            // there turns into a large near-camera block.
            if (!pistol && !firstPerson)
            {
                PrototypeSoldierVisual.Part("Stock tube", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, -.005f, -.37f), new Vector3(.085f, .075f, .34f), steel);
                PrototypeSoldierVisual.Part("Stock cheek", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, .045f, -.47f), new Vector3(.15f, .095f, .28f), furniture);
                PrototypeSoldierVisual.Part("Stock butt", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, -.045f, -.62f), new Vector3(.17f, .21f, .055f), trim);
            }
            if (!pistol)
                PrototypeSoldierVisual.Part("Vertical foregrip", PrimitiveType.Cube,
                    root.transform, new Vector3(0f, -.16f, .37f),
                    new Vector3(.075f, .19f, .075f), trim);
            PrototypeSoldierVisual.Part("Top rail", PrimitiveType.Cube, root.transform,
                new Vector3(0f, .115f, .12f), new Vector3(.11f, .025f, .66f), trim);
            for (int i = 0; i < 7; i++)
                PrototypeSoldierVisual.Part("Rail tooth", PrimitiveType.Cube,
                    root.transform, new Vector3(0f, .132f, -.15f + i * .085f),
                    new Vector3(.14f, .018f, .027f), steel);
            PrototypeSoldierVisual.Part("Front sight post", PrimitiveType.Cube,
                root.transform, new Vector3(0f, .18f, .53f),
                new Vector3(.018f, .085f, .018f), trim);
            if (longRifle)
            {
                GameObject scope = PrototypeSoldierVisual.Part("Scope", PrimitiveType.Cylinder,
                    root.transform, new Vector3(0f, .22f, .07f),
                    new Vector3(.06f, .25f, .06f), trim);
                scope.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                PrototypeSoldierVisual.Part("Scope objective", PrimitiveType.Cylinder,
                    root.transform, new Vector3(0f, .22f, .32f),
                    new Vector3(.085f, .035f, .085f), steel)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                PrototypeSoldierVisual.Part("Scope lens", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, .22f, .35f), new Vector3(.055f, .055f, .008f),
                    game.GlassMaterial);
            }
            else
            {
                PrototypeSoldierVisual.Part("Sight base", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, .15f, .04f), new Vector3(.15f, .035f, .11f), trim);
                for (int side = -1; side <= 1; side += 2)
                    PrototypeSoldierVisual.Part("Sight frame side", PrimitiveType.Cube,
                        root.transform, new Vector3(side * .064f, .22f, .08f),
                        new Vector3(.014f, .12f, .018f), steel);
                PrototypeSoldierVisual.Part("Sight frame top", PrimitiveType.Cube,
                    root.transform, new Vector3(0f, .28f, .08f),
                    new Vector3(.14f, .014f, .018f), steel);
                PrototypeSoldierVisual.Part("Sight reticle", PrimitiveType.Sphere,
                    root.transform, new Vector3(0f, .22f, .075f),
                    new Vector3(.012f, .012f, .012f), game.RedTrimMaterial);
            }
            if (weaponId == PrototypeWeaponId.Lmg)
            {
                PrototypeSoldierVisual.Part("Belt box", PrimitiveType.Cube, root.transform,
                    new Vector3(.12f, -.22f, .08f), new Vector3(.29f, .28f, .31f),
                    game.ArmorMaterial);
                PrototypeSoldierVisual.Part("Carry handle", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, .27f, .05f), new Vector3(.035f, .22f, .27f), trim);
                for (int side = -1; side <= 1; side += 2)
                    PrototypeSoldierVisual.Part("Bipod", PrimitiveType.Cube,
                        root.transform, new Vector3(side * .11f, -.17f, .65f),
                        new Vector3(.025f, .34f, .035f), trim)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, side * 18f);
            }
            else if (weaponId == PrototypeWeaponId.Sniper)
            {
                PrototypeSoldierVisual.Part("Long floating barrel", PrimitiveType.Cylinder,
                    root.transform, new Vector3(0f, .025f, 1.05f),
                    new Vector3(.055f, .37f, .055f), steel)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                PrototypeSoldierVisual.Part("Bolt handle", PrimitiveType.Cube,
                    root.transform, new Vector3(.15f, .08f, -.11f),
                    new Vector3(.20f, .035f, .045f), game.BlueTrimMaterial);
                for (int side = -1; side <= 1; side += 2)
                    PrototypeSoldierVisual.Part("Sniper bipod", PrimitiveType.Cube,
                        root.transform, new Vector3(side * .09f, -.21f, .69f),
                        new Vector3(.025f, .35f, .03f), trim)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, side * 17f);
            }
            else if (weaponId == PrototypeWeaponId.Smg)
            {
                PrototypeSoldierVisual.Part("Compact folding stock", PrimitiveType.Cube,
                    root.transform, new Vector3(0f, .02f, -.40f),
                    new Vector3(.045f, .05f, .30f), trim);
                PrototypeSoldierVisual.Part("Muzzle shroud", PrimitiveType.Cylinder,
                    root.transform, new Vector3(0f, .025f, .73f),
                    new Vector3(.075f, .12f, .075f), steel)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else if (weaponId == PrototypeWeaponId.BattleRifle)
            {
                for (int side = -1; side <= 1; side += 2)
                    PrototypeSoldierVisual.Part("Reinforced side rail", PrimitiveType.Cube,
                        root.transform, new Vector3(side * .115f, .075f, .34f),
                        new Vector3(.023f, .055f, .43f), game.BlueTrimMaterial);
                PrototypeSoldierVisual.Part("Heavy muzzle brake", PrimitiveType.Cylinder,
                    root.transform, new Vector3(0f, .025f, 1.04f),
                    new Vector3(.085f, .09f, .085f), trim)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            if (firstPerson)
                AddHands(root.transform, game, team);
            return root;
        }

        private static void BuildPistol(Transform root, PrototypeRuntime game)
        {
            PrototypeSoldierVisual.Part("Pistol slide", PrimitiveType.Cube, root,
                new Vector3(0f, .04f, .13f), new Vector3(.14f, .13f, .43f),
                game.GunMaterial);
            PrototypeSoldierVisual.Part("Slide serrations", PrimitiveType.Cube, root,
                new Vector3(.076f, .065f, -.02f), new Vector3(.009f, .07f, .16f),
                game.ArmorMaterial);
            PrototypeSoldierVisual.Part("Pistol frame", PrimitiveType.Cube, root,
                new Vector3(0f, -.045f, .08f), new Vector3(.13f, .09f, .32f),
                game.ArmorMaterial);
            PrototypeSoldierVisual.Part("Pistol barrel", PrimitiveType.Cylinder, root,
                new Vector3(0f, .02f, .37f), new Vector3(.038f, .06f, .038f),
                game.BootMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PrototypeSoldierVisual.Part("Magazine", PrimitiveType.Cube, root,
                new Vector3(0f, -.19f, -.045f), new Vector3(.11f, .28f, .13f),
                game.BootMaterial).transform.localRotation = Quaternion.Euler(16f, 0f, 0f);
            PrototypeSoldierVisual.Part("Trigger guard", PrimitiveType.Cube, root,
                new Vector3(0f, -.12f, .14f), new Vector3(.02f, .025f, .14f),
                game.GunMaterial);
            PrototypeSoldierVisual.Part("Front sight", PrimitiveType.Cube, root,
                new Vector3(0f, .13f, .34f), new Vector3(.04f, .04f, .025f),
                game.BlueTrimMaterial);
            PrototypeSoldierVisual.Part("Rear sight", PrimitiveType.Cube, root,
                new Vector3(0f, .13f, -.05f), new Vector3(.09f, .035f, .025f),
                game.BlueTrimMaterial);
        }

        private static void AddHands(Transform root, PrototypeRuntime game,
            PrototypeTeam team)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side < 0 ? .31f : -.17f;
                PrototypeSoldierVisual.Part("Gloved hand", PrimitiveType.Cube,
                    root, new Vector3(side * .15f, -.18f, z),
                    new Vector3(.17f, .14f, .21f), game.BootMaterial);
                PrototypeSoldierVisual.Part("Sleeve", PrimitiveType.Cylinder,
                    root, new Vector3(side * .24f, -.30f, z - .15f),
                    new Vector3(.11f, .31f, .11f), team == PrototypeTeam.Blue ?
                    game.BlueClothMaterial : game.RedClothMaterial)
                    .transform.localRotation = Quaternion.Euler(75f, 0f, side * 12f);
            }
        }
    }
}
