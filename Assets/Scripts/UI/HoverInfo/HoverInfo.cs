using System.Collections.Generic;

/// <summary>
/// Lo que se muestra de un objeto con el puntero encima: el nombre (va en el cartelito junto al
/// puntero y en la barra de abajo), su estado y las acciones que se pueden hacer con él, cada una con
/// el ícono de su control. Lo llena <see cref="HoverInfoResolver"/> y lo dibuja <see cref="HoverInfoLayer"/>.
/// </summary>
public class HoverInfo
{
    /// <summary>Hasta cuántas acciones entran en la barra.</summary>
    public const int MaxActions = 4;

    public readonly struct Action
    {
        public readonly HintPrompt Prompt;
        public readonly string Text;

        public Action(HintPrompt prompt, string text)
        {
            Prompt = prompt;
            Text = text;
        }
    }

    /// <summary>Nombre del objeto ("Carbón", "Vacío", "Cliente").</summary>
    public string Title;

    /// <summary>Estado en una línea ("Encendido · le quedan 40 s"). Puede ir vacío.</summary>
    public string Status;

    /// <summary>El estado es un aviso (se quema, ceniza, se va el cliente): va en otro color.</summary>
    public bool Warning;

    /// <summary>Se ve solo en la barra, sin el cartelito junto al puntero (mientras se arrastra algo).</summary>
    public bool HideCursorLabel;

    public readonly List<Action> Actions = new List<Action>(MaxActions);

    public bool IsEmpty => string.IsNullOrEmpty(Title);

    public void Clear()
    {
        Title = null;
        Status = null;
        Warning = false;
        HideCursorLabel = false;
        Actions.Clear();
    }

    public void Add(HintInput input, string textKey) => Add(new HintPrompt(input), textKey);

    public void Add(GameAction action, string textKey) => Add(HintPrompt.For(action), textKey);

    public void Add(HintPrompt prompt, string textKey)
    {
        if (Actions.Count < MaxActions)
            Actions.Add(new Action(prompt, Loc.Get(textKey)));
    }

    /// <summary>Misma información (para no redibujar la barra cada frame si no cambió nada).</summary>
    public bool SameAs(HoverInfo other)
    {
        if (other == null || Title != other.Title || Status != other.Status || Warning != other.Warning
            || HideCursorLabel != other.HideCursorLabel || Actions.Count != other.Actions.Count)
            return false;

        for (int i = 0; i < Actions.Count; i++)
        {
            Action a = Actions[i];
            Action b = other.Actions[i];
            if (a.Text != b.Text || a.Prompt.input != b.Prompt.input || a.Prompt.action != b.Prompt.action)
                return false;
        }
        return true;
    }

    public void CopyFrom(HoverInfo other)
    {
        Title = other.Title;
        Status = other.Status;
        Warning = other.Warning;
        HideCursorLabel = other.HideCursorLabel;
        Actions.Clear();
        Actions.AddRange(other.Actions);
    }
}
