using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ironfront.UnityPrototype.Editor
{
    // The generated prefabs are normal Unity assets: every mesh, material, camera and light
    // can be edited in the Inspector after generation. Run this only when rebuilding art.
    public static class PrototypeMenuAssetBuilder
    {
        private const string Root = "Assets/Resources/Menu";
        private const int MenuLayer = 30;

        [MenuItem("IRONFRONT/Generate Editable Menu Assets")]
        public static void Generate()
        {
            EnsureFolder("Assets/Resources", "Menu");
            EnsureFolder(Root, "Materials");
            GeneratePanelSettings();
            GenerateHangar();
            GenerateOperator();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("IRONFRONT editable menu assets generated in " + Root);
        }

        private static void GeneratePanelSettings()
        {
            string path = Root + "/StartMenuPanel.asset";
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, path);
            }
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(960, 600);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = .5f;
            EditorUtility.SetDirty(panel);
        }

        private static void GenerateHangar()
        {
            Material wall = Material("Hangar Wall", new Color(.040f, .052f, .050f));
            Material panel = Material("Hangar Panel", new Color(.054f, .067f, .062f));
            Material inset = Material("Hangar Inset", new Color(.030f, .043f, .043f));
            Material beam = Material("Hangar Beam", new Color(.020f, .027f, .030f));
            Material floor = Material("Hangar Floor", new Color(.046f, .058f, .050f));
            Material line = Material("Hangar Line", new Color(.12f, .20f, .18f));
            Material crate = Material("Hangar Crate", new Color(.079f, .093f, .065f));
            Material lid = Material("Hangar Crate Lid", new Color(.050f, .060f, .048f));
            Material signal = Material("Hangar Signal", new Color(.36f, .70f, .73f));

            var stage = new GameObject("Editable Menu Hangar");
            stage.layer = MenuLayer;
            Transform root = stage.transform;
            Box("Concrete floor", root, new Vector3(0f, -.19f, -.3f),
                new Vector3(15f, .25f, 13f), floor);
            Box("Rear wall", root, new Vector3(0f, 2.3f, -4f),
                new Vector3(15f, 5f, .20f), wall);
            for (int i = -3; i <= 3; i++)
            {
                float x = i * 2.1f;
                Box("Wall panel " + (i + 4), root, new Vector3(x, 2.3f, -3.86f),
                    new Vector3(1.93f, 4.55f, .06f), i % 2 == 0 ? panel : inset);
                Box("Wall beam " + (i + 4), root, new Vector3(x + 1.02f, 2.3f, -3.7f),
                    new Vector3(.20f, 5.1f, .36f), beam);
            }
            Box("Upper gantry", root, new Vector3(0f, 4.1f, -3.5f),
                new Vector3(15f, .20f, .45f), beam);
            Box("Lower wall seam", root, new Vector3(0f, .43f, -3.72f),
                new Vector3(15f, .07f, .08f), beam);
            for (int i = -3; i <= 3; i++)
            {
                Box("Lane dash " + (i + 4), root, new Vector3(i * 1.43f, -.048f, 2.25f),
                    new Vector3(.75f, .012f, .035f), line);
                Box("Rear floor mark " + (i + 4), root,
                    new Vector3(i * 1.43f, -.048f, -.58f),
                    new Vector3(.60f, .012f, .025f), line);
            }
            Box("Left storage crate", root, new Vector3(-3.72f, .44f, .70f),
                new Vector3(1.10f, .90f, .93f), crate);
            Box("Left storage lid", root, new Vector3(-3.72f, .93f, .70f),
                new Vector3(1.19f, .12f, 1.02f), lid);
            Box("Right storage crate", root, new Vector3(3.65f, .56f, 1.20f),
                new Vector3(1.27f, 1.12f, 1.13f), crate);
            Box("Right storage lid", root, new Vector3(3.65f, 1.15f, 1.20f),
                new Vector3(1.37f, .13f, 1.23f), lid);
            Box("Wall status strip", root, new Vector3(3.30f, 3.66f, -3.67f),
                new Vector3(.80f, .035f, .02f), signal);

            var cameraObject = new GameObject("Menu Camera");
            cameraObject.transform.SetParent(root, false);
            cameraObject.transform.localPosition = new Vector3(0f, 2.75f, 8.2f);
            cameraObject.transform.LookAt(root.position + new Vector3(0f, 1.1f, 0f));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.045f, .065f, .065f);
            camera.orthographic = true;
            camera.orthographicSize = 3.1f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 35f;
            camera.cullingMask = 1 << MenuLayer;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            var keyObject = new GameObject("Warm key light");
            keyObject.transform.SetParent(root, false);
            keyObject.transform.localRotation = Quaternion.Euler(46f, -32f, 0f);
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, .88f, .76f);
            key.intensity = .83f;
            key.cullingMask = 1 << MenuLayer;
            key.shadows = LightShadows.None;

            var fillObject = new GameObject("Cool fill light");
            fillObject.transform.SetParent(root, false);
            fillObject.transform.localRotation = Quaternion.Euler(12f, 140f, 0f);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(.58f, .81f, 1f);
            fill.intensity = .28f;
            fill.cullingMask = 1 << MenuLayer;
            fill.shadows = LightShadows.None;
            SavePrefab(stage, Root + "/MenuHangar.prefab");
        }

        private static void GenerateOperator()
        {
            Material cloth = Material("Operator Cloth", new Color(.32f, .36f, .23f));
            Material olive = Material("Operator Olive", new Color(.20f, .25f, .18f));
            Material armor = Material("Operator Armor", new Color(.16f, .21f, .16f));
            Material trim = Material("Operator Trim", new Color(.45f, .45f, .29f));
            Material skin = Material("Operator Skin", new Color(.60f, .42f, .27f));
            Material shadow = Material("Operator Shadow", new Color(.06f, .10f, .09f));
            Material badge = Material("Operator Badge", new Color(.31f, .64f, .68f));
            Material("Medic Badge", new Color(.66f, .34f, .30f));
            Material("Engineer Badge", new Color(.72f, .62f, .32f));
            Material("Recon Badge", new Color(.35f, .53f, .59f));

            var actor = new GameObject("Editable Menu Operator");
            actor.layer = MenuLayer;
            Transform root = actor.transform;
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
            Box("Class arm patch", root, new Vector3(-.62f, 1.49f, .15f),
                new Vector3(.035f, .14f, .16f), badge);
            var mount = new GameObject("Held class weapon");
            mount.transform.SetParent(root, false);
            mount.transform.localPosition = new Vector3(.05f, 1.15f, .47f);
            mount.transform.localScale = Vector3.one * .70f;
            SavePrefab(actor, Root + "/MenuOperator.prefab");
        }

        private static void Box(string label, Transform parent, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = label;
            part.layer = MenuLayer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static Material Material(string label, Color color)
        {
            string path = Root + "/Materials/" + label.Replace(' ', '_') + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Material template = Resources.Load<Material>("Materials/PrototypeLit");
                material = template != null ? new Material(template) :
                    new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SavePrefab(GameObject instance, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
