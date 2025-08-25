using UnityEngine;

[DisallowMultipleComponent]
public class StackablePiece : MonoBehaviour
{
    [Header("Manual")]
    public float pieceHeight = 1f;   // local units of THIS piece
    public float topSpacing = 0f;    // extra gap above this piece
    public float pivotBias = 0f;     // small nudge if your pivot isn't bottom

    public float HeightTotal => Mathf.Max(0f, pieceHeight + topSpacing);

    // Called by StackHub when (re)parented under a parent piece
    public void SnapAboveParent(StackablePiece parentPiece)
    {
        var lp = transform.localPosition;
        float h = parentPiece ? parentPiece.HeightTotal : 0f;
        lp.x = 0f; lp.y = h + pivotBias; lp.z = 0f;
        transform.localPosition = lp;
    }
}
