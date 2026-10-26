using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ironfront.UnityPrototype
{
    // Per-menu thumbnails render the actual gun geometry on transparent RGBA.
    public sealed class PrototypeWeaponMenuPreview : IDisposable
    {
        private const int Layer = 29;
        private readonly PrototypeRuntime game;
        private readonly GameObject stage;
        private readonly Camera camera;
        private readonly RenderTexture target;
        private readonly Dictionary<PrototypeWeaponId, Texture2D> cache = new Dictionary<PrototypeWeaponId, Texture2D>();
        private readonly Dictionary<int, Texture2D> equipmentCache = new Dictionary<int, Texture2D>();
        private readonly Dictionary<Material, Material> remapped = new Dictionary<Material, Material>();
        private readonly List<Material> materials = new List<Material>();
        private GameObject weapon;

        public PrototypeWeaponMenuPreview(PrototypeRuntime runtime, Transform parent)
        {
            game = runtime; stage = new GameObject("Actual weapon thumbnail stage");
            stage.transform.SetParent(parent, false); stage.transform.localPosition = new Vector3(30f, 0f, 0f);
            var cameraObject = new GameObject("Weapon thumbnail camera"); cameraObject.transform.SetParent(stage.transform, false);
            camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << Layer; camera.orthographic = true;
            camera.nearClipPlane = .03f; camera.farClipPlane = 6f; camera.allowHDR = false; camera.allowMSAA = false;
            target = new RenderTexture(640, 304, 24, RenderTextureFormat.ARGB32);
            target.Create(); camera.targetTexture = target;
            var lightObject = new GameObject("Thumbnail warm key"); lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(32f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.color = new Color(1f, .84f, .63f); light.intensity = 1.45f; light.cullingMask = 1 << Layer;
        }

        public Texture2D Get(PrototypeWeaponId id)
        {
            if (!Enum.IsDefined(typeof(PrototypeWeaponId), id)) return null;
            if (cache.TryGetValue(id, out Texture2D image) && image != null) return image;
            if (weapon != null) { weapon.SetActive(false); UnityEngine.Object.Destroy(weapon); }
            weapon = PrototypeWeaponVisual.Build(stage.transform, id, game);
            foreach (LODGroup group in weapon.GetComponentsInChildren<LODGroup>()) group.ForceLOD(0);
            PrototypeMenuPreview.SetLayerRecursively(weapon.transform, Layer);
            foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>())
                if(weapon.GetComponent<PrototypeWeaponArtRig>()==null)
                    renderer.sharedMaterial = ThumbnailMaterial(renderer.sharedMaterial);
            Bounds bounds = new Bounds(weapon.transform.position, Vector3.zero);
            foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            Vector3 focus = bounds.center;
            camera.transform.position = focus + new Vector3(2.5f, .38f, -.16f);
            camera.transform.LookAt(focus);
            camera.aspect = (float)target.width / target.height;
            camera.orthographicSize = Mathf.Max(bounds.size.y * .62f, bounds.size.z / camera.aspect * .57f, .14f);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Physics.SyncTransforms(); camera.Render(); RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false)
                { name = "Actual " + id + " menu thumbnail", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0); image.Apply();
                cache.Add(id, image); return image;
            }
            finally { RenderTexture.active = previous; }
        }

        public Texture2D GetGadget(PrototypeInfantryClass role) => Equipment((int)role, role, null);
        public Texture2D GetThrowable(PrototypeThrowableId kind) => Equipment(10 + (int)kind, PrototypeInfantryClass.Assault, kind);

        private Texture2D Equipment(int key, PrototypeInfantryClass role, PrototypeThrowableId? throwable)
        {
            if (equipmentCache.TryGetValue(key, out Texture2D image)) return image;
            if (weapon != null) { weapon.SetActive(false); UnityEngine.Object.Destroy(weapon); }
            if (throwable.HasValue) weapon = PrototypeEquipmentVisual.BuildThrowable(stage.transform, throwable.Value, game);
            else
            {
                weapon = PrototypeEquipmentArt.Create(stage.transform, PrototypeEquipmentArt.ForRole(role));
                if (weapon != null)
                {
                    if (role == PrototypeInfantryClass.Engineer)
                        weapon.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                }
                else
                {
                    weapon = new GameObject("Actual class equipment thumbnail - " + role);
                    weapon.transform.SetParent(stage.transform, false);
                    PrototypeEquipmentVisual.BuildClassGear(weapon.transform, role, game);
                    Transform launcher = weapon.transform.Find("Stowed launcher");
                    if (launcher != null) launcher.gameObject.SetActive(false);
                }
            }
            PrototypeEquipmentArt.ForceNearLod(weapon);
            PrototypeMenuPreview.SetLayerRecursively(weapon.transform, Layer);
            Renderer[] renderers = weapon.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            camera.transform.position = bounds.center + new Vector3(.45f, .24f, 2.5f);
            camera.transform.LookAt(bounds.center);
            camera.aspect = (float)target.width / target.height;
            camera.orthographicSize = Mathf.Max(bounds.size.y * .65f, bounds.size.x / camera.aspect * .65f, .09f);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Physics.SyncTransforms(); camera.Render(); RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false)
                { name = "Actual equipment thumbnail " + key, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                equipmentCache.Add(key, image); return image;
            }
            finally { RenderTexture.active = previous; }
        }

        private Material ThumbnailMaterial(Material source)
        {
            if (source != null && remapped.TryGetValue(source, out Material cached)) return cached;
            Material material = new Material(Resources.Load<Material>("Materials/PrototypeLit") ?? game.GunMaterial);
            Color color = source == game.GunMaterial ? new Color(.25f, .26f, .22f) :
                source == game.ArmorMaterial ? new Color(.34f, .33f, .23f) : new Color(.10f, .12f, .10f);
            material.color = color; if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            materials.Add(material); if (source != null) remapped.Add(source, material); return material;
        }

        public void Dispose()
        {
            if (stage != null) { stage.SetActive(false); UnityEngine.Object.Destroy(stage); }
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            foreach (Texture2D image in cache.Values) if (image != null) UnityEngine.Object.Destroy(image);
            foreach (Material material in materials) if (material != null) UnityEngine.Object.Destroy(material);
            foreach (Texture2D image in equipmentCache.Values) if (image != null) UnityEngine.Object.Destroy(image);
            equipmentCache.Clear(); cache.Clear(); materials.Clear(); remapped.Clear();
        }
    }
}
