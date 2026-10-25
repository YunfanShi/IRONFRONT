using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Keeps the authored FBX pivots in step with the prototype's gameplay pivots.
    // The art pivots can sit at their true mesh positions; muzzle, seat and collision
    // transforms stay on PrototypeCombinedVehicleVisual until separately calibrated.
    public sealed class PrototypeVehicleArtRig : MonoBehaviour
    {
        public Transform VisualRoot;
        public Transform[] Turrets;
        public Transform[] Barrels;
        public Transform[] MainRotors;
        public Transform[] TailRotors;
        public Transform[] Radars;
        public Transform[] Wheels;
        public Transform[] SteeringAxles;
        public float[] WheelRadii;
        public float WheelRadius = .62f;
        // Authored rest anchors in the prefab's Unity metre coordinates.
        public Vector3 TurretAnchor, BarrelAnchor, MuzzleAnchor;
        public Vector3 HullCenter, HullSize;
        public Vector3 PilotEye, PilotSeat, SteeringAnchor;
        public string[] ContactNames;
        public Vector3[] ContactAnchors;
        private Transform lightTurret, lightBarrel;
        private System.Func<float> lightSteering;
        private bool motorcycle;
        public Vector3 Contact(string name)
        {
            int index = System.Array.IndexOf(ContactNames, name);
            return index >= 0 ? ContactAnchors[index] : Vector3.zero;
        }
        public bool BindLight(Transform turret, Transform barrel, System.Func<float> steering)
        {
            if (VisualRoot == null || Turrets.Length != 3 || Barrels.Length != 3 || Wheels.Length != 12 || SteeringAxles.Length != 6) return false;
            lightTurret = turret; lightBarrel = barrel; lightSteering = steering;
            InitializeWheels(); Sync(); return true;
        }
        private void InitializeWheels()
        {
            previousPosition = transform.position;
            if (Wheels == null) return;
            previousWheelPositions = new Vector3[Wheels.Length]; wheelAngles = new float[Wheels.Length];
            for (int i = 0; i < Wheels.Length; i++) previousWheelPositions[i] = Wheels[i].position;
        }

        private PrototypeCombinedVehicleVisual visual;
        private PrototypeTankVisual tank;
        private Vector3 previousPosition;
        private float wheelAngle;
        private Vector3[] previousWheelPositions;
        private float[] wheelAngles;
        private Vector3 tankBarrelRest;

        public void CalibrateWeapon(Transform turret, Transform barrel, Transform muzzle)
        {
            turret.localPosition = TurretAnchor;
            barrel.localPosition = BarrelAnchor - TurretAnchor;
            muzzle.localPosition = MuzzleAnchor - BarrelAnchor;
        }

        public bool Bind(PrototypeTankVisual source)
        {
            if (source == null || VisualRoot == null || Turrets == null || Turrets.Length != 3 ||
                Barrels == null || Barrels.Length != 3 || !AllPresent(Turrets) || !AllPresent(Barrels)) return false;
            tank = source;
            tankBarrelRest = source.BarrelPivot.localPosition;
            Sync();
            return true;
        }

        public bool Bind(PrototypeCombinedVehicleVisual source, PrototypeVehicleKind kind)
        {
            if (source == null || VisualRoot == null) return false;
            if ((kind == PrototypeVehicleKind.Helicopter || kind == PrototypeVehicleKind.ReconHelicopter) &&
                (MainRotors == null || MainRotors.Length == 0 ||
                 TailRotors == null || TailRotors.Length == 0)) return false;
            if (kind == PrototypeVehicleKind.AntiAir &&
                (Turrets == null || Turrets.Length == 0 ||
                 Barrels == null || Barrels.Length == 0 ||
                 Radars == null || Radars.Length == 0)) return false;
            if (kind == PrototypeVehicleKind.Ifv &&
                (Turrets == null || Turrets.Length != 3 || Barrels == null || Barrels.Length != 3 ||
                 Wheels == null || Wheels.Length != 24 || SteeringAxles == null || SteeringAxles.Length != 12)) return false;
            if (!AllPresent(MainRotors) || !AllPresent(TailRotors) ||
                !AllPresent(Turrets) || !AllPresent(Barrels) || !AllPresent(Radars) ||
                !AllPresent(Wheels) || !AllPresent(SteeringAxles))
                return false;
            if (kind == PrototypeVehicleKind.Motorcycle && (Wheels.Length != 6 || SteeringAxles.Length != 3)) return false;
            if (kind == PrototypeVehicleKind.Jet && Wheels.Length != 9) return false;
            motorcycle = kind == PrototypeVehicleKind.Motorcycle;
            visual = source;
            previousPosition = transform.position;
            if (Wheels != null)
            {
                previousWheelPositions = new Vector3[Wheels.Length];
                wheelAngles = new float[Wheels.Length];
                for (int i = 0; i < Wheels.Length; i++) previousWheelPositions[i] = Wheels[i].position;
            }
            Sync();
            return true;
        }

        private static bool AllPresent(Transform[] transforms)
        {
            if (transforms == null) return true;
            foreach (Transform item in transforms) if (item == null) return false;
            return true;
        }

        private void LateUpdate()
        {
            Sync();
            if (Wheels == null || Wheels.Length == 0 || WheelRadius <= .01f) return;
            Vector3 position = transform.position;
            float travel = Vector3.Dot(position - previousPosition, transform.forward);
            previousPosition = position;
            if (WheelRadii != null && WheelRadii.Length == Wheels.Length && previousWheelPositions != null)
            {
                for (int i = 0; i < Wheels.Length; i++)
                {
                    Vector3 delta = Wheels[i].position - previousWheelPositions[i];
                    previousWheelPositions[i] = Wheels[i].position;
                    if (delta.sqrMagnitude > 4f || WheelRadii[i] <= .01f) continue;
                    float distance = Vector3.Dot(delta, Wheels[i].parent.forward);
                    wheelAngles[i] = Mathf.Repeat(wheelAngles[i] + distance / WheelRadii[i] * Mathf.Rad2Deg, 360f);
                    Wheels[i].localRotation = Quaternion.Euler(wheelAngles[i], 0f, 0f);
                }
                return;
            }
            // Respawn and network corrections are teleports, not wheel travel.
            if (Mathf.Abs(travel) > 2f) return;
            wheelAngle = Mathf.Repeat(wheelAngle + travel / WheelRadius * Mathf.Rad2Deg, 360f);
            Quaternion rotation = Quaternion.Euler(wheelAngle, 0f, 0f);
            foreach (Transform wheel in Wheels) wheel.localRotation = rotation;
        }

        private void Sync()
        {
            if (tank != null)
            {
                CopyRotation(Turrets, tank.Turret);
                CopyRotation(Barrels, tank.BarrelPivot);
                foreach (Transform barrel in Barrels)
                    barrel.localPosition = BarrelAnchor - TurretAnchor + tank.BarrelPivot.localPosition - tankBarrelRest;
                return;
            }
            if (lightTurret != null)
            {
                CopyRotation(Turrets, lightTurret); CopyRotation(Barrels, lightBarrel);
                foreach (var axle in SteeringAxles) axle.localRotation = Quaternion.Euler(0, lightSteering() * 25f, 0);
                return;
            }
            if (visual == null) return;
            VisualRoot.localRotation = motorcycle ? visual.MotorcycleBody.localRotation : visual.Airframe != null ?
                visual.Airframe.localRotation : Quaternion.identity;
            CopyRotation(Turrets, visual.Turret);
            CopyRotation(Barrels, visual.Barrel);
            CopyRotation(MainRotors, visual.Rotor);
            CopyRotation(TailRotors, visual.TailRotor);
            CopyRotation(Radars, visual.Radar);
            if (SteeringAxles != null)
                foreach (Transform axle in SteeringAxles)
                    axle.localRotation = motorcycle ? visual.MotorcycleSteering.localRotation : Quaternion.Euler(0f, visual.WheelSteering * 25f, 0f);
        }

        private static void CopyRotation(Transform[] destinations, Transform source)
        {
            if (destinations == null || source == null) return;
            Quaternion rotation = source.localRotation;
            foreach (Transform destination in destinations) destination.localRotation = rotation;
        }
    }
}
