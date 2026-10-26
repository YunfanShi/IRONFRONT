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
        public Vector3 MagazineTravel = new Vector3(-.035f, -.30f, -.055f);
        public float ChargingTravel = .095f, BoltTravel = .075f, EyeRelief = .45f;
        public bool CycleHandleOnShot;
        public bool UsesScope;
        public bool ScopeViewActive { get; private set; }
        Renderer[] scopedRenderers;
        bool[] scopedEnabled;
        static Texture2D scopeMask;
        Vector3 magazineHome, chargingHome, boltHome, leftHome, rightContactLocal;
        Quaternion magazineRotation;
        PrototypeSoldierArtRig viewArms;
        bool initialized;
        bool gripsCalibrated;
        Vector3 supportLocal;
        Quaternion supportLocalRotation;
        public Vector3 RightContactPosition => gripsCalibrated ? transform.TransformPoint(rightContactLocal) : RightGrip.position;
        public Vector3 SupportPosition => transform.TransformPoint(supportLocal);
        public Quaternion SupportRotation => transform.rotation * supportLocalRotation;
        public bool ReloadActive { get; private set; }
        public float VisualScale { get; private set; } = 1f;

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
            string resource = id == PrototypeWeaponId.Carbine ? "IF27" : "Batch2/" + id;
            var prefab = Resources.Load<GameObject>("Weapons/" + resource + (firstPerson ? "View" : "World"));
            if (prefab == null) return null;
            var rig = Instantiate(prefab, parent, false).GetComponent<PrototypeWeaponArtRig>();
            rig.Initialize();
            rig.ResizeWeapon(id == PrototypeWeaponId.Pistol ? 1.55f : 1.15f);
            rig.CalibrateWristTargets(id);
            if (firstPerson)
            {
                rig.viewArms = PrototypeSoldierArtRig.CreateWeaponViewArms(rig.transform, team);
                rig.SetPresentation(false, 0, 100);
            }
            return rig;
        }
        void ResizeWeapon(float scale)
        {
            // Scale the authored prop and all of its sockets together. Arms are
            // instantiated afterwards, outside this node, and keep their size.
            var parts = new Transform[transform.childCount];
            for(int i=0;i<parts.Length;i++)parts[i]=transform.GetChild(i);
            var visual=new GameObject("Weapon visual size").transform;
            visual.SetParent(transform,false);
            foreach(var part in parts)part.SetParent(visual,false);
            visual.localScale=Vector3.one*scale;VisualScale=scale;
            GetComponent<LODGroup>().RecalculateBounds();
        }
        private void CalibrateWristTargets(PrototypeWeaponId id)
        {
            if (gripsCalibrated) return;
            rightContactLocal = transform.InverseTransformPoint(RightGrip.position);
            gripsCalibrated = true;
            bool pistol = id == PrototypeWeaponId.Pistol;
            // Imported anchors describe contact on the prop. The pose solver
            // needs the wrist behind that contact, not the wrist inside it.
            RightGrip.localPosition += new Vector3(.05f, .06f, -.10f)/VisualScale;
            RightGrip.localRotation = Quaternion.Euler(35f, 180f, 0f);
            if (pistol)
            {
                LeftGrip.localPosition = RightGrip.localPosition + new Vector3(-.11f, -.02f, .02f)/VisualScale;
                LeftHandRotation = Quaternion.Euler(35f, 180f, 0f);
            }
            else
            {
                // Keep the support hand on the rear of the longer handguard,
                // within the character's reach after the visual enlargement.
                Vector3 support=LeftGrip.localPosition;support.z/=VisualScale;
                LeftGrip.localPosition = support + new Vector3(-.14f, -.04f, -.035f)/VisualScale;
                LeftHandRotation = Quaternion.AngleAxis(90f,Vector3.forward)*Quaternion.Euler(10f,180f,0f);
            }
            leftHome = LeftGrip.localPosition;
            MagazineGrip.localPosition += new Vector3(-.075f, .09f, -.015f)/VisualScale;
            ChargingGrip.localPosition += new Vector3(-.025f, -.015f, -.035f)/VisualScale;
        }
        public Vector3 AimPosition(float displayScale)
        {
            Vector3 sight = transform.InverseTransformPoint(Sight.position);
            return new Vector3(-sight.x * displayScale, -sight.y * displayScale, EyeRelief - sight.z * displayScale);
        }
        public void SetAiming(bool aiming)
        {
            bool scoped=UsesScope && viewArms!=null && aiming;
            if(scoped==ScopeViewActive)return;
            if(scoped)
            {
                scopedRenderers=GetComponentsInChildren<Renderer>();scopedEnabled=new bool[scopedRenderers.Length];
                for(int i=0;i<scopedRenderers.Length;i++){scopedEnabled[i]=scopedRenderers[i].enabled;scopedRenderers[i].enabled=false;}
            }
            else if(scopedRenderers!=null)
                for(int i=0;i<scopedRenderers.Length;i++)if(scopedRenderers[i]!=null)scopedRenderers[i].enabled=scopedEnabled[i];
            ScopeViewActive=scoped;
        }
        void OnGUI()
        {
            if(!ScopeViewActive||Event.current.type!=EventType.Repaint)return;
            // Use the actual aimed camera image and its existing weapon FOV.
            // The magnifying view replaces the reconstructed solid lens, while
            // the authored scope remains visible during hip fire and reloads.
            if(scopeMask==null)
            {
                const int size=512;var pixels=new Color32[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*256f);pixels[y*size+x]=new Color32(0,0,0,(byte)Mathf.RoundToInt(Mathf.Clamp01(d-232f)*255f));}
                scopeMask=new Texture2D(size,size,TextureFormat.RGBA32,false);scopeMask.SetPixels32(pixels);scopeMask.Apply(false,true);scopeMask.wrapMode=TextureWrapMode.Clamp;
            }
            int depth=GUI.depth;Color color=GUI.color;GUI.depth=100;GUI.color=Color.black;
            float side=Screen.height,left=(Screen.width-side)*.5f,cx=Screen.width*.5f,cy=Screen.height*.5f,r=side*.452f;
            GUI.DrawTexture(new Rect(0,0,Mathf.Max(0,left),Screen.height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(left+side,0,Mathf.Max(0,left),Screen.height),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.DrawTexture(new Rect(left,0,side,side),scopeMask);
            GUI.color=Color.black;float line=Mathf.Max(1f,Screen.height/700f);
            GUI.DrawTexture(new Rect(cx-r,cy-line*.5f,r*2,line),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx-line*.5f,cy-r,line,r*2),Texture2D.whiteTexture);
            for(int n=-3;n<=3;n++)if(n!=0)
            {float step=r*.18f*n;GUI.DrawTexture(new Rect(cx+step-line*.5f,cy-4*line,line,8*line),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(cx-4*line,cy+step-line*.5f,8*line,line),Texture2D.whiteTexture);}
            GUI.color=new Color(.8f,.05f,.02f);GUI.DrawTexture(new Rect(cx-line,cy-line,line*2,line*2),Texture2D.whiteTexture);GUI.color=color;GUI.depth=depth;
        }
        static float Ease(float t, float from, float to) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, t));
        public void SetPresentation(bool reloading, float phase, float shotAge)
        {
            Initialize(); if (!initialized) return;
            ReloadActive = reloading;
            float p = reloading ? Mathf.Clamp01(phase) : 0f;
            float outAmount = Ease(p,.18f,.40f) * (1f-Ease(p,.52f,.72f));
            Magazine.localPosition = magazineHome + MagazineTravel * outAmount;
            Magazine.localRotation = magazineRotation * Quaternion.Euler(-12f*outAmount,0,-8f*outAmount);
            float charge = Ease(p,.82f,.88f)*(1f-Ease(p,.90f,.94f));
            float shot = shotAge >= 0 && shotAge < .085f ? Mathf.Sin(shotAge/.085f*Mathf.PI) : 0;
            float cycle = CycleHandleOnShot && shotAge >= 0 && shotAge < .65f ?
                Ease(shotAge,.10f,.26f)*(1f-Ease(shotAge,.38f,.60f)) : 0f;
            ChargingHandle.localPosition = chargingHome + Vector3.back * (ChargingTravel*Mathf.Max(charge,cycle));
            Bolt.localPosition = boltHome + Vector3.back * (BoltTravel * Mathf.Max(charge,shot));
            float magReach=Ease(p,.03f,.17f)*(1f-Ease(p,.73f,.80f));
            float chargeReach=Ease(p,.78f,.82f)*(1f-Ease(p,.94f,1f));
            supportLocal=Vector3.Lerp(transform.InverseTransformPoint(LeftGrip.parent.TransformPoint(leftHome)),transform.InverseTransformPoint(MagazineGrip.position),magReach);
            supportLocal=Vector3.Lerp(supportLocal,transform.InverseTransformPoint(ChargingGrip.position),chargeReach);
            Quaternion magazineWrist=Quaternion.Inverse(transform.rotation)*MagazineGrip.rotation*Quaternion.Euler(10f,180f,0f);
            supportLocalRotation=Quaternion.Slerp(LeftHandRotation,magazineWrist,magReach);
            supportLocalRotation=Quaternion.Slerp(supportLocalRotation,Quaternion.Euler(70f,180f,15f),chargeReach);
            if(viewArms!=null) viewArms.PoseWeaponViewArms(RightGrip.position,SupportPosition,
                RightGrip.rotation,SupportRotation);
        }
    }
}
