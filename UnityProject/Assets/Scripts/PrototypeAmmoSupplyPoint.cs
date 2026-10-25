using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Deliberately not a soldier or vehicle: neutral supplies block and take
    // real rounds, but never generate combat score or team-ticket losses.
    public sealed class PrototypeAmmoSupplyPoint : MonoBehaviour
    {
        public const float MaxHealth = 120f;
        private PrototypeRuntime runtime;
        private BoxCollider hull;
        public bool Alive => Health > 0f && gameObject.activeInHierarchy;
        public float Health { get; private set; } = MaxHealth;
        public bool IsDeployable { get; private set; }
        public PrototypePlayer Owner { get; private set; }
        public PrototypeTeam SourceTeam { get; private set; }
        public string DisplayName { get; private set; }
        public Vector2 MapPosition => new Vector2(transform.position.x, transform.position.z);

        public void Initialize(PrototypeRuntime game, Vector2 position, string name,
            PrototypePlayer owner = null)
        {
            runtime = game; Owner = owner; IsDeployable = owner != null;
            SourceTeam = owner != null ? owner.Team : PrototypeTeam.Blue;
            DisplayName = name; Health = MaxHealth;
            transform.position = new Vector3(position.x,
                PrototypeLayout.HeightAt(position.x, position.y) + .025f, position.y);
            transform.rotation = owner != null ? Quaternion.Euler(0f, owner.transform.eulerAngles.y, 0f) : Quaternion.identity;
            BuildModel();
            hull = gameObject.AddComponent<BoxCollider>();
            hull.center = new Vector3(0f, IsDeployable ? .36f : .5f, 0f);
            hull.size = IsDeployable ? new Vector3(1.15f, .72f, .83f) : new Vector3(1.48f, 1f, 1.10f);
        }

        private void BuildModel()
        {
            Part("Neutral ammunition case", new Vector3(0f, .34f, 0f),
                new Vector3(1.12f, .62f, .78f), runtime.ArmorMaterial);
            Part("Raised reinforced lid", new Vector3(0f, .68f, 0f),
                new Vector3(1.18f, .08f, .84f), runtime.GunMaterial);
            Part("Transport handle", new Vector3(0f, .77f, 0f),
                new Vector3(.36f, .08f, .08f), runtime.GunMaterial);
            foreach (float side in new[] { -.4f, .4f })
                Part("Case retaining strap", new Vector3(side, .37f, 0f),
                    new Vector3(.06f, .68f, .805f), runtime.GunMaterial);
            Part("Neutral yellow identification plate", new Vector3(0f, .40f, .397f),
                new Vector3(.61f, .29f, .018f), runtime.LampMaterial);
            Part("Neutral yellow rear identification plate", new Vector3(0f, .40f, -.397f),
                new Vector3(.61f, .29f, .018f), runtime.LampMaterial);
            for (int i = 0; i < 3; i++)
            {
                var round = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                round.name = "Ordinary and explosive ammo symbol";
                round.transform.SetParent(transform, false);
                round.transform.localPosition = new Vector3((i - 1) * .14f, .40f, .416f);
                round.transform.localScale = new Vector3(.034f, .09f, .02f);
                round.GetComponent<Renderer>().sharedMaterial = runtime.GunMaterial;
                RemoveCollider(round);
            }
            Label("AMMO", new Vector3(0f, .40f, -.407f), 0f, .035f);
            if (!IsDeployable)
            {
                Part("Fortified fixed supply cabinet base", new Vector3(0f, .08f, 0f),
                    new Vector3(1.48f, .16f, 1.10f), runtime.GunMaterial);
                foreach (float side in new[] { -.66f, .66f })
                    Part("Fixed cabinet protective side frame", new Vector3(side, .56f, 0f),
                        new Vector3(.12f, .96f, 1f), runtime.GunMaterial);
                Part("Fixed cabinet protective roof", new Vector3(0f, 1.06f, 0f),
                    new Vector3(1.46f, .10f, 1.04f), runtime.ArmorMaterial);
                Part("Fixed supply marker mast", new Vector3(.72f, 1.12f, 0f),
                    new Vector3(.06f, 2.2f, .06f), runtime.GunMaterial);
                Part("Fixed neutral supply sign", new Vector3(.72f, 2.12f, 0f),
                    new Vector3(.84f, .46f, .055f), runtime.LampMaterial);
                Label("AMMO", new Vector3(.72f, 2.12f, .033f), 180f, .055f);
                Label("AMMO", new Vector3(.72f, 2.12f, -.033f), 0f, .055f);
            }
        }

        private void Part(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name; part.transform.SetParent(transform, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material; RemoveCollider(part);
        }
        private static void RemoveCollider(GameObject part)
        {
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false; Object.Destroy(collider);
        }
        private void Label(string text, Vector3 position, float yaw, float size)
        {
            var label = new GameObject(text + " label"); label.transform.SetParent(transform, false);
            label.transform.localPosition = position; label.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var mesh = label.AddComponent<TextMesh>(); mesh.text = text; mesh.fontSize = 40;
            mesh.characterSize = size; mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center; mesh.color = new Color(.10f, .14f, .13f);
        }

        public void TakeDamage(float amount)
        {
            if (runtime == null || runtime.IsNetworkReplica || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue || !IsDeployable || !Alive || float.IsNaN(amount) ||
                float.IsInfinity(amount) || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            if (Health <= 0f) Retire();
        }

        public void Retire()
        {
            Health = 0f;
            if (hull != null) hull.enabled = false;
            gameObject.SetActive(false);
        }
    }
}
