using System;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public sealed partial class PrototypeSoldierVisual
    {
        [Serializable] private sealed class SlideLibrary
        {
            public float hipHeight;
            public SlideClip[] clips;
        }
        [Serializable] private sealed class SlideClip
        {
            public string name;
            public float duration;
            public SlideFrame[] frames;
        }
        [Serializable] private struct SlideFrame
        {
            public Vector3 position, leftThigh, leftShin, rightThigh, rightShin;
            public Quaternion pelvis, spine, chest, head, leftFoot, rightFoot;
        }
        private static SlideLibrary slideLibrary;
        private PrototypeRemotePlayer remotePlayer;
        private PrototypeSlideState SlidePose => player != null ? player.Slide : remotePlayer?.Slide;
        public bool HasSlideAnimation => LoadSlideLibrary();
        private float slidePoseWeight;
        private Quaternion slideFrameRotation;

        private static bool LoadSlideLibrary()
        {
            if (slideLibrary == null)
            {
                TextAsset source = Resources.Load<TextAsset>("Animation/InfantrySlide");
                if (source != null) slideLibrary = JsonUtility.FromJson<SlideLibrary>(source.text);
            }
            return slideLibrary != null && slideLibrary.clips != null && slideLibrary.clips.Length == 3;
        }

        private void ApplySlideBody()
        {
            slidePoseWeight = 0f;
            PrototypeSlideState slide = SlidePose;
            if (downed || slide == null || !slide.Active || !LoadSlideLibrary()) return;
            float weight = slide.Weight;
            if (weight <= 0f) return;
            slidePoseWeight = weight;
            float time = slide.PoseTime;
            SlideFrame f;
            if (time < PrototypeSlideState.EnterDuration)
                f = SampleSlide(0, time / PrototypeSlideState.EnterDuration);
            else if (time < PrototypeSlideState.ExitStart)
                f = SampleSlide(1, (time - PrototypeSlideState.EnterDuration) / PrototypeSlideState.GlideDuration);
            else if (slide.StandExit && !slide.ForcedExit)
                f = SampleSlide(2, slide.Recovery);
            else f = SampleSlide(1, 1f);

            float yaw = Mathf.Atan2(slide.Direction.x, slide.Direction.z) * Mathf.Rad2Deg;
            slideFrameRotation = model.transform.rotation * Quaternion.Euler(0f,
                Mathf.DeltaAngle(transform.eulerAngles.y, yaw), 0f);
            Quaternion basis = slideFrameRotation;
            float scale = HipHeight / Mathf.Max(.5f, slideLibrary.hipHeight);
            Vector3 hip = f.position * scale;
            hip.y = Mathf.Max(.17f, hip.y);
            Vector3 oldHip = pelvis.position;
            Quaternion oldPelvis = pelvis.rotation, oldSpine = spine.rotation,
                oldChest = torso.rotation, oldHead = head.rotation;
            Quaternion oldLT = leftLeg.rotation, oldLS = leftKnee.rotation, oldLF = leftFoot.rotation,
                oldRT = rightLeg.rotation, oldRS = rightKnee.rotation, oldRF = rightFoot.rotation;
            pelvis.SetPositionAndRotation(model.transform.position + basis * hip, basis * f.pelvis);
            spine.rotation = basis * f.spine;
            torso.position = spine.position + spine.up * SpineLength;
            torso.rotation = basis * f.chest;
            head.rotation = basis * f.head;
            leftLeg.rotation = basis * Quaternion.FromToRotation(Vector3.down, f.leftThigh);
            leftKnee.rotation = basis * Quaternion.FromToRotation(Vector3.down, f.leftShin);
            rightLeg.rotation = basis * Quaternion.FromToRotation(Vector3.down, f.rightThigh);
            rightKnee.rotation = basis * Quaternion.FromToRotation(Vector3.down, f.rightShin);
            leftFoot.rotation = basis * f.leftFoot;
            rightFoot.rotation = basis * f.rightFoot;
            Vector3 targetHip = pelvis.position;
            pelvis.SetPositionAndRotation(Vector3.Lerp(oldHip, targetHip, weight),
                Quaternion.Slerp(oldPelvis, basis * f.pelvis, weight));
            spine.rotation = Quaternion.Slerp(oldSpine, basis * f.spine, weight);
            torso.position = spine.position + spine.up * SpineLength;
            torso.rotation = Quaternion.Slerp(oldChest, basis * f.chest, weight);
            head.rotation = Quaternion.Slerp(oldHead, basis * f.head, weight);
            leftLeg.rotation = Quaternion.Slerp(oldLT, basis * Quaternion.FromToRotation(Vector3.down, f.leftThigh), weight);
            leftKnee.rotation = Quaternion.Slerp(oldLS, basis * Quaternion.FromToRotation(Vector3.down, f.leftShin), weight);
            rightLeg.rotation = Quaternion.Slerp(oldRT, basis * Quaternion.FromToRotation(Vector3.down, f.rightThigh), weight);
            rightKnee.rotation = Quaternion.Slerp(oldRS, basis * Quaternion.FromToRotation(Vector3.down, f.rightShin), weight);
            leftFoot.rotation = Quaternion.Slerp(oldLF, basis * f.leftFoot, weight);
            rightFoot.rotation = Quaternion.Slerp(oldRF, basis * f.rightFoot, weight);
            // Blending rotations alone can swing an ankle through the floor.
            // Keep the source knee direction but solve the final blended legs
            // to reachable boot contacts, without lifting the seated pelvis.
            CorrectSlideBoot(leftLeg, leftKnee, leftFoot);
            CorrectSlideBoot(rightLeg, rightKnee, rightFoot);
            if (artRig != null) artRig.SupportHandWeight = 0f;
        }

        private void CorrectSlideBoot(Transform thigh, Transform knee, Transform foot)
        {
            float lift = SlideBootClearance(foot);
            if (lift <= 0f) return;
            Vector3 origin = model.transform.InverseTransformPoint(thigh.position);
            Vector3 target = model.transform.InverseTransformPoint(foot.position) + Vector3.up * lift;
            Vector3 delta = target - origin;
            float reach = ThighLength + ShinLength - .002f;
            float horizontal = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);
            float maximum = Mathf.Sqrt(Mathf.Max(0f, reach * reach - delta.y * delta.y));
            if (horizontal > maximum && horizontal > .001f)
            { target.x = origin.x + delta.x * maximum / horizontal; target.z = origin.z + delta.z * maximum / horizontal; }
            Vector3 pole = model.transform.InverseTransformDirection(knee.position - thigh.position);
            Quaternion rotation = foot.rotation;
            PoseLegToTarget(thigh, knee, foot, target, pole, -1f);
            foot.rotation = rotation;
        }

        private float SlideBootClearance(Transform foot)
        {
            Vector3 up = model.transform.up;
            float extent = FootHeight * Mathf.Abs(Vector3.Dot(foot.up, up)) +
                .18f * Mathf.Abs(Vector3.Dot(foot.forward, up)) + .07f * Mathf.Abs(Vector3.Dot(foot.right, up));
            return extent - Vector3.Dot(foot.position - model.transform.position, up);
        }

        private void ApplySlideWeapon()
        {
            if (slidePoseWeight <= 0f) return;
            Vector3 offset = new Vector3(.085f, .23f, .34f);
            if (heldEquipmentKind == PrototypeHeldEquipment.Throwable) offset = new Vector3(.20f, .06f, .35f);
            if (heldEquipmentKind == PrototypeHeldEquipment.Launcher || heldEquipmentKind == PrototypeHeldEquipment.AntiAirLauncher)
                offset = new Vector3(.20f, .18f, .30f);
            weaponMount.position = Vector3.Lerp(weaponMount.position, torso.position + slideFrameRotation * offset, slidePoseWeight);
            weaponMount.rotation = Quaternion.Slerp(weaponMount.rotation,
                slideFrameRotation * Quaternion.Euler(4f, -10f, -5f), slidePoseWeight);
        }

        private static SlideFrame SampleSlide(int clip, float normalized)
        {
            SlideFrame[] frames = slideLibrary.clips[clip].frames;
            float sample = Mathf.Clamp01(normalized) * (frames.Length - 1);
            int i = Mathf.Min((int)sample, frames.Length - 2); float t = sample - i;
            SlideFrame a = frames[i], b = frames[i + 1];
            return new SlideFrame { position = Vector3.Lerp(a.position, b.position, t),
                pelvis = Quaternion.Slerp(a.pelvis,b.pelvis,t), spine = Quaternion.Slerp(a.spine,b.spine,t),
                chest = Quaternion.Slerp(a.chest,b.chest,t), head = Quaternion.Slerp(a.head,b.head,t),
                leftFoot = Quaternion.Slerp(a.leftFoot,b.leftFoot,t), rightFoot = Quaternion.Slerp(a.rightFoot,b.rightFoot,t),
                leftThigh = Vector3.Slerp(a.leftThigh,b.leftThigh,t), leftShin = Vector3.Slerp(a.leftShin,b.leftShin,t),
                rightThigh = Vector3.Slerp(a.rightThigh,b.rightThigh,t), rightShin = Vector3.Slerp(a.rightShin,b.rightShin,t) };
        }
    }
}
