using UnityEngine;

[ExecuteAlways]
public class GridTransformGroup : MonoBehaviour
{
    public Vector2 cellSize = new Vector2(100, 100);
    public Vector2 spacing = new Vector2(10, 10);
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

    public void ArrangeChildrenInGrid()
    {
        int safeColumns = Mathf.Max(1, columns);
        int activeIndex = 0;
        int childCount = transform.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.gameObject.activeSelf)
                continue;

            int row = activeIndex / safeColumns;
            int column = activeIndex % safeColumns;
            Vector3 newPosition = new Vector3(
                column * (cellSize.x + spacing.x),
                -row * (cellSize.y + spacing.y),
                0f
            );

            if (child.localPosition != newPosition)
                child.localPosition = newPosition;

            activeIndex++;
        }
    }
}