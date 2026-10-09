using UnityEngine;

namespace Ironfront.UnityPrototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayer : MonoBehaviour
    {
        private const float WalkSpeed = 8.6f;
        private const float SprintSpeed = 13.7f;
        private CharacterController controller;
        private Camera viewCamera;
        private AudioSource audioSource;
        private AudioClip shotSound;
        private PrototypeRuntime runtime;
        private float pitch;
        private float verticalSpeed;
        private float respawnAt;
        private PrototypeInfantryKit kit = new PrototypeInfantryKit(PrototypeInfantryClass.Assault);
        private float supportStatusUntil;

        public bool Alive => Health > 0f;
        public float Health { get; private set; } = 100f;
        public int Ammo => kit.Ammo;
        public int Reserve => kit.Reserve;
        public bool Reloading => kit.Reloading;
        public PrototypeInfantryClass Class => kit.Role;
        public PrototypeInfantryKit Kit => kit;
        public string SupportStatus { get; private set; } = "";
        public Vector2 MapPosition => new Vector2(transform.position.x, transform.position.z);
        public Camera ViewCamera => viewCamera;

        public void Initialize(PrototypeRuntime game)
        {
            runtime = game;
            controller = GetComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.34f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 48f;
            gameObject.layer = 2; // Ignore Raycast: the muzzle cannot hit the player collider.

            var cameraObject = new GameObject("First Person Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            viewCamera = cameraObject.AddComponent<Camera>();
            viewCamera.fieldOfView = 76f;
            viewCamera.farClipPlane = 950f;
            viewCamera.backgroundColor = new Color(0.55f, 0.69f, 0.76f);
            cameraObject.AddComponent<AudioListener>();
            audioSource = gameObject.AddComponent<AudioSource>();
            shotSound = Resources.Load<AudioClip>("Audio/carbine");

            GameObject rifle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rifle.name = "IF-27 Carbine Placeholder";
            rifle.transform.SetParent(cameraObject.transform, false);
            rifle.transform.localPosition = new Vector3(0.32f, -0.26f, 0.62f);
            rifle.transform.localScale = new Vector3(0.13f, 0.16f, 0.65f);
            Destroy(rifle.GetComponent<Collider>());
            rifle.GetComponent<Renderer>().sharedMaterial = runtime.GunMaterial;
            Respawn();
        }

        public void SelectClass(PrototypeInfantryClass selectedClass)
        {
            kit = new PrototypeInfantryKit(selectedClass);
            kit.ResetForSpawn();
            Health = 100f;
        }

        private void Update()
        {
            if (runtime == null) return;
            if (!runtime.MatchStarted) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                return;
            }
            if (!Alive)
            {
                if (Time.time >= respawnAt && !runtime.Match.Winner.HasValue) Respawn();
                return;
            }
            if (runtime.Match.Winner.HasValue) return;
            kit.Tick(Time.time);
            if (Time.time > supportStatusUntil) SupportStatus = "";

            transform.Rotate(0f, Input.GetAxis("Mouse X") * 2.2f, 0f);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2.2f, -86f, 86f);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);
            bool sprint = Input.GetKey(KeyCode.LeftShift) && input.y > 0.1f;
            float speed = sprint ? SprintSpeed : WalkSpeed;
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space)) verticalSpeed = 6.2f;
            verticalSpeed -= 22f * Time.deltaTime;
            controller.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.R)) kit.StartReload(Time.time);
            if (Input.GetKeyDown(KeyCode.X)) UseSupport();
            if (kit.Weapon.Automatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0)) Fire();
        }

        private void Fire()
        {
            if (kit.Ammo == 0) { kit.StartReload(Time.time); return; }
            if (!kit.TryFire(Time.time)) return;
            if (shotSound != null) audioSource.PlayOneShot(shotSound, 0.3f);
            Vector3 origin = viewCamera.transform.position;
            Vector3 direction = (viewCamera.transform.forward +
                viewCamera.transform.right * Random.Range(-kit.Weapon.HipSpread, kit.Weapon.HipSpread) +
                viewCamera.transform.up * Random.Range(-kit.Weapon.HipSpread, kit.Weapon.HipSpread)).normalized;
            float range = kit.Weapon.MaxRange;
            Vector3 end = origin + direction * range;
            // Bots move by Transform in Update; synchronize their colliders before this raycast.
            Physics.SyncTransforms();
            if (Physics.Raycast(origin, direction, out RaycastHit hit, range, ~(1 << 2)))
            {
                end = hit.point;
                PrototypeBot bot = hit.collider.GetComponent<PrototypeBot>();
                if (bot != null && bot.Team == PrototypeTeam.Red)
                    bot.TakeDamage(kit.Weapon.DamageAtRange(hit.distance));
            }
            runtime.ShowTracer(origin + viewCamera.transform.right * 0.25f - viewCamera.transform.up * 0.15f,
                end, PrototypeTeam.Blue);
            if (kit.Ammo == 0) kit.StartReload(Time.time);
        }

        private void UseSupport()
        {
            float now = Time.time;
            bool used = false;
            if (kit.Role == PrototypeInfantryClass.Assault)
            {
                used = kit.TryResupply(now, kit, 0f);
                if (!used)
                    foreach (PrototypeBot bot in runtime.Bots)
                    {
                        if (!bot.Alive || bot.Team != PrototypeTeam.Blue) continue;
                        if (kit.TryResupply(now, bot.Kit,
                            Vector2.Distance(MapPosition, bot.MapPosition))) { used = true; break; }
                    }
            }
            else if (kit.Role == PrototypeInfantryClass.Medic)
            {
                if (kit.TryHeal(now, Health, 0f, out float healed))
                {
                    Health = healed;
                    used = true;
                }
                else
                    foreach (PrototypeBot bot in runtime.Bots)
                    {
                        if (!bot.Alive || bot.Team != PrototypeTeam.Blue) continue;
                        if (kit.TryHeal(now, bot.Health,
                            Vector2.Distance(MapPosition, bot.MapPosition), out healed))
                        {
                            bot.HealTo(healed);
                            used = true;
                            break;
                        }
                    }
            }
            else if (kit.Role == PrototypeInfantryClass.Engineer)
            {
                used = kit.TryApplyArmor(now, kit, 0f);
                if (!used)
                    foreach (PrototypeBot bot in runtime.Bots)
                    {
                        if (!bot.Alive || bot.Team != PrototypeTeam.Blue) continue;
                        if (kit.TryApplyArmor(now, bot.Kit,
                            Vector2.Distance(MapPosition, bot.MapPosition))) { used = true; break; }
                    }
            }
            else if (kit.Role == PrototypeInfantryClass.Recon)
            {
                PrototypeBot spotted = null;
                float nearest = kit.DetectionRange;
                foreach (PrototypeBot bot in runtime.Bots)
                {
                    if (!bot.Alive || bot.Team != PrototypeTeam.Red) continue;
                    Vector2 target = bot.MapPosition;
                    float distance = Vector2.Distance(MapPosition, target);
                    if (distance >= nearest ||
                        PrototypeLayout.LineBlocked(MapPosition, target, 0.1f)) continue;
                    Vector3 toTarget = (bot.transform.position - viewCamera.transform.position).normalized;
                    if (Vector3.Dot(viewCamera.transform.forward, toTarget) < 0.65f) continue;
                    spotted = bot;
                    nearest = distance;
                }
                if (spotted != null && kit.TrySpot(now))
                {
                    runtime.BlueCommander.ReportEnemy(spotted.MapPosition, now);
                    used = true;
                }
            }
            SupportStatus = used ? "SUPPORT ACTIVE" : "NO VALID SUPPORT TARGET";
            supportStatusUntil = now + 2f;
        }

        public void TakeDamage(float amount)
        {
            if (!Alive || runtime.Match.Winner.HasValue) return;
            Health = Mathf.Max(0f, Health - kit.AbsorbDamage(amount));
            if (Alive) return;
            runtime.Match.RecordDeath(PrototypeTeam.Blue);
            respawnAt = Time.time + 5f;
        }

        public void HealTo(float health)
        {
            if (Alive) Health = Mathf.Clamp(health, Health, 100f);
        }

        private void Respawn()
        {
            if (controller != null) controller.enabled = false;
            Vector2 point = new Vector2(-64f, -108f);
            transform.position = new Vector3(point.x,
                PrototypeLayout.HeightAt(point.x, point.y) + 0.3f, point.y);
            transform.rotation = Quaternion.Euler(0f, 48f, 0f);
            pitch = 0f;
            verticalSpeed = 0f;
            Health = 100f;
            kit.ResetForSpawn();
            if (controller != null) controller.enabled = true;
        }
    }
}
