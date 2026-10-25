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
        public float HipHeight = .91f, ThighLength = .38003f, ShinLength = .39208f;
        public float UpperArmLength = .25080f, ForearmLength = .31658f;
        private Transform[] drivers;
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
                Fingers[i].localRotation = FingerRest[i] * Quaternion.Euler(i==0?Mathf.Lerp(55f,12f,SupportHandWeight):55f, 0, 0);
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
            art.transform.localScale=Vector3.one*2f;art.GetComponent<LODGroup>().ForceLOD(0);
            return art;
        }
        public void PoseWeaponViewArms(Vector3 right,Vector3 left,Quaternion rightRotation,Quaternion leftRotation)
        {
            PoseViewArm(true,new Vector3(-.55f,-.80f,-.25f)*.5f,transform.InverseTransformPoint(left));
            PoseViewArm(false,new Vector3(.65f,-.85f,-.65f)*.5f,transform.InverseTransformPoint(right));
            SetArmBone(7,transform.InverseTransformPoint(left),Quaternion.Inverse(transform.rotation)*leftRotation);
            SetArmBone(8,transform.InverseTransformPoint(right),Quaternion.Inverse(transform.rotation)*rightRotation);
            CurlFingers();
        }
        private void PoseViewArm(bool left, Vector3 origin, Vector3 target)
        {
            float upper = UpperArmLength, lower = ForearmLength;
            Vector3 delta = target - origin; float distance = Mathf.Min(delta.magnitude, upper + lower - .001f);
            Vector3 direction = delta.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(new Vector3(left ? -1 : 1, -.7f, -.3f), direction).normalized;
            float along = (upper * upper + distance * distance - lower * lower) / (2 * distance);
            Vector3 elbow = origin + direction * along + pole * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            SetArmBone(left ? 3 : 4, origin, Quaternion.FromToRotation(Vector3.down, elbow - origin));
            SetArmBone(left ? 5 : 6, elbow, Quaternion.FromToRotation(Vector3.down, target - elbow));
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
