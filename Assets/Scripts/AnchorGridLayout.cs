using UnityEngine;

/// <summary>
/// Arranges all child UI elements in a grid by adjusting their anchorMin/anchorMax
/// in a normalized space of 0..1. 
///
/// Supports:
/// - user-defined columns (rows calculated by child count),
/// - margin on all sides (left, right, top, bottom),
/// - spacing between each cell horizontally/vertically,
/// - an option to fill from top to bottom.
/// 
/// Example:
///   - If you have only 1 child and all margins = 0, that child’s anchors become (0,0) to (1,1).
///   - If you add more children and set margins/spacings, each child anchors to 
///     its own cell in the grid.
/// </summary>
[ExecuteAlways] // So it updates in Editor when values change
public class AnchorGridLayout : MonoBehaviour
{
    [Header("Layout Settings")]
    [Tooltip("Number of columns to distribute child elements. Rows are computed based on child count.")]
    [SerializeField] private int columns = 1;

    [Tooltip("Margins on each side, specified in normalized coordinates (0..1).")]
    [Range(0f, 1f)][SerializeField] private float leftMargin = 0f;
    [Range(0f, 1f)][SerializeField] private float rightMargin = 0f;
    [Range(0f, 1f)][SerializeField] private float topMargin = 0f;
    [Range(0f, 1f)][SerializeField] private float bottomMargin = 0f;

    [Header("Spacing (normalized)")]
    [Tooltip("Horizontal spacing between columns, in 0..1 normalized space.")]
    [Range(0f, 1f)][SerializeField] private float horizontalSpacing = 0f;
    [Tooltip("Vertical spacing between rows, in 0..1 normalized space.")]
    [Range(0f, 1f)][SerializeField] private float verticalSpacing = 0f;

    [Header("Row Order")]
    [Tooltip("If true, the first child is placed in the top row; if false, the bottom row.")]
    [SerializeField] private bool fillFromTop = false;

    private void OnEnable()
    {
        RefreshLayout();
    }

    // Called in the Editor whenever a value changes in the Inspector
    private void OnValidate()
    {
        RefreshLayout();
    }

    /// <summary>
    /// Main method that repositions all child RectTransforms based on the current settings.
    /// </summary>
    public void RefreshLayout()
    {
        RectTransform parentRect = GetComponent<RectTransform>();
        if (parentRect == null) return;

        int childCount = parentRect.childCount;
        if (childCount == 0) return;

        // Calculate how many rows we need, given the number of columns
        int rowCount = Mathf.CeilToInt(childCount / (float)columns);

        // Compute total available width/height (normalized 0..1, minus margins)
        float totalWidth = 1f - leftMargin - rightMargin;
        float totalHeight = 1f - topMargin - bottomMargin;

        // For each column/row cell, we reserve some space plus spacing between cells
        // We'll do "columns" across the X-axis, "rowCount" across the Y-axis
        float cellWidth = (totalWidth - (columns - 1) * horizontalSpacing) / columns;
        float cellHeight = (totalHeight - (rowCount - 1) * verticalSpacing) / rowCount;

        // Loop over children, assigning each child a "cell" in the grid
        for (int i = 0; i < childCount; i++)
        {
            RectTransform child = parentRect.GetChild(i) as RectTransform;
            if (child == null) continue;

            // Determine which cell (row/column) this child should occupy
            int colIndex = i % columns;
            int rowIndex = i / columns;

            // Optionally invert the row to fill from top
            if (fillFromTop)
            {
                rowIndex = (rowCount - 1) - rowIndex;
            }

            // Calculate anchorMin and anchorMax for this child's cell
            float anchorMinX = leftMargin + colIndex * (cellWidth + horizontalSpacing);
            float anchorMinY = bottomMargin + rowIndex * (cellHeight + verticalSpacing);
            float anchorMaxX = anchorMinX + cellWidth;
            float anchorMaxY = anchorMinY + cellHeight;

            // Apply anchors to the child
            child.anchorMin = new Vector2(anchorMinX, anchorMinY);
            child.anchorMax = new Vector2(anchorMaxX, anchorMaxY);

            // Ensure offset/pivot doesn't shift it
            // Setting offsetMin/offsetMax to zero so it exactly fits the anchor rect
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
        }
    }
}
