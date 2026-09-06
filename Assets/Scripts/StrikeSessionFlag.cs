using UnityEngine;

public class StrikeSessionFlag : MonoBehaviour
{
    public static StrikeSessionFlag Instance { get; private set; }

    public bool ClosedByStrikes { get; private set; } = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void MarkClosedByStrikes() => ClosedByStrikes = true;

    /// <summary>Debe llamarse al arrancar una nueva jornada (Start de GameScene).</summary>
    public void Reset() => ClosedByStrikes = false;
}