using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // An isolated camera renders the actual infantry model used in battle into the menu.
    // The preview has no player, bot, or network component.
    public sealed class PrototypeMenuPreview
    {
        private const int PreviewLayer = 30;
        private readonly PrototypeRuntime game;
        private readonly GameObject stage;
        private readonly RenderTexture texture;
        private readonly Material floorMaterial;
        private readonly Material crateMaterial;
        private readonly Dictionary<Material, Material> uniformMaterials =
            new Dictionary<Material, Material>();
        private GameObject soldier;
        private PrototypeInfantryClass? shownClass;

        public Texture Texture => texture;

        public PrototypeMenuPreview(PrototypeRuntime runtime)
        {
            game = runtime;
            stage = new GameObject("Start menu model preview");
            stage.transform.SetParent(runtime.transform, false);
            stage.transform.position = new Vector3(0f, -4000f, 0f);
            Material unlit = Resources.Load<Material>("Materials/PrototypeUnlit");
            floorMaterial = MakeBackdropMaterial(unlit != null ? unlit : game.BootMaterial,
                new Color(.010f, .016f, .015f));
            crateMaterial = MakeBackdropMaterial(unlit != null ? unlit : game.ArmorMaterial,
                new Color(.040f, .052f, .035f));
            BuildBackdrop();
            texture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
            {
                name = "IRONFRONT menu operator"
            };
            texture.Create();

            GameObject cameraObject = new GameObject("Menu preview camera");
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(2.8f, 1.8f, 3.5f);
            cameraObject.transform.LookAt(stage.transform.position + new Vector3(0f, 1.05f, 0f));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.006f, .013f, .016f);
            camera.cullingMask = 1 << PreviewLayer;
            camera.fieldOfView = 31f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 20f;
            camera.targetTexture = texture;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            GameObject lightObject = new GameObject("Menu preview light");
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(35f, -32f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .84f, .69f);
            light.intensity = 1.0f;
            light.cullingMask = 1 << PreviewLayer;
        }

        private void BuildBackdrop()
        {
            Transform parent = stage.transform;
            PrototypeSoldierVisual.Part("Hangar floor", PrimitiveType.Cube, parent,
                new Vector3(0f, -.14f, -.25f), new Vector3(3.5f, .2f, 3.5f),
                floorMaterial);
            foreach (float x in new[] { -2.5f, 2.5f })
            {
                PrototypeSoldierVisual.Part("Hangar upright", PrimitiveType.Cube, parent,
                    new Vector3(x, 1.25f, -1.7f), new Vector3(.16f, 2.5f, .17f),
                    floorMaterial);
                PrototypeSoldierVisual.Part("Storage crate", PrimitiveType.Cube, parent,
                    new Vector3(x * .8f, .32f, -.8f), new Vector3(.62f, .64f, .64f),
                    crateMaterial);
            }
            SetLayerRecursively(parent);
        }

        public void SetClass(PrototypeInfantryClass role)
        {
            if (shownClass == role) return;
            if (soldier != null) Object.Destroy(soldier);
            soldier = new GameObject("Menu operator");
            soldier.transform.SetParent(stage.transform, false);
            soldier.transform.localRotation = Quaternion.Euler(0f, -12f, 0f);
            soldier.AddComponent<PrototypeSoldierVisual>().Initialize(game,
                PrototypeTeam.Blue, role, 0f);
            foreach (Renderer renderer in soldier.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = MenuUniform(renderer.sharedMaterial);
            SetLayerRecursively(soldier.transform);
            shownClass = role;
        }

        public void SetVisible(bool visible)
        {
            if (stage != null) stage.SetActive(visible);
        }

        public void Dispose()
        {
            if (stage != null) Object.Destroy(stage);
            if (texture != null)
            {
                texture.Release();
                Object.Destroy(texture);
            }
            if (floorMaterial != null) Object.Destroy(floorMaterial);
            if (crateMaterial != null) Object.Destroy(crateMaterial);
            foreach (Material material in uniformMaterials.Values)
                if (material != null) Object.Destroy(material);
            uniformMaterials.Clear();
        }

        private Material MenuUniform(Material source)
        {
            if (source == null) return null;
            if (uniformMaterials.TryGetValue(source, out Material existing))
                return existing;
            Color color;
            if (source == game.BlueClothMaterial) color = new Color(.16f, .19f, .075f);
            else if (source == game.BlueHelmetMaterial) color = new Color(.07f, .09f, .04f);
            else if (source == game.BlueTrimMaterial) color = new Color(.24f, .25f, .13f);
            else if (source == game.ArmorMaterial) color = new Color(.10f, .13f, .085f);
            else if (source == game.SkinMaterial) color = new Color(.50f, .31f, .16f);
            else if (source == game.BootMaterial) color = new Color(.05f, .06f, .045f);
            else if (source == game.GunMaterial) color = new Color(.08f, .095f, .08f);
            else if (source == game.VisorMaterial || source == game.GlassMaterial)
                color = new Color(.02f, .04f, .035f);
            else return source;
            Material material = MakeBackdropMaterial(source, color);
            uniformMaterials.Add(source, material);
            return material;
        }

        private static Material MakeBackdropMaterial(Material source, Color color)
        {
            Material material = new Material(source);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            return material;
        }

        private static void SetLayerRecursively(Transform root)
        {
            root.gameObject.layer = PreviewLayer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i));
        }
    }
}
