using UnityEngine;

public enum TutorialConditionType
{
    ChangeView,
    ConfirmButton,
    DragMeatToGrill,
    DragMeatToBuild,
    DragCoalToGrill,
    DragMeatToGrillSlots,
    ToggleGrillLayer,
    DragCoalToGrillSlots,
    FlipMeat,
    MeatReachesDoneness,
    DragMeatToMeatHolder,
    BeginDeliverySelection,
    DeliverProduct,
    DragMeatToBuildZone
}

public enum TutorialStartAction
{
    None,
    SpawnCustomer
}

[CreateAssetMenu(fileName = "NewTutorialStep", menuName = "Tutorial/Tutorial Step")]
public class TutorialStepSO : ScriptableObject
{
    [Header("Instruction Info")]
    [TextArea(3, 5)]
    public string instructionText;

    [Header("UI Panel")]
    public GameObject panelPrefab; // The panel designed by the user for this step

    [Header("Completion Condition")]
    public TutorialConditionType conditionType;
    public ViewType requiredView; // Only used if conditionType is ChangeView

    [Header("Drag & Drop Condition Settings")]
    public MeatCutSO requiredMeatCut; // Used if condition is DragMeatToGrill or DragMeatToBuild
    public CoalSO requiredCoal; // Used if condition is DragCoalToGrill

    [Header("Grill Layer Settings")]
    public GrillLayerToggle.GrillLayer requiredGrillLayer; // Used if condition is ToggleGrillLayer

    [Header("Doneness Condition Settings")]
    public MeatStates requiredMeatState;
    public bool checkCustomerOrderState;
    public bool checkBothSides;
    [Tooltip("Si está activo, un estado más cocido que el pedido también cuenta como alcanzado.")]
    public bool acceptHigherDonenessAsReached;
    [Min(1)]
    [Tooltip("Cantidad de niveles menos cocidos que también se aceptan. Usar 1 para permitir un nivel menos.")]
    public int allowedLowerDonenessSteps = 4;
    [Header("Cooking Control")]
    public bool pauseCooking; // If true, pauses cooking on the grill while this step is active

    [Header("Action on Step Start")]
    public TutorialStartAction startAction; // Triggers action when this tutorial step begins
}
