using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Cosmetic weapon articulation. Ammo, timings and ballistics stay in the kit.
    public sealed class PrototypeWeaponArtRig : MonoBehaviour
    {
        public Transform RightGrip, LeftGrip, Muzzle, Sight, Magazine, ChargingHandle, Bolt;
        public Transform MagazineGrip, ChargingGrip;
        public Vector3 HipPosition = new Vector3(.25f, -.23f, .60f);
        public Quaternion LeftHandRotation = Quaternion.Euler(0f, 0f, 75f);
        Vector3 magazineHome, chargingHome, boltHome, leftHome;
        Quaternion magazineRotation;
        PrototypeSoldierArtRig viewArms;
        bool initialized;
        Vector3 supportLocal;
        Quaternion supportLocalRotation;
        public Vector3 SupportPosition => transform.TransformPoint(supportLocal);
        public Quaternion SupportRotation => transform.rotation * supportLocalRotation;
        public bool ReloadActive { get; private set; }

        void Awake() => Initialize();
        void Initialize()
        {
            if (initialized || Magazine == null) return;
            magazineHome = Magazine.localPosition; magazineRotation = Magazine.localRotation;
            chargingHome = ChargingHandle.localPosition; boltHome = Bolt.localPosition;
            leftHome = LeftGrip.localPosition; initialized = true;
            SetPresentation(false, 0f, 100f);
        }
        public static PrototypeWeaponArtRig Create(Transform parent, PrototypeWeaponId id,
            bool firstPerson, PrototypeTeam team)
        {
            if (id != PrototypeWeaponId.Carbine) return null;
            var prefab = Resources.Load<GameObject>("Weapons/IF27" + (firstPerson ? "View" : "World"));
            if (prefab == null) return null;
            var rig = Instantiate(prefab, parent, false).GetComponent<PrototypeWeaponArtRig>();
            rig.Initialize();
            if (firstPerson)
            {
                rig.viewArms = PrototypeSoldierArtRig.CreateWeaponViewArms(rig.transform, team);
                rig.SetPresentation(false, 0, 100);
            }
            return rig;
        }
        public Vector3 AimPosition(float displayScale)
        {
            Vector3 sight = transform.InverseTransformPoint(Sight.position);
            return new Vector3(-sight.x * displayScale, -sight.y * displayScale, .45f - sight.z * displayScale);
        }
        static float Ease(float t, float from, float to) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, t));
        public void SetPresentation(bool reloading, float phase, float shotAge)
        {
            Initialize(); if (!initialized) return;
            ReloadActive = reloading;
            float p = reloading ? Mathf.Clamp01(phase) : 0f;
            float outAmount = Ease(p,.18f,.40f) * (1f-Ease(p,.52f,.72f));
            Magazine.localPosition = magazineHome + new Vector3(-.035f,-.30f,-.055f) * outAmount;
            Magazine.localRotation = magazineRotation * Quaternion.Euler(-12f*outAmount,0,-8f*outAmount);
            float charge = Ease(p,.82f,.88f)*(1f-Ease(p,.90f,.94f));
            ChargingHandle.localPosition = chargingHome + Vector3.back * (.095f*charge);
            float shot = shotAge >= 0 && shotAge < .085f ? Mathf.Sin(shotAge/.085f*Mathf.PI) : 0;
            Bolt.localPosition = boltHome + Vector3.back * (.075f * Mathf.Max(charge,shot));
            float magReach=Ease(p,.03f,.17f)*(1f-Ease(p,.73f,.80f));
            float chargeReach=Ease(p,.78f,.82f)*(1f-Ease(p,.94f,1f));
            supportLocal=Vector3.Lerp(leftHome,transform.InverseTransformPoint(MagazineGrip.position),magReach);
            supportLocal=Vector3.Lerp(supportLocal,transform.InverseTransformPoint(ChargingGrip.position),chargeReach);
            supportLocalRotation=Quaternion.Slerp(LeftHandRotation,Quaternion.identity,magReach);
            supportLocalRotation=Quaternion.Slerp(supportLocalRotation,Quaternion.Euler(0,0,-10f),chargeReach);
            if(viewArms!=null) viewArms.PoseWeaponViewArms(RightGrip.position,SupportPosition,
                RightGrip.rotation,SupportRotation);
        }
    }
}
