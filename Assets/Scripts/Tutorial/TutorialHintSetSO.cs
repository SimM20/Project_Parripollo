using System.Collections.Generic;
using UnityEngine;

/// <summary>Los carteles de una escena (la partida, la tienda), en orden: en empate de prioridad gana el primero.</summary>
[CreateAssetMenu(fileName = "NewTutorialHintSet", menuName = "Tutorial/Set de carteles")]
public class TutorialHintSetSO : ScriptableObject
{
    public List<TutorialHintSO> hints = new List<TutorialHintSO>();
}
