


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }
    public static bool IsCookingPaused { get; private set; } = false;

    [Header("References")]
    [SerializeField] private Transform canvasParent; // Where to instantiate panel prefabs

    [Header("Tutorial Sequence")]
    [SerializeField] private List<TutorialStepSO> tutorialSteps = new List<TutorialStepSO>();

    [Header("Stock Configuration")]
    [SerializeField] private ItemDataSO chorizoItem;
    [SerializeField] private ItemDataSO tiraAsadoItem;
    [SerializeField] private ItemDataSO chorizoTutorialItem;

    private readonly Dictionary<ItemDataSO, int> tutorialStockBackup = new Dictionary<ItemDataSO, int>();
    private int currentStepIndex = -1;
    private GameObject currentPanelInstance;
    private ViewManager viewManager;
    private bool isTutorialActive = false;
    private bool pausedByDonenessCompletion = false;
    private Coroutine pulseRoutine;
    private Coroutine spawnCustomerRoutine;

    private void BackupAndSetTutorialStock()
    {
        if (CoolerSystem.Instance == null)
        {
            Debug.LogWarning("[TutorialManager] CoolerSystem.Instance not found. Cannot configure tutorial stock.");
            return;
        }

        tutorialStockBackup.Clear();

        // 1. Back up current values
        if (chorizoItem != null)
            tutorialStockBackup[chorizoItem] = CoolerSystem.Instance.GetCount(chorizoItem);
        if (tiraAsadoItem != null)
            tutorialStockBackup[tiraAsadoItem] = CoolerSystem.Instance.GetCount(tiraAsadoItem);
        if (chorizoTutorialItem != null)
            tutorialStockBackup[chorizoTutorialItem] = CoolerSystem.Instance.GetCount(chorizoTutorialItem);

        // 2. Adjust stock in CoolerSystem
        if (chorizoItem != null)
            CoolerSystem.Instance.SetStockDirectly(chorizoItem, 0);
        if (tiraAsadoItem != null)
            CoolerSystem.Instance.SetStockDirectly(tiraAsadoItem, 0);
        if (chorizoTutorialItem != null)
            CoolerSystem.Instance.SetStockDirectly(chorizoTutorialItem, 10);

        Debug.Log("[TutorialManager] Tutorial stock set up: Chorizo=0, Tira de Asado=0, ChorizoTutorial=10.");
    }

    private void RestoreTutorialStock()
    {
        if (CoolerSystem.Instance == null) return;

        foreach (var kvp in tutorialStockBackup)
        {
            CoolerSystem.Instance.SetStockDirectly(kvp.Key, kvp.Value);
        }

        tutorialStockBackup.Clear();
        Debug.Log("[TutorialManager] Restored stock from tutorial backup.");
    }

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

        if (tutorialStockBackup.Count > 0)
        {
            RestoreTutorialStock();
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

        BackupAndSetTutorialStock();

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
        // Cancel pending tutorial-only coroutines from the previous step.
        if (spawnCustomerRoutine != null)
        {
            StopCoroutine(spawnCustomerRoutine);
            spawnCustomerRoutine = null;
        }

        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

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

        if (pausedByDonenessCompletion)
        {
            IsCookingPaused = false;
            pausedByDonenessCompletion = false;
            Debug.Log("[TutorialManager] Resumed cooking after advancing past the post-doneness step.");
        }
        else
        {
            IsCookingPaused = step.pauseCooking;
        }

        if (step.pauseCooking)
        {
            IsCookingPaused = true;
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

            // Animate only this panel. Do not use StopAllCoroutines here,
            // because it would also cancel the delayed customer spawn.
            pulseRoutine = StartCoroutine(PulseAnimation(currentPanelInstance));
        }
    }

    private void ExecuteStartAction(TutorialStartAction action)
    {
        switch (action)
        {
            case TutorialStartAction.SpawnCustomer:
                if (spawnCustomerRoutine != null)
                {
                    StopCoroutine(spawnCustomerRoutine);
                }

                spawnCustomerRoutine = StartCoroutine(SpawnTutorialCustomerWhenReady());
                break;
        }
    }

    private IEnumerator SpawnTutorialCustomerWhenReady()
    {
        // TutorialManager.Start and CustomerSystem.Start can run in either order.
        // Waiting one frame ensures CustomerSystem has calculated the night target
        // and initialized its OrderSystem before SpawnCustomer is called.
        yield return null;

        const float timeoutSeconds = 5f;
        float elapsed = 0f;

        while (elapsed < timeoutSeconds)
        {
            CustomerSystem customerSystem = FindFirstObjectByType<CustomerSystem>();

            if (customerSystem != null)
            {
                int customersBefore = customerSystem.ActiveCustomers != null
                    ? customerSystem.ActiveCustomers.Count
                    : 0;

                customerSystem.SpawnCustomer();

                int customersAfter = customerSystem.ActiveCustomers != null
                    ? customerSystem.ActiveCustomers.Count
                    : 0;

                if (customersAfter > customersBefore)
                {
                    Debug.Log("[TutorialManager] Forced customer spawn.");
                    spawnCustomerRoutine = null;
                    yield break;
                }
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning(
            "[TutorialManager] Cannot force customer spawn: " +
            "CustomerSystem was not ready or had no free customer slot within the timeout."
        );

        spawnCustomerRoutine = null;
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

    public static void NotifyMeatStateChanged(Meat meat)
    {
        if (Instance != null) Instance.OnMeatStateChanged(meat);
    }

    public static void NotifyDeliverySelectionBegun()
    {
        if (Instance != null) Instance.OnDeliverySelectionBegun();
    }

    public static void NotifyProductDelivered()
    {
        if (Instance != null) Instance.OnProductDelivered();
    }

    public static void NotifyMeatPlacedOnBuildZone(MeatCutSO cut)
    {
        if (Instance != null) Instance.OnMeatPlacedOnBuildZone(cut);
    }

    // ── Notification Handlers ─────────────────────────────────────────
    private void OnMeatDraggedToGrill(MeatCutSO cut)
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.DragMeatToGrill ||
            step.conditionType == TutorialConditionType.DragMeatToMeatHolder)
        {
            if (step.requiredMeatCut == null || step.requiredMeatCut == cut)
            {
                Debug.Log($"[TutorialManager] DragMeatToGrill/DragMeatToMeatHolder condition met with cut: {cut.cutName}");
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

    private void OnMeatStateChanged(Meat meat)
    {
        if (!isTutorialActive ||
            currentStepIndex < 0 ||
            currentStepIndex >= tutorialSteps.Count ||
            meat == null)
        {
            return;
        }

        TutorialStepSO step = tutorialSteps[currentStepIndex];

        if (step.conditionType != TutorialConditionType.MeatReachesDoneness)
            return;

        // Estado y corte configurados directamente en el TutorialStep.
        MeatCutSO targetCut = step.requiredMeatCut;
        MeatStates targetState = step.requiredMeatState;

        // Cuando está activo, toma el corte y el punto pedido
        // por el primer cliente del tutorial.
        if (step.checkCustomerOrderState)
        {
            CustomerSystem customerSystem =
                FindFirstObjectByType<CustomerSystem>();

            if (customerSystem == null ||
                customerSystem.ActiveCustomers == null ||
                customerSystem.ActiveCustomers.Count == 0)
            {
                return;
            }

            Customer firstCustomer =
                customerSystem.ActiveCustomers[0];

            if (firstCustomer == null ||
                firstCustomer.order == null)
            {
                return;
            }

            targetCut = firstCustomer.order.meat;

            if (firstCustomer.order.requestedStates != null &&
                firstCustomer.order.requestedStates.Count > 0)
            {
                targetState =
                    firstCustomer.order.requestedStates[0];
            }
        }

        // Ignorar otros cortes que no sean el objetivo del tutorial.
        if (targetCut != null && targetCut != meat.cut)
            return;

        bool conditionMet;

        if (step.checkBothSides)
        {
            bool sideAReached = HasReachedRequiredDoneness(
     meat.SideAState,
     targetState,
     step.acceptHigherDonenessAsReached,
     step.allowedLowerDonenessSteps
 );


            bool sideBReached = HasReachedRequiredDoneness(
     meat.SideBState,
     targetState,
     step.acceptHigherDonenessAsReached,
     step.allowedLowerDonenessSteps
             );

            conditionMet = sideAReached && sideBReached;
        }
        else
        {
            conditionMet = HasReachedRequiredDoneness(
    meat.ActiveSideState,
    targetState,
    step.acceptHigherDonenessAsReached,
    step.allowedLowerDonenessSteps
            );
        }

        if (!conditionMet)
            return;

        Debug.Log(
            "[TutorialManager] MeatReachesDoneness completado." +
            " Corte: " + meat.cut.cutName +
            " | Pedido: " + targetState +
            " | Cara A: " + meat.SideAState +
            " | Cara B: " + meat.SideBState +
            " | Acepta pasado: " +
            step.acceptHigherDonenessAsReached
        );

        IsCookingPaused = true;
        pausedByDonenessCompletion = true;

        AdvanceStep();
    }
    private bool HasReachedRequiredDoneness(
     MeatStates currentState,
     MeatStates targetState,
     bool acceptHigherState,
     int allowedLowerSteps)
    {
        int currentValue = (int)currentState;
        int targetValue = (int)targetState;

        // Punto exacto.
        if (currentValue == targetValue)
            return true;

        // Más cocido que el pedido.
        if (currentValue > targetValue)
            return acceptHigherState;

        // Menos cocido que el pedido, pero dentro de la tolerancia.
        int difference = targetValue - currentValue;

        return difference <= Mathf.Max(0, allowedLowerSteps);
    }
    private void OnDeliverySelectionBegun()
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.BeginDeliverySelection)
        {
            Debug.Log("[TutorialManager] BeginDeliverySelection condition met.");
            AdvanceStep();
        }
    }

    private void OnProductDelivered()
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.DeliverProduct)
        {
            Debug.Log("[TutorialManager] DeliverProduct condition met.");
            AdvanceStep();
        }
    }

    private void OnMeatPlacedOnBuildZone(MeatCutSO cut)
    {
        if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
            return;

        TutorialStepSO step = tutorialSteps[currentStepIndex];
        if (step.conditionType == TutorialConditionType.DragMeatToBuildZone)
        {
            if (step.requiredMeatCut == null || step.requiredMeatCut == cut)
            {
                Debug.Log($"[TutorialManager] DragMeatToBuildZone condition met with cut: {cut.cutName}");
                AdvanceStep();
            }
        }
    }

    private void EndTutorial()
    {
        RestoreTutorialStock();
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
        pulseRoutine = null;
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