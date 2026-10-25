using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // The gameplay capsule stays on the parent. This small articulated rig only
    // changes the silhouette, leaving movement, hit rules and cameras untouched.
    [DefaultExecutionOrder(100)]
    public sealed class PrototypeSoldierVisual : MonoBehaviour
    {
        private float HipHeight = .86f;
        private float ThighLength = .38f;
        private float ShinLength = .36f;
        private float UpperArmLength = .34f;
        private float ForearmLength = .35f;
        private GameObject model;
        private PrototypeSoldierArtRig artRig;
        private Renderer[] fallbackBody;
        private float FootHeight => artRig != null ? .14f : .12f;
        public PrototypeSoldierArtRig ArtRig => artRig;
        private Transform pelvis;
        private Transform torso;
        private Transform spine;
        private float SpineLength => artRig != null && artRig.Bindings.Length > 15 ? .27f : 0f;
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
        private PrototypeWeaponArtRig weaponArt;
        private GameObject heldWeapon;
        private PrototypeWeaponId heldWeaponId;
        private GameObject heldEquipment;
        private PrototypeHeldEquipment heldEquipmentKind;
        private GameObject stowedLauncher;
        private PrototypeBot bot;
        private PrototypePlayer player;
        private Vector3 lastPosition;
        private float gait;
        private float motion;
        private float runWeight;
        private float sprintMotionWeight;
        public float GaitPhase => gait;
        public float SprintMotionWeight => sprintMotionWeight;
        private float aimWeight;
        private Vector2 strideDirection = Vector2.up;
        private float legYaw;
        private float baseHeight;
        private float crouchWeight;
        private float proneWeight;
        private bool downed;
        private bool motorcycleFirstPerson;

        public void Initialize(PrototypeRuntime game, PrototypeTeam team,
            PrototypeInfantryClass role, float feetOffset)
        {
            if (model != null) { model.SetActive(false); Destroy(model); }
            HipHeight = .86f; ThighLength = .38f; ShinLength = .36f;
            UpperArmLength = .34f; ForearmLength = .35f;
            artRig = null;
            bot = GetComponent<PrototypeBot>();
            player = GetComponent<PrototypePlayer>();
            downed = false;
            motorcycleFirstPerson = false;
            baseHeight = feetOffset;
            crouchWeight = proneWeight = motion = gait = runWeight = aimWeight = sprintMotionWeight = 0f;
            strideDirection = Vector2.up;
            legYaw = 0f;
            model = new GameObject("Armored infantry visual");
            model.transform.SetParent(transform, false);
            model.transform.localPosition = new Vector3(0f, feetOffset, 0f);
            artRig = PrototypeSoldierArtRig.Create(model.transform, team);
            if (artRig != null)
            {
                HipHeight = artRig.HipHeight; ThighLength = artRig.ThighLength; ShinLength = artRig.ShinLength;
                UpperArmLength = artRig.UpperArmLength; ForearmLength = artRig.ForearmLength;
            }
            Material cloth = team == PrototypeTeam.Blue ?
                game.BlueClothMaterial : game.RedClothMaterial;
            Material helmet = team == PrototypeTeam.Blue ?
                game.BlueHelmetMaterial : game.RedHelmetMaterial;
            Material trim = team == PrototypeTeam.Blue ?
                game.BlueTrimMaterial : game.RedTrimMaterial;

            pelvis = Pivot("Pelvis", model.transform, new Vector3(0f, HipHeight, 0f));
            spine = Pivot("Spine", pelvis, Vector3.zero);
            torso = Pivot("Torso", pelvis, Vector3.up * SpineLength);
            head = Pivot("Head", torso, new Vector3(0f, artRig != null ? .60f - SpineLength : .61f, 0f));
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

            leftArm = Pivot("Left arm", torso, new Vector3(artRig != null ? -.235f : -.33f, artRig != null ? .52f - SpineLength : .50f, 0f));
            rightArm = Pivot("Right arm", torso, new Vector3(artRig != null ? .235f : .33f, artRig != null ? .52f - SpineLength : .50f, 0f));
            leftLeg = Pivot("Left leg", pelvis, new Vector3(-.17f, 0f, 0f));
            rightLeg = Pivot("Right leg", pelvis, new Vector3(.17f, 0f, 0f));
            Arm(leftArm, cloth, trim, game.BootMaterial, out leftElbow, out leftHand);
            Arm(rightArm, cloth, trim, game.BootMaterial, out rightElbow, out rightHand);
            Leg(leftLeg, cloth, game.ArmorMaterial, game.BootMaterial,
                out leftKnee, out leftFoot);
            Leg(rightLeg, cloth, game.ArmorMaterial, game.BootMaterial,
                out rightKnee, out rightFoot);

            fallbackBody = pelvis.GetComponentsInChildren<Renderer>(true);
            if (artRig != null)
            {
                foreach (var r in fallbackBody) r.enabled = false;
                artRig.Bind(new[] { pelvis, torso, head, leftArm, rightArm, leftElbow, rightElbow,
                    leftHand, rightHand, leftLeg, rightLeg, leftKnee, rightKnee, leftFoot, rightFoot, spine });
            }
            weaponMount = Pivot("Held weapon", model.transform, new Vector3(.05f, 1.16f, .18f));
            weaponMount.localScale = Vector3.one * .65f;
            SetWeapon(PrototypeInfantryRoles.DefaultWeapon(role), game);
            if (role == PrototypeInfantryClass.Medic)
                BodyPart("Medical insignia", PrimitiveType.Cube,
                    new Vector3(-.32f, 1.39f, .15f), new Vector3(.09f, .12f, .025f),
                    game.LampMaterial);
            // Class equipment is authored in standing coordinates. One origin
            // conversion keeps every pouch, antenna and launcher on the torso.
            Transform gear = Pivot("Class equipment", torso, new Vector3(0f, -HipHeight - SpineLength, 0f));
            PrototypeEquipmentVisual.BuildClassGear(gear, role, game, team);
            if (artRig != null)
            {
                // The authored vest already includes the generic pouches. Keep
                // functional class identifiers fitted to its narrower silhouette.
                foreach (Transform item in gear)
                {
                    if (item.name == "Ammunition satchel" || item.name == "Magazine") item.gameObject.SetActive(false);
                    else if (item.name.StartsWith("Medical"))
                    { item.localPosition = new Vector3(-.22f, .99f, item.name == "Medical kit" ? .10f : .172f); item.localScale *= .6f; }
                    else if (item.name == "Repair tool") { item.localPosition = new Vector3(.23f, .89f, -.08f); item.localScale *= .65f; }
                    else if (item.name == "Stowed launcher") item.localPosition = new Vector3(-.23f, 1.27f, -.25f);
                    else if (item.name.Contains("Beacon") || item.name.Contains("beacon"))
                        item.localPosition = new Vector3(.20f, item.localPosition.y + .16f, -.20f);
                }
            }
            stowedLauncher = gear.Find("Stowed launcher")?.gameObject;
            heldEquipment = null;
            heldEquipmentKind = PrototypeHeldEquipment.Firearm;
            lastPosition = transform.position;
            RefreshPose(true);
        }

        public Transform WeaponMount => weaponMount;
        public GameObject HeldWeapon => heldWeapon;

        public void SetWeapon(PrototypeWeaponId weaponId, PrototypeRuntime game)
        {
            if (heldWeapon != null) { heldWeapon.SetActive(false); Destroy(heldWeapon); }
            heldWeaponId = weaponId;
            heldWeapon = PrototypeWeaponVisual.Build(weaponMount, weaponId, game);
            weaponArt = heldWeapon.GetComponent<PrototypeWeaponArtRig>();
        }

        public void SetHeldEquipment(GameObject equipment, PrototypeHeldEquipment kind)
        {
            heldEquipment = equipment;
            heldEquipmentKind = kind;
            if (stowedLauncher != null)
                stowedLauncher.SetActive(kind != PrototypeHeldEquipment.Launcher && kind != PrototypeHeldEquipment.AntiAirLauncher);
            RefreshPose(true);
        }

        public void SetVisible(bool visible)
        {
            if (model == null || model.activeSelf == visible) return;
            model.SetActive(visible);
            lastPosition = transform.position;
            motion = 0f;
            runWeight = 0f;
            if (visible) RefreshPose(true);
        }

        // First person uses the same articulated arms and the same grip IK as
        // the external rider. Hide the torso/head rather than duplicating hands.
        public void SetMotorcycleFirstPerson(bool value)
        {
            if (model == null || motorcycleFirstPerson == value) return;
            motorcycleFirstPerson = value;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = !value || renderer.transform.IsChildOf(leftArm) ||
                    renderer.transform.IsChildOf(rightArm);
            if (artRig != null)
            {
                foreach (var r in fallbackBody) r.enabled = false;
                artRig.SetFirstPerson(value);
            }
        }

        public void SetDowned(bool value)
        {
            downed = value;
            lastPosition = transform.position;
            motion = 0f;
            runWeight = 0f;
            sprintMotionWeight = 0f;
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
                bot != null && (bot.TacticalState == PrototypeTacticalState.InCover ||
                bot.TacticalState == PrototypeTacticalState.Revive);
            float crouchTarget = crouching && !prone ? 1f : 0f;
            float proneTarget = prone || downed ? 1f : 0f;
            crouchWeight = immediate ? crouchTarget :
                Mathf.MoveTowards(crouchWeight, crouchTarget, Time.deltaTime * 7f);
            proneWeight = immediate ? proneTarget :
                Mathf.MoveTowards(proneWeight, proneTarget, Time.deltaTime * 2.7f);
            float aimTarget = player != null && player.IsAiming || bot != null &&
                (bot.TacticalState == PrototypeTacticalState.Engage ||
                bot.TacticalState == PrototypeTacticalState.InCover) ? 1f : 0f;
            aimWeight = immediate ? aimTarget :
                Mathf.MoveTowards(aimWeight, aimTarget, Time.deltaTime * 9f);
            Pose(crouchWeight, proneWeight, Mathf.Sin(gait), motion);
        }

        private void Update()
        {
            // Update after player movement, even when the first-person body is
            // hidden. Camera and gun presentation read this same stride in LateUpdate.
            if (model == null) return;
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            AdvanceLocomotion(delta, Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (model != null && model.activeSelf) RefreshPose();
        }

        private void AdvanceLocomotion(Vector3 delta, float dt)
        {
            delta.y = 0f;
            bool teleported = delta.magnitude > 3f;
            float speed = downed || teleported ? 0f :
                delta.magnitude / Mathf.Max(dt, .001f);
            if (speed > .15f)
            {
                Vector3 local = transform.InverseTransformDirection(delta).normalized;
                strideDirection = new Vector2(local.x, local.z).normalized;
                // Turn the lower-body gait plane with travel. Backpedalling
                // keeps the knees facing toward the aim hemisphere.
                float targetYaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
                if(targetYaw>90f)targetYaw-=180f;
                else if(targetYaw<-90f)targetYaw+=180f;
                legYaw=Mathf.MoveTowardsAngle(legYaw,targetYaw,540f*dt);
            }
            if(teleported)legYaw=0f;
            motion = teleported ? 0f : Mathf.MoveTowards(motion,
                Mathf.Clamp01(speed / .25f), dt * 5f);
            float running = player != null ? (player.IsSprinting ? 1f : Mathf.InverseLerp(2.2f,4.2f,speed)*.65f) :
                Mathf.InverseLerp(2.2f, 6.6f, speed);
            runWeight = teleported ? 0f : Mathf.MoveTowards(runWeight,
                downed ? 0f : running, dt * 4f);
            float sprintTarget = player != null ?
                (player.IsSprinting && player.Alive && player.CurrentVehicle == null &&
                 !player.IsAiming && !player.Reloading && !player.IsSliding &&
                 !player.IsCrouched && !player.IsProne ? Mathf.Clamp01(speed / 6.6f) : 0f) :
                Mathf.InverseLerp(4.2f, 6.6f, speed);
            sprintMotionWeight = teleported || downed ? 0f : Mathf.MoveTowards(
                sprintMotionWeight, sprintTarget, dt * (sprintTarget > sprintMotionWeight ? 6f : 10f));
            // Travel distance, stance duration and foot travel use the same
            // cycle definition. A planted foot moves back as the root advances.
            float duty = GroundDuty(runWeight,crouchWeight);
            float span = GroundSpan(runWeight,crouchWeight);
            duty = Mathf.Lerp(duty,.40f,proneWeight); span = Mathf.Lerp(span,CrawlSpan,proneWeight);
            if (!teleported && speed > .04f) gait += delta.magnitude * (Mathf.PI*2f) * duty / span;
        }

        private void Pose(float crouch, float prone, float phase, float movement)
        {
            // Body dimensions stay fixed in every stance; only the joints move.
            model.transform.localScale = Vector3.one;
            model.transform.localRotation = Quaternion.identity;
            float floorOffset = baseHeight;
            float terrainFloor=PrototypeLayout.HeightAt(transform.position.x,transform.position.z);
            // Preserve airborne/isolated review actors. Near the ground, use
            // the visible walking surface for local, AI and remote soldiers.
            if(Mathf.Abs(transform.position.y+baseHeight-terrainFloor)<.6f)
                floorOffset=Mathf.Max(baseHeight,PrototypeWorldVisual.InfantrySurfaceAt(transform.position.x,transform.position.z)-transform.position.y);
            model.transform.localPosition = new Vector3(0f, floorOffset, 0f);
            float ground = PrototypeLayout.HeightAt(transform.position.x,transform.position.z);
            if (Mathf.Abs(ground-model.transform.position.y)<.25f)
            {
                float dx=PrototypeLayout.HeightAt(transform.position.x+.4f,transform.position.z)-PrototypeLayout.HeightAt(transform.position.x-.4f,transform.position.z);
                float dz=PrototypeLayout.HeightAt(transform.position.x,transform.position.z+.4f)-PrototypeLayout.HeightAt(transform.position.x,transform.position.z-.4f);
                Vector3 normal=new Vector3(-dx/.8f,1f,-dz/.8f).normalized;
                float tilt=Vector3.Angle(Vector3.up,normal);
                normal=Vector3.Slerp(Vector3.up,normal,tilt>25f?25f/tilt:1f);
                model.transform.rotation=Quaternion.FromToRotation(Vector3.up,normal)*transform.rotation;
            }
            if(artRig!=null) artRig.SupportHandWeight=prone*movement*(1f-aimWeight*.7f);
            leftLeg.localPosition = new Vector3(-.17f, 0f, 0f);
            rightLeg.localPosition = new Vector3(.17f, 0f, 0f);
            float running = runWeight * (1f - crouch) * (1f - prone);
            float legHeading=legYaw*movement*(1f-prone);
            Quaternion legFrame=Quaternion.Euler(0,legHeading,0);
            float wave = Mathf.Sin(gait);
            float swing = wave * movement;
            // Chest counters the pelvis, with a small delay through the spine.
            // Keep the head facing the view and leave the leg solution unchanged.
            float sprintTwist = Mathf.Sin(gait - .12f) * 3.2f * sprintMotionWeight *
                (1f - crouch) * (1f - prone) * (1f - aimWeight);
            float strideLean = running * movement;
            float crawl = prone * movement;
            // The hips rise over the supporting leg in a walk. Running has
            // separate landing compression, push-off and flight, rather than
            // the same sinusoid played faster. Both legs share one half-cycle.
            float halfCycle=Mathf.Repeat(gait/(Mathf.PI*2f),.5f);
            float walkHeight=.81f-.04f*Mathf.Cos(gait*2f);
            float runHeight=RunHipHeight.Evaluate(halfCycle);
            float movingHeight=Mathf.Lerp(walkHeight,runHeight,running);
            // Backward contact is behind the hip while the chest still faces
            // the sights. A small knee bend keeps that foot within leg reach.
            movingHeight-=Mathf.Max(0,-strideDirection.y)*.06f;
            movingHeight=Mathf.Lerp(movingHeight,.655f+.008f*Mathf.Cos(gait*2f),crouch);
            float hipY = Mathf.Lerp(Mathf.Lerp(HipHeight-.055f,.655f,crouch),movingHeight,movement);
            hipY = Mathf.Lerp(hipY,.225f,prone);
            hipY += Mathf.Abs(Mathf.Sin(gait)) * crawl * .008f;
            float support=Mathf.Sin(gait+Mathf.PI*(.5f-GroundDuty(running,crouch)))*movement;
            float hipShift=-support*Mathf.Lerp(.025f,.012f,running);
            pelvis.localPosition = legFrame*new Vector3(Mathf.Lerp(hipShift,swing*.012f,prone), hipY,
                Mathf.Lerp(-.06f*crouch-.04f*strideLean, -.20f, prone));
            float pelvisPitch = Mathf.Lerp(4f+crouch*8f+strideLean*4f,84f,prone);
            pelvis.localRotation = Quaternion.Euler(pelvisPitch, legHeading*.70f-swing*(2f+running)*(1f-prone), Mathf.Lerp(support, -swing*2f,prone));
            float breath=Mathf.Sin(Time.time*1.7f)*(1f-movement)*(1f-prone);
            float chestPitch = Mathf.Lerp(7f+crouch*19f+strideLean*10f,downed?92f:78f,prone)+breath*.6f;
            spine.rotation = model.transform.rotation * Quaternion.Euler(Mathf.Lerp(pelvisPitch,chestPitch,.45f),
                legHeading*.40f+swing*2f*(1f-prone)+sprintTwist*.55f,-swing*1.5f);
            torso.position = spine.position + spine.up*SpineLength;
            torso.rotation = model.transform.rotation * Quaternion.Euler(chestPitch,
                legHeading*.12f+swing*(3f-running)*(1f-prone)+sprintTwist,downed?12f:-swing*(1.5f+crawl*2f));
            head.rotation = model.transform.rotation * Quaternion.Euler(Mathf.Lerp(1f,downed?22f:10f,prone),
                -swing*1.5f*(1f-prone),downed?-12f:0f);

            Vector3 travel = new Vector3(strideDirection.x,0,strideDirection.y).normalized;
            if (travel.sqrMagnitude < .1f) travel = Vector3.forward;
            for (int sideIndex=0;sideIndex<2;sideIndex++)
            {
                bool left=sideIndex==0; float side=left?-1f:1f;
                float cycle = gait+(left?0f:Mathf.PI);
                Vector3 standing = StepFoot(cycle,running,crouch,movement,travel,side,legFrame,out float footPitch);
                Vector3 proneFoot = CrawlFoot(cycle,movement,side,out Quaternion crawlRotation);
                Vector3 target = Vector3.Lerp(standing,proneFoot,prone);
                target.y += Mathf.Sin(prone*Mathf.PI)*.035f;
                Transform hip=left?leftLeg:rightLeg, knee=left?leftKnee:rightKnee, foot=left?leftFoot:rightFoot;
                Vector3 pole = Vector3.Lerp(legFrame*Vector3.forward,new Vector3(side,-.5f,0f),prone).normalized;
                PoseLegToTarget(hip,knee,foot,target,pole,prone>.98f?.115f:-1f);
                foot.rotation = model.transform.rotation * Quaternion.Slerp(legFrame*Quaternion.Euler(footPitch,side*5f,0),crawlRotation,prone);
            }

            weaponMount.localPosition = new Vector3(Mathf.Lerp(.05f, .015f, prone),
                Mathf.Lerp((artRig != null ? 1.33f : 1.16f) - crouch * .22f - strideLean*.06f, .36f, prone),
                Mathf.Lerp((artRig != null ? .18f : .18f) + crouch * .10f, .49f, prone));
            weaponMount.localPosition += new Vector3(-swing*.009f, Mathf.Cos(gait*2f)*movement*.008f*(1f-prone)+breath*.002f,0);
            weaponMount.localRotation = Quaternion.Euler(prone*2f,-swing*2f*movement,prone*(-3f+swing*3f));
            bool compact = heldWeaponId == PrototypeWeaponId.Pistol ||
                heldWeaponId == PrototypeWeaponId.Smg;
            bool firearm = heldEquipmentKind == PrototypeHeldEquipment.Firearm;
            float carry = firearm && (player == null || !player.Reloading) ?
                strideLean * (1f - aimWeight) : 0f;
            if (firearm && !compact)
            {
                // A long gun remains in both hands, with a diagonal low-ready
                // hold and the arms following the counter-rotation of the chest.
                weaponMount.localPosition += new Vector3(swing * carry * .02f,
                    -carry * .06f + Mathf.Sin(gait * 2f) * carry * .01f,
                    carry * .13f + swing * carry * .01f);
                weaponMount.localRotation = Quaternion.Euler(carry * 24f,
                    -carry * 22f + swing * carry * 4f, -carry * 12f + swing * carry * 2f);
            }
            if (firearm && weaponArt != null)
            {
                // The authored receiver extends behind its origin. Keep its
                // pistol grip in front of the vest, with the stock at the shoulder.
                weaponMount.localPosition += new Vector3(.035f, 0f, .18f) * (1f-prone);
                weaponMount.localRotation *= Quaternion.Euler(0f, -12f*(1f-prone), 0f);
            }
            if (firearm)
                weaponMount.localRotation = Quaternion.Euler(0f, sprintTwist * .65f, 0f) * weaponMount.localRotation;
            if ((heldEquipmentKind == PrototypeHeldEquipment.Launcher || heldEquipmentKind == PrototypeHeldEquipment.AntiAirLauncher))
            {
                weaponMount.localPosition += new Vector3(.12f, .12f, 0f);
                if (bot != null && bot.IsUsingLauncher)
                    weaponMount.localRotation = Quaternion.Euler(bot.LauncherAimPitch, 0f, 0f);
            }
            else if (heldEquipmentKind == PrototypeHeldEquipment.Throwable)
                weaponMount.localPosition += new Vector3(.14f, .05f, .13f);
            if (downed)
            {
                weaponMount.localPosition = new Vector3(.45f, .28f, .15f);
                weaponMount.localRotation = Quaternion.Euler(0f, 20f, 55f);
            }
            Transform weaponFrame = heldEquipment != null ? heldEquipment.transform :
                heldWeapon != null ? heldWeapon.transform : weaponMount;
            bool pistol = heldWeaponId == PrototypeWeaponId.Pistol;
            if (compact && carry > 0f && heldWeapon != null)
            {
                // Match the actual grip after the player's pitch/bob update.
                // This keeps a low-held SMG or pistol attached to the right
                // hand even though their local grip offsets are different.
                weaponMount.localRotation = Quaternion.Slerp(Quaternion.identity,
                    Quaternion.Euler(63f, -5f, -8f), carry);
                Vector3 currentGrip = model.transform.InverseTransformPoint(
                    weaponFrame.TransformPoint(new Vector3(0f, -.18f, pistol ? -.045f : -.18f)));
                Vector3 carryGrip = new Vector3(.37f + swing * .025f,
                    hipY + .23f + swing * .04f, .12f + swing * .25f);
                weaponMount.localPosition += (carryGrip - currentGrip) * carry;
            }
            Vector3 rightGrip = model.transform.InverseTransformPoint(
                weaponFrame.TransformPoint(new Vector3(0f, -.18f, pistol ? -.045f : -.18f)));
            Vector3 leftGrip = model.transform.InverseTransformPoint(
                weaponFrame.TransformPoint(pistol ? new Vector3(-.055f, -.16f, .015f) :
                    new Vector3(0f, -.16f, .37f)));
            if (firearm && weaponArt != null)
            {
                rightGrip=model.transform.InverseTransformPoint(weaponArt.RightGrip.position);
                leftGrip=model.transform.InverseTransformPoint(weaponArt.SupportPosition);
            }
            if (compact && carry > 0f)
                leftGrip = Vector3.Lerp(leftGrip,
                    new Vector3(-.39f, hipY + .25f - swing * .025f,
                        .12f - swing * .34f), carry);
            if ((heldEquipmentKind == PrototypeHeldEquipment.Launcher || heldEquipmentKind == PrototypeHeldEquipment.AntiAirLauncher) && heldEquipment != null)
            {
                rightGrip = model.transform.InverseTransformPoint(weaponFrame.TransformPoint(
                    new Vector3(0f, -.20f, .02f)));
                leftGrip = model.transform.InverseTransformPoint(weaponFrame.TransformPoint(
                    new Vector3(0f, -.15f, .34f)));
            }
            else if (heldEquipmentKind == PrototypeHeldEquipment.Throwable && heldEquipment != null)
            {
                rightGrip = model.transform.InverseTransformPoint(weaponFrame.TransformPoint(
                    new Vector3(0f, -.045f, 0f)));
                leftGrip = rightGrip + new Vector3(-.18f, -.14f, -.12f);
            }
            if (firearm && !downed && weaponFrame.IsChildOf(weaponMount))
            {
                // Keep the rigid gun inside the actual two-arm reach envelope.
                // Correct the prop instead of stretching the forearm or leaving
                // a hand behind when chest rotation reaches its stride extreme.
                float reach=UpperArmLength+ForearmLength-.012f;
                Vector3 rightOrigin=model.transform.InverseTransformPoint(rightArm.position);
                Vector3 leftOrigin=model.transform.InverseTransformPoint(leftArm.position);
                for(int pass=0;pass<4;pass++)
                {
                    Vector3 correction=ReachCorrection(rightOrigin,rightGrip,reach);
                    weaponMount.localPosition+=correction;rightGrip+=correction;
                    leftGrip+=correction*(compact?1f-carry:1f);
                    if((!compact||carry<.001f)&&prone*movement<.001f)
                    {
                        correction=ReachCorrection(leftOrigin,leftGrip,reach);
                        weaponMount.localPosition+=correction;rightGrip+=correction;leftGrip+=correction;
                    }
                }
            }
            if (downed)
            {
                rightGrip = new Vector3(.49f, .14f, .35f);
                leftGrip = new Vector3(-.45f, .13f, .43f);
            }
            // The elbows flare out and settle low while crawling, rather than
            // swinging a rigid rifle through the ground with the torso.
            Vector3 armPole = Vector3.Lerp(new Vector3(.25f, -1f, -.12f),
                new Vector3(.22f, -1f, -.12f), prone);
            if (firearm && !compact)
                armPole = Vector3.Lerp(armPole, new Vector3(.65f, -.70f, -.25f), carry);
            Vector3 leftPole = new Vector3(-armPole.x, armPole.y, armPole.z);
            Vector3 rightPole = armPole;
            if (compact && carry > 0f)
            {
                // Keep a free running elbow low and close to the ribs. An
                // outward pole otherwise splays both upper arms like a T pose.
                leftPole = Vector3.Lerp(leftPole, new Vector3(-.15f, -1f, -swing * .6f), carry);
                rightPole = Vector3.Lerp(rightPole, new Vector3(.2f, -1f, swing * .3f), carry);
            }
            PoseArm(leftArm, leftElbow, leftHand, leftGrip,
                leftPole, prone>.98f?.09f:-1f);
            PoseArm(rightArm, rightElbow, rightHand, rightGrip, rightPole, prone>.98f?.09f:-1f);
            if(firearm && weaponArt!=null && !downed)
            {
                rightHand.rotation=weaponArt.RightGrip.rotation;
                leftHand.rotation=weaponArt.SupportRotation;
            }
            if (compact && carry > .8f)
                leftHand.rotation = model.transform.rotation * Quaternion.Euler(10f, 0f, -10f);
            if (prone > .98f && !downed)
            {
                // Rifle stays in the right hand. The support arm plants its
                // elbow and draws the body forward, then reaches during recovery.
                float u=Mathf.Repeat(gait/(Mathf.PI*2f),1f);
                float pull=u<.30f?Mathf.Lerp(.14625f,-.14625f,u/.30f):Mathf.Lerp(-.14625f,.14625f,Mathf.SmoothStep(0,1,(u-.30f)/.70f));
                float release=u<.30f?0f:Mathf.Sin((u-.30f)/.70f*Mathf.PI)*.055f;
                float reach=movement*(1f-aimWeight*.7f);
                Vector3 origin=model.transform.InverseTransformPoint(leftArm.position);
                float dy=(.09f+release)-origin.y;
                float radius=Mathf.Sqrt(Mathf.Max(.001f,UpperArmLength*UpperArmLength-dy*dy));
                float z=Mathf.Clamp(pull,-radius*.97f,radius*.97f);
                Vector3 elbow=origin+new Vector3(-Mathf.Sqrt(Mathf.Max(0,radius*radius-z*z)),dy,z);
                Vector3 hand=elbow+new Vector3(.025f,.035f,.3136f).normalized*ForearmLength;
                Vector3 oldElbow=model.transform.InverseTransformPoint(leftElbow.position);
                elbow=Vector3.Lerp(oldElbow,elbow,reach);
                hand=Vector3.Lerp(leftGrip,hand,reach);
                leftArm.rotation=model.transform.rotation*Quaternion.FromToRotation(Vector3.down,elbow-origin);
                leftElbow.rotation=model.transform.rotation*Quaternion.FromToRotation(Vector3.down,hand-elbow);
                leftHand.rotation=Quaternion.Slerp(leftHand.rotation,model.transform.rotation*Quaternion.Euler(25f,0,-65f),reach);
            }
            if (artRig != null) artRig.Synchronize();
        }

        private static Vector3 ReachCorrection(Vector3 origin,Vector3 grip,float reach)
        {
            Vector3 delta=grip-origin;
            return delta.magnitude>reach ? origin+delta.normalized*reach-grip : Vector3.zero;
        }

        // Durations are fractions of a full left/right cycle. Distance follows
        // root travel, but the resulting cadence must also remain plausible:
        // 6.6 m/s run = 3.68 steps/s; 2 m/s crouch = 2.55 steps/s.
        private static float GroundDuty(float running,float crouch) => Mathf.Lerp(Mathf.Lerp(.60f,.24f,running),.56f,crouch);
        private static float GroundSpan(float running,float crouch) => Mathf.Lerp(.86f,.88f,crouch);
        private static readonly AnimationCurve RunHipHeight=new AnimationCurve(
            new Keyframe(0,.785f,-.35f,-.35f),new Keyframe(.075f,.748f,0,0),
            new Keyframe(.24f,.80f,.4f,.4f),new Keyframe(.37f,.86f,0,0),
            new Keyframe(.5f,.785f,-.35f,-.35f));
        private static readonly AnimationCurve RunRecoveryTravel=new AnimationCurve(
            new Keyframe(0,-.5f,-.7f,-.7f),new Keyframe(.12f,-.58f,0,0),
            new Keyframe(.32f,-.35f,2.2f,2.2f),new Keyframe(.58f,.22f,1.6f,1.6f),
            new Keyframe(.82f,.53f,0,0),new Keyframe(1,.5f,-.5f,-.5f));
        private static readonly AnimationCurve RunRecoveryLift=new AnimationCurve(
            new Keyframe(0,0,1.3f,1.3f),new Keyframe(.12f,.14f,1.7f,1.7f),
            new Keyframe(.32f,.38f,0,0),new Keyframe(.58f,.24f,-.6f,-.6f),
            new Keyframe(.82f,.10f,-.5f,-.5f),new Keyframe(1,0,-.25f,-.25f));
        private static readonly AnimationCurve RunRecoveryPitch=new AnimationCurve(
            new Keyframe(0,28,160,160),new Keyframe(.32f,58,0,0),
            new Keyframe(.58f,10,-140,-140),new Keyframe(.82f,-8,0,0),new Keyframe(1,-5,0,0));

        private Vector3 StepFoot(float cycle,float running,float crouch,float movement,Vector3 travel,float side,Quaternion legFrame,out float pitch)
        {
            float u=Mathf.Repeat(cycle/(Mathf.PI*2f),1f);
            float duty=GroundDuty(running,crouch);
            float span=GroundSpan(running,crouch);
            float along, lift;
            if(u<duty)
            {
                float t=u/duty; along=Mathf.Lerp(span*.5f,-span*.5f,t); lift=0;
                pitch=Mathf.Lerp(Mathf.Lerp(-7f,-5f,running),-3f,crouch)*(1f-Mathf.Clamp01(t*6f))+
                    Mathf.SmoothStep(0,Mathf.Lerp(Mathf.Lerp(24f,28f,running),12f,crouch),Mathf.InverseLerp(.70f,1f,t));
            }
            else
            {
                float t=(u-duty)/(1f-duty); along=Mathf.Lerp(-span*.5f,span*.5f,Mathf.SmoothStep(0,1,t));
                lift=Mathf.Sin(t*Mathf.PI)*Mathf.Lerp(.085f,.07f,crouch);
                pitch=Mathf.Lerp(24f,-7f,t);
                along=Mathf.Lerp(along,RunRecoveryTravel.Evaluate(t)*span,running);
                lift=Mathf.Lerp(lift,RunRecoveryLift.Evaluate(t),running);
                pitch=Mathf.Lerp(pitch,RunRecoveryPitch.Evaluate(t),running);
                pitch=Mathf.Lerp(pitch,Mathf.Lerp(12f,-3f,t),crouch);
            }
            along-=.07f*running;
            pitch*=movement;
            float clearance=FootHeight*Mathf.Cos(pitch*Mathf.Deg2Rad)+.14f*Mathf.Abs(Mathf.Sin(pitch*Mathf.Deg2Rad));
            Vector3 target=legFrame*new Vector3(side*Mathf.Lerp(.17f,.18f,crouch),clearance+lift*movement,.025f-side*.035f*(1f-movement))+travel*along*movement;
            // During an abrupt A/D reversal the gait plane needs a few frames
            // to turn. Replant within each leg's own lane instead of sending
            // its ankle through the opposite thigh while that turn settles.
            Vector3 gaitLocal=Quaternion.Inverse(legFrame)*target;
            float outside=side*gaitLocal.x;
            if(outside<.11f)target+=legFrame*Vector3.right*(side*(.11f-outside));
            return target;
        }
        private float CrawlSpan => Mathf.Lerp(.18f,.39f,Mathf.Abs(strideDirection.y));

        private Vector3 CrawlFoot(float cycle,float movement,float side,out Quaternion rotation)
        {
            float u=Mathf.Repeat(cycle/(Mathf.PI*2f),1f);
            float recovery=u<.70f?0f:Mathf.Sin((u-.70f)/.30f*Mathf.PI);
            float draw=u<.40f?Mathf.Lerp(1f,0f,u/.40f):u<.70f?0f:Mathf.SmoothStep(0,1,(u-.70f)/.30f);
            rotation=Quaternion.Euler(68f-recovery*12f*movement,side*(25f+recovery*12f*movement),side*18f);
            Vector3 travel=new Vector3(strideDirection.x,0,strideDirection.y).normalized;
            float backOffset=Mathf.Max(0,-travel.z)*CrawlSpan;
            return new Vector3(side*(.28f+recovery*.08f*movement),.230f+recovery*.035f*movement,
                -.94f+(backOffset+.08f*Mathf.Abs(travel.x))*movement)+travel*(draw*CrawlSpan*movement);
        }
        private void PoseLegToTarget(Transform hip,Transform knee,Transform foot,Vector3 target,Vector3 preferredPole,float contactHeight)
        {
            Vector3 origin=model.transform.InverseTransformPoint(hip.position);
            Vector3 joint=SolveJoint(origin,target,ThighLength,ShinLength,preferredPole,contactHeight,out Vector3 end);
            hip.rotation=model.transform.rotation*Quaternion.FromToRotation(Vector3.down,joint-origin);
            knee.rotation=model.transform.rotation*Quaternion.FromToRotation(Vector3.down,end-joint);
        }
        private static Vector3 SolveJoint(Vector3 origin,Vector3 target,float upper,float lower,Vector3 pole,float contactHeight,out Vector3 end)
        {
            Vector3 delta=target-origin; float d=Mathf.Clamp(delta.magnitude,.025f,upper+lower-.001f);
            Vector3 direction=delta.sqrMagnitude>.00001f?delta.normalized:Vector3.down;
            float along=(upper*upper+d*d-lower*lower)/(2f*d);
            float radius=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            Vector3 center=origin+direction*along;
            Vector3 bend=Vector3.ProjectOnPlane(pole,direction).normalized;
            if(bend.sqrMagnitude<.001f) bend=Vector3.ProjectOnPlane(Vector3.right,direction).normalized;
            if(contactHeight>=0 && radius>.001f)
            {
                Vector3 vertical=Vector3.ProjectOnPlane(Vector3.up,direction);
                float heightRange=vertical.magnitude*radius;
                if(heightRange>.0001f)
                {
                    Vector3 up=vertical.normalized, across=Vector3.Cross(direction,up);
                    float h=Mathf.Clamp((contactHeight-center.y)/heightRange,-1,1);
                    float sign=Vector3.Dot(across,bend)<0?-1:1;
                    bend=up*h+across*(sign*Mathf.Sqrt(1-h*h));
                }
            }
            end=origin+direction*d; return center+bend*radius;
        }

        private void PoseMotorcycle(PrototypeCombinedArmsVehicle bike)
        {
            // Seat and grip anchors are on the visual chassis/front fork. The
            // same rig follows steering in both views without stretching limbs.
            bool driver = bike.PlayerSeat == 0;
            if(artRig!=null)artRig.SupportHandWeight=0f;
            model.transform.localScale = Vector3.one;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localPosition = new Vector3(0f, -HipHeight, 0f);
            pelvis.localPosition = new Vector3(0f, HipHeight, 0f);
            pelvis.localRotation = Quaternion.identity;
            spine.localRotation = Quaternion.Euler(driver ? 20f : 6f,0,0);
            torso.position = spine.position + spine.up*SpineLength;
            torso.localRotation = Quaternion.Euler(driver ? 20f : 6f, 0f, 0f);
            head.localRotation = Quaternion.Euler(driver ? -15f : -6f, 0f, 0f);
            leftLeg.localPosition = new Vector3(-.25f, 0f, 0f);
            rightLeg.localPosition = new Vector3(.25f, 0f, 0f);
            Transform leftPeg = bike.MotorcyclePegTarget(true, bike.PlayerSeat);
            Transform rightPeg = bike.MotorcyclePegTarget(false, bike.PlayerSeat);
            PoseMotorcycleLeg(leftLeg, leftKnee, leftFoot, leftPeg, -1f);
            PoseMotorcycleLeg(rightLeg, rightKnee, rightFoot, rightPeg, 1f);
            weaponMount.localRotation = Quaternion.identity;
            Transform leftGrip = bike.MotorcycleGripTarget(true, bike.PlayerSeat);
            Transform rightGrip = bike.MotorcycleGripTarget(false, bike.PlayerSeat);
            PoseArm(leftArm, leftElbow, leftHand, model.transform.InverseTransformPoint(leftGrip.position),
                new Vector3(-1f, -.3f, -.4f));
            PoseArm(rightArm, rightElbow, rightHand, model.transform.InverseTransformPoint(rightGrip.position),
                new Vector3(1f, -.3f, -.4f));
            leftHand.rotation = leftGrip.rotation;
            rightHand.rotation = rightGrip.rotation;
            if (artRig != null) artRig.Synchronize();
        }

        private void PoseMotorcycleLeg(Transform hip, Transform knee, Transform foot, Transform peg, float side)
        {
            Vector3 origin = model.transform.InverseTransformPoint(hip.position);
            Vector3 target = model.transform.InverseTransformPoint(peg.position + (artRig != null ? peg.up * FootHeight : Vector3.zero));
            Vector3 delta = target - origin;
            float distance = Mathf.Clamp(delta.magnitude, .025f, ThighLength + ShinLength - .001f);
            Vector3 direction = delta.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(new Vector3(side * .35f, .1f, 1f), direction).normalized;
            float along = (ThighLength * ThighLength + distance * distance - ShinLength * ShinLength) / (2f * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0f, ThighLength * ThighLength - along * along));
            Vector3 joint = origin + direction * along + pole * bend;
            Vector3 ankle = origin + direction * distance;
            hip.localRotation = Quaternion.FromToRotation(Vector3.down,
                hip.parent.InverseTransformDirection(model.transform.TransformDirection(joint - origin)));
            knee.localRotation = Quaternion.FromToRotation(Vector3.down,
                hip.InverseTransformDirection(model.transform.TransformDirection(ankle - joint)));
            foot.rotation = peg.rotation;
        }

        private void PoseArm(Transform shoulder, Transform elbow, Transform hand, Vector3 target, Vector3 pole, float contactHeight=-1f)
        {
            Vector3 origin=model.transform.InverseTransformPoint(shoulder.position);
            Vector3 joint=SolveJoint(origin,target,UpperArmLength,ForearmLength,pole,contactHeight,out Vector3 grip);
            shoulder.rotation=model.transform.rotation*Quaternion.FromToRotation(Vector3.down,joint-origin);
            elbow.rotation=model.transform.rotation*Quaternion.FromToRotation(Vector3.down,grip-joint);
            hand.rotation=heldEquipment!=null?heldEquipment.transform.rotation:heldWeapon!=null?heldWeapon.transform.rotation:weaponMount.rotation;
        }

        private void BodyPart(string label, PrimitiveType shape, Vector3 position,
            Vector3 scale, Material material) =>
            Part(label, shape, torso, position - Vector3.up * (HipHeight + SpineLength), scale, material);

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

        private void Arm(Transform pivot, Material cloth, Material trim,
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

        private void Leg(Transform pivot, Material cloth, Material armor,
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
            if (collider != null) { collider.enabled = false; Destroy(collider); }
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
            var authored=PrototypeWeaponArtRig.Create(parent,weaponId,firstPerson,team);
            if(authored!=null)return authored.gameObject;
            var root = new GameObject(weaponId + " weapon model");
            root.transform.SetParent(parent, false);
            bool pistol = weaponId == PrototypeWeaponId.Pistol;
            if (pistol)
            {
                BuildPistol(root.transform, game);
                if (firstPerson) AddHands(root.transform, game, team, pistol);
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
                AddHands(root.transform, game, team, pistol);
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
            PrototypeTeam team, bool pistol)
        {
            if (PrototypeSoldierArtRig.BuildViewArms(root, team, pistol)) return;
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
