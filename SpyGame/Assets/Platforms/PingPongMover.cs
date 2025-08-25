using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VelocityPlatformMover : MonoBehaviour
{
    [Header("Movement")]
    public Vector3 moveOffset = new Vector3(0f, 2f, 0f); // how far from start
    public float moveDuration = 2f;   // seconds to go out or back
    public bool smooth = true;

    private Rigidbody rb;
    private Vector3 startPos;
    private Vector3 lastPos;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;          // platform controlled by script
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        startPos = transform.position;
        lastPos = startPos;
    }

    void FixedUpdate()
    {
        if (moveDuration <= 0f) return;

        float t = Mathf.PingPong(Time.time / moveDuration, 1f);
        if (smooth) t = Mathf.SmoothStep(0f, 1f, t);

        Vector3 newPos = Vector3.Lerp(startPos, startPos + moveOffset, t);

        // Move via rigidbody to keep velocity valid
        rb.MovePosition(newPos);

        // Calculate velocity (for other scripts to use)
        rb.linearVelocity = (newPos - lastPos) / Time.fixedDeltaTime;

        lastPos = newPos;
    }
}
