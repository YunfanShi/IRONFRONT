using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Swept ammunition for the added vehicles. Ammo gravity, blast and armor
    // coefficients are independent from the prototype T90's fixed shell.
    public sealed class PrototypeVehicleProjectile : MonoBehaviour
    {
        private PrototypeRuntime runtime;
        private PrototypeTeam team;
        private Transform owner;
        private object scoreActor;
        private float guidedSpeed;
        private PrototypeDamageClass damageClass;
        private IPrototypeVehicle target;
        private Vector3 velocity;
        private Vector3 launchOrigin;
        private Vector3 damageOrigin;
        private float remaining, damage, gravity, radius, armor;
        private LineRenderer trail;
        private readonly List<Vector3> points = new List<Vector3>();
        private static int activeCount;
        public bool IsGuided => target != null;
        public Collider HitCollider { get; private set; }

        public static PrototypeVehicleProjectile Launch(PrototypeRuntime game,
            PrototypeTeam side, Transform shooter, Vector3 origin, Vector3 direction,
            float speed, float range, float damage, float gravity, float radius,
            float armor, IPrototypeVehicle guidedTarget = null, PrototypeDamageClass? weaponClass = null)
        {
            if (game == null || !game.MatchStarted || game.IsNetworkReplica ||
                game.Match.Winner.HasValue || activeCount >= 96) return null;
            GameObject shell = new GameObject(guidedTarget == null ?
                "Combined-arms round" : "Vehicle guided missile");
            shell.transform.position = origin;
            shell.transform.rotation = Quaternion.LookRotation(direction.normalized);
            var projectile = shell.AddComponent<PrototypeVehicleProjectile>();
            projectile.runtime = game; projectile.team = side; projectile.owner = shooter;
            projectile.launchOrigin = origin;
            projectile.scoreActor = PrototypeScoreSystem.ActorForShooter(shooter);
            projectile.damageClass = weaponClass ?? (guidedTarget != null ?
                PrototypeDamageClass.AntiAirMissile : PrototypeDamageClass.Cannon);
            projectile.guidedSpeed = speed;
            projectile.velocity = direction.normalized * speed; projectile.remaining = range;
            projectile.damage = damage; projectile.gravity = gravity;
            projectile.radius = radius; projectile.armor = armor; projectile.target = guidedTarget;
            if (guidedTarget != null) PrototypeEquipmentVisual.BuildAntiAirMissile(shell.transform, game);
            projectile.trail = shell.AddComponent<LineRenderer>();
            projectile.trail.sharedMaterial = game.TracerMaterial(side);
            projectile.trail.startWidth = guidedTarget == null ? .10f : .16f;
            projectile.trail.endWidth = .035f; projectile.trail.numCapVertices = 2;
            projectile.points.Add(origin); projectile.trail.positionCount = 1;
            projectile.trail.SetPosition(0, origin); activeCount++;
            PrototypeCombatFeedback.Play(shooter, guidedTarget == null ?
                PrototypeCombatSound.TankCannon : PrototypeCombatSound.RocketLaunch);
            PrototypeCombatFeedback.Muzzle(game, origin, direction, .7f);
            return projectile;
        }

        // Flares sever the seeker immediately. There is no reacquisition path,
        // even after the aircraft's ten-second immunity expires.
        public static void BreakGuidanceTo(IPrototypeVehicle aircraft)
        {
            foreach (PrototypeVehicleProjectile missile in Object.FindObjectsByType<PrototypeVehicleProjectile>())
                if (ReferenceEquals(missile.target, aircraft)) missile.target = null;
        }

        private void OnDestroy() { activeCount = Mathf.Max(0, activeCount - 1); }

        private void Update()
        {
            if (runtime == null || !runtime.MatchStarted || runtime.Match.Winner.HasValue ||
                runtime.IsNetworkReplica) { Destroy(gameObject); return; }
            float elapsed = Time.deltaTime;
            while (elapsed > 0f && enabled)
            {
                float dt = Mathf.Min(.02f, elapsed); elapsed -= dt; Step(dt);
            }
        }

        private void Step(float dt)
        {
            if (target != null)
            {
                if (!target.Alive || target.VehicleTransform == null ||
                    target is PrototypeCombinedArmsVehicle countermeasure && countermeasure.CountermeasureActive)
                    target = null;
                else
                {
                    Vector3 destination = target.VehicleTransform.position + Vector3.up * 1.7f;
                    Vector3 wanted = (destination - transform.position).normalized;
                    if (Vector3.Angle(velocity, wanted) > 1.35f * Mathf.Rad2Deg) target = null;
                    else
                    {
                        velocity = Vector3.RotateTowards(velocity.normalized, wanted,
                            1.25f * dt, 0f) * guidedSpeed;
                        if (target is PrototypeCombinedArmsVehicle warned) warned.WarnIncoming();
                    }
                }
            }
            if (velocity.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(velocity.normalized);
            Vector3 start = transform.position;
            Vector3 delta = velocity * dt + Vector3.down * (gravity * .5f * dt * dt);
            float allowed = Mathf.Min(delta.magnitude, remaining);
            Vector3 direction = delta.normalized;
            Collider closest = null; float nearest = allowed;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.RaycastAll(start, direction, allowed,
                ~0, QueryTriggerInteraction.Ignore))
            {
                if (owner != null && hit.collider.transform.IsChildOf(owner)) continue;
                // Combat feedback removes primitive colliders at end-of-frame.
                // A freshly launched round must not collide with its muzzle flash.
                if (hit.collider.GetComponentInParent<PrototypePulseVisual>() != null ||
                    hit.collider.GetComponentInParent<PrototypeVehicleCountermeasure>() != null) continue;
                if (hit.distance > nearest) continue;
                nearest = hit.distance; closest = hit.collider;
            }
            transform.position = start + direction * nearest;
            if (closest != null)
            {
                HitCollider = closest;
                Explode(transform.position, closest); enabled = false;
                Destroy(gameObject, .12f); return;
            }
            remaining -= allowed; velocity += Vector3.down * (gravity * dt);
            points.Add(transform.position); if (points.Count > 16) points.RemoveAt(0);
            trail.positionCount = points.Count; trail.SetPositions(points.ToArray());
            if (remaining <= .001f)
            {
                if (radius > 0f) Explode(transform.position, null);
                enabled = false; Destroy(gameObject, .12f);
            }
        }

        private void Explode(Vector3 center, Collider direct)
        {
            damageOrigin = radius > 0f ? center : launchOrigin;
            PrototypeCombatFeedback.Explosion(runtime, center, Mathf.Max(.5f, radius),
                PrototypeCombatSound.TankExplosion);
            var damaged = new HashSet<Object>();
            if (direct != null) Apply(direct, damage, damaged, center);
            if (radius <= 0f) return;
            foreach (Collider nearby in Physics.OverlapSphere(center, radius,
                ~0, QueryTriggerInteraction.Ignore))
            {
                if (owner != null && nearby.transform.IsChildOf(owner)) continue;
                // Terrain is a non-convex mesh and has no damage receiver.
                // Do not request Physics.ClosestPoint for decorative/static geometry.
                if (PrototypeVehicleHit.Find(nearby) == null &&
                    nearby.GetComponentInParent<PrototypeAmmoSupplyPoint>() == null &&
                    nearby.GetComponentInParent<PrototypePlayer>() == null &&
                    nearby.GetComponentInParent<PrototypeBot>() == null &&
                    nearby.GetComponentInParent<PrototypeRemotePlayer>() == null) continue;
                Vector3 point = nearby.ClosestPoint(center);
                Vector3 line = point - center;
                bool blocked = false;
                foreach (RaycastHit hit in Physics.RaycastAll(center + line.normalized * .08f,
                    line.normalized, Mathf.Max(0f, line.magnitude - .1f),
                    ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider == direct || hit.collider == nearby ||
                        hit.collider.transform.IsChildOf(nearby.transform) ||
                        owner != null && hit.collider.transform.IsChildOf(owner)) continue;
                    // Only terrain/static structures screen blast; soldiers cannot shield tanks.
                    if (hit.collider.GetComponentInParent<PrototypeBot>() == null &&
                        hit.collider.GetComponentInParent<PrototypePlayer>() == null &&
                        PrototypeVehicleHit.Find(hit.collider) == null) { blocked = true; break; }
                }
                if (!blocked) Apply(nearby, damage * Mathf.Clamp01(1f - line.magnitude / radius), damaged, point);
            }
        }

        private void Apply(Collider collider, float amount, HashSet<Object> damaged, Vector3 impactPoint)
        {
            if (amount <= 0f) return;
            PrototypeAmmoSupplyPoint supply = collider.GetComponentInParent<PrototypeAmmoSupplyPoint>();
            if (supply != null)
            { if (damaged.Add(supply)) supply.TakeDamage(amount); return; }
            IPrototypeVehicle vehicle = PrototypeVehicleHit.Find(collider);
            if (vehicle != null)
            {
                if (!damaged.Add(vehicle.VehicleTransform)) return;
                PrototypeCombatFeedback.VehicleImpact(runtime, vehicle, impactPoint, damageClass);
                if (vehicle.Team == team) return;
                float hullDamage = PrototypeVehicleArmor.Resolve(vehicle, amount * armor, damageClass);
                if (hullDamage <= 0f) return;
                if (runtime.Scores != null)
                    runtime.Scores.ApplyDamage(vehicle, scoreActor, team, hullDamage, () => vehicle.TakeDamage(hullDamage));
                else vehicle.TakeDamage(hullDamage);
                return;
            }
            PrototypePlayer player = collider.GetComponentInParent<PrototypePlayer>();
            if (player != null)
            {
                if (player.Team != team && damaged.Add(player) && player.CurrentVehicle == null)
                {
                    if (runtime.Scores != null) runtime.Scores.ApplyDamage(player, scoreActor, team, amount, () => player.TakeDamageFrom(amount, damageOrigin));
                    else player.TakeDamageFrom(amount, damageOrigin);
                }
                return;
            }
            PrototypeBot bot = collider.GetComponentInParent<PrototypeBot>();
            if (bot != null)
            {
                if (bot.Team != team && !bot.IsPassenger && damaged.Add(bot))
                {
                    Vector2? location = owner == null ? (Vector2?)null : new Vector2(owner.position.x, owner.position.z);
                    if (runtime.Scores != null) runtime.Scores.ApplyDamage(bot, scoreActor, team, amount, () => bot.TakeDamage(amount, location));
                    else bot.TakeDamage(amount, location);
                }
                return;
            }
            PrototypeRemotePlayer remote = collider.GetComponentInParent<PrototypeRemotePlayer>();
            if (remote != null && remote.Team != team && damaged.Add(remote))
            {
                if (runtime.Scores != null) runtime.Scores.ApplyDamage(remote, scoreActor, team, amount, () => remote.TakeDamageFrom(amount, damageOrigin));
                else remote.TakeDamageFrom(amount, damageOrigin);
            }
        }
    }
}
