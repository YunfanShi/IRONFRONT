using UnityEngine;

namespace Ironfront.UnityPrototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayer : MonoBehaviour
    {
        private const float WalkSpeed = 8.6f;
        private const float SprintSpeed = 13.7f;
        private const float FireInterval = 0.105f;
        private const float WeaponRange = 165f;
        private CharacterController controller;
        private Camera viewCamera;
        private AudioSource audioSource;
        private AudioClip shotSound;
        private PrototypeRuntime runtime;
        private float pitch;
        private float verticalSpeed;
        private float nextShot;
        private float reloadAt;
        private float respawnAt;
        private int reserve = 150;

        public bool Alive => Health > 0f;
        public float Health { get; private set; } = 100f;
        public int Ammo { get; private set; } = 30;
        public int Reserve => reserve;
        public bool Reloading => reloadAt > 0f;
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
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (runtime == null) return;
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

            if (reloadAt > 0f && Time.time >= reloadAt)
            {
                int amount = Mathf.Min(30 - Ammo, reserve);
                Ammo += amount;
                reserve -= amount;
                reloadAt = 0f;
            }
            if (Input.GetKeyDown(KeyCode.R)) Reload();
            if (Input.GetMouseButton(0)) Fire();
        }

        private void Fire()
        {
            if (Time.time < nextShot || reloadAt > 0f) return;
            if (Ammo == 0) { Reload(); return; }
            nextShot = Time.time + FireInterval;
            Ammo--;
            if (shotSound != null) audioSource.PlayOneShot(shotSound, 0.3f);
            Vector3 origin = viewCamera.transform.position;
            Vector3 direction = (viewCamera.transform.forward +
                viewCamera.transform.right * Random.Range(-0.01f, 0.01f) +
                viewCamera.transform.up * Random.Range(-0.01f, 0.01f)).normalized;
            Vector3 end = origin + direction * WeaponRange;
            // Bots move by Transform in Update; synchronize their colliders before this raycast.
            Physics.SyncTransforms();
            if (Physics.Raycast(origin, direction, out RaycastHit hit, WeaponRange, ~(1 << 2)))
            {
                end = hit.point;
                PrototypeBot bot = hit.collider.GetComponent<PrototypeBot>();
                if (bot != null && bot.Team == PrototypeTeam.Red) bot.TakeDamage(31f);
            }
            runtime.ShowTracer(origin + viewCamera.transform.right * 0.25f - viewCamera.transform.up * 0.15f,
                end, PrototypeTeam.Blue);
            if (Ammo == 0) Reload();
        }

        private void Reload()
        {
            if (reloadAt > 0f || Ammo == 30 || reserve == 0) return;
            reloadAt = Time.time + 1.9f;
        }

        public void TakeDamage(float amount)
        {
            if (!Alive || runtime.Match.Winner.HasValue) return;
            Health = Mathf.Max(0f, Health - amount);
            if (Alive) return;
            runtime.Match.RecordDeath(PrototypeTeam.Blue);
            respawnAt = Time.time + 5f;
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
            Ammo = 30;
            reserve = 150;
            reloadAt = 0f;
            if (controller != null) controller.enabled = true;
        }
    }
}
