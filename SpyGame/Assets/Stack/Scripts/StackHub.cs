using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Stack/StackHub (global settings + utils)")]
[DefaultExecutionOrder(-100)] // ensure it's ready before pieces tick
public class StackHub : MonoBehaviour
{
    public static StackHub Active { get; private set; }

    [Header("Global Follow Settings")]
    [Tooltip("How strongly pieces pull toward their target position.")]
    public float positionSpring = 40f;

    [Tooltip("Damping to reduce overshoot/jitter.")]
    public float positionDamping = 12f;

    [Tooltip("How fast pieces align upright & to parent yaw (0 = no rotation assist).")]
    public float orientSpeed = 8f;

    [Header("Global Geometry")]
    [Tooltip("Extra world-space offset between parent top and child bottom.")]
    public Vector3 stackGapWorld = new Vector3(0f, 0.01f, 0f);

    [Header("Global Safety")]
    [Tooltip("> 0 enables auto-detach when a seam stretches beyond this distance (0 = off).")]
    public float breakDistance = 0f;

    void OnEnable()
    {
        if (Active != null && Active != this)
        {
            Debug.LogWarning("Multiple StackHubs active. The most recently enabled one becomes Active.");
        }
        Active = this;
    }

    void OnDisable()
    {
        if (Active == this) Active = null;
    }

    // ---------- Utility Ops (unchanged behavior) ----------

    public static void AddOnTop(StackablePiece basePiece, StackablePiece newPiece, bool snap = true)
    {
        if (!IsValid(basePiece, newPiece)) return;
        RefreshChainLinksAround(basePiece);
        StackablePiece top = GetTopOfChain(basePiece);
        newPiece.transform.SetParent(top.transform, true);
        RefreshChainLinksAround(newPiece, top);
        if (snap) SnapToParentAnchor(newPiece);
    }

    public static void InsertAbove(StackablePiece below, StackablePiece node, bool snap = true)
    {
        if (!IsValid(below, node)) return;
        RefreshChainLinksAround(below, node);
        StackablePiece oldChild = FindDirectChildPiece(below.transform);
        node.transform.SetParent(below.transform, true);
        if (oldChild) oldChild.transform.SetParent(node.transform, true);
        RefreshChainLinksAround(below, node, oldChild);
        if (snap) SnapToParentAnchor(node);
    }

    public static void RemovePiece(StackablePiece node, bool snapNeighbor = true)
    {
        if (!node) return;
        RefreshChainLinksAround(node);

        StackablePiece parent = FindNearestAncestorPiece(node.transform);
        StackablePiece child = FindDirectChildPiece(node.transform);

        if (child)
        {
            if (parent)
            {
                child.transform.SetParent(parent.transform, true);
                if (snapNeighbor) SnapToParentAnchor(child);
            }
            else child.transform.SetParent(null, true);
        }

        node.transform.SetParent(null, true);
        RefreshChainLinksAround(parent, child, node);
    }

    public static void DetachSubstack(StackablePiece node)
    {
        if (!node) return;
        RefreshChainLinksAround(node);
        node.transform.SetParent(null, true);
        RefreshChainLinksAround(node);
    }

    public static void MoveSubstackToTop(StackablePiece basePiece, StackablePiece substackRoot, bool snap = true)
    {
        if (!IsValid(basePiece, substackRoot)) return;
        RefreshChainLinksAround(basePiece, substackRoot);
        StackablePiece top = GetTopOfChain(basePiece);
        substackRoot.transform.SetParent(top.transform, true);
        RefreshChainLinksAround(top, substackRoot);
        if (snap) SnapToParentAnchor(substackRoot);
    }

    public static void RebuildLinksUnder(Transform root)
    {
        if (!root) return;
        var all = root.GetComponentsInChildren<StackablePiece>(true);
        for (int i = 0; i < all.Length; i++) all[i].RefreshLinks();
    }

    // ---------- Internals ----------
    static bool IsValid(StackablePiece a, StackablePiece b) => a && b && a != b;

    static StackablePiece GetTopOfChain(StackablePiece start)
    {
        Transform t = start.transform;
        StackablePiece cur = start;
        while (true)
        {
            var next = FindDirectChildPiece(t);
            if (!next) break;
            cur = next;
            t = next.transform;
        }
        return cur;
    }

    static void RefreshChainLinksAround(params StackablePiece[] pieces)
    {
        for (int i = 0; i < pieces.Length; i++)
        {
            var p = pieces[i];
            if (!p) continue;

            p.RefreshLinks();

            var ancestor = FindNearestAncestorPiece(p.transform);
            if (ancestor) ancestor.RefreshLinks();

            var directChild = FindDirectChildPiece(p.transform);
            if (directChild) directChild.RefreshLinks();

            int childCount = p.transform.childCount;
            for (int c = 0; c < childCount; c++)
            {
                var maybe = p.transform.GetChild(c).GetComponent<StackablePiece>();
                if (maybe) maybe.RefreshLinks();
            }
        }
    }

    public static StackablePiece FindNearestAncestorPiece(Transform t)
    {
        for (var p = t.parent; p != null; p = p.parent)
        {
            var piece = p.GetComponent<StackablePiece>();
            if (piece) return piece;
        }
        return null;
    }

    public static StackablePiece FindDirectChildPiece(Transform t)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            var c = t.GetChild(i).GetComponent<StackablePiece>();
            if (c) return c;
        }
        return null;
    }

    public static void SnapToParentAnchor(StackablePiece node)
    {
        if (!node) return;
        node.RefreshLinks();
        var parent = FindNearestAncestorPiece(node.transform);
        if (!parent) return;

        Vector3 parentTop = parent.GetTopAnchorWorld();
        Vector3 targetPos = parentTop + (Active ? Active.stackGapWorld : Vector3.zero);

        Vector3 bottomNow = node.GetBottomAnchorWorld();
        node.transform.position += (targetPos - bottomNow);
    }
}
