using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ironfront.UnityPrototype
{
    // A genuine game rig rendered over a separate hangar UI image.
    public sealed class PrototypeMenuPreview : IDisposable
    {
        private const int PreviewLayer = 30;
        private readonly PrototypeRuntime game;
        private readonly GameObject stage;
        private readonly Camera camera;
        private readonly RenderTexture texture;
        private readonly Texture2D backdrop;
        private readonly Texture2D lobbyBackdrop;
        private readonly RenderTexture portrait;
        private readonly Material template;
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<Material, Material> remapped = new Dictionary<Material, Material>();
        private readonly PrototypeWeaponMenuPreview thumbnails;
        private Mesh shadowMesh;
        private GameObject soldier;
        private PrototypeSoldierVisual operatorRig;
        private PrototypeInfantryClass? shownClass;
        private PrototypeWeaponId? shownWeapon;
        private bool loadoutComposition;
        private bool lobbyComposition;

        public Texture Texture => texture;
        public Texture2D BackgroundTexture => backdrop;
        public Texture2D LobbyBackgroundTexture => lobbyBackdrop != null ? lobbyBackdrop : backdrop;
        public Texture Portrait => portrait;
        public bool HasBackdrop => backdrop != null;
        public PrototypeInfantryClass? ShownClass => shownClass;
        public PrototypeWeaponId? ShownWeapon => shownWeapon;
        public GameObject Operator => soldier;
        public bool IsLoadoutComposition => loadoutComposition;
        public Vector3 OperatorViewportPoint => soldier != null ?
            camera.WorldToViewportPoint(soldier.transform.position + Vector3.up * .9f) : Vector3.zero;

        public PrototypeMenuPreview(PrototypeRuntime runtime)
        {
            game = runtime;
            backdrop = Resources.Load<Texture2D>("Menu/HangarBackdrop053");
            lobbyBackdrop = Resources.Load<Texture2D>("Menu/HangarBackdrop061");
            template = Resources.Load<Material>("Materials/PrototypeLit") ?? game.ArmorMaterial;
            stage = new GameObject("Menu foreground operator stage");
            stage.transform.SetParent(runtime.transform, false);
            stage.transform.position = new Vector3(0f, -4000f, 0f);
            texture = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGB32)
            { name = "IRONFRONT transparent operator foreground", antiAliasing = 1, filterMode = FilterMode.Bilinear };
            texture.Create();
            portrait = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32)
            { name = "IRONFRONT live operator portrait", filterMode = FilterMode.Bilinear };
            portrait.Create();
            var cameraObject = new GameObject("Menu foreground camera");
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.32f, 5f);
            cameraObject.transform.LookAt(stage.transform.position + new Vector3(0f, 1.0f, 0f));
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = HasBackdrop ? Color.clear : new Color(.055f, .065f, .055f, 1f);
            camera.cullingMask = 1 << PreviewLayer;
            camera.orthographic = true; camera.orthographicSize = 1.20f;
            camera.nearClipPlane = .1f; camera.farClipPlane = 15f;
            camera.allowHDR = false; camera.allowMSAA = false; camera.targetTexture = texture;
            camera.aspect = (float)texture.width / texture.height;
            AddLight("Warm hangar key", Quaternion.Euler(35f, 150f, 0f), new Color(1f, .74f, .42f), 1.45f);
            AddLight("Cool hangar fill", Quaternion.Euler(22f, -25f, 0f), new Color(.56f, .67f, .80f), .45f);
            if (!HasBackdrop) BuildFallbackHangar();
            thumbnails = new PrototypeWeaponMenuPreview(game, stage.transform);
            // Thumbnail meshes use a different layer and are created later.
            SetLayerRecursively(stage.transform, PreviewLayer);
        }

        public void SetClass(PrototypeInfantryClass role) => SetOperator(role, PrototypeInfantryRoles.DefaultWeapon(role));
        public void SetClass(PrototypeInfantryClass role, PrototypeWeaponId weapon) => SetOperator(role, weapon);

        public void SetOperator(PrototypeInfantryClass role, PrototypeWeaponId weapon)
        {
            if (!Enum.IsDefined(typeof(PrototypeInfantryClass), role)) role = PrototypeInfantryClass.Assault;
            if (!Enum.IsDefined(typeof(PrototypeWeaponId), weapon)) weapon = PrototypeInfantryRoles.DefaultWeapon(role);
            if (shownClass == role && shownWeapon == weapon) return;
            if (soldier != null) { soldier.SetActive(false); UnityEngine.Object.Destroy(soldier); }
            soldier = new GameObject("Menu operator - " + role + " carrying " + weapon);
            soldier.transform.SetParent(stage.transform, false);
            soldier.transform.localRotation = Quaternion.Euler(0f, -16f, 0f);
            operatorRig = soldier.AddComponent<PrototypeSoldierVisual>();
            operatorRig.Initialize(game, PrototypeTeam.Blue, role, 0f);
            if (weapon != PrototypeInfantryRoles.DefaultWeapon(role))
            {
                operatorRig.HeldWeapon.SetActive(false);
                operatorRig.SetWeapon(weapon, game);
            }
            operatorRig.RefreshPose(true); operatorRig.enabled = false;
            soldier.AddComponent<PrototypeMenuOperatorPose>().Initialize(operatorRig, weapon);
            foreach (Renderer renderer in soldier.GetComponentsInChildren<Renderer>())
                if (!(renderer is SkinnedMeshRenderer) && renderer.GetComponentInParent<PrototypeWeaponArtRig>()==null)
                    renderer.sharedMaterial = MenuMaterial(renderer.sharedMaterial);
            AddContactShadow(soldier.transform);
            SetLayerRecursively(soldier.transform, PreviewLayer);
            shownClass = role; shownWeapon = weapon;
            SetComposition(loadoutComposition, lobbyComposition);
            RenderPortrait();
        }

        public void SetComposition(bool loadout, bool lobby = false)
        {
            loadoutComposition = loadout;
            lobbyComposition = lobby;
            if (soldier == null) return;
            camera.orthographicSize = loadout ? .91f : lobby ? 1.08f : 1.20f;
            camera.transform.localPosition = new Vector3(0f, loadout ? 1.31f : lobby ? 1.15f : 1.32f, 5f);
            camera.transform.LookAt(stage.transform.position + new Vector3(0f, loadout ? 1.08f : lobby ? .83f : 1f, 0f));
            float frameWidth = camera.orthographicSize * 2f * ((float)texture.width / texture.height);
            Vector3 right = stage.transform.InverseTransformDirection(camera.transform.right);
            soldier.transform.localPosition = right * (((loadout ? .63f : lobby ? .50f : .56f) - .5f) * frameWidth) + new Vector3(0f, 0f, .15f);
            Render();
        }

        private void RenderPortrait()
        {
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float size = camera.orthographicSize, aspect = camera.aspect;
            try
            {
                Vector3 target = soldier.transform.position + Vector3.up * 1.58f;
                camera.transform.position = target + new Vector3(0f, .025f, 3f);
                camera.transform.LookAt(target);
                camera.orthographicSize = .28f; camera.aspect = 1f;
                camera.targetTexture = portrait;
                camera.Render();
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.orthographicSize = size; camera.aspect = aspect;
                camera.targetTexture = texture;
            }
        }

        public Texture2D WeaponThumbnail(PrototypeWeaponId weapon) => thumbnails.Get(weapon);
        public Texture2D GadgetThumbnail(PrototypeInfantryClass role) => thumbnails.GetGadget(role);
        public Texture2D ThrowableThumbnail(PrototypeThrowableId kind) => thumbnails.GetThrowable(kind);
        public void Render()
        {
            if (camera != null && stage.activeInHierarchy) { Physics.SyncTransforms(); camera.Render(); }
        }
        public void SetVisible(bool visible)
        {
            if (stage != null && stage.activeSelf != visible) stage.SetActive(visible);
        }

        private Material MenuMaterial(Material source)
        {
            if (source == null) return MakeMaterial(new Color(.14f, .15f, .12f));
            if (remapped.TryGetValue(source, out Material existing)) return existing;
            Color color = source == game.BlueClothMaterial ? new Color(.35f, .36f, .26f) :
                source == game.BlueHelmetMaterial || source == game.BlueMaterial ? new Color(.25f, .27f, .19f) :
                source == game.BlueTrimMaterial ? new Color(.47f, .43f, .27f) :
                source == game.ArmorMaterial ? new Color(.18f, .20f, .15f) :
                source == game.SkinMaterial ? new Color(.57f, .39f, .25f) :
                source == game.GunMaterial ? new Color(.12f, .13f, .11f) :
                source == game.LampMaterial ? new Color(.61f, .54f, .33f) : new Color(.075f, .085f, .07f);
            Material material = MakeMaterial(color);
            remapped.Add(source, material); return material;
        }

        private Material MakeMaterial(Color color, bool transparent = false)
        {
            Material material = new Material(transparent ? Resources.Load<Material>("Materials/PrototypeUnlit") ?? template : template);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f); material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = 3000;
            }
            materials.Add(material); return material;
        }

        private void AddContactShadow(Transform parent)
        {
            Shader shader = Resources.Load<Shader>("Menu/MenuContactShadow");
            if (shader == null) return;
            if (shadowMesh == null)
            {
                shadowMesh = new Mesh { name = "Menu contact shadow quad" };
                shadowMesh.vertices = new[] { new Vector3(-.62f, 0f, -.32f), new Vector3(.62f, 0f, -.32f),
                    new Vector3(.62f, 0f, .32f), new Vector3(-.62f, 0f, .32f) };
                shadowMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                shadowMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                shadowMesh.RecalculateBounds();
            }
            var shadow = new GameObject("Soft foot contact shadow");
            shadow.transform.SetParent(parent, false);
            shadow.transform.localPosition = new Vector3(0f, -.005f, .035f);
            shadow.AddComponent<MeshFilter>().sharedMesh = shadowMesh;
            Material material = new Material(shader); materials.Add(material);
            MeshRenderer renderer = shadow.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void AddLight(string name, Quaternion rotation, Color color, float intensity)
        {
            var lightObject = new GameObject(name); lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localRotation = rotation;
            Light light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.color = color; light.intensity = intensity; light.cullingMask = 1 << PreviewLayer;
            light.shadows = LightShadows.None;
        }

        private void BuildFallbackHangar()
        {
            Material wall = MakeMaterial(new Color(.065f, .075f, .06f));
            Material floor = MakeMaterial(new Color(.11f, .115f, .09f));
            PrototypeSoldierVisual.Part("Fallback hangar wall", PrimitiveType.Cube, stage.transform,
                new Vector3(0f, 2f, -2f), new Vector3(12f, 5f, .2f), wall);
            PrototypeSoldierVisual.Part("Fallback hangar floor", PrimitiveType.Cube, stage.transform,
                new Vector3(0f, -.10f, 0f), new Vector3(12f, .12f, 10f), floor);
            for (int beam = -3; beam <= 3; beam++)
                PrototypeSoldierVisual.Part("Fallback hangar frame", PrimitiveType.Cube, stage.transform,
                    new Vector3(beam * 1.8f, 2f, -1.85f), new Vector3(.08f, 5f, .10f), wall);
        }

        internal static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayerRecursively(child, layer);
        }

        public void Dispose()
        {
            thumbnails.Dispose();
            if (stage != null) { stage.SetActive(false); UnityEngine.Object.Destroy(stage); }
            if (texture != null) { texture.Release(); UnityEngine.Object.Destroy(texture); }
            if (portrait != null) { portrait.Release(); UnityEngine.Object.Destroy(portrait); }
            if (shadowMesh != null) UnityEngine.Object.Destroy(shadowMesh);
            foreach (Material material in materials) if (material != null) UnityEngine.Object.Destroy(material);
            materials.Clear(); remapped.Clear();
        }
    }
}
