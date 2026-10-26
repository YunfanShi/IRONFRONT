using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public sealed partial class PrototypePlayer
    {
        public PrototypeSlideState Slide { get; } = new PrototypeSlideState();
        private bool slideKeyHeld;
        private bool slideInputPending;

        // Latch a press until the next 20 Hz network input so a short Alt tap
        // cannot disappear between packets. The host still validates speed.
        public bool ConsumeSlideInput()
        {
            bool pending = slideInputPending; slideInputPending = false; return pending;
        }

        private Vector3 ResolveInfantryMotion(Vector2 input, bool shift, bool fire, bool aim,
            bool crouchPress, bool pronePress, bool altHeld, float dt)
        {
            bool altPress = altHeld && !slideKeyHeld; slideKeyHeld = altHeld;
            if (altPress) slideInputPending = true;
            if (Slide.EndsAt > 0f && !Slide.Active)
            {
                crouched = !Slide.StandExit || !CanStand();
                Slide.Finish();
            }
            if (Slide.Active)
            {
                if (aim || crouchPress || pronePress || !controller.isGrounded) Slide.Recover();
                if (Slide.Recovering && !Slide.ForcedExit)
                    Slide.StandExit = shift && input.y > .1f && !fire && !aim && CanStand();
            }
            else if (pronePress)
            {
                if (prone) { prone = false; crouched = true; }
                else { prone = true; crouched = false; }
            }
            else if (crouchPress)
            {
                if (prone) { prone = false; crouched = true; }
                else if (crouched) { if (CanStand()) crouched = false; }
                else crouched = true;
            }
            bool wasSprinting = sprinting;
            bool wantsSprint = ResolveSprintInput(input, shift, fire, aim);
            Vector3 velocity = controller.velocity; velocity.y = 0f;
            if (altPress && !runtime.IsNetworkReplica && !Reloading && !prone && !crouched &&
                Slide.TryStart(velocity.magnitude, velocity, controller.isGrounded,
                    wantsSprint && wasSprinting)) crouched = true;
            float speed = ResolveMovementSpeed(wantsSprint, aim);
            if (!Slide.Active) return (transform.right * input.x + transform.forward * input.y) * speed;
            // Mouse look is independent of travel; A/D gives only a small bend.
            if (!Slide.Recovering && Mathf.Abs(input.x) > .05f)
                Slide.Direction = Quaternion.Euler(0f, input.x * 25f * dt, 0f) * Slide.Direction;
            return Slide.Direction * Slide.Speed;
        }

        private void ResolveSlideCollision(CollisionFlags flags, Vector3 before, Vector3 requested, float dt)
        {
            if (!Slide.Active || Slide.Recovering || dt <= 0f) return;
            Vector3 moved = transform.position - before; moved.y = 0f;
            if ((flags & CollisionFlags.Sides) != 0 && moved.magnitude < requested.magnitude * dt * .65f)
                Slide.Recover(true);
        }

        public void ApplyNetworkSlide(PrototypeLanActor actor)
        {
            if (runtime == null || !runtime.IsNetworkReplica) return;
            Slide.ReadSnapshot(actor);
            crouched = actor.crouched; prone = actor.prone;
        }
    }
}
