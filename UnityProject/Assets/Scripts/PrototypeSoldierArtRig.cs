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
        private Material viewArmMaterial;
        private void OnDestroy(){if(viewArmMaterial!=null)Destroy(viewArmMaterial);}
        [NonSerialized] public float SupportHandWeight;
        public bool FirstPerson { get; private set; }

        public static PrototypeSoldierArtRig Create(Transform parent, PrototypeTeam team)
        {
            var prefab = Resources.Load<GameObject>("Soldiers/" + team + "Soldier");
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
            for (int i = 0; i < Fingers.Length; i++)
                Fingers[i].localRotation = FingerRest[i] * Quaternion.Euler(i == 0 ? Mathf.Lerp(55f, 12f, SupportHandWeight) : 55f, 0f, 0f);
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
        public static PrototypeSoldierArtRig CreateWeaponViewArms(Transform parent, PrototypeTeam team)
        {
            var art=Create(parent,team); if(art==null)return null;
            art.name="Authored first person arms";art.SetFirstPerson(true,true);
            var camera=parent.GetComponentInParent<Camera>();
            art.viewCamera=camera!=null?camera.transform:null;
            // Camera arms can be viewed from inside their shoulder seam. Render
            // the existing inner surface instead of culling it into a hole.
            art.viewArmMaterial=new Material(art.Arms[0].sharedMaterial){name="First-person sleeve surface"};
            art.viewArmMaterial.SetFloat("_Cull",0f);
            foreach(var arm in art.Arms)arm.sharedMaterial=art.viewArmMaterial;
            // Authored weapons are normalized by 1/.65 during import. Match
            // that scale, instead of doubling only the hands relative to guns.
            art.transform.localScale=Vector3.one/.65f;art.GetComponent<LODGroup>().ForceLOD(0);
            return art;
        }
        public void PoseWeaponViewArms(Vector3 right,Vector3 left,Quaternion rightRotation,Quaternion leftRotation)
        {
            PoseViewArm(true,ViewShoulder(true),transform.InverseTransformPoint(left),Quaternion.Inverse(transform.rotation)*leftRotation);
            PoseViewArm(false,ViewShoulder(false),transform.InverseTransformPoint(right),Quaternion.Inverse(transform.rotation)*rightRotation);
            SetArmBone(7,transform.InverseTransformPoint(left),Quaternion.Inverse(transform.rotation)*leftRotation);
            SetArmBone(8,transform.InverseTransformPoint(right),Quaternion.Inverse(transform.rotation)*rightRotation);
            CurlFingers();
        }
        private Vector3 ViewShoulder(bool left)
        {
            // Shoulders belong to the camera/chest frame, not the gun. Keeping
            // them on the weapon pushes both cuffs toward the sights in ADS.
            if(viewCamera!=null)return transform.InverseTransformPoint(viewCamera.TransformPoint(new Vector3(left?-.22f:.22f,-.48f,-.02f)));
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
            Vector3 pole = Vector3.ProjectOnPlane(new Vector3(left ? -1 : 1, -.7f, -.3f), direction).normalized;
            float along = (upper * upper + distance * distance - lower * lower) / (2 * distance);
            Vector3 elbow = origin + direction * along + pole * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            Quaternion upperArm = Quaternion.FromToRotation(Vector3.down, elbow - origin);
            Quaternion forearm = Quaternion.FromToRotation(Vector3.down, target - elbow);
            if (gripRotation.HasValue)
            {
                Quaternion hand = gripRotation.Value * Quaternion.Euler(0,left?-90f:90f,0);
                Vector3 forward = Vector3.ProjectOnPlane(hand * Vector3.forward, target-elbow);
                if (forward.sqrMagnitude > .0001f)
                    forearm = Quaternion.LookRotation(forward.normalized, (elbow-target).normalized);
                Vector3 upperForward = Vector3.ProjectOnPlane(hand * Vector3.forward, elbow-origin);
                if (upperForward.sqrMagnitude > .0001f)
                    upperArm = Quaternion.LookRotation(upperForward.normalized, (origin-elbow).normalized);
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
