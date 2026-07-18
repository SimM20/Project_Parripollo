using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform canvasParent; // Where to instantiate panel prefabs

    [Header("Tutorial Sequence")]
    [SerializeField] private List<TutorialStepSO> tutorialSteps = new List<TutorialStepSO>();

    private int currentStepIndex = -1;
    private GameObject currentPanelInstance;
    private ViewManager viewManager;
    private bool isTutorialActive = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        viewManager = FindFirstObjectByType<ViewManager>();
        if (viewManager != null)
        {
            viewManager.OnViewChanged += OnViewChanged;
        }

        // We only run the tutorial if we are in the TutorialScene!
        if (SceneManagementUtils.GetCurrentName() == "TutorialScene")
        {
            StartTutorial();
        }
    }

    void OnDestroy()
    {
        if (viewManager != null)
        {
            viewManager.OnViewChanged -= OnViewChanged;
        }
    }

    public void StartTutorial()
    {
        if (tutorialSteps.Count == 0)
        {
            Debug.LogWarning("[TutorialManager] No tutorial steps assigned.");
            return;
        }

        // Ensure EventSystem exists in the scene so UI buttons are clickable
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[TutorialManager] EventSystem was missing in the scene. Created one dynamically.");
        }

        isTutorialActive = true;
        ShowStep(0);
    }

    public void AdvanceStep()
    {
        Debug.Log($"[TutorialManager] AdvanceStep called. Current index: {currentStepIndex}");
        if (!isTutorialActive) return;
        ShowStep(currentStepIndex + 1);
    }

    private void ShowStep(int index)
    {
        // Clean up previous panel
        if (currentPanelInstance != null)
        {
            Destroy(currentPanelInstance);
        }

        if (index < 0 || index >= tutorialSteps.Count)
        {
            EndTutorial();
            return;
        }

        currentStepIndex = index;
        TutorialStepSO step = tutorialSteps[index];

        if (step == null)
        {
            Debug.LogError($"[TutorialManager] Step at index {index} is null.");
            return;
        }

        // Execute start action (e.g. Spawn customer) before showing panel
        ExecuteStartAction(step.startAction);

        // Instantiate panel
        if (step.panelPrefab != null)
        {
            Transform parent = canvasParent != null ? canvasParent : FindCanvasTransform();
            currentPanelInstance = Instantiate(step.panelPrefab, parent);
            
            // If the prefab itself is a Canvas, ensure it has a GraphicRaycaster for click support
            Canvas canvasComp = currentPanelInstance.GetComponent<Canvas>();
            if (canvasComp != null && currentPanelInstance.GetComponent<GraphicRaycaster>() == null)
            {
                currentPanelInstance.AddComponent<GraphicRaycaster>();
                Debug.Log($"[TutorialManager] Added GraphicRaycaster to canvas panel '{currentPanelInstance.name}' to enable click events.");
            }

            // Try to set dynamic text if instructionText is provided
            if (!string.IsNullOrEmpty(step.instructionText))
            {
                TextMeshProUGUI textComp = currentPanelInstance.GetComponentInChildren<TextMeshProUGUI>();
                if (textComp != null)
                {
                    textComp.text = step.instructionText;
                }
            }

            // Register button listener if conditionType is ConfirmButton
            if (step.conditionType == TutorialConditionType.ConfirmButton)
            {
                Button btn = FindConfirmButton(currentPanelInstance);
                if (btn != null)
                {
                    Debug.Log($"[TutorialManager] Found confirm button: '{btn.gameObject.name}'. Adding AdvanceStep listener.");
                    btn.onClick.RemoveListener(AdvanceStep); // Prevent duplicate hooks
                    btn.onClick.AddListener(AdvanceStep);
                }
                else
                {
                    Debug.LogWarning($"[TutorialManager] No Button component found in prefab '{step.panelPrefab.name}' children!");
                }
            }

            // Animate
            StopAllCoroutines();
            StartCoroutine(PulseAnimation(currentPanelInstance));
        }
    }

    private void ExecuteStartAction(TutorialStartAction action)
    {
        switch (action)
        {
            case TutorialStartAction.SpawnCustomer:
                CustomerSystem customerSystem = FindFirstObjectByType<CustomerSystem>();
                if (customerSystem != null)
                {
                    customerSystem.SpawnCustomer();
                    Debug.Log("[TutorialManager] Forced customer spawn.");
                }
                else
                {
                    Debug.LogWarning("[TutorialManager] Cannot force customer spawn: CustomerSystem not found in the scene.");
                }
                break;
        }
    }

    private void OnViewChanged(ViewType newView)
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.ChangeView && newView == step.requiredView)
        {
            ShowStep(currentStepIndex + 1);
        }
    }

    // ── Static Notifications ───────────────────────────────────────────
    public static void NotifyMeatDraggedToGrill(MeatCutSO cut)
    {
        if (Instance != null) Instance.OnMeatDraggedToGrill(cut);
    }

    public static void NotifyMeatDraggedToBuild(MeatCutSO cut)
    {
        if (Instance != null) Instance.OnMeatDraggedToBuild(cut);
    }

    public static void NotifyCoalDraggedToGrill(CoalSO coal)
    {
        if (Instance != null) Instance.OnCoalDraggedToGrill(coal);
    }

    public static void NotifyMeatPlacedOnGrill(MeatCutSO cut)
    {
        if (Instance != null) Instance.OnMeatPlacedOnGrill(cut);
    }

    public static void NotifyGrillLayerChanged(GrillLayerToggle.GrillLayer newLayer)
    {
        if (Instance != null) Instance.OnGrillLayerChanged(newLayer);
    }

    public static void NotifyCoalPlacedOnGrill(CoalSO coal)
    {
        if (Instance != null) Instance.OnCoalPlacedOnGrill(coal);
    }

    public static void NotifyMeatFlipped(MeatCutSO cut)
    {
        if (Instance != null) Instance.OnMeatFlipped(cut);
    }

    public static void NotifyMeatStateChanged(MeatCutSO cut, MeatStates newState)
    {
        if (Instance != null) Instance.OnMeatStateChanged(cut, newState);
    }

    // ── Notification Handlers ─────────────────────────────────────────
    private void OnMeatDraggedToGrill(MeatCutSO cut)
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.DragMeatToGrill)
        {
            if (step.requiredMeatCut == null || step.requiredMeatCut == cut)
            {
                Debug.Log($"[TutorialManager] DragMeatToGrill condition met with cut: {cut.cutName}");
                AdvanceStep();
            }
        }
    }

    private void OnMeatDraggedToBuild(MeatCutSO cut)
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.DragMeatToBuild)
        {
            if (step.requiredMeatCut == null || step.requiredMeatCut == cut)
            {
                Debug.Log($"[TutorialManager] DragMeatToBuild condition met with cut: {cut.cutName}");
                AdvanceStep();
            }
        }
    }

    private void OnCoalDraggedToGrill(CoalSO coal)
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.DragCoalToGrill)
        {
            if (step.requiredCoal == null || step.requiredCoal == coal)
            {
                Debug.Log($"[TutorialManager] DragCoalToGrill condition met.");
                AdvanceStep();
            }
        }
    }

    private void OnMeatPlacedOnGrill(MeatCutSO cut)
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.DragMeatToGrillSlots)
        {
            if (step.requiredMeatCut == null || step.requiredMeatCut == cut)
            {
                Debug.Log($"[TutorialManager] DragMeatToGrillSlots condition met with cut: {cut.cutName}");
                AdvanceStep();
            }
        }
    }

    private void OnGrillLayerChanged(GrillLayerToggle.GrillLayer newLayer)
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.ToggleGrillLayer)
        {
            if (step.requiredGrillLayer == newLayer)
            {
                Debug.Log($"[TutorialManager] ToggleGrillLayer condition met: {newLayer}");
                AdvanceStep();
            }
        }
    }

    private void OnCoalPlacedOnGrill(CoalSO coal)
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.DragCoalToGrillSlots)
        {
            if (step.requiredCoal == null || step.requiredCoal == coal)
            {
                Debug.Log($"[TutorialManager] DragCoalToGrillSlots condition met.");
                AdvanceStep();
            }
        }
    }

    private void OnMeatFlipped(MeatCutSO cut)
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.FlipMeat)
        {
            if (step.requiredMeatCut == null || step.requiredMeatCut == cut)
            {
                Debug.Log($"[TutorialManager] FlipMeat condition met with cut: {cut.cutName}");
                AdvanceStep();
            }
        }
    }

    private void OnMeatStateChanged(MeatCutSO cut, MeatStates newState)
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.MeatReachesDoneness)
        {
            // Resolve required cut and required state
            MeatCutSO targetCut = step.requiredMeatCut;
            MeatStates targetState = step.requiredMeatState;

            if (step.checkCustomerOrderState)
            {
                CustomerSystem cs = FindFirstObjectByType<CustomerSystem>();
                if (cs != null && cs.ActiveCustomers.Count > 0)
                {
                    Customer firstCustomer = cs.ActiveCustomers[0];
                    if (firstCustomer != null && firstCustomer.order != null)
                    {
                        targetCut = firstCustomer.order.meat;
                        if (firstCustomer.order.requestedStates.Count > 0)
                        {
                            targetState = firstCustomer.order.requestedStates[0];
                        }
                    }
                }
            }

            // Check if matches
            if (targetCut == null || targetCut == cut)
            {
                if (newState == targetState)
                {
                    Debug.Log($"[TutorialManager] MeatReachesDoneness condition met: {cut.cutName} reaches state {newState}");
                    AdvanceStep();
                }
            }
        }
    }

    private void EndTutorial()
    {
        isTutorialActive = false;
        if (currentPanelInstance != null)
        {
            Destroy(currentPanelInstance);
        }
        
        Debug.Log("[TutorialManager] Tutorial completed!");
        
        // Return to GameScene since they completed the tutorial!
        SceneManagementUtils.LoadSceneByName("GameScene");
    }

    private Transform FindCanvasTransform()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
            return canvas.transform;

        // Fallback: create dynamic canvas
        GameObject canvasGo = new GameObject("TutorialCanvas");
        Canvas c = canvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return canvasGo.transform;
    }

    private IEnumerator PulseAnimation(GameObject panel)
    {
        if (panel == null) yield break;
        
        Vector3 originalScale = panel.transform.localScale;
        float popTime = 0.15f;
        float elapsed = 0f;
        while (elapsed < popTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popTime;
            panel.transform.localScale = Vector3.Lerp(originalScale, originalScale * 1.05f, t);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < popTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popTime;
            panel.transform.localScale = Vector3.Lerp(originalScale * 1.05f, originalScale, t);
            yield return null;
        }
        panel.transform.localScale = originalScale;
    }

    private Button FindConfirmButton(GameObject panel)
    {
        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        if (buttons.Length == 0) return null;

        foreach (var b in buttons)
        {
            string bName = b.gameObject.name.ToLower();
            if (bName.Contains("confirm") || bName.Contains("next") || bName.Contains("siguiente") || bName.Contains("ok"))
            {
                return b;
            }
        }

        return buttons[0];
    }
}
