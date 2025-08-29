using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class SpriteFlipWhenNotCarrying : MonoBehaviour
{
    [Header("Animator & Params")]
    [SerializeField] private Animator animator;                 // auto-fills from this GameObject
    [SerializeField] private string carryingParam = "isCarrying";
    [SerializeField] private string backwardsParam = "isBackwards";

    private int _carryHash;
    private int _backHash;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();      // prefer local animator
        if (animator == null) animator = GetComponentInParent<Animator>(); // fallback to parent

        _carryHash = Animator.StringToHash(carryingParam);
        _backHash = Animator.StringToHash(backwardsParam);
    }

    void LateUpdate()
    {
        if (animator == null) return;

        // If carrying, do nothing (keep whatever scale it currently has)
        if (animator.GetBool(_carryHash)) return;

        // Not carrying: flip X by isBackwards
        bool backwards = animator.GetBool(_backHash);
        Vector3 s = transform.localScale;
        float sign = backwards ? -1f : 1f;
        s.x = Mathf.Abs(s.x) * sign;   // preserve magnitude, just set the sign
        transform.localScale = s;
    }
}
