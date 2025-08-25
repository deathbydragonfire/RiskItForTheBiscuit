using UnityEngine;

[DisallowMultipleComponent]
public class StackHub : MonoBehaviour
{
    [Tooltip("The bottom-most piece (first in the chain). Optional; can be created at runtime.")]
    public Transform basePiece;

    // Add a piece as the new top (chain it under the current top).
    public Transform AddPiece(GameObject prefab)
    {
        var newGo = Instantiate(prefab);
        var newPiece = newGo.GetComponent<StackablePiece>();
        if (!newPiece) { Debug.LogWarning("Prefab missing StackablePiece"); return null; }

        // Find current top (deepest child)
        Transform parentForNew = basePiece ? GetDeepest(basePiece) : transform;

        // Parent under top (or StackRoot if none yet)
        newGo.transform.SetParent(parentForNew, false);

        // Snap position relative to parent piece
        var parentSP = parentForNew.GetComponent<StackablePiece>();
        newPiece.SnapAboveParent(parentSP);

        return newGo.transform;
    }

    // Remove the top piece (if any)
    public void RemoveTop()
    {
        if (!basePiece) return;
        var top = GetDeepest(basePiece);
        if (top == basePiece) { Destroy(top.gameObject); basePiece = null; return; }
        Destroy(top.gameObject);
    }

    // Utility: find deepest descendant (the current top of the chain)
    Transform GetDeepest(Transform t)
    {
        while (t.childCount > 0)
            t = t.GetChild(0); // by construction we always keep only 1 child in the chain
        return t;
    }

    // One-time helper (optional): convert siblings under StackRoot into a chain, sorted by local Y
    [ContextMenu("Chainify Children (Bottom->Top by local Y)")]
    void ChainifyChildren()
    {
        // collect and sort by localY
        var list = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < transform.childCount; i++) list.Add(transform.GetChild(i));
        list.Sort((a, b) => a.localPosition.y.CompareTo(b.localPosition.y));

        // rechain
        Transform parent = transform;
        StackablePiece parentSP = null;
        foreach (var t in list)
        {
            if (t == basePiece) { parent = basePiece; parentSP = basePiece.GetComponent<StackablePiece>(); continue; }
            t.SetParent(parent, false);
            var sp = t.GetComponent<StackablePiece>();
            if (sp) sp.SnapAboveParent(parentSP);
            parent = t;
            parentSP = sp;
        }
        if (list.Count > 0) basePiece = list[0]; // first becomes base
    }
}
