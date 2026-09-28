using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// La tienda (EndScene) en la foto del tutorial: si se puede usar, el panel de requisitos, el primer
/// Comprar que se puede apretar, uno apagado por los mínimos y la línea de la racha de strikes.
///
/// Mientras el popup de cierre por strikes (orden 20) tapa la tienda no vale nada: los carteles
/// esperan a que se cierre. Con la derrota total el canvas de la tienda está apagado y tampoco.
/// </summary>
public partial class TutorialHintContext
{
    private ShopGridUI shopGrid;
    private ShopRequirementsUI shopRequirements;
    private StrikeEndPopup strikePopup;
    private Camera shopCamera;

    private bool shopReady;
    private Transform firstBuyButton;
    private Transform blockedBuyButton;
    private Transform streakLine;

    private static readonly Vector3[] ShopCorners = new Vector3[4];

    private void BindShop()
    {
        // GameScene tiene un ShopSystem, pero sin grilla: sin grilla no hay tienda que explicar.
        shopGrid = Object.FindFirstObjectByType<ShopGridUI>(FindObjectsInactive.Include);
        if (shopGrid == null)
            return;

        shopRequirements = Object.FindFirstObjectByType<ShopRequirementsUI>(FindObjectsInactive.Include);
        strikePopup = Object.FindFirstObjectByType<StrikeEndPopup>(FindObjectsInactive.Include);

        Canvas canvas = shopGrid.GetComponentInParent<Canvas>(true);
        Canvas root = canvas != null ? canvas.rootCanvas : null;
        shopCamera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
    }

    private void RefreshShop()
    {
        shopReady = false;
        firstBuyButton = null;
        blockedBuyButton = null;
        streakLine = null;

        if (shopGrid == null || !shopGrid.isActiveAndEnabled)
            return;
        if (strikePopup != null && strikePopup.IsOpen)
            return;

        shopReady = true;

        // Solo las tarjetas que se ven: las de más abajo pueden estar scrolleadas fuera de la grilla.
        RectTransform viewport = shopGrid.Viewport;
        IReadOnlyList<ShopItemCellUI> cells = shopGrid.Cells;
        for (int i = 0; i < cells.Count; i++)
        {
            ShopItemCellUI cell = cells[i];
            if (cell == null || !cell.isActiveAndEnabled || cell.BuyButton == null)
                continue;

            RectTransform button = (RectTransform)cell.BuyButton.transform;
            if (!IsInsideViewport(button, viewport))
                continue;

            if (firstBuyButton == null && cell.CanBuy)
                firstBuyButton = button;
            if (blockedBuyButton == null && cell.BlockedByMinimums)
                blockedBuyButton = button;
        }

        if (shopRequirements != null && StrikeSystem.ConsecutiveStrikeNights > 0)
        {
            Transform line = shopRequirements.StreakLine;
            if (line != null && line.gameObject.activeInHierarchy)
                streakLine = line;
        }
    }

    private bool EvaluateShop(HintCondition condition)
    {
        switch (condition)
        {
            case HintCondition.ShopReady: return shopReady;
            case HintCondition.ShopCanBuy: return firstBuyButton != null;
            case HintCondition.ShopPurchaseBlocked: return blockedBuyButton != null;
            case HintCondition.StrikeStreakActive: return streakLine != null;
            default: return false;
        }
    }

    private Transform ResolveShopAnchor(HintAnchorId anchor)
    {
        switch (anchor)
        {
            case HintAnchorId.ShopRequirements:
                // Sin mínimos (ShopTutorial) el panel está apagado: no hay nada que señalar.
                if (!shopReady || shopRequirements == null)
                    return null;
                Transform panel = shopRequirements.Panel;
                return panel.gameObject.activeInHierarchy ? panel : null;

            case HintAnchorId.ShopFirstBuyButton: return firstBuyButton;
            case HintAnchorId.ShopBlockedBuyButton: return blockedBuyButton;
            case HintAnchorId.ShopStreakLine: return streakLine;
            default: return null;
        }
    }

    /// <summary>El centro del elemento cae adentro de la parte visible de la grilla.</summary>
    private bool IsInsideViewport(RectTransform target, RectTransform viewport)
    {
        if (viewport == null)
            return true;

        target.GetWorldCorners(ShopCorners);
        Vector3 center = (ShopCorners[0] + ShopCorners[2]) * 0.5f;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(shopCamera, center);
        return RectTransformUtility.RectangleContainsScreenPoint(viewport, screen, shopCamera);
    }
}
