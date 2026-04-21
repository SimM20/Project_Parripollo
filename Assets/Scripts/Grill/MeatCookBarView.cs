using UnityEngine;

public class MeatCookBarView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Meat meat;
    [SerializeField] private Transform sideAFill;
    [SerializeField] private Transform sideBFill;

    [Header("Active Side (Optional)")]
    [SerializeField] private bool tintActiveSide = true;
    [SerializeField] private SpriteRenderer sideARowRenderer;
    [SerializeField] private SpriteRenderer sideBRowRenderer;
    [SerializeField] private Color activeSideTint = Color.white;
    [SerializeField] private Color inactiveSideTint = new Color(1f, 1f, 1f, 0.55f);

    private Vector3 sideAFillBaseScale = Vector3.one;
    private Vector3 sideBFillBaseScale = Vector3.one;

    void Awake()
    {
        ResolveReferences();
        CacheBaseScales();
    }

    void LateUpdate()
    {
        if (meat == null)
            return;

        UpdateFillScale(sideAFill, sideAFillBaseScale, meat.SideAProgress01);
        UpdateFillScale(sideBFill, sideBFillBaseScale, meat.SideBProgress01);

        if (tintActiveSide)
            UpdateActiveSideTint();
    }

    void OnValidate()
    {
        ResolveReferences();
        CacheBaseScales();
    }

    private void ResolveReferences()
    {
        if (meat == null)
            meat = GetComponentInParent<Meat>();
    }

    private void CacheBaseScales()
    {
        if (sideAFill != null)
            sideAFillBaseScale = sideAFill.localScale;

        if (sideBFill != null)
            sideBFillBaseScale = sideBFill.localScale;
    }

    private static void UpdateFillScale(Transform fill, Vector3 baseScale, float progress01)
    {
        if (fill == null)
            return;

        Vector3 scale = baseScale;
        scale.x = baseScale.x * Mathf.Clamp01(progress01);
        fill.localScale = scale;
    }

    private void UpdateActiveSideTint()
    {
        bool sideAActive = meat.IsSideAActive;

        SetTint(sideARowRenderer, sideAActive ? activeSideTint : inactiveSideTint);
        SetTint(sideBRowRenderer, sideAActive ? inactiveSideTint : activeSideTint);
    }

    private static void SetTint(SpriteRenderer target, Color tint)
    {
        if (target != null)
            target.color = tint;
    }
}
