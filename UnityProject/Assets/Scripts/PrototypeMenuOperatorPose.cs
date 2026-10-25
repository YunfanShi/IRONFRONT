using System;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Menu-only retarget: sampled CC0 body motion, existing soldier proportions,
    // fixed boot contacts and weapon-specific two-hand constraints.
    public sealed class PrototypeMenuOperatorPose : MonoBehaviour
    {
        [Serializable] private sealed class BodyClip
        {
            public float duration;
            public BodyFrame[] frames;
        }
        [Serializable] private struct BodyFrame
        {
            public Vector3 position;
            public Quaternion pelvis, spine, chest, head;
        }
        private static BodyClip clip;
        private const float PlaybackRate = .68f;
        private PrototypeSoldierVisual visual;
        private Transform model, pelvis, spine, chest, head, leftArm, rightArm, leftLeg, rightLeg;
        private PrototypeWeaponArtRig weaponArt;
        private PrototypeWeaponId weapon;
        private float elapsed;
        private float hipHeight, spineLength, ankleHeight;
        private Quaternion chestBase;
        private Vector3 mountOffset;
        private Quaternion mountRotation;
        public bool Automatic { get; set; } = true;
        public float LoopDuration => clip != null ? clip.duration / PlaybackRate : 4f;
        public bool HasSourceMotion => clip != null;

        public void Initialize(PrototypeSoldierVisual rig, PrototypeWeaponId id)
        {
            visual = rig; weapon = id;
            model = transform.Find("Armored infantry visual");
            pelvis = model.Find("Pelvis"); spine = pelvis.Find("Spine"); chest = pelvis.Find("Torso");
            head = chest.Find("Head"); leftArm = chest.Find("Left arm"); rightArm = chest.Find("Right arm");
            leftLeg = pelvis.Find("Left leg"); rightLeg = pelvis.Find("Right leg");
            weaponArt = visual.HeldWeapon.GetComponent<PrototypeWeaponArtRig>();
            hipHeight = visual.ArtRig != null ? visual.ArtRig.HipHeight - .012f : .83f;
            ankleHeight = visual.ArtRig != null ? .14f : .12f;
            spineLength = chest.localPosition.y;
            chestBase = Quaternion.Euler(2.5f, -4f, -1.2f);
            bool pistol = weapon == PrototypeWeaponId.Pistol;
            Vector3 mountHome = pistol ? new Vector3(.025f, 1.16f, .39f) :
                weaponArt != null ? new Vector3(.095f, 1.23f, .29f) : new Vector3(.065f, 1.17f, .24f);
            Vector3 chestHome = new Vector3(.025f, hipHeight + spineLength, 0f);
            mountOffset = Quaternion.Inverse(chestBase) * (mountHome - chestHome);
            mountRotation = Quaternion.Inverse(chestBase) *
                (pistol ? Quaternion.Euler(30f, -8f, 0f) : Quaternion.Euler(28f, -38f, 3f));
            if (clip == null)
            {
                TextAsset source = Resources.Load<TextAsset>("Menu/Animation/LobbyIdleBody");
                if (source != null) clip = JsonUtility.FromJson<BodyClip>(source.text);
            }
            Evaluate(0f);
        }

        private void LateUpdate()
        {
            if (!Automatic || visual == null) return;
            elapsed += Time.unscaledDeltaTime;
            Evaluate(elapsed);
        }

        public void Evaluate(float seconds)
        {
            if (visual == null) return;
            BodyFrame f = Sample(seconds);
            // Slightly more weight on the rear/right leg; the left foot leads.
            // Retain the authored limb lengths and solve to fixed ankle targets.
            pelvis.localPosition = new Vector3(.025f, hipHeight, 0f) + f.position * .45f;
            pelvis.localRotation = Quaternion.Euler(1.5f, -3f, -1f) *
                Quaternion.Slerp(Quaternion.identity, f.pelvis, .52f);
            spine.rotation = model.rotation * Quaternion.Euler(2f, -3.5f, -1.1f) *
                Quaternion.Slerp(Quaternion.identity, f.spine, .45f);
            chest.position = spine.position + spine.up * spineLength;
            chest.rotation = model.rotation * chestBase * Quaternion.Slerp(Quaternion.identity, f.chest, .45f);
            head.rotation = model.rotation * Quaternion.Euler(1f, 5f, -.5f) *
                Quaternion.Slerp(Quaternion.identity, f.head, .25f);
            PoseLeg(leftLeg, new Vector3(-.19f, ankleHeight, .10f), -7f);
            PoseLeg(rightLeg, new Vector3(.195f, ankleHeight, -.075f), 6f);

            Transform mount = visual.WeaponMount;
            mount.SetPositionAndRotation(chest.TransformPoint(mountOffset), chest.rotation * mountRotation);
            Transform gun = visual.HeldWeapon.transform;
            bool pistol = weapon == PrototypeWeaponId.Pistol;
            Vector3 rightGrip = weaponArt != null ? weaponArt.RightGrip.position :
                gun.TransformPoint(new Vector3(0f, -.18f, pistol ? -.045f : -.18f));
            Vector3 leftGrip = weaponArt != null ? weaponArt.SupportPosition :
                gun.TransformPoint(pistol ? new Vector3(-.055f, -.16f, .015f) : new Vector3(0f, -.16f, .37f));
            // Move the whole firearm by the minimum reach correction instead
            // of extending an arm or letting a hand detach on longer weapons.
            for (int pass = 0; pass < 3; pass++)
            {
                Vector3 correction = ReachCorrection(leftArm, leftGrip);
                mount.position += correction; leftGrip += correction; rightGrip += correction;
                correction = ReachCorrection(rightArm, rightGrip);
                mount.position += correction; leftGrip += correction; rightGrip += correction;
            }
            // Elbows hang below the shoulder and stay close to the torso.
            SolveLimb(leftArm, "Elbow", "Hand", leftGrip,
                model.TransformDirection(new Vector3(-.25f, -1f, -.12f)),
                weaponArt != null ? weaponArt.SupportRotation : gun.rotation);
            SolveLimb(rightArm, "Elbow", "Hand", rightGrip,
                model.TransformDirection(new Vector3(.28f, -1f, -.10f)),
                weaponArt != null ? weaponArt.RightGrip.rotation : gun.rotation);
            if (visual.ArtRig != null) visual.ArtRig.Synchronize();
        }

        private static Vector3 ReachCorrection(Transform arm, Vector3 grip)
        {
            Transform elbow = arm.Find("Elbow");
            float reach = elbow.localPosition.magnitude + elbow.Find("Hand").localPosition.magnitude - .012f;
            Vector3 delta = grip - arm.position;
            return delta.magnitude > reach ? -delta.normalized * (delta.magnitude - reach) : Vector3.zero;
        }

        private void PoseLeg(Transform hip, Vector3 ankle, float yaw)
        {
            SolveLimb(hip, "Knee", "Ankle", model.TransformPoint(ankle), model.forward,
                model.rotation * Quaternion.Euler(0f, yaw, 0f));
        }

        private void SolveLimb(Transform root, string jointName, string endName,
            Vector3 target, Vector3 pole, Quaternion endRotation)
        {
            Transform joint = root.Find(jointName), end = joint.Find(endName);
            float upper = joint.localPosition.magnitude, lower = end.localPosition.magnitude;
            Vector3 delta = target - root.position;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upper - lower) + .001f, upper + lower - .001f);
            Vector3 direction = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
            float along = (upper * upper + distance * distance - lower * lower) / (2f * distance);
            Vector3 knee = root.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            Vector3 contact = root.position + direction * distance;
            root.rotation = model.rotation * Quaternion.FromToRotation(Vector3.down, model.InverseTransformDirection(knee - root.position));
            joint.rotation = model.rotation * Quaternion.FromToRotation(Vector3.down, model.InverseTransformDirection(contact - knee));
            end.rotation = endRotation;
        }

        private static BodyFrame Sample(float seconds)
        {
            if (clip == null || clip.frames == null || clip.frames.Length < 2)
                return new BodyFrame { pelvis = Quaternion.identity, spine = Quaternion.identity,
                    chest = Quaternion.identity, head = Quaternion.identity };
            float frame = Mathf.Repeat(seconds * PlaybackRate, clip.duration) / clip.duration * (clip.frames.Length - 1);
            int i = Mathf.Min((int)frame, clip.frames.Length - 2); float t = frame - i;
            BodyFrame a = clip.frames[i], b = clip.frames[i + 1];
            return new BodyFrame { position = Vector3.Lerp(a.position, b.position, t),
                pelvis = Quaternion.Slerp(a.pelvis, b.pelvis, t), spine = Quaternion.Slerp(a.spine, b.spine, t),
                chest = Quaternion.Slerp(a.chest, b.chest, t), head = Quaternion.Slerp(a.head, b.head, t) };
        }
    }
}
