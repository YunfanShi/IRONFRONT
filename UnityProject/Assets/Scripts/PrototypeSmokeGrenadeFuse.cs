using System;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Real terrain or solid obstacle contact lights a smoke grenade's fuse.
    // Wall/vehicle contacts keep an emitting canister physical until it reaches
    // stationary ground support; moving roofs never leave a hovering model.
    public sealed class PrototypeSmokeGrenadeFuse : MonoBehaviour
    {
        public const float ContactDelay = .35f;
        public const float FlightTimeout = 14f;
        private static readonly RaycastHit[] SupportHits = new RaycastHit[12];
        private PrototypeRuntime runtime;
        private Transform owner;
        private Rigidbody body;
        private Action<GameObject> onRemoved;
        private float bornAt;
        private bool initialized;
        private bool cloudReleased;
        public bool HasContact { get; private set; }
        public bool HasEmitted { get; private set; }
        public Collider ContactCollider { get; private set; }
        public Vector3 ContactPoint { get; private set; }
        public float ContactTime { get; private set; }
        public float EmissionTime { get; private set; }
        public PrototypeSmokeCloud Cloud { get; private set; }
        public float FuseRemaining => HasContact ? Mathf.Max(0f, ContactTime + ContactDelay - Time.time) : ContactDelay;
        public bool ReadyToEmit => initialized && HasContact && !HasEmitted && Time.time >= ContactTime + ContactDelay;

        public void Initialize(PrototypeRuntime game, Transform thrower, Action<GameObject> removed = null)
        {
            if (initialized) return;
            runtime = game; owner = thrower; onRemoved = removed;
            body = GetComponent<Rigidbody>(); bornAt = Time.time; initialized = true;
        }

        private void OnCollisionEnter(Collision collision) => RegisterContact(collision);
        private void OnCollisionStay(Collision collision) => RegisterContact(collision);
        private void RegisterContact(Collision collision)
        {
            if (!initialized || HasContact || collision == null || !IsFuseContact(collision.collider)) return;
            HasContact = true; ContactTime = Time.time; ContactCollider = collision.collider;
            ContactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        }

        private bool IsFuseContact(Collider collider)
        {
            if (collider == null || !collider.enabled || collider.isTrigger ||
                collider.transform.IsChildOf(transform) || owner != null && collider.transform.IsChildOf(owner) ||
                collider.GetComponentInParent<PrototypePulseVisual>() != null ||
                collider.GetComponentInParent<PrototypeVehicleCountermeasure>() != null ||
                collider.GetComponentInParent<PrototypePlayer>() != null ||
                collider.GetComponentInParent<PrototypeBot>() != null ||
                collider.GetComponentInParent<PrototypeRemotePlayer>() != null) return false;
            return PrototypeVehicleHit.Find(collider) != null || collider.attachedRigidbody == null;
        }

        private void Update()
        {
            if (!initialized) return;
            if (runtime == null || !runtime.MatchStarted || runtime.IsNetworkReplica || runtime.Match.Winner.HasValue)
            { Destroy(gameObject); return; }
            if (!HasContact && Time.time - bornAt >= FlightTimeout)
            {
                // An out-of-bounds/high throw expires rather than producing an
                // unrelated stationary cloud suspended in empty air.
                Destroy(gameObject); return;
            }
            if (ReadyToEmit)
            {
                HasEmitted = true;
                EmissionTime = Time.time;
                Cloud = PrototypeSmokeCloud.Spawn(runtime, transform.position);
                Cloud.FollowCanister(transform);
                PrototypeCombatFeedback.PlayAt(transform.position, PrototypeCombatSound.SmokeRelease);
            }
            if (!HasEmitted) return;
            if (Cloud == null) { Destroy(gameObject); return; }
            bool grounded = Grounded();
            // Keep a roof-supported emitter physical when the hull moves out
            // from beneath it, even if the resting sphere had gone to sleep.
            if (!grounded && body != null && body.IsSleeping()) body.WakeUp();
            if (grounded && (body == null || Mathf.Abs(body.linearVelocity.y) < .35f))
            {
                Cloud.StopFollowingCanister(transform.position);
                cloudReleased = true;
                Destroy(gameObject);
            }
        }

        private bool Grounded()
        {
            Vector3 from = transform.position + Vector3.up * .05f;
            PhysicsScene physics = gameObject.scene.GetPhysicsScene();
            int count = physics.Raycast(from, Vector3.down, SupportHits,
                PrototypeThrowableTrajectory.CollisionRadius + .075f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (IsFuseContact(SupportHits[i].collider) && SupportHits[i].normal.y > .45f &&
                    PrototypeVehicleHit.Find(SupportHits[i].collider) == null) return true;
            return false;
        }

        private void OnDestroy()
        {
            // The fuse owns this cloud until it has handed off a grounded
            // emitter. Aborting a falling canister must not freeze it in air.
            if (!cloudReleased && Cloud != null) Destroy(Cloud.gameObject);
            onRemoved?.Invoke(gameObject); onRemoved = null;
        }
    }
}
