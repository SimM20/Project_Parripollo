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
    MeatReachesDoneness
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
    public MeatStates requiredMeatState; // Used if condition is MeatReachesDoneness
    public bool checkCustomerOrderState; // If true, matches active customer's ordered cut & doneness state

    [Header("Action on Step Start")]
    public TutorialStartAction startAction; // Triggers action when this tutorial step begins
}
