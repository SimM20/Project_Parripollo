using UnityEngine;

[ExecuteAlways]
public class GridTransformGroup : MonoBehaviour
{
    public Vector2 cellSize = new Vector2(100, 100);
    [Tooltip("Spacing used by any row that does not have an override.")]
    public Vector2 spacing = new Vector2(10, 10);
    [Tooltip("Per row spacing. Element 0 is the first row. X = gap between columns of that row, Y = gap below that row. Each row is centered on the local X axis.")]
    public Vector2[] rowSpacing = new Vector2[0];
    [Min(1)]
    public int columns = 3;

    private void OnEnable() => ArrangeChildrenInGrid();

    private void OnValidate() => ArrangeChildrenInGrid();

    private void OnTransformChildrenChanged() => ArrangeChildrenInGrid();

#if UNITY_EDITOR
    private void Update()
    {
        if (!Application.isPlaying)
            ArrangeChildrenInGrid();
    }
#endif

    private int CountActiveChildren()
    {
        int count = 0;
        int childCount = transform.childCount;

        for (int i = 0; i < childCount; i++)
        {
            if (transform.GetChild(i).gameObject.activeSelf)
                count++;
        }

        return count;
    }

    public Vector2 GetRowSpacing(int row)
    {
        if (rowSpacing != null && row >= 0 && row < rowSpacing.Length)
            return rowSpacing[row];

        return spacing;
    }

    public void ArrangeChildrenInGrid()
    {
        int safeColumns = Mathf.Max(1, columns);
        int activeCount = CountActiveChildren();
        int activeIndex = 0;
        int childCount = transform.childCount;
        int currentRow = 0;
        float rowOffsetY = 0f;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.gameObject.activeSelf)
                continue;

            int row = activeIndex / safeColumns;
            int column = activeIndex % safeColumns;
            int itemsInRow = Mathf.Min(safeColumns, activeCount - row * safeColumns);

            while (currentRow < row)
            {
                rowOffsetY -= cellSize.y + GetRowSpacing(currentRow).y;
                currentRow++;
            }

            float stepX = cellSize.x + GetRowSpacing(row).x;
            float rowStartX = -(itemsInRow - 1) * stepX * 0.5f;

            Vector3 newPosition = new Vector3(
                rowStartX + column * stepX,
                rowOffsetY,
                0f
            );

            if (child.localPosition != newPosition)
                child.localPosition = newPosition;

            activeIndex++;
        }
    }
}
