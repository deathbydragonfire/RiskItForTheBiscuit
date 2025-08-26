using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class KinematicBridgeFollower : MonoBehaviour
{
    Rigidbody rb;
    Vector3 lastPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        lastPos = transform.position;
    }

    // Important: run in FixedUpdate so PhysX sees motion
    void FixedUpdate()
    {
        // Move kinematic RB to current Transform position.
        // Because this happens inside the physics step, PhysX computes a platform velocity
        // and bodies in contact will be carried accordingly.
        rb.MovePosition(transform.position);

        // (If you ever rotate the platform, also call rb.MoveRotation(transform.rotation);)
        lastPos = transform.position;
    }
}
