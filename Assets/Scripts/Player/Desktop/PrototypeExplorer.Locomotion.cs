using UnityEngine;

namespace CampusEmergency.Prototype
{
    public sealed partial class PrototypeExplorer
    {
        public void MoveWithInput(Vector2 input, bool sprint, bool jump, float deltaTime)
        {
            input = Vector2.ClampMagnitude(input, 1);
            Vector3 desired = (transform.right * input.x + transform.forward * input.y)
                * (sprint ? sprintSpeed : walkSpeed);
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desired,
                (input.sqrMagnitude > 0 ? acceleration : braking) * deltaTime);
            if (jump) RequestJump();
            MoveCharacter(horizontalVelocity, horizontalVelocity.magnitude, deltaTime);
        }

        public void RequestJump() => jumpBuffer = .12f;

        public void MoveCharacter(Vector3 direction, float speed, float deltaTime)
        {
            if (!controller.enabled || deltaTime <= 0) return;
            Vector3 before = transform.position;
            groundedGrace = controller.isGrounded && verticalSpeed <= 0 ? .1f : Mathf.Max(0, groundedGrace - deltaTime);
            if (jumpBuffer > 0 && groundedGrace > 0)
            {
                verticalSpeed = Mathf.Sqrt(2 * gravity * jumpHeight);
                jumpBuffer = groundedGrace = 0;
            }
            else if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
            jumpBuffer = Mathf.Max(0, jumpBuffer - deltaTime);
            verticalSpeed -= gravity * deltaTime;
            CollisionFlags contacts = controller.Move((direction.normalized * speed + Vector3.up * verticalSpeed) * deltaTime);
            if ((contacts & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
            if ((contacts & CollisionFlags.Below) != 0 && verticalSpeed < 0) verticalSpeed = -2f;
            Vector3 localVelocity = transform.InverseTransformDirection((transform.position - before) / deltaTime);
            if (avatar != null) avatar.Animate(localVelocity, controller.isGrounded, deltaTime, HeldTowel != null);
            if (firstPersonArms != null) firstPersonArms.Animate(localVelocity, controller.isGrounded,
                HeldTowel != null, deltaTime);
        }
    }
}
