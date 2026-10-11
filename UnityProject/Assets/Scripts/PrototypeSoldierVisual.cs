using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // The gameplay capsule stays on the parent. This small articulated rig only
    // changes the silhouette, leaving movement, hit rules and cameras untouched.
    public sealed class PrototypeSoldierVisual : MonoBehaviour
    {
        private const float HipHeight = .86f;
        private const float ThighLength = .38f;
        private const float ShinLength = .36f;
        private const float UpperArmLength = .34f;
        private const float ForearmLength = .35f;
        private GameObject model;
        private Transform pelvis;
        private Transform torso;
        private Transform head;
        private Transform leftArm;
        private Transform rightArm;
        private Transform leftElbow;
        private Transform rightElbow;
        private Transform leftHand;
        private Transform rightHand;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform leftKnee;
        private Transform rightKnee;
        private Transform leftFoot;
        private Transform rightFoot;
        private Transform weaponMount;
        private GameObject heldWeapon;
        private PrototypeWeaponId heldWeaponId;
        private PrototypeBot bot;
        private PrototypePlayer player;
        private Vector3 lastPosition;
        private float gait;
        private float motion;
        private float baseHeight;
        private float crouchWeight;
        private float proneWeight;
        private bool downed;

        public void Initialize(PrototypeRuntime game, PrototypeTeam team,
            PrototypeInfantryClass role, float feetOffset)
        {
            if (model != null) Destroy(model);
            bot = GetComponent<PrototypeBot>();
            player = GetComponent<PrototypePlayer>();
            downed = false;
            baseHeight = feetOffset;
            crouchWeight = proneWeight = motion = gait = 0f;
            model = new GameObject("Armored infantry visual");
            model.transform.SetParent(transform, false);
            model.transform.localPosition = new Vector3(0f, feetOffset, 0f);
            Material cloth = team == PrototypeTeam.Blue ?
                game.BlueClothMaterial : game.RedClothMaterial;
            Material helmet = team == PrototypeTeam.Blue ?
                game.BlueHelmetMaterial : game.RedHelmetMaterial;
            Material trim = team == PrototypeTeam.Blue ?
                game.BlueTrimMaterial : game.RedTrimMaterial;

            pelvis = Pivot("Pelvis", model.transform, new Vector3(0f, HipHeight, 0f));
            torso = Pivot("Torso", pelvis, Vector3.zero);
            head = Pivot("Head", torso, new Vector3(0f, .61f, 0f));
            BodyPart("Uniform", PrimitiveType.Cylinder,
                new Vector3(0f, 1.12f, 0f), new Vector3(.45f, .34f, .30f), cloth);
            BodyPart("Vest", PrimitiveType.Cylinder,
                new Vector3(0f, 1.14f, .035f), new Vector3(.48f, .30f, .35f), game.ArmorMaterial);
            BodyPart("Front plate", PrimitiveType.Cube,
                new Vector3(0f, 1.17f, .22f), new Vector3(.45f, .43f, .095f), game.ArmorMaterial);
            BodyPart("Backpack", PrimitiveType.Cube,
                new Vector3(0f, 1.14f, -.25f), new Vector3(.44f, .46f, .20f), helmet);
            Part("Webbing belt", PrimitiveType.Cube, pelvis,
                new Vector3(0f, -.025f, .035f), new Vector3(.55f, .11f, .35f), game.BootMaterial);
            for (int i = -1; i <= 1; i++)
                BodyPart("Magazine pouch", PrimitiveType.Cube,
                    new Vector3(i * .15f, .95f, .265f), new Vector3(.12f, .18f, .10f), trim);
            foreach (float side in new[] { -1f, 1f })
            {
                BodyPart("Shoulder pad", PrimitiveType.Sphere,
                    new Vector3(side * .33f, 1.40f, 0f),
                    new Vector3(.25f, .20f, .28f), helmet);
                BodyPart("Side pouch", PrimitiveType.Cube,
                    new Vector3(side * .32f, .92f, -.08f),
                    new Vector3(.15f, .22f, .16f), trim);
            }

            HeadPart("Face", PrimitiveType.Sphere,
                new Vector3(0f, 1.59f, .035f), new Vector3(.34f, .38f, .34f), game.SkinMaterial);
            HeadPart("Helmet shell", PrimitiveType.Sphere,
                new Vector3(0f, 1.68f, -.015f), new Vector3(.47f, .28f, .45f), helmet);
            HeadPart("Helmet brow", PrimitiveType.Cube,
                new Vector3(0f, 1.66f, .225f), new Vector3(.45f, .07f, .14f), helmet);
            HeadPart("Visor", PrimitiveType.Cube,
                new Vector3(0f, 1.59f, .212f), new Vector3(.36f, .09f, .055f), game.VisorMaterial);
            HeadPart("Chin guard", PrimitiveType.Cube,
                new Vector3(0f, 1.44f, .175f), new Vector3(.34f, .10f, .11f), trim);
            foreach (float side in new[] { -1f, 1f })
                HeadPart("Ear guard", PrimitiveType.Cube,
                    new Vector3(side * .245f, 1.58f, 0f),
                    new Vector3(.09f, .16f, .20f), helmet);

            leftArm = Pivot("Left arm", torso, new Vector3(-.33f, .50f, 0f));
            rightArm = Pivot("Right arm", torso, new Vector3(.33f, .50f, 0f));
            leftLeg = Pivot("Left leg", pelvis, new Vector3(-.17f, 0f, 0f));
            rightLeg = Pivot("Right leg", pelvis, new Vector3(.17f, 0f, 0f));
            Arm(leftArm, cloth, trim, game.BootMaterial, out leftElbow, out leftHand);
            Arm(rightArm, cloth, trim, game.BootMaterial, out rightElbow, out rightHand);
            Leg(leftLeg, cloth, game.ArmorMaterial, game.BootMaterial,
                out leftKnee, out leftFoot);
            Leg(rightLeg, cloth, game.ArmorMaterial, game.BootMaterial,
                out rightKnee, out rightFoot);

            weaponMount = Pivot("Held weapon", model.transform, new Vector3(.05f, 1.16f, .18f));
            weaponMount.localScale = Vector3.one * .65f;
            SetWeapon(PrototypeInfantryRoles.DefaultWeapon(role), game);
            if (role == PrototypeInfantryClass.Medic)
                BodyPart("Medical insignia", PrimitiveType.Cube,
                    new Vector3(-.32f, 1.39f, .15f), new Vector3(.09f, .12f, .025f),
                    game.LampMaterial);
            // Class equipment is authored in standing coordinates. One origin
            // conversion keeps every pouch, antenna and launcher on the torso.
            Transform gear = Pivot("Class equipment", torso, new Vector3(0f, -HipHeight, 0f));
            PrototypeEquipmentVisual.BuildClassGear(gear, role, game, team);
            lastPosition = transform.position;
            RefreshPose(true);
        }

        public Transform WeaponMount => weaponMount;
        public GameObject HeldWeapon => heldWeapon;

        public void SetWeapon(PrototypeWeaponId weaponId, PrototypeRuntime game)
        {
            if (heldWeapon != null) Destroy(heldWeapon);
            heldWeaponId = weaponId;
            heldWeapon = PrototypeWeaponVisual.Build(weaponMount, weaponId, game);
        }

        public void SetVisible(bool visible)
        {
            if (model == null || model.activeSelf == visible) return;
            model.SetActive(visible);
            lastPosition = transform.position;
            motion = 0f;
            if (visible) RefreshPose(true);
        }

        public void SetDowned(bool value)
        {
            downed = value;
            lastPosition = transform.position;
            motion = 0f;
            RefreshPose(true);
        }

        // Also used by editor verification after changing stance without waiting
        // a frame. Perspective switches and revival restore the current pose.
        public void RefreshPose(bool immediate = false)
        {
            if (model == null) return;
            if (player != null && player.CurrentVehicle is PrototypeCombinedArmsVehicle bike &&
                bike.Kind == PrototypeVehicleKind.Motorcycle)
            {
                PoseMotorcycle(bike);
                return;
            }
            bool prone = player != null && player.IsProne;
            bool crouching = player != null ? player.IsCrouched || player.IsSliding :
                bot != null && (bot.TacticalState == PrototypeTacticalState.SeekCover ||
                bot.TacticalState == PrototypeTacticalState.InCover ||
                bot.TacticalState == PrototypeTacticalState.Revive);
            float crouchTarget = crouching && !prone ? 1f : 0f;
            float proneTarget = prone || downed ? 1f : 0f;
            crouchWeight = immediate ? crouchTarget :
                Mathf.MoveTowards(crouchWeight, crouchTarget, Time.deltaTime * 7f);
            proneWeight = immediate ? proneTarget :
                Mathf.MoveTowards(proneWeight, proneTarget, Time.deltaTime * 6f);
            Pose(crouchWeight, proneWeight, Mathf.Sin(gait), motion);
        }

        private void LateUpdate()
        {
            if (model == null || !model.activeSelf) return;
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            float speed = downed || delta.magnitude > 3f ? 0f :
                delta.magnitude / Mathf.Max(Time.deltaTime, .001f);
            motion = Mathf.MoveTowards(motion, Mathf.Clamp01(speed / 5.5f),
                Time.deltaTime * 5f);
            // A crawl cycles more slowly than a standing stride.
            gait += Time.deltaTime * Mathf.Lerp(2f + motion * 11f,
                1.2f + motion * 7f, proneWeight);
            RefreshPose();
        }

        private void Pose(float crouch, float prone, float phase, float movement)
        {
            // Body dimensions stay fixed in every stance; only the joints move.
            model.transform.localScale = Vector3.one;
            model.transform.localRotation = Quaternion.identity;
            float floorOffset = baseHeight;
            if (player != null)
            {
                float floor = PrototypeLayout.HeightAt(transform.position.x, transform.position.z);
                floorOffset = Mathf.Max(baseHeight, floor - transform.position.y);
            }
            model.transform.localPosition = new Vector3(0f, floorOffset, 0f);
            leftLeg.localPosition = new Vector3(-.17f, 0f, 0f);
            rightLeg.localPosition = new Vector3(.17f, 0f, 0f);
            float swing = phase * movement;
            float hipY = Mathf.Lerp(HipHeight - crouch * .40f, .29f, prone);
            hipY -= movement * .035f * (1f - prone);
            hipY += Mathf.Abs(phase) * movement * .012f * prone;
            pelvis.localPosition = new Vector3(0f, hipY,
                Mathf.Lerp(-.14f * crouch, -.25f, prone));
            torso.localRotation = Quaternion.Euler(
                Mathf.Lerp(crouch * 32f, downed ? 94f : 82f, prone),
                0f, downed ? 12f : swing * prone * 2f);
            head.localRotation = Quaternion.Euler(
                Mathf.Lerp(-crouch * 23f, downed ? -82f : -70f, prone), 0f,
                downed ? -15f : 0f);

            float stride = Mathf.Lerp(.20f, .10f, crouch) * (1f - prone);
            float footZ = Mathf.Lerp(.06f, -.72f, prone);
            float footLift = Mathf.Lerp(.065f, .018f, prone);
            PoseLeg(leftLeg, leftKnee, leftFoot, -1f, hipY,
                footZ + swing * Mathf.Lerp(stride, .06f, prone),
                .12f + Mathf.Max(0f, swing) * footLift, prone);
            PoseLeg(rightLeg, rightKnee, rightFoot, 1f, hipY,
                footZ - swing * Mathf.Lerp(stride, .06f, prone),
                .12f + Mathf.Max(0f, -swing) * footLift, prone);

            weaponMount.localPosition = new Vector3(Mathf.Lerp(.05f, .015f, prone),
                Mathf.Lerp(1.16f - crouch * .30f, .49f, prone),
                Mathf.Lerp(.18f + crouch * .10f, .51f, prone));
            weaponMount.localRotation = Quaternion.identity;
            if (downed)
            {
                weaponMount.localPosition = new Vector3(.45f, .28f, .15f);
                weaponMount.localRotation = Quaternion.Euler(0f, 20f, 55f);
            }
            Transform weaponFrame = heldWeapon != null ? heldWeapon.transform : weaponMount;
            bool pistol = heldWeaponId == PrototypeWeaponId.Pistol;
            Vector3 rightGrip = model.transform.InverseTransformPoint(
                weaponFrame.TransformPoint(new Vector3(0f, -.18f, pistol ? -.045f : -.18f)));
            Vector3 leftGrip = model.transform.InverseTransformPoint(
                weaponFrame.TransformPoint(pistol ? new Vector3(-.055f, -.16f, .015f) :
                    new Vector3(0f, -.16f, .37f)));
            if (downed)
            {
                rightGrip = new Vector3(.49f, .14f, .35f);
                leftGrip = new Vector3(-.45f, .13f, .43f);
            }
            // The elbows flare out and settle low while crawling, rather than
            // swinging a rigid rifle through the ground with the torso.
            Vector3 armPole = Vector3.Lerp(new Vector3(1f, -.3f, -.25f),
                new Vector3(.55f, -1f, -.15f), prone);
            PoseArm(leftArm, leftElbow, leftHand, leftGrip,
                new Vector3(-armPole.x, armPole.y, armPole.z));
            PoseArm(rightArm, rightElbow, rightHand, rightGrip, armPole);
        }

        private void PoseMotorcycle(PrototypeCombinedArmsVehicle bike)
        {
            // Vehicle seat positions are seat points, while an infantry root is
            // at the feet. Place the pelvis on the saddle and keep both legs
            // outside the frame, with bent knees and boots at footpeg height.
            bool driver = bike.PlayerSeat == 0;
            model.transform.localScale = Vector3.one;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localPosition = new Vector3(0f, -.65f, 0f);
            pelvis.localPosition = new Vector3(0f, HipHeight, 0f);
            torso.localRotation = Quaternion.Euler(driver ? 18f : 5f, 0f, 0f);
            head.localRotation = Quaternion.Euler(driver ? -12f : -5f, 0f, 0f);
            leftLeg.localPosition = new Vector3(-.30f, 0f, 0f);
            rightLeg.localPosition = new Vector3(.30f, 0f, 0f);
            PoseLeg(leftLeg, leftKnee, leftFoot, -1f, HipHeight, .15f, .32f, 0f);
            PoseLeg(rightLeg, rightKnee, rightFoot, 1f, HipHeight, .15f, .32f, 0f);
            weaponMount.localRotation = Quaternion.identity;
            Vector3 leftGrip = driver ? model.transform.InverseTransformPoint(
                bike.transform.TransformPoint(new Vector3(-.37f, 1.40f, .83f))) :
                new Vector3(-.30f, .70f, .24f);
            Vector3 rightGrip = driver ? model.transform.InverseTransformPoint(
                bike.transform.TransformPoint(new Vector3(.37f, 1.40f, .83f))) :
                new Vector3(.30f, .70f, .24f);
            PoseArm(leftArm, leftElbow, leftHand, leftGrip, new Vector3(-1f, -.2f, 0f));
            PoseArm(rightArm, rightElbow, rightHand, rightGrip, new Vector3(1f, -.2f, 0f));
            leftHand.rotation = rightHand.rotation = bike.transform.rotation *
                Quaternion.Euler(0f, 90f, 0f);
        }

        private void PoseLeg(Transform hip, Transform knee, Transform foot, float side,
            float hipY, float footZ, float footY, float prone)
        {
            // Sagittal two-bone IK plants each boot while the pelvis drops.
            // Bending the knee forward produces a squat; extending backwards
            // produces the long, low silhouette of a prone soldier.
            float vertical = hipY - footY;
            float distance = Mathf.Clamp(Mathf.Sqrt(vertical * vertical + footZ * footZ),
                .025f, ThighLength + ShinLength - .0001f);
            float direction = -Mathf.Atan2(footZ, vertical) * Mathf.Rad2Deg;
            float upper = Mathf.Acos(Mathf.Clamp((ThighLength * ThighLength +
                distance * distance - ShinLength * ShinLength) /
                (2f * ThighLength * distance), -1f, 1f)) * Mathf.Rad2Deg;
            float bend = 180f - Mathf.Acos(Mathf.Clamp((ThighLength * ThighLength +
                ShinLength * ShinLength - distance * distance) /
                (2f * ThighLength * ShinLength), -1f, 1f)) * Mathf.Rad2Deg;
            hip.localRotation = Quaternion.Euler(direction - upper, side * prone * 7f, 0f);
            knee.localRotation = Quaternion.Euler(bend, 0f, 0f);
            foot.rotation = model.transform.rotation;
        }

        private void PoseArm(Transform shoulder, Transform elbow, Transform hand,
            Vector3 target, Vector3 pole)
        {
            Vector3 origin = model.transform.InverseTransformPoint(shoulder.position);
            Vector3 delta = target - origin;
            float distance = Mathf.Clamp(delta.magnitude, .035f,
                UpperArmLength + ForearmLength - .001f);
            Vector3 direction = delta.normalized;
            Vector3 side = Vector3.ProjectOnPlane(pole, direction).normalized;
            float along = (UpperArmLength * UpperArmLength + distance * distance -
                ForearmLength * ForearmLength) / (2f * distance);
            float outward = Mathf.Sqrt(Mathf.Max(0f,
                UpperArmLength * UpperArmLength - along * along));
            Vector3 elbowPosition = origin + direction * along + side * outward;
            Vector3 grip = origin + direction * distance;
            shoulder.localRotation = Quaternion.FromToRotation(Vector3.down,
                shoulder.parent.InverseTransformDirection(
                    model.transform.TransformDirection(elbowPosition - origin)));
            elbow.localRotation = Quaternion.FromToRotation(Vector3.down,
                shoulder.InverseTransformDirection(
                    model.transform.TransformDirection(grip - elbowPosition)));
            hand.rotation = heldWeapon != null ? heldWeapon.transform.rotation : weaponMount.rotation;
        }

        private void BodyPart(string label, PrimitiveType shape, Vector3 position,
            Vector3 scale, Material material) =>
            Part(label, shape, torso, position - Vector3.up * HipHeight, scale, material);

        private void HeadPart(string label, PrimitiveType shape, Vector3 position,
            Vector3 scale, Material material) =>
            Part(label, shape, head, position - Vector3.up * 1.47f, scale, material);

        private static Transform Pivot(string label, Transform parent, Vector3 position)
        {
            var pivot = new GameObject(label).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            return pivot;
        }

        private static void Arm(Transform pivot, Material cloth, Material trim,
            Material gloves, out Transform elbow, out Transform hand)
        {
            Part("Upper sleeve", PrimitiveType.Cylinder, pivot,
                new Vector3(0f, -UpperArmLength * .5f, 0f),
                new Vector3(.18f, UpperArmLength * .5f, .18f), cloth);
            elbow = Pivot("Elbow", pivot, Vector3.down * UpperArmLength);
            Part("Elbow guard", PrimitiveType.Sphere, elbow,
                new Vector3(0f, 0f, -.02f), new Vector3(.20f, .16f, .20f), trim);
            Part("Forearm sleeve", PrimitiveType.Cylinder, elbow,
                new Vector3(0f, -ForearmLength * .5f, 0f),
                new Vector3(.15f, ForearmLength * .5f, .15f), cloth);
            hand = Pivot("Hand", elbow, Vector3.down * ForearmLength);
            Part("Glove", PrimitiveType.Cube, hand,
                Vector3.zero, new Vector3(.16f, .15f, .18f), gloves);
        }

        private static void Leg(Transform pivot, Material cloth, Material armor,
            Material boots, out Transform knee, out Transform foot)
        {
            Part("Upper trouser", PrimitiveType.Cylinder, pivot,
                new Vector3(0f, -ThighLength * .5f, 0f),
                new Vector3(.21f, ThighLength * .5f, .23f), cloth);
            knee = Pivot("Knee", pivot, Vector3.down * ThighLength);
            Part("Knee pad", PrimitiveType.Sphere, knee,
                new Vector3(0f, 0f, .035f), new Vector3(.23f, .13f, .19f), armor);
            Part("Lower trouser", PrimitiveType.Cylinder, knee,
                new Vector3(0f, -ShinLength * .5f, 0f),
                new Vector3(.17f, ShinLength * .5f, .18f), cloth);
            foot = Pivot("Ankle", knee, Vector3.down * ShinLength);
            Part("Boot", PrimitiveType.Cube, foot,
                new Vector3(0f, 0f, .08f), new Vector3(.27f, .24f, .39f), boots);
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
