using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Marker and factory for the six imported visual-only equipment prefabs.
    // Gameplay colliders, cooldowns, charges and projectile ownership stay outside.
    public sealed class PrototypeEquipmentArt : MonoBehaviour
    {
        public string AssetKey;
        public Bounds LocalBounds;
        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        public static string ForRole(PrototypeInfantryClass role)
        {
            switch (role)
            {
                case PrototypeInfantryClass.Medic: return "MedicalKit";
                case PrototypeInfantryClass.Recon: return "Beacon";
                case PrototypeInfantryClass.Engineer: return "RepairTool";
                default: return "SupplyBag";
            }
        }

        public static GameObject Create(Transform parent, string key)
        {
            if (!Prefabs.TryGetValue(key, out GameObject prefab) || prefab == null)
            {
                prefab = Resources.Load<GameObject>("Equipment/" + key);
                if (prefab == null) return null;
                Prefabs[key] = prefab;
            }
            GameObject instance = Instantiate(prefab, parent, false);
            instance.name = key + " optimized model";
            return instance;
        }

        public static void ForceNearLod(GameObject model)
        {
            foreach (LODGroup group in model.GetComponentsInChildren<LODGroup>()) group.ForceLOD(0);
        }

        public static bool BuildClassGear(Transform parent, PrototypeInfantryClass role, PrototypeRuntime game)
        {
            GameObject model = Create(parent, ForRole(role));
            if (model == null) return false;
            switch (role)
            {
                case PrototypeInfantryClass.Medic:
                    model.transform.localPosition = new Vector3(-.26f, .78f, .02f);
                    model.transform.localRotation = Quaternion.Euler(0f, -65f, 0f);
                    model.transform.localScale = Vector3.one * .85f;
                    break;
                case PrototypeInfantryClass.Engineer:
                    model.transform.localPosition = new Vector3(.22f, .97f, .01f);
                    model.transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
                    model.transform.localScale = Vector3.one * .8f;
                    GameObject launcher = PrototypeEquipmentVisual.BuildLauncher(parent, game);
                    launcher.name = "Stowed launcher";
                    launcher.transform.localPosition = new Vector3(-.37f, 1.16f, -.42f);
                    launcher.transform.localRotation = Quaternion.Euler(-20f, 0f, -18f);
                    launcher.transform.localScale = Vector3.one * .55f;
                    break;
                case PrototypeInfantryClass.Recon:
                    model.transform.localPosition = new Vector3(0f, 1.02f, -.24f);
                    model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    model.transform.localScale = Vector3.one * .85f;
                    break;
                default:
                    model.transform.localPosition = new Vector3(0f, .99f, -.25f);
                    model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    model.transform.localScale = Vector3.one * .82f;
                    break;
            }
            return true;
        }
    }
}
