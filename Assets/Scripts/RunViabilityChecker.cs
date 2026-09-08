using UnityEngine;
using UnityEngine.SceneManagement;

public class RunViabilityChecker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopSystem shop;
    [SerializeField] private ShopConfigSO config;

    [Header("Scene")]
    [SerializeField] private string gameOverSceneName = "GameOverScene";

    void OnEnable()
    {
        if (shop != null)
        {
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged += CheckViability;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged += _ => CheckViability();
        }
    }

    void OnDisable()
    {
        if (shop != null)
        {
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= CheckViability;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged -= _ => CheckViability();
        }
    }

    void Start() => CheckViability();

    private void CheckViability()
    {
        if (config == null || DayCounter.Instance == null || shop == null || shop.Wallet == null || shop.Cooler == null)
            return;

        int nextDay = DayCounter.Instance.CurrentDay + 1;
        int meatRequired = config.GetMeatMinimumForDay(nextDay);
        int coalRequired = config.GetCoalMinimumForDay(nextDay);

        int meatCurrent = GetCurrentMeat();
        int coalCurrent = GetCurrentCoal();

        int meatDeficit = Mathf.Max(0, meatRequired - meatCurrent);
        int coalDeficit = Mathf.Max(0, coalRequired - coalCurrent);

        if (meatDeficit == 0 && coalDeficit == 0) return;

        // Calcular costos aproximados para cubrir el déficit
        float meatCost = CalculateMinimumMeatCost(meatDeficit);
        float coalCost = CalculateMinimumCoalCost(coalDeficit);
        float totalCost = meatCost + coalCost;

        if (shop.Wallet.Money < totalCost)
        {
            Debug.Log($"[RunViability] Game Over: falta plata para cubrir mínimos. Necesita ${totalCost:F0}, tiene ${shop.Wallet.Money:F0}");
            TriggerGameOver();
        }
    }

    private int GetCurrentMeat()
    {
        int total = 0;
        foreach (var entry in shop.Cooler.EnumerateStock())
            if (entry.Key is MeatCutSO) total += entry.Value;
        return total;
    }

    private int GetCurrentCoal()
    {
        int total = 0;
        foreach (var entry in shop.Cooler.EnumerateStock())
            if (entry.Key is CoalSO) total += entry.Value;
        return total;
    }

    private float CalculateMinimumMeatCost(int cortesNecesarios)
    {
        if (cortesNecesarios <= 0) return 0f;

        // Usamos el corte más barato disponible desbloqueado
        float minPrice = float.MaxValue;
        var cuts = shop.Catalog?.GetUnlockedCuts();
        if (cuts == null) return 0f;

        foreach (var cut in cuts)
        {
            if (cut != null && cut.basePrice < minPrice)
                minPrice = cut.basePrice;
        }

        if (minPrice == float.MaxValue) return float.MaxValue;
        return minPrice * cortesNecesarios;
    }

    private float CalculateMinimumCoalCost(int unidadesNecesarias)
    {
        if (unidadesNecesarias <= 0) return 0f;
        if (config.coal == null) return float.MaxValue;

        // Precio por unidad = precio bolsa / unidades por bolsa
        int unitsPerBag = Mathf.Max(1, config.coal.unitsPerBag);
        float pricePerUnit = config.coal.basePrice / unitsPerBag;
        return pricePerUnit * unidadesNecesarias;
    }

    private void TriggerGameOver()
    {
        // Guardar contexto si querés que la GameOverScene lo lea
        SceneManager.LoadScene(gameOverSceneName);
    }
}