using UnityEngine;

[CreateAssetMenu(fileName = "StrikeConfig", menuName = "Parrilla/Strike Config")]
public class StrikeConfigSO : ScriptableObject
{
    [Header("Configuration")]
    [Min(1)] public int maxStrikes = 3;
    [Min(0.1f)] public float gameplayNoticeDuration = 5f;

    [Header("Gameplay Notice (Te clavaron el cartel)")]
    public string noticeTitle = "¡Te clavaron el cartel!";
    [TextArea(2, 4)]
    public string noticeBody = "Se te fueron tres clientes con una calentura bárbara y corrieron la voz. Por hoy no para nadie más. Cerrá la parrilla y mañana será otro día.";

    [Header("Shop Closure Popup")]
    public string popupTitle = "Se terminó el día antes de tiempo";
    [TextArea(3, 6)]
    public string popupBody = "Llegaste al límite de clientes perdidos de la jornada.\n\nCuando tres clientes se van por falta de paciencia, corren la voz y dejan de llegar nuevos clientes.\n\nAtendiste a los que quedaban, pero por hoy no pasa nadie más. Mañana arrancás de nuevo.";
    public string popupButtonText = "Ir a la tienda";
}