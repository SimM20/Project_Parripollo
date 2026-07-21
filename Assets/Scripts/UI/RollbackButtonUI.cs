using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta el botón de Rollback existente con el historial de undo del armado.
/// Se deshabilita cuando no hay acciones reversibles.
/// </summary>
[RequireComponent(typeof(Button))]
public class RollbackButtonUI : MonoBehaviour
{
    [SerializeField] private BuildUndoHistory undoHistory;

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void OnEnable()
    {
        button.onClick.AddListener(HandleClick);

        ResolveHistory();
        if (undoHistory != null)
            undoHistory.OnHistoryChanged += RefreshInteractable;

        RefreshInteractable();
    }

    void OnDisable()
    {
        button.onClick.RemoveListener(HandleClick);

        if (undoHistory != null)
            undoHistory.OnHistoryChanged -= RefreshInteractable;
    }

    private void ResolveHistory()
    {
        if (undoHistory == null)
            undoHistory = BuildUndoHistory.Instance;
    }

    private void HandleClick()
    {
        ResolveHistory();
        if (undoHistory != null)
            undoHistory.UndoLast();
    }

    private void RefreshInteractable()
    {
        if (button != null)
            button.interactable = undoHistory != null && undoHistory.Count > 0;
    }
}
