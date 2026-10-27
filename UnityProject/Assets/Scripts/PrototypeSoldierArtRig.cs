using System;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // The accepted mesh is driven by the existing gameplay pose, with rest-space
    // offsets authored by SoldierArtAssetSetup. It has no gameplay authority.
    public sealed class PrototypeSoldierArtRig : MonoBehaviour
    {
        [Serializable] public struct Binding
        {
            public Transform Bone;
            public int Driver;
            public Quaternion RotationOffset;
        }
        [Serializable] public struct DigitJoint
        {
            public Transform Bone;
            public Quaternion Rest;
            public bool Left;
            public int Digit, Joint;
        }
        public DigitJoint[] Digits;
        [NonSerialized] public Vector3[] StudyFingerAngles;
        [NonSerialized] public bool PistolPose;
        [NonSerialized] public float MagazinePoseBlend, ChargingPoseBlend;
        [NonSerialized] public float LeftGrasp = .85f;
        [NonSerialized] public float LeftPinch;
        [NonSerialized] public float TriggerRelease;
        public Binding[] Bindings;
        public SkinnedMeshRenderer[] Bodies;
        public SkinnedMeshRenderer[] Arms;
        public Mesh[] FullArmMeshes, ViewArmMeshes;
        public Transform[] Fingers;
        public Quaternion[] FingerRest;
        public Transform[] Thumbs;
        public Quaternion[] ThumbRest;
        public float HipHeight = .91f, ThighLength = .38003f, ShinLength = .39208f;
        public float UpperArmLength = .25080f, ForearmLength = .31658f;
        private Transform[] drivers;
        private Transform viewCamera;
        private Material[] viewArmMaterials;
        [NonSerialized] public float SourcePoseWeight;
        [NonSerialized] public Vector3 SourceLeftElbow, SourceRightElbow;
        private Quaternion[] sourceFingerRest, sourceFingerBaseline;
        public bool UsesLegacySourceHands { get; private set; }
        private Vector3[] legacySourceFingerDirections;
        private Quaternion legacyLeftForearmInHand;
        private Vector3 legacyLeftForearmDistal;
        private Vector3 legacyLeftThumbSkinAxis=Vector3.up;
        public Vector3 LeftThumbSurfaceDirection => Thumbs[0].TransformDirection(legacyLeftThumbSkinAxis);
        [NonSerialized] public Vector3 SourceLeftThumb, SourceRightThumb;
        // Measured across the actual four-finger row in the unchanged v03 mesh.
        // Local X is almost perpendicular to that row and twisted all four digits.
        static readonly Vector3[] LegacyFingerAxes = {
            new Vector3(.096511f,.027319f,-.994957f).normalized,
            new Vector3(-.109828f,.032130f,-.993431f).normalized
        };
        private void ConfigureLegacySourceHands()
        {
            if (Fingers == null || Fingers.Length != 2 || Thumbs == null || Thumbs.Length != 2) return;
            UsesLegacySourceHands = true;
            var hand=Bindings[7].Bone;var forearm=Bindings[5].Bone;
            legacyLeftForearmInHand=Quaternion.Inverse(hand.rotation)*forearm.rotation;
            legacyLeftForearmDistal=forearm.InverseTransformDirection(hand.position-forearm.position).normalized;
            // This old thumb bone does not run along the visible thumb. Measure
            // the fully weighted tip region in bind space once per arm instance.
            Vector3 thumbTip=Vector3.zero;int count=0;
            for(int arm=0;arm<Mathf.Min(2,Arms.Length);arm++)
            {
                var mesh=Arms[arm].sharedMesh;int bone=Array.IndexOf(Arms[arm].bones,Thumbs[0]);
                if(bone<0)continue;
                var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes[bone];
                for(int i=0;i<weights.Length;i++)
                {
                    var w=weights[i];float t=(w.boneIndex0==bone?w.weight0:0)+(w.boneIndex1==bone?w.weight1:0)+
                        (w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0);
                    if(t>.999f){thumbTip+=bind.MultiplyPoint3x4(vertices[i]);count++;}
                }
            }
            if(count>0 && thumbTip.sqrMagnitude>.0001f)legacyLeftThumbSkinAxis=thumbTip.normalized;
            // Preserve its original binding offsets; the weapon retargets
            // authored wrist motion relative to this model's calibrated hold.
        }
        public void CaptureSourceFingerRest()
        {
            if (Digits == null || Digits.Length != 30) return;
            sourceFingerRest = new Quaternion[30]; sourceFingerBaseline = new Quaternion[30];
            for (int i=0;i<30;i++)
            {
                var hand=Bindings[Digits[i].Left?7:8];
                Quaternion semantic=hand.Bone.rotation*Quaternion.Inverse(hand.RotationOffset);
                sourceFingerRest[i]=Quaternion.Inverse(semantic)*Digits[i].Bone.rotation;
            }
        }
        public void CalibrateSourceFingerDirections(Vector3[] directions)
        {
            if(UsesLegacySourceHands)
            { legacySourceFingerDirections=directions; return; }
            if(sourceFingerRest==null || directions==null || directions.Length!=30)return;
            for(int i=0;i<30;i++)
            {
                var joint=Digits[i];var hand=Bindings[joint.Left?7:8];
                Quaternion semantic=hand.Bone.rotation*Quaternion.Inverse(hand.RotationOffset);
                Vector3 axis=joint.Joint<2?Digits[i+1].Bone.position-joint.Bone.position:joint.Bone.up;
                Vector3 restDirection=Quaternion.Inverse(semantic)*axis.normalized;
                sourceFingerRest[i]=Quaternion.FromToRotation(restDirection,directions[i])*sourceFingerRest[i];
            }
        }
        public void ApplySourceFingers(Quaternion[] deltas, float weight)
        {
            if(UsesLegacySourceHands)
            { ApplyLegacySourceFingers(deltas,weight); return; }
            if (sourceFingerRest == null || deltas == null || deltas.Length != 30 || weight <= 0f) return;
            for(int i=0;i<30;i++)sourceFingerBaseline[i]=Digits[i].Bone.rotation;
            for(int i=0;i<30;i++)
            {
                var hand=Bindings[Digits[i].Left?7:8];
                Quaternion semantic=hand.Bone.rotation*Quaternion.Inverse(hand.RotationOffset);
                Digits[i].Bone.rotation=Quaternion.Slerp(sourceFingerBaseline[i],semantic*deltas[i]*sourceFingerRest[i],weight);
            }
        }
        private void ApplyLegacySourceFingers(Quaternion[] deltas,float weight)
        {
            if(deltas==null || deltas.Length!=30 || legacySourceFingerDirections==null ||
                legacySourceFingerDirections.Length!=30 || weight<=0f)return;
            for(int side=0;side<2;side++)
            {
                // The old mesh has one knuckle hinge for all four fingers.
                // Average the authored MCP flex to retain its opening timing.
                float flex=0f;
                for(int digit=0;digit<4;digit++)
                    flex+=LegacySourceFlex(deltas,side*15+digit*3,side);
                float curl=side==0?Mathf.Clamp(flex*.25f*(70f/30.7f),4f,85f):
                    Mathf.Clamp(70f+(flex*.25f-30.7f)*.3f,64f,76f);
                Fingers[side].localRotation=Quaternion.Slerp(Fingers[side].localRotation,
                    FingerRest[side]*Quaternion.AngleAxis(curl,LegacyFingerAxes[side]),weight);
                Vector3 thumbDirection=side==0?SourceLeftThumb:SourceRightThumb;
                if(thumbDirection.sqrMagnitude>.0001f)
                {
                    Vector3 visibleAxis=side==0?LeftThumbSurfaceDirection:Thumbs[side].up;
                    Quaternion target=Quaternion.FromToRotation(visibleAxis,thumbDirection)*Thumbs[side].rotation;
                    Thumbs[side].rotation=Quaternion.Slerp(Thumbs[side].rotation,target,weight);
                }
            }
        }
        private float LegacySourceFlex(Quaternion[] deltas,int joint,int side)
        {
            Vector3 rest=Vector3.ProjectOnPlane(legacySourceFingerDirections[joint],Vector3.up);
            Vector3 posed=Vector3.ProjectOnPlane(deltas[joint]*legacySourceFingerDirections[joint],Vector3.up);
            return Vector3.SignedAngle(rest,posed,Vector3.up)*(side==0?1f:-1f);
        }
        private void OnDestroy(){if(viewArmMaterials!=null)foreach(var m in viewArmMaterials)Destroy(m);}
        [NonSerialized] public float SupportHandWeight;
        public bool FirstPerson { get; private set; }

        public static PrototypeSoldierArtRig Create(Transform parent, PrototypeTeam team, bool candidate = false)
        {
            var prefab = Resources.Load<GameObject>("Soldiers/" + (candidate ? "HandCandidate/" : "") + team + "Soldier");
            if (prefab == null) return null;
            return Instantiate(prefab, parent, false).GetComponent<PrototypeSoldierArtRig>();
        }

        public void Bind(Transform[] poseDrivers) { drivers = poseDrivers; Synchronize(); }
        public void Synchronize()
        {
            if (drivers == null) return;
            // Lumbar is the parent of the chest; apply it before child bindings.
            ApplyBinding(Bindings[0]);
            if (Bindings.Length > 15) ApplyBinding(Bindings[15]);
            foreach (var binding in Bindings)
            {
                if (binding.Driver != 15 && binding.Driver != 0) ApplyBinding(binding);
            }
            CurlFingers();
        }
        private void ApplyBinding(Binding binding)
        {
            var driver=drivers[binding.Driver];
            binding.Bone.SetPositionAndRotation(driver.position,driver.rotation*binding.RotationOffset);
        }
        private void CurlFingers()
        {
            if (Digits != null && Digits.Length > 0)
            {
                for (int i = 0; i < Digits.Length; i++)
                {
                    var joint = Digits[i];
                    if (StudyFingerAngles != null && StudyFingerAngles.Length == Digits.Length)
                    { joint.Bone.localRotation = joint.Rest * Quaternion.Euler(StudyFingerAngles[i]); continue; }
                    if (FirstPerson)
                    {
                        int n = joint.Digit * 3 + joint.Joint;
                        Vector3 closed = PrototypeFingerPoses.WeaponGrip[n];
                        if (joint.Left)
                        {
                            closed = Vector3.Lerp(PistolPose ? PrototypeFingerPoses.PistolSupport[n] : PrototypeFingerPoses.RifleSupport[n], PrototypeFingerPoses.Magazine[n], MagazinePoseBlend);
                            closed = Vector3.Lerp(closed, PistolPose ? PrototypeFingerPoses.Slide[n] : PrototypeFingerPoses.RifleCharge[n], ChargingPoseBlend);
                            closed = Vector3.Lerp(PrototypeFingerPoses.Relaxed(joint.Digit, joint.Joint), closed, LeftGrasp);
                        }
                        else if (joint.Digit == 0)
                            closed = Vector3.Lerp(closed, new Vector3(-5f,0,joint.Joint == 0 ? -14f : 0f), TriggerRelease);
                        joint.Bone.localRotation = joint.Rest * Quaternion.Euler(closed);
                        continue;
                    }
                    float grasp = joint.Left ? LeftGrasp : 1f;
                    if (joint.Left && LeftPinch > 0f && joint.Digit > 1 && joint.Digit < 4)
                        grasp = Mathf.Lerp(grasp, .20f, LeftPinch);
                    float max = joint.Joint == 0 ? 58f : joint.Joint == 1 ? 78f : 46f;
                    if (joint.Digit == 0 && !joint.Left) grasp = Mathf.Lerp(.35f, .08f, TriggerRelease);
                    if (joint.Digit == 4) max = joint.Joint == 0 ? 30f : joint.Joint == 1 ? 42f : 30f;
                    float relaxed = joint.Digit == 4 ? 3f : 7f;
                    float spread = !joint.Left && joint.Digit == 0 && joint.Joint == 0 ? Mathf.Lerp(4f,14f,TriggerRelease) : 0f;
                    joint.Bone.localRotation = joint.Rest * Quaternion.Euler(-Mathf.Lerp(relaxed, max, grasp), 0f, spread);
                }
                return;
            }
            for (int i = 0; i < Fingers.Length; i++)
                // The imported finger X axis bends toward the palm at negative
                // angles. Positive flex folded the fingers over the hand plate.
                Fingers[i].localRotation = FingerRest[i] * (UsesLegacySourceHands ?
                    Quaternion.AngleAxis(i==0?Mathf.Lerp(70f,12f,SupportHandWeight):70f,LegacyFingerAxes[i]) :
                    Quaternion.Euler(i == 0 ? Mathf.Lerp(-65f, -12f, SupportHandWeight) : -65f, 0f, 0f));
            if (Thumbs != null && ThumbRest != null)
                for (int i = 0; i < Thumbs.Length; i++)
                    Thumbs[i].localRotation = ThumbRest[i] * Quaternion.Euler(18f, 0f, 0f);
        }

        public static bool BuildViewArms(Transform parent, PrototypeTeam team, bool pistol)
        {
            var art = Create(parent, team); if (art == null) return false;
            art.name = "Authored first person arms"; art.SetFirstPerson(true, true);
            // Camera weapon props use a .55 display scale. Keep near-camera
            // forearms close to human scale, with shoulder cuts below the frame.
            art.transform.localScale = Vector3.one * 2f;
            art.GetComponent<LODGroup>().ForceLOD(0);
            art.PoseViewArm(true, new Vector3(-.55f, -.80f, -.25f) * .5f,
                (pistol ? new Vector3(-.055f, -.16f, .015f) : new Vector3(0, -.16f, .37f)) * .5f);
            art.PoseViewArm(false, new Vector3(.65f, -.85f, -.65f) * .5f,
                new Vector3(0, -.18f, pistol ? -.045f : -.18f) * .5f);
            art.CurlFingers();
            return true;
        }
        public static PrototypeSoldierArtRig CreateWeaponViewArms(Transform parent, PrototypeTeam team, bool candidate = false, bool legacySourceHands = false)
        {
            var art=Create(parent,team,candidate); if(art==null)return null;
            art.name="Authored first person arms";art.SetFirstPerson(true,true);
            if(legacySourceHands)art.ConfigureLegacySourceHands();
            var camera=parent.GetComponentInParent<Camera>();
            art.viewCamera=camera!=null?camera.transform:null;
            // Camera arms can be viewed from inside their shoulder seam. Render
            // the existing inner surface instead of culling it into a hole.
            var clones = new System.Collections.Generic.Dictionary<Material,Material>();
            foreach(var arm in art.Arms)
            {
                var materials=arm.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    if(!clones.TryGetValue(materials[i],out var clone))
                    {
                        clone=new Material(materials[i]) { name="First-person "+materials[i].name };
                        clone.SetFloat("_Cull",0f);clones.Add(materials[i],clone);
                    }
                    materials[i]=clone;
                }
                arm.sharedMaterials=materials;
            }
            art.viewArmMaterials=new Material[clones.Count];clones.Values.CopyTo(art.viewArmMaterials,0);
            // Authored weapons are normalized by 1/.65 during import. Match
            // that scale, instead of doubling only the hands relative to guns.
            art.transform.localScale=Vector3.one/.65f;art.GetComponent<LODGroup>().ForceLOD(0);
            art.CaptureSourceFingerRest();
            return art;
        }
        public void PoseWeaponViewArms(Vector3 right,Vector3 left,Quaternion rightRotation,Quaternion leftRotation)
        {
            PoseViewArm(true,ViewShoulder(true,left),transform.InverseTransformPoint(left),Quaternion.Inverse(transform.rotation)*leftRotation);
            PoseViewArm(false,ViewShoulder(false,right),transform.InverseTransformPoint(right),Quaternion.Inverse(transform.rotation)*rightRotation);
            SetArmBone(7,transform.InverseTransformPoint(left),Quaternion.Inverse(transform.rotation)*leftRotation);
            SetArmBone(8,transform.InverseTransformPoint(right),Quaternion.Inverse(transform.rotation)*rightRotation);
            CurlFingers();
        }
        private Vector3 ViewShoulder(bool left, Vector3 target)
        {
            // Shoulders belong to the camera/chest frame, not the gun. Keeping
            // them on the weapon pushes both cuffs toward the sights in ADS.
            if(viewCamera!=null)
            {
                // A first-person sleeve must enter from below the image. Keep
                // its shoulder behind the hand, rather than near the camera
                // where perspective exposes the source body's shoulder cut.
                if (UsesLegacySourceHands || Digits != null && Digits.Length > 0)
                {
                    var anchor = new Vector3(left ? -.26f : .26f, -.32f, -.08f);
                    // Bring the support elbow around the pistol as the palm
                    // reaches over its slide. A straight approach from below
                    // points the sleeve into the camera and hides the fingers.
                    if (left && PistolPose)
                        anchor = Vector3.Lerp(anchor, new Vector3(-.44f, -.20f, .20f), ChargingPoseBlend);
                    // Full-body source shoulders are above a hip-held rifle. The
                    // view mesh has long shoulder seams: anchor them to the
                    // camera chest and retarget the authored elbow pole below.
                    return transform.InverseTransformPoint(viewCamera.TransformPoint(anchor));
                }
                float depth=Mathf.Max(.12f,viewCamera.InverseTransformPoint(target).z-.38f);
                var shoulder=new Vector3(left?-.30f:.30f,-Mathf.Max(.58f,depth),depth);
                return transform.InverseTransformPoint(viewCamera.TransformPoint(shoulder));
            }
            return new Vector3(left?-.275f:.325f,-.4f,-.25f);
        }
        private void PoseViewArm(bool left, Vector3 origin, Vector3 target, Quaternion? gripRotation = null)
        {
            float upper = UpperArmLength, lower = ForearmLength;
            Vector3 delta = target - origin;
            float reach = upper + lower - .006f;
            // Camera-only shoulder cuts may move out of frame. Stretching the
            // forearm to an unreachable hand instead collapses its wrist skin.
            if (delta.magnitude > reach) { origin = target - delta.normalized * reach; delta = target - origin; }
            float distance = Mathf.Max(.001f, delta.magnitude);
            Vector3 direction = delta.normalized;
            Vector3 poleDirection=new Vector3(left ? -1 : 1, -.7f, -.3f);
            if(viewCamera!=null)poleDirection=transform.InverseTransformDirection(viewCamera.TransformDirection(poleDirection));
            if (SourcePoseWeight > 0f)
                poleDirection=Vector3.Lerp(poleDirection.normalized,
                    (transform.InverseTransformPoint(left?SourceLeftElbow:SourceRightElbow)-origin).normalized,SourcePoseWeight);
            Vector3 pole = Vector3.ProjectOnPlane(poleDirection, direction).normalized;
            float along = (upper * upper + distance * distance - lower * lower) / (2 * distance);
            Vector3 elbow = origin + direction * along + pole * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            if(left && UsesLegacySourceHands && gripRotation.HasValue)
            {
                // Keep the support palm and forearm nearly in line. The full-body
                // source elbow is not a valid pole after moving the wrist onto P8.
                Vector3 distal=(gripRotation.Value*Bindings[7].RotationOffset*Fingers[0].localPosition).normalized;
                float wristLimit=Mathf.Lerp(18f,38f,MagazinePoseBlend);
                Vector3 approach=Vector3.RotateTowards(distal,(target-elbow).normalized,wristLimit*Mathf.Deg2Rad,0f);
                elbow=target-approach*lower;
                // Shoulder seam is off-camera; preserve both arm segment lengths.
                Vector3 shoulderDirection=(origin-elbow).normalized;
                if(viewCamera!=null)
                {
                    Vector3 down=transform.InverseTransformDirection(viewCamera.TransformDirection(new Vector3(-.15f,-1f,-.15f)));
                    shoulderDirection=Vector3.Slerp(shoulderDirection,down.normalized,MagazinePoseBlend);
                }
                origin=elbow+shoulderDirection*upper;
            }
            Quaternion upperArm = Quaternion.FromToRotation(Vector3.down, elbow - origin);
            Quaternion forearm = Quaternion.FromToRotation(Vector3.down, target - elbow);
            if (gripRotation.HasValue)
            {
                Quaternion hand = gripRotation.Value * Quaternion.Euler(0,left?-90f:90f,0);
                Vector3 handForward = Digits != null && Digits.Length > 0 ?
                    gripRotation.Value * (left ? Vector3.right : Vector3.left) : hand * Vector3.forward;
                Vector3 forward = Vector3.ProjectOnPlane(handForward, target-elbow);
                if (forward.sqrMagnitude > .0001f)
                    forearm = Quaternion.LookRotation(forward.normalized, (elbow-target).normalized);
                Vector3 upperForward = Vector3.ProjectOnPlane(handForward, elbow-origin);
                if (upperForward.sqrMagnitude > .0001f)
                    upperArm = Quaternion.LookRotation(upperForward.normalized, (origin-elbow).normalized);
            }
            if(left && UsesLegacySourceHands && gripRotation.HasValue)
            {
                // Preserve the mesh's neutral wrist roll as well as its bend.
                Quaternion boneRotation=gripRotation.Value*Bindings[7].RotationOffset*legacyLeftForearmInHand;
                boneRotation=Quaternion.FromToRotation(boneRotation*legacyLeftForearmDistal,target-elbow)*boneRotation;
                forearm=boneRotation*Quaternion.Inverse(Bindings[5].RotationOffset);
            }
            SetArmBone(left ? 3 : 4, origin, upperArm);
            SetArmBone(left ? 5 : 6, elbow, forearm);
            SetArmBone(left ? 7 : 8, target, Quaternion.identity);
        }
        private void SetArmBone(int index, Vector3 position, Quaternion rotation)
        {
            var b = Bindings[index];
            b.Bone.SetPositionAndRotation(transform.TransformPoint(position), transform.rotation * rotation * b.RotationOffset);
        }
        public void SetFirstPerson(bool value, bool isolatedArms = false)
        {
            FirstPerson = value;
            foreach (var r in Bodies) r.enabled = !value;
            for (int i = 0; i < Arms.Length; i++)
            {
                Arms[i].sharedMesh = value && isolatedArms ? ViewArmMeshes[i] : FullArmMeshes[i];
                Arms[i].enabled = true;
            }
        }
    }
}
