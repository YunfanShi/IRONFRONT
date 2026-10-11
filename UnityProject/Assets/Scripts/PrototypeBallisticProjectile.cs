using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Swept, gravity-driven projectiles. A sweep on every substep keeps fast
    // rifle rounds from passing through thin walls between rendered frames.
    public sealed class PrototypeBallisticProjectile : MonoBehaviour
    {
        private const float StepSeconds = 0.02f;
        private const float ShellBlastRadius = 4.5f;
        private readonly List<Vector3> trail = new List<Vector3>(32);
        private PrototypeRuntime runtime;
        private PrototypeTeam team;
        private Transform owner;
        private PrototypeWeaponDefinition weapon;
        private LineRenderer line;
        private Vector3 velocity;
        private float maxRange;
        private float distanceTravelled;
        private float damageScale;
        private float mountedDamage;
        private bool shell;
        private bool rocket;

        public static PrototypeBallisticProjectile LaunchBullet(PrototypeRuntime game,
            PrototypeTeam side, Transform shooter, Vector3 origin, Vector3 direction,
            PrototypeWeaponDefinition gun, float scale = 1f)
        {
            return Launch(game, side, shooter, origin, direction.normalized * gun.MuzzleVelocity,
                gun.MaxRange, gun, scale, false, false, 0f);
        }

        public static PrototypeBallisticProjectile LaunchMountedRound(PrototypeRuntime game,
            PrototypeTeam side, Transform shooter, Vector3 origin, Vector3 direction,
            float speed, float range, float damage)
        {
            return Launch(game, side, shooter, origin, direction.normalized * speed,
                range, null, 1f, false, false, damage);
        }

        public static PrototypeBallisticProjectile LaunchShell(PrototypeRuntime game,
            PrototypeTeam side, Transform shooter, Vector3 origin, Vector3 direction,
            float speed, float range)
        {
            return Launch(game, side, shooter, origin, direction.normalized * speed,
                range, null, 1f, true, false, 0f);
        }

        public static PrototypeBallisticProjectile LaunchRocket(PrototypeRuntime game,
            PrototypeTeam side, Transform shooter, Vector3 origin, Vector3 direction)
        {
            return Launch(game, side, shooter, origin, direction.normalized * 110f,
                160f, null, 1f, false, true, 0f);
        }

        private static PrototypeBallisticProjectile Launch(PrototypeRuntime game,
            PrototypeTeam side, Transform shooter, Vector3 origin, Vector3 initialVelocity,
            float range, PrototypeWeaponDefinition gun, float scale, bool isShell,
            bool isRocket, float directDamage)
        {
            var projectileObject = new GameObject(isShell ? "T90 cannon shell" :
                isRocket ? "Anti-armor rocket" : "Ballistic round");
            projectileObject.transform.position = origin;
            projectileObject.transform.rotation = Quaternion.LookRotation(initialVelocity.normalized);
            var projectile = projectileObject.AddComponent<PrototypeBallisticProjectile>();
            projectile.runtime = game;
            projectile.team = side;
            projectile.owner = shooter;
            projectile.weapon = gun;
            projectile.velocity = initialVelocity;
            projectile.maxRange = range;
            projectile.damageScale = scale;
            projectile.mountedDamage = directDamage;
            projectile.shell = isShell;
            projectile.rocket = isRocket;
            projectile.line = projectileObject.AddComponent<LineRenderer>();
            projectile.line.sharedMaterial = game.TracerMaterial(side);
            projectile.line.useWorldSpace = true;
            projectile.line.numCapVertices = 2;
            projectile.line.startWidth = isShell || isRocket ? 0.13f : 0.045f;
            projectile.line.endWidth = isShell || isRocket ? 0.055f : 0.012f;
            projectile.trail.Add(origin);
            projectile.line.positionCount = 1;
            projectile.line.SetPosition(0, origin);
            if (isShell)
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = "Shell body";
                body.transform.SetParent(projectileObject.transform, false);
                body.transform.localScale = Vector3.one * 0.22f;
                Destroy(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = game.GunMaterial;
            }
            else if (isRocket)
                PrototypeEquipmentVisual.BuildRocket(projectileObject.transform, game);
            if (gun != null) PrototypeCombatFeedback.PlayWeapon(shooter, gun.Id);
            else PrototypeCombatFeedback.Play(shooter, isShell ?
                PrototypeCombatSound.TankCannon : isRocket ?
                PrototypeCombatSound.RocketLaunch : PrototypeCombatSound.MountedGun);
            PrototypeCombatFeedback.Muzzle(game, origin, initialVelocity.normalized,
                isShell ? 1.7f : isRocket ? .9f : .33f);
            return projectile;
        }

        private void Update()
        {
            if (runtime == null || !runtime.MatchStarted || runtime.Match.Winner.HasValue)
            {
                Destroy(gameObject);
                return;
            }
            Physics.SyncTransforms();
            float remaining = Time.deltaTime;
            while (remaining > 0f && distanceTravelled < maxRange)
            {
                float dt = Mathf.Min(remaining, StepSeconds);
                remaining -= dt;
                // Exact constant-acceleration position for this step. The
                // segment is raycast, so collision does not depend on FPS.
                Vector3 start = transform.position;
                Vector3 end = start + velocity * dt + Physics.gravity * (0.5f * dt * dt);
                Vector3 segment = end - start;
                float length = segment.magnitude;
                if (length < 0.0001f) break;
                float allowed = Mathf.Min(length, maxRange - distanceTravelled);
                Vector3 direction = segment / length;
                if (TryHit(start, direction, allowed, out Vector3 impact,
                    out Collider collider))
                {
                    transform.position = impact;
                    AddTrailPoint(impact);
                    if (shell || rocket) Explode(impact);
                    else
                    {
                        ApplyBulletDamage(collider,
                            distanceTravelled + Vector3.Distance(start, impact));
                        PrototypeCombatFeedback.Impact(runtime, impact);
                    }
                    Destroy(gameObject, shell || rocket ? 0.10f : 0.025f);
                    enabled = false;
                    return;
                }
                transform.position = start + direction * allowed;
                distanceTravelled += allowed;
                velocity += Physics.gravity * dt * (rocket ? .15f : 1f);
                if (rocket) transform.rotation = Quaternion.LookRotation(velocity.normalized);
                AddTrailPoint(transform.position);
                if (allowed < length) break;
            }
            if (distanceTravelled >= maxRange)
            {
                if (rocket) Explode(transform.position);
                Destroy(gameObject);
            }
        }

        private bool TryHit(Vector3 start, Vector3 direction, float length,
            out Vector3 impact, out Collider collider)
        {
            impact = start + direction * length;
            collider = null;
            float nearest = length;
            foreach (RaycastHit hit in Physics.RaycastAll(start, direction, length,
                ~0, QueryTriggerInteraction.Ignore))
            {
                if (owner != null && hit.collider.transform.IsChildOf(owner)) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                collider = hit.collider;
                impact = hit.point;
            }
            // The local player uses Unity's Ignore Raycast layer. Test their
            // body explicitly so AI and host-controlled guests can still hit it.
            PrototypePlayer player = runtime.Player;
            if (player != null && player.Alive && player.Team != team &&
                player.CurrentVehicle == null && player.transform != owner)
            {
                Vector3 center = player.transform.position + Vector3.up * 0.95f;
                float along = Mathf.Clamp(Vector3.Dot(center - start, direction), 0f, length);
                if (along < nearest &&
                    (center - (start + direction * along)).sqrMagnitude < 0.65f * 0.65f)
                {
                    nearest = along;
                    collider = player.GetComponent<Collider>();
                    impact = start + direction * along;
                }
            }
            return collider != null;
        }

        private void ApplyBulletDamage(Collider hit, float distance)
        {
            float damage = weapon == null ? mountedDamage :
                weapon.DamageAtRange(distance) * damageScale;
            if (damage <= 0f) return;
            PrototypePlayer player = hit.GetComponentInParent<PrototypePlayer>();
            if (player != null && player.Team != team) { player.TakeDamage(damage); return; }
            PrototypeBot bot = hit.GetComponentInParent<PrototypeBot>();
            if (bot != null && bot.Team != team)
            {
                Vector2? source = owner != null ?
                    new Vector2(owner.position.x, owner.position.z) : (Vector2?)null;
                bot.TakeDamage(damage, source);
                return;
            }
            PrototypeRemotePlayer remote = hit.GetComponentInParent<PrototypeRemotePlayer>();
            if (remote != null && remote.Team != team) { remote.TakeDamage(damage); return; }
            IPrototypeVehicle vehicle = PrototypeVehicleHit.Find(hit);
            if (vehicle != null && vehicle.Team != team) vehicle.TakeDamage(damage * 0.55f);
        }

        private void Explode(Vector3 center)
        {
            float radius = ShellBlastRadius;
            float damage = rocket ? 90f : 145f;
            var damaged = new HashSet<Component>();
            foreach (Collider nearby in Physics.OverlapSphere(center, radius,
                ~0, QueryTriggerInteraction.Ignore))
            {
                // ClosestPoint supports only primitive shapes and convex meshes.
                // Terrain and non-convex mesh colliders use bounds falloff.
                bool supported = nearby is BoxCollider || nearby is SphereCollider ||
                    nearby is CapsuleCollider ||
                    nearby is MeshCollider mesh && mesh.convex;
                Vector3 closest = supported ? nearby.ClosestPoint(center) :
                    nearby.bounds.ClosestPoint(center);
                float distance = Vector3.Distance(center, closest);
                float amount = damage * Mathf.Clamp01(1f - distance / radius);
                PrototypePlayer player = nearby.GetComponentInParent<PrototypePlayer>();
                if (player != null && player.Team != team && damaged.Add(player))
                    player.TakeDamage(amount);
                PrototypeBot bot = nearby.GetComponentInParent<PrototypeBot>();
                if (bot != null && bot.Team != team && damaged.Add(bot))
                    bot.TakeDamage(amount, new Vector2(center.x, center.z));
                PrototypeRemotePlayer remote = nearby.GetComponentInParent<PrototypeRemotePlayer>();
                if (remote != null && remote.Team != team && damaged.Add(remote))
                    remote.TakeDamage(amount);
                IPrototypeVehicle vehicle = PrototypeVehicleHit.Find(nearby);
                if (vehicle is Component component && vehicle.Team != team &&
                    damaged.Add(component)) vehicle.TakeDamage(rocket ?
                        210f * Mathf.Clamp01(1f - distance / radius) : amount);
            }
            // Include the local player's body even though it is on Ignore Raycast.
            PrototypePlayer local = runtime.Player;
            if (local != null && local.Alive && local.Team != team &&
                local.CurrentVehicle == null && damaged.Add(local))
            {
                float distance = Vector3.Distance(center,
                    local.transform.position + Vector3.up);
                if (distance < radius)
                    local.TakeDamage(damage * (1f - distance / radius));
            }
            PrototypeCombatFeedback.Explosion(runtime, center, radius,
                rocket ? PrototypeCombatSound.RocketExplosion :
                    PrototypeCombatSound.TankExplosion);
        }

        private void AddTrailPoint(Vector3 point)
        {
            trail.Add(point);
            int limit = shell ? 32 : 8;
            if (trail.Count > limit) trail.RemoveAt(0);
            line.positionCount = trail.Count;
            for (int i = 0; i < trail.Count; i++) line.SetPosition(i, trail[i]);
        }
    }
}
