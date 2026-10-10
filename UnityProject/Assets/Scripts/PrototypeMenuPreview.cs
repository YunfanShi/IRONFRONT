using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // A full-screen hangar render, isolated from gameplay and networking.
    public sealed class PrototypeMenuPreview
    {
        private const int PreviewLayer = 30;
        private readonly PrototypeRuntime game;
        private readonly GameObject stage;
        private readonly RenderTexture texture;
        private readonly Material template;
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<Material, Material> weaponMaterials =
            new Dictionary<Material, Material>();
        private readonly Material cloth;
        private readonly Material olive;
        private readonly Material armor;
        private readonly Material trim;
        private readonly Material skin;
        private readonly Material shadow;
        private readonly Material accent;
        private readonly Material medicBadge;
        private readonly Material engineerBadge;
        private readonly Material reconBadge;
        private GameObject soldier;
        private PrototypeInfantryClass? shownClass;

        public Texture Texture => texture;

        public PrototypeMenuPreview(PrototypeRuntime runtime)
        {
            game = runtime;
            stage = new GameObject("Start menu hangar");
            stage.transform.SetParent(runtime.transform, false);
            stage.transform.position = new Vector3(0f, -4000f, 0f);
            template = Resources.Load<Material>("Materials/PrototypeUnlit");
            if (template == null) template = game.BootMaterial;
            cloth = CreateMaterial(new Color(.32f, .36f, .23f));
            olive = CreateMaterial(new Color(.20f, .25f, .18f));
            armor = CreateMaterial(new Color(.16f, .21f, .16f));
            trim = CreateMaterial(new Color(.45f, .45f, .29f));
            skin = CreateMaterial(new Color(.60f, .42f, .27f));
            shadow = CreateMaterial(new Color(.06f, .10f, .09f));
            accent = CreateMaterial(new Color(.31f, .64f, .68f));
            medicBadge = CreateMaterial(new Color(.66f, .34f, .30f));
            engineerBadge = CreateMaterial(new Color(.72f, .62f, .32f));
            reconBadge = CreateMaterial(new Color(.35f, .53f, .59f));
            BuildHangar();

            texture = new RenderTexture(1280, 800, 16, RenderTextureFormat.ARGB32)
            {
                name = "IRONFRONT menu hangar",
                antiAliasing = 2
            };
            texture.Create();
            GameObject cameraObject = new GameObject("Menu hangar camera");
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 2.75f, 8.2f);
            cameraObject.transform.LookAt(stage.transform.position + new Vector3(0f, 1.1f, 0f));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.09f, .12f, .12f);
            camera.cullingMask = 1 << PreviewLayer;
            camera.orthographic = true;
            camera.orthographicSize = 3.1f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 35f;
            camera.targetTexture = texture;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            SetLayerRecursively(stage.transform);
        }

        private void BuildHangar()
        {
            Transform root = stage.transform;
            Material wall = CreateMaterial(new Color(.025f, .035f, .034f));
            Material panel = CreateMaterial(new Color(.034f, .044f, .043f));
            Material inset = CreateMaterial(new Color(.019f, .028f, .028f));
            Material beam = CreateMaterial(new Color(.012f, .019f, .021f));
            Material floor = CreateMaterial(new Color(.030f, .040f, .035f));
            Material road = CreateMaterial(new Color(.10f, .15f, .13f));
            Material crate = CreateMaterial(new Color(.050f, .065f, .045f));
            Material lid = CreateMaterial(new Color(.027f, .036f, .030f));
            Box("Concrete floor", root, new Vector3(0f, -.19f, -.3f),
                new Vector3(15f, .25f, 13f), floor);
            Box("Rear wall", root, new Vector3(0f, 2.3f, -4f),
                new Vector3(15f, 5f, .20f), wall);
            for (int i = -3; i <= 3; i++)
            {
                float x = i * 2.1f;
                Box("Hangar wall panel", root, new Vector3(x, 2.3f, -3.86f),
                    new Vector3(1.93f, 4.55f, .06f), i % 2 == 0 ? panel : inset);
                Box("Hangar frame", root, new Vector3(x + 1.02f, 2.3f, -3.7f),
                    new Vector3(.20f, 5.1f, .36f), beam);
            }
            Box("Upper gantry", root, new Vector3(0f, 4.1f, -3.5f),
                new Vector3(15f, .20f, .45f), beam);
            Box("Lower wall seam", root, new Vector3(0f, .43f, -3.72f),
                new Vector3(15f, .07f, .08f), beam);
            for (int i = -3; i <= 3; i++)
            {
                Box("Lane dash", root, new Vector3(i * 1.43f, -.048f, 2.25f),
                    new Vector3(.75f, .012f, .035f), road);
                Box("Rear floor mark", root, new Vector3(i * 1.43f, -.048f, -.58f),
                    new Vector3(.60f, .012f, .025f), road);
            }
            Box("Left storage crate", root, new Vector3(-3.72f, .44f, .70f),
                new Vector3(1.10f, .90f, .93f), crate);
            Box("Left storage lid", root, new Vector3(-3.72f, .93f, .70f),
                new Vector3(1.19f, .12f, 1.02f), lid);
            Box("Right storage crate", root, new Vector3(3.65f, .56f, 1.20f),
                new Vector3(1.27f, 1.12f, 1.13f), crate);
            Box("Right storage lid", root, new Vector3(3.65f, 1.15f, 1.20f),
                new Vector3(1.37f, .13f, 1.23f), lid);
            Box("Wall status light", root, new Vector3(3.30f, 3.66f, -3.67f),
                new Vector3(.80f, .035f, .02f), accent);
        }

        public void SetClass(PrototypeInfantryClass role)
        {
            if (shownClass == role) return;
            if (soldier != null)
            {
                soldier.SetActive(false);
                Object.Destroy(soldier);
            }
            soldier = new GameObject("Menu operator");
            soldier.transform.SetParent(stage.transform, false);
            soldier.transform.localPosition = new Vector3(-1.85f, 0f, .52f);
            soldier.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
            Transform root = soldier.transform;
            Box("Uniform torso", root, new Vector3(0f, 1.35f, 0f),
                new Vector3(.72f, .78f, .43f), cloth);
            Box("Plate carrier", root, new Vector3(0f, 1.35f, .265f),
                new Vector3(.61f, .53f, .17f), armor);
            Box("Chest plate", root, new Vector3(0f, 1.44f, .365f),
                new Vector3(.48f, .25f, .05f), olive);
            Box("Left chest strap", root, new Vector3(-.27f, 1.55f, .35f),
                new Vector3(.065f, .55f, .06f), trim);
            Box("Right chest strap", root, new Vector3(.27f, 1.55f, .35f),
                new Vector3(.065f, .55f, .06f), trim);
            Box("Belt", root, new Vector3(0f, .96f, .01f),
                new Vector3(.82f, .13f, .49f), shadow);
            Box("Backpack", root, new Vector3(0f, 1.36f, -.36f),
                new Vector3(.65f, .70f, .30f), olive);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * .48f;
                Box("Upper arm", root, new Vector3(x, 1.39f, .01f),
                    new Vector3(.25f, .57f, .29f), cloth);
                Box("Glove", root, new Vector3(x, 1.02f, .22f),
                    new Vector3(.24f, .20f, .28f), shadow);
                Box("Trouser", root, new Vector3(side * .20f, .55f, 0f),
                    new Vector3(.29f, .74f, .35f), cloth);
                Box("Knee guard", root, new Vector3(side * .20f, .45f, .20f),
                    new Vector3(.28f, .20f, .07f), olive);
                Box("Boot", root, new Vector3(side * .20f, .11f, .09f),
                    new Vector3(.33f, .23f, .48f), shadow);
            }
            Box("Neck", root, new Vector3(0f, 1.83f, 0f),
                new Vector3(.29f, .22f, .29f), skin);
            Box("Face", root, new Vector3(0f, 2.10f, .01f),
                new Vector3(.66f, .66f, .64f), skin);
            Box("Helmet", root, new Vector3(0f, 2.45f, -.02f),
                new Vector3(.76f, .19f, .75f), olive);
            Box("Helmet brow", root, new Vector3(0f, 2.35f, .34f),
                new Vector3(.75f, .13f, .13f), olive);
            Box("Left eye", root, new Vector3(-.16f, 2.16f, .341f),
                new Vector3(.085f, .075f, .018f), shadow);
            Box("Right eye", root, new Vector3(.16f, 2.16f, .341f),
                new Vector3(.085f, .075f, .018f), shadow);
            Box("Mouth", root, new Vector3(0f, 1.94f, .342f),
                new Vector3(.27f, .065f, .018f), shadow);
            Material badge = role == PrototypeInfantryClass.Medic ? medicBadge :
                role == PrototypeInfantryClass.Engineer ? engineerBadge :
                role == PrototypeInfantryClass.Recon ? reconBadge : accent;
            Box("Class arm patch", root, new Vector3(-.62f, 1.49f, .15f),
                new Vector3(.035f, .14f, .16f), badge);

            GameObject weaponMount = new GameObject("Held class weapon");
            weaponMount.transform.SetParent(root, false);
            weaponMount.transform.localPosition = new Vector3(.05f, 1.15f, .47f);
            weaponMount.transform.localScale = Vector3.one * .70f;
            PrototypeWeaponVisual.Build(weaponMount.transform, role, game);
            foreach (Renderer renderer in weaponMount.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = WeaponMaterial(renderer.sharedMaterial);
            SetLayerRecursively(root);
            shownClass = role;
        }

        public void SetVisible(bool visible)
        {
            if (stage != null && stage.activeSelf != visible) stage.SetActive(visible);
        }

        public void Dispose()
        {
            if (stage != null) Object.Destroy(stage);
            if (texture != null)
            {
                texture.Release();
                Object.Destroy(texture);
            }
            foreach (Material material in materials)
                if (material != null) Object.Destroy(material);
            materials.Clear();
            weaponMaterials.Clear();
        }

        private Material WeaponMaterial(Material source)
        {
            if (source == null) return shadow;
            if (weaponMaterials.TryGetValue(source, out Material existing)) return existing;
            Color color = source == game.GunMaterial ? new Color(.12f, .16f, .15f) :
                source == game.ArmorMaterial ? new Color(.25f, .28f, .21f) :
                source == game.GlassMaterial ? new Color(.08f, .18f, .18f) :
                new Color(.08f, .11f, .10f);
            Material result = CreateMaterial(color);
            weaponMaterials.Add(source, result);
            return result;
        }

        private Material CreateMaterial(Color color)
        {
            Material material = new Material(template);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            materials.Add(material);
            return material;
        }

        private static void Box(string label, Transform parent, Vector3 position,
            Vector3 size, Material material)
        {
            PrototypeSoldierVisual.Part(label, PrimitiveType.Cube, parent,
                position, size, material);
        }

        private static void SetLayerRecursively(Transform root)
        {
            root.gameObject.layer = PreviewLayer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i));
        }
    }
}
