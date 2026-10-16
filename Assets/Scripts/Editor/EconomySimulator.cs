using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Simulador Monte Carlo de la economía de Parrilla 40. Lee los datos reales del proyecto
/// (cortes, precios, clientes y mejoras de la GameScene abierta y de los SO) y cobra cada
/// entrega con las reglas reales: <see cref="CookingDeliveryEvaluator"/> y
/// <see cref="RunEconomyEvaluator"/>. Lo que no se puede ejecutar fuera de la escena
/// (manos del jugador, calor de la parrilla) se modela con perfiles de habilidad.
/// Menú: Parripollo/Balance/Run Economy Simulation (con la GameScene abierta).
/// </summary>
public static class EconomySimulator
{
    // ── Perfiles de habilidad ────────────────────────────────────────────────

    public class Profile
    {
        public string name;
        public float reactionSeconds;    // demora media en arrancar un pedido
        public int parallelOrders;       // pedidos que maneja a la vez
        public float heatPerSlot;        // calor/s por celda que logra con su manejo del carbón
        public float cookNoise;          // desvío relativo del tiempo de cocción
        public float pExact, pOffOne;    // precisión del punto; el resto (sin raw/burnt) es desfase 2
        public float pRawOrBurnt;        // entrega cruda o quemada (strike)
        public float toppingError;       // error por topping pedido
        public float assemblySeconds;    // armado + entrega
        public float grillBurnWaste;     // corte que se le quema en la parrilla y va a la basura
        public float reuseLeftover;      // reaprovecha un corte cocido cuyo cliente se fue
        public float coalsOnGrill;       // carbones encendidos en promedio mientras cocina
        public float restockBuffer;      // compra sobre la demanda esperada
        public int learnNights;          // >0: arranca como Principiante y llega a Promedio en esas noches

        /// <summary>Perfil efectivo en la noche dada (solo cambia para el que aprende).</summary>
        public Profile At(int night)
        {
            if (learnNights <= 0) return this;
            float t = Mathf.Clamp01((night - 1f) / learnNights);
            Profile a = Profiles[0], b = Profiles[1];
            return new Profile
            {
                name = name,
                reactionSeconds = Mathf.Lerp(a.reactionSeconds, b.reactionSeconds, t),
                parallelOrders = Mathf.RoundToInt(Mathf.Lerp(a.parallelOrders, b.parallelOrders, t)),
                heatPerSlot = Mathf.Lerp(a.heatPerSlot, b.heatPerSlot, t),
                cookNoise = Mathf.Lerp(a.cookNoise, b.cookNoise, t),
                pExact = Mathf.Lerp(a.pExact, b.pExact, t),
                pOffOne = Mathf.Lerp(a.pOffOne, b.pOffOne, t),
                pRawOrBurnt = Mathf.Lerp(a.pRawOrBurnt, b.pRawOrBurnt, t),
                toppingError = Mathf.Lerp(a.toppingError, b.toppingError, t),
                assemblySeconds = Mathf.Lerp(a.assemblySeconds, b.assemblySeconds, t),
                grillBurnWaste = Mathf.Lerp(a.grillBurnWaste, b.grillBurnWaste, t),
                reuseLeftover = Mathf.Lerp(a.reuseLeftover, b.reuseLeftover, t),
                coalsOnGrill = Mathf.Lerp(a.coalsOnGrill, b.coalsOnGrill, t),
                restockBuffer = Mathf.Lerp(a.restockBuffer, b.restockBuffer, t),
            };
        }
    }

    public static readonly Profile[] Profiles =
    {
        new Profile { name = "Principiante", reactionSeconds = 6f, parallelOrders = 2, heatPerSlot = 2.5f, cookNoise = .25f,
            pExact = .40f, pOffOne = .35f, pRawOrBurnt = .10f, toppingError = .25f, assemblySeconds = 6f,
            grillBurnWaste = .12f, reuseLeftover = .2f, coalsOnGrill = 6f, restockBuffer = 1.25f },
        new Profile { name = "Promedio", reactionSeconds = 3.5f, parallelOrders = 3, heatPerSlot = 3.5f, cookNoise = .18f,
            pExact = .60f, pOffOne = .27f, pRawOrBurnt = .05f, toppingError = .12f, assemblySeconds = 4.5f,
            grillBurnWaste = .06f, reuseLeftover = .5f, coalsOnGrill = 5f, restockBuffer = 1.15f },
        new Profile { name = "Bueno", reactionSeconds = 2f, parallelOrders = 4, heatPerSlot = 4.5f, cookNoise = .12f,
            pExact = .78f, pOffOne = .17f, pRawOrBurnt = .02f, toppingError = .05f, assemblySeconds = 3.5f,
            grillBurnWaste = .03f, reuseLeftover = .7f, coalsOnGrill = 4.5f, restockBuffer = 1.1f },
        new Profile { name = "Experto", reactionSeconds = 1.2f, parallelOrders = 5, heatPerSlot = 5.5f, cookNoise = .08f,
            pExact = .90f, pOffOne = .09f, pRawOrBurnt = .005f, toppingError = .02f, assemblySeconds = 2.8f,
            grillBurnWaste = .01f, reuseLeftover = .9f, coalsOnGrill = 4f, restockBuffer = 1.05f },
    };

    /// <summary>Principiante que mejora hasta Promedio en 5 noches: mide si se puede salir del pozo.</summary>
    public static readonly Profile Learner = new Profile { name = "Aprendiz", learnNights = 5 };

    // ── Datos del proyecto ───────────────────────────────────────────────────

    class CutData
    {
        public MeatCutSO so;
        public float weight;
        public bool unlockNightTwo;
        public int slots;
    }

    class CustomerTypeData { public CustomerType type; public float weight; public float patienceMult; }

    class GameData
    {
        public List<CutData> cuts = new List<CutData>();
        public List<CustomerTypeData> types = new List<CustomerTypeData>();
        public List<ToppingSO> toppings = new List<ToppingSO>();
        public List<UpgradeSO> upgrades = new List<UpgradeSO>();
        public CoalSO coal;
        public RunDefeatConfigSO defeat;
        public AnimationCurve affluence;
        public float affluenceAvg = 1f;
        public float basePatience, dayDuration, toppingChance, startMoney, feedbackSeconds;
        public int maxSim, firstNight, perNight, maxNight, maxToppings, maxStrikes, coalStorageCap;
        public Dictionary<ItemDataSO, int> initialStock = new Dictionary<ItemDataSO, int>();
    }

    static GameData LoadData()
    {
        var d = new GameData();
        var cs = UnityEngine.Object.FindFirstObjectByType<CustomerSystem>(FindObjectsInactive.Include);
        if (cs == null) throw new InvalidOperationException("Abrí la GameScene antes de simular.");
        var so = new SerializedObject(cs);

        d.maxSim = so.FindProperty("maxSimultaneousCustomers").intValue;
        d.firstNight = so.FindProperty("customersFirstNight").intValue;
        d.perNight = so.FindProperty("customersAddedPerNight").intValue;
        d.maxNight = so.FindProperty("maximumCustomersPerNight").intValue;
        d.basePatience = so.FindProperty("basePatienceSeconds").floatValue;
        d.toppingChance = so.FindProperty("toppingOrderChance").floatValue;
        d.maxToppings = so.FindProperty("maxToppingsPerOrder").intValue;
        d.affluence = so.FindProperty("affluenceCurve").animationCurveValue;
        var nightTwoCut = so.FindProperty("nightTwoCut").objectReferenceValue as MeatCutSO;

        var cutsProp = so.FindProperty("availableOrderCuts");
        for (int i = 0; i < cutsProp.arraySize; i++)
        {
            var e = cutsProp.GetArrayElementAtIndex(i);
            var cut = e.FindPropertyRelative("cut").objectReferenceValue as MeatCutSO;
            float w = e.FindPropertyRelative("weight").floatValue;
            if (cut == null || w <= 0f) continue;
            d.cuts.Add(new CutData { so = cut, weight = w, unlockNightTwo = cut == nightTwoCut,
                slots = Mathf.Max(1, cut.GrillSpace.x * cut.GrillSpace.y) });
        }

        var prefabs = so.FindProperty("customerPrefabs");
        for (int i = 0; i < prefabs.arraySize; i++)
        {
            var e = prefabs.GetArrayElementAtIndex(i);
            d.types.Add(new CustomerTypeData
            {
                type = (CustomerType)e.FindPropertyRelative("type").intValue,
                weight = e.FindPropertyRelative("spawnWeight").floatValue,
                patienceMult = e.FindPropertyRelative("patienceMultiplier").floatValue
            });
        }
        // Mismo fallback que CustomerSystem.PickCustomerEntry: pesos en 0 = sorteo parejo.
        if (d.types.Sum(t => Mathf.Max(0f, t.weight)) <= 0.0001f)
            foreach (var t in d.types) t.weight = 1f;

        var clock = UnityEngine.Object.FindFirstObjectByType<DayClock>(FindObjectsInactive.Include);
        d.dayDuration = clock != null ? clock.DayDurationSeconds : 300f;

        var wallet = UnityEngine.Object.FindFirstObjectByType<PlayerWallet>(FindObjectsInactive.Include);
        d.startMoney = wallet != null ? new SerializedObject(wallet).FindProperty("startingMoney").floatValue : 1000f;

        var strikes = UnityEngine.Object.FindFirstObjectByType<StrikeSystem>(FindObjectsInactive.Include);
        if (strikes != null)
        {
            var sso = new SerializedObject(strikes);
            d.maxStrikes = sso.FindProperty("maxStrikes").intValue;
            d.defeat = sso.FindProperty("runDefeatConfig").objectReferenceValue as RunDefeatConfigSO;
        }
        if (d.maxStrikes <= 0) d.maxStrikes = 3;
        if (d.defeat == null) d.defeat = AssetDatabase.LoadAssetAtPath<RunDefeatConfigSO>("Assets/ScriptableObjects/RunDefeatConfig.asset");

        var cooler = UnityEngine.Object.FindFirstObjectByType<CoolerSystem>(FindObjectsInactive.Include);
        if (cooler != null)
        {
            var stock = new SerializedObject(cooler).FindProperty("initialStock");
            for (int i = 0; i < stock.arraySize; i++)
            {
                var e = stock.GetArrayElementAtIndex(i);
                var item = e.FindPropertyRelative("item").objectReferenceValue as ItemDataSO;
                int amount = e.FindPropertyRelative("amount").intValue;
                if (item != null && amount > 0) d.initialStock[item] = amount;
            }
        }
        d.coalStorageCap = CoolerSystem.CoalStorageCap;

        var catalog = AssetDatabase.LoadAssetAtPath<FoodCatalogSO>("Assets/ScriptableObjects/FoodCatalog.asset");
        d.toppings = catalog.GetAvailableToppings().Where(t => t != null).ToList();
        d.upgrades = catalog.GetAllUpgrades().Where(u => u != null).ToList();
        d.coal = AssetDatabase.LoadAssetAtPath<ShopConfigSO>("Assets/ScriptableObjects/ShopConfig.asset").coal;
        d.feedbackSeconds = CustomerFeedbackConfigSO.Instance.FeedbackDuration;

        const int samples = 64;
        float sum = 0f;
        for (int i = 0; i < samples; i++) sum += Mathf.Max(0f, d.affluence.Evaluate((i + 0.5f) / samples));
        d.affluenceAvg = sum / samples > 0.01f ? sum / samples : 1f;
        return d;
    }

    // ── Estado de la run ─────────────────────────────────────────────────────

    class RunState
    {
        public int night = 1;
        public float money;
        public Dictionary<MeatCutSO, int> stock = new Dictionary<MeatCutSO, int>();
        public int coal;
        public Dictionary<UpgradeSO, int> levels = new Dictionary<UpgradeSO, int>();
        public int strikeNightsInRow;
        public int Level(UpgradeSO u) => levels.TryGetValue(u, out int l) ? l : 0;
        public int StreakPerfect; // racha de entregas perfectas, para las reglas que la usen
    }

    public class NightResult
    {
        public int night;
        public float sales, tips, cogsMeat, cogsCoal, waste, purchases, upgradesSpent, moneyStart, moneyEnd;
        public int arrived, served, lostPatience, lostNoSeat, badCooking, wrongish, withTip, strikes, cutsWasted;
        public bool endedByStrikes;
        public float maxTip, minTipPositive = float.MaxValue;
        public Dictionary<string, float[]> tipByType = new Dictionary<string, float[]>();  // {sum tips, n, sum sales}
        public Dictionary<string, float[]> tipByOrder = new Dictionary<string, float[]>();
        public float OperatingProfit => sales + tips - cogsMeat - cogsCoal;
    }

    class Cust
    {
        public Customer c;
        public float arrive, leaveAt = -1f, jobStart = -1f, readyAt = -1f;
        public bool done;
        public MeatCutSO cut;
        public bool tipVoided;
    }

    // ── Una noche ────────────────────────────────────────────────────────────

    static NightResult SimulateNight(GameData d, Profile p, RunState s, System.Random rng)
    {
        var r = new NightResult { night = s.night, moneyStart = s.money };
        s.StreakPerfect = 0; // DayStats.ResetDay
        int target = Mathf.Min(d.firstNight + (s.night - 1) * d.perNight, Mathf.Max(d.firstNight, d.maxNight));
        int seats = d.maxSim + d.upgrades.Where(u => u.effectType == UpgradeEffectType.MaxSimultaneousCustomers)
                                         .Sum(u => s.Level(u) * Mathf.Max(1, u.customersPerLevel));
        float patienceMult = 1f + d.upgrades.Where(u => u.effectType == UpgradeEffectType.CustomerPatience)
                                            .Sum(u => s.Level(u) * u.patienceBonusPerLevel);
        float tipMult = 1f + d.upgrades.Where(u => u.effectType == UpgradeEffectType.TipPercent)
                                       .Sum(u => s.Level(u) * u.tipBonusPerLevel);
        float burnTime = d.coal.maxBurnTime;
        foreach (var u in d.upgrades)
            if (u.effectType == UpgradeEffectType.CoalBurnTime && s.Level(u) > 0 && u.upgradedMaxBurnTime > 0f)
                burnTime = u.upgradedMaxBurnTime;

        var unlocked = d.cuts.Where(c => !c.unlockNightTwo || s.night >= 2).ToList();
        float interval = Mathf.Max(0.1f, d.dayDuration / Mathf.Max(1, target));

        const float dt = 0.5f;
        var slots = new Cust[seats];
        var feedbackUntil = new float[seats];
        float nextSpawn = NextDelay(d, interval, 0f);
        int strikes = 0;
        bool spawnBlocked = false;
        float coalBudget = s.coal; // unidades de carbón disponibles
        float coalUsed = 0f;
        int activeJobs = 0;
        var queue = new List<Cust>();

        for (float t = 0f; ; t += dt)
        {
            bool doorsOpen = t < d.dayDuration && !spawnBlocked;
            bool anyone = slots.Any(x => x != null) || feedbackUntil.Any(f => f > t);
            if (!doorsOpen && !anyone) break;
            if (t > d.dayDuration * 3f) break;

            // Llegadas
            if (doorsOpen && t >= nextSpawn)
            {
                nextSpawn = t + NextDelay(d, interval, t / d.dayDuration);
                int free = -1;
                for (int i = 0; i < seats; i++) if (slots[i] == null && feedbackUntil[i] <= t) { free = i; break; }
                r.arrived++;
                if (free < 0) r.lostNoSeat++;
                else
                {
                    var type = Pick(d.types, x => x.weight, rng);
                    var order = MakeOrder(d, unlocked, rng);
                    var c = new Customer();
                    c.Init(type.type, order, d.basePatience * type.patienceMult * patienceMult, free);
                    var cu = new Cust { c = c, arrive = t, cut = order.PrimaryCut };
                    slots[free] = cu;
                    queue.Add(cu);
                }
            }

            // El jugador arranca pedidos hasta su capacidad
            for (int i = 0; i < queue.Count && activeJobs < p.parallelOrders; i++)
            {
                var cu = queue[i];
                if (t < cu.arrive + p.reactionSeconds * (0.5f + (float)rng.NextDouble())) continue;
                if (!TryStartJob(d, p, s, cu, unlocked, rng, t, r)) continue;
                queue.RemoveAt(i--);
                activeJobs++;
            }

            // Paciencia, entregas
            for (int i = 0; i < seats; i++)
            {
                var cu = slots[i];
                if (cu == null) continue;
                cu.c.UpdatePatience(dt);

                if (cu.readyAt >= 0f && t >= cu.readyAt)
                {
                    Deliver(d, p, s, cu, tipMult, rng, r, ref strikes);
                    activeJobs--;
                    slots[i] = null;
                    feedbackUntil[i] = t + d.feedbackSeconds;
                    continue;
                }

                if (cu.c.IsAngry)
                {
                    r.lostPatience++;
                    strikes++;
                    s.StreakPerfect = 0;
                    if (cu.jobStart >= 0f)
                    {
                        activeJobs--;
                        if (rng.NextDouble() >= p.reuseLeftover) { r.cutsWasted++; r.waste += cu.cut.basePrice; }
                        else s.stock[cu.cut] = s.stock.TryGetValue(cu.cut, out int st) ? st + 1 : 1;
                    }
                    queue.Remove(cu);
                    slots[i] = null;
                }
            }

            // Carbón: los encendidos se consumen mientras haya algo cocinando
            if (activeJobs > 0)
            {
                float use = p.coalsOnGrill * dt / burnTime;
                coalUsed += use;
                if (coalUsed > coalBudget)
                {
                    // Sin carbón no se cocina: los pedidos en curso se frenan.
                    for (int i = 0; i < seats; i++)
                        if (slots[i] != null && slots[i].readyAt >= 0f) slots[i].readyAt += dt;
                }
            }

            if (strikes >= d.maxStrikes && !spawnBlocked) { spawnBlocked = true; r.endedByStrikes = true; }
        }

        int coalUnits = Mathf.Min(s.coal, Mathf.CeilToInt(coalUsed));
        s.coal -= coalUnits;
        r.cogsCoal = coalUnits * d.coal.basePrice / Mathf.Max(1, d.coal.unitsPerBag);
        r.strikes = strikes;
        s.strikeNightsInRow = r.endedByStrikes ? s.strikeNightsInRow + 1 : 0;
        return r;
    }

    static bool TryStartJob(GameData d, Profile p, RunState s, Cust cu, List<CutData> unlocked, System.Random rng, float t, NightResult r)
    {
        MeatCutSO cut = cu.c.order.PrimaryCut;
        if (!s.stock.TryGetValue(cut, out int st) || st <= 0)
        {
            // Mismo flujo que GameManager → TriggerMissingCutChange: otro corte en stock, propina anulada.
            var sub = unlocked.Select(x => x.so).FirstOrDefault(x => s.stock.TryGetValue(x, out int n) && n > 0);
            if (sub == null) return false;
            cu.c.order.SetSingleCut(sub, cu.c.order.GetRequestedState(0));
            bool sandwich = sub.servingMode == ServingMode.SandwichOnly || (sub.servingMode == ServingMode.Both && cu.c.order.IsSandwich);
            cu.c.order.bread = sandwich ? sub.requiredBread : null;
            if (cu.c.order.IsSandwich) cu.c.order.toppings.Clear();
            cu.c.IsTipAnulada = true;
            cut = sub;
            t += d.feedbackSeconds;
        }

        var cd = d.cuts.First(x => x.so == cut);
        s.stock[cut] = s.stock[cut] - 1;
        r.cogsMeat += cut.basePrice;
        cu.cut = cut;

        float band = cut.GetStateBandHeat();
        float targetHeat = band * ((int)cu.c.order.GetRequestedState(0) + 0.5f);
        float cook = 2f * targetHeat / (p.heatPerSlot * cd.slots);
        cook *= Mathf.Max(0.5f, 1f + p.cookNoise * Gauss(rng));

        // Se le quema en la parrilla: a la basura y vuelve a empezar.
        while (rng.NextDouble() < p.grillBurnWaste && s.stock.TryGetValue(cut, out int left) && left > 0)
        {
            s.stock[cut] = left - 1;
            r.cogsMeat += cut.basePrice;
            r.waste += cut.basePrice;
            r.cutsWasted++;
            cook *= 2f;
        }

        float assembly = p.assemblySeconds + cu.c.order.toppings.Count * 1.5f;
        cu.jobStart = t;
        cu.readyAt = t + cook + assembly;
        return true;
    }

    static void Deliver(GameData d, Profile p, RunState s, Cust cu, float tipMult, System.Random rng, NightResult r, ref int strikes)
    {
        r.served++;
        Order order = cu.c.order;
        MeatCutSO cut = order.PrimaryCut;
        string orderKey = (order.IsSandwich ? "Sándwich de " : "") + cut.itemName + (order.toppings.Count > 0 ? " + toppings" : "");
        string typeKey = cu.c.type.ToString();

        if (rng.NextDouble() < p.pRawOrBurnt)
        {
            // Crudo/Quemado: no paga, strike (GameManager.EvaluateDelivery → causesStrike).
            r.badCooking++;
            strikes++;
            s.StreakPerfect = 0;
            Accumulate(r.tipByType, typeKey, 0f, 0f);
            Accumulate(r.tipByOrder, orderKey, 0f, 0f);
            return;
        }

        MeatStates requested = order.GetRequestedState(0);
        double roll = rng.NextDouble();
        int offset = roll < p.pExact ? 0 : roll < p.pExact + p.pOffOne ? 1 : 2;
        MeatStates delivered = Shift(requested, offset, rng);

        float basePrice = order.IsSandwich ? cut.sellPriceSandwich : cut.sellPricePlate;
        var cutResult = CookingDeliveryEvaluator.EvaluateCut(delivered, delivered, requested, basePrice);
        float payment = cutResult.price;
        float unreduced = cutResult.worstOffset < 2 ? cutResult.price : 0f;
        int worst = cutResult.worstOffset;

        var assembled = new List<ToppingSO>();
        foreach (var top in order.toppings) if (rng.NextDouble() >= p.toppingError) assembled.Add(top);
        var extras = CookingDeliveryEvaluator.EvaluateExtras(order.toppings, assembled, order.IsSandwich, order.IsSandwich);
        if (extras.offset > worst) worst = extras.offset;
        if (extras.offset >= 2) payment = (payment - unreduced) + CookingDeliveryEvaluator.ApplyReducedPrice(unreduced);
        if (worst > 0) r.wrongish++;

        float tip = Tip(cu.c, basePrice, worst, tipMult, s, out _);
        if (worst == 0 && !cu.c.IsTipAnulada) s.StreakPerfect++; else s.StreakPerfect = 0;

        r.sales += payment;
        r.tips += tip;
        if (tip > 0f) { r.withTip++; r.maxTip = Mathf.Max(r.maxTip, tip); r.minTipPositive = Mathf.Min(r.minTipPositive, tip); }
        Accumulate(r.tipByType, typeKey, tip, payment);
        Accumulate(r.tipByOrder, orderKey, tip, payment);
    }

    /// <summary>Único punto donde el simulador pide la propina: usa la regla real del juego.</summary>
    static float Tip(Customer c, float basePrice, int worstOffset, float tipMult, RunState s, out CustomerFeedbackState state)
    {
        if (UseLegacyTips)
        {
            state = CustomerFeedbackState.SinPropina;
            return LegacyTip(c, basePrice, worstOffset, tipMult);
        }

        var eval = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(c, basePrice, worstOffset, tipMult, s.StreakPerfect);
        state = eval.state;
        return eval.tipAmount;
    }

    /// <summary>Simula con la regla de propina anterior al rebalanceo, para el informe "antes".</summary>
    public static bool UseLegacyTips;

    /// <summary>Copia textual de la regla de propina previa (commit 80bd892, CookingDeliveryEvaluator).</summary>
    static float LegacyTip(Customer customer, float basePrice, int worstOffset, float tipMultiplier)
    {
        tipMultiplier = Mathf.Max(0f, tipMultiplier);
        if (customer == null || customer.IsTipAnulada) return 0f;
        if (worstOffset == 0)
        {
            if (customer.type == CustomerType.Turista) return Mathf.Max(1f, Mathf.Round(basePrice * 0.20f * tipMultiplier));
            if (customer.Patience01 >= 0.5f) return Mathf.Max(1f, Mathf.Round(basePrice * 0.10f * tipMultiplier));
            return Mathf.Max(1f, Mathf.Round(basePrice * 0.05f * tipMultiplier));
        }
        if (worstOffset == 1 && customer.Patience01 >= 0.3f)
            return Mathf.Max(1f, Mathf.Round(basePrice * 0.05f * tipMultiplier));
        return 0f;
    }

    // ── Tienda entre noches ──────────────────────────────────────────────────

    static void Shop(GameData d, Profile p, RunState s, NightResult r)
    {
        int nextNight = s.night + 1;
        var unlocked = d.cuts.Where(c => !c.unlockNightTwo || nextNight >= 2).ToList();
        float totalW = unlocked.Sum(c => c.weight);
        float before = s.money;

        // Primero el carbón: sin brasas no se cocina nada y es parte del mínimo de la run.
        float burnTime = d.coal.maxBurnTime;
        foreach (var u in d.upgrades)
            if (u.effectType == UpgradeEffectType.CoalBurnTime && s.Level(u) > 0 && u.upgradedMaxBurnTime > 0f)
                burnTime = u.upgradedMaxBurnTime;
        int coalNeed = Mathf.CeilToInt(p.coalsOnGrill * d.dayDuration * 0.9f / burnTime) + 2;
        coalNeed = Mathf.Max(coalNeed, d.defeat != null ? d.defeat.GetCoalRequirement(nextNight) : 0);
        int perBag = Mathf.Max(1, d.coal.unitsPerBag);
        if (d.coalStorageCap > 0) coalNeed = Mathf.Min(coalNeed, d.coalStorageCap);
        while (s.coal < coalNeed && s.money >= d.coal.basePrice) { s.coal += perBag; s.money -= d.coal.basePrice; }
        if (d.coalStorageCap > 0) s.coal = Mathf.Min(s.coal, d.coalStorageCap);

        // Después la carne: lo que usó anoche (atendidos + desperdicio), con el colchón del perfil.
        // Nunca menos que el mínimo de carne de la regla de derrota.
        float used = r.served + r.cutsWasted + r.lostPatience + 1;
        int meatMin = d.defeat != null ? d.defeat.GetMeatRequirement(nextNight) : 0;
        foreach (var c in unlocked)
        {
            float share = c.weight / totalW;
            int want = Mathf.Max(Mathf.CeilToInt(used * share * p.restockBuffer), Mathf.CeilToInt(meatMin * share));
            int have = s.stock.TryGetValue(c.so, out int n) ? n : 0;
            int buy = Mathf.Max(0, want - have);
            int affordable = c.so.basePrice > 0f ? Mathf.FloorToInt(s.money / c.so.basePrice) : buy;
            buy = Mathf.Min(buy, affordable);
            s.stock[c.so] = have + buy;
            s.money -= buy * c.so.basePrice;
        }
        r.purchases = before - s.money;

        // Mejoras: compra la más barata disponible si después le queda un colchón para reponer.
        float reserve = unlocked.Sum(c => c.so.basePrice * used * c.weight / totalW) * 0.5f + d.coal.basePrice;
        bool bought = true;
        while (bought)
        {
            bought = false;
            foreach (var u in d.upgrades.Where(u => u.isUnlocked && s.Level(u) < u.MaxLevel).OrderBy(u => u.basePrice))
            {
                // Más mesas solo si la noche anterior no se le escapó nadie por paciencia:
                // un jugador desbordado no suma clientes que no puede atender.
                if (u.effectType == UpgradeEffectType.MaxSimultaneousCustomers && (r.lostPatience > 0 || r.endedByStrikes)) continue;
                if (s.money - u.basePrice < reserve) continue;
                s.money -= u.basePrice;
                s.levels[u] = s.Level(u) + 1;
                r.upgradesSpent += u.basePrice;
                bought = true;
                break;
            }
        }
    }

    // ── Escenarios ───────────────────────────────────────────────────────────

    public class RunSummary
    {
        public Profile profile;
        public List<NightResult>[] nights;  // por noche, todas las corridas
        public int defeats, defeatsByMoney, defeatsByStrikes;
        public int[] defeatNight;
        public Dictionary<string, List<int>> nightUpgradeBought = new Dictionary<string, List<int>>();
    }

    static RunSummary SimulateRuns(GameData d, Profile p, int runs, int nights, int seed)
    {
        var sum = new RunSummary { profile = p, nights = new List<NightResult>[nights], defeatNight = new int[nights + 1] };
        for (int n = 0; n < nights; n++) sum.nights[n] = new List<NightResult>();
        var rng = new System.Random(seed);

        for (int run = 0; run < runs; run++)
        {
            var s = new RunState { money = d.startMoney };
            foreach (var kv in d.initialStock)
            {
                if (kv.Key is MeatCutSO m) s.stock[m] = kv.Value;
                else if (kv.Key is CoalSO) s.coal += kv.Value;
            }

            for (int n = 1; n <= nights; n++)
            {
                s.night = n;
                Profile pn = p.At(n);
                var r = SimulateNight(d, pn, s, rng);
                s.money += r.sales + r.tips;

                var levelsBefore = new Dictionary<UpgradeSO, int>(s.levels);
                Shop(d, pn, s, r);
                foreach (var u in d.upgrades)
                {
                    if (s.Level(u) <= (levelsBefore.TryGetValue(u, out int lb) ? lb : 0)) continue;
                    string key = u.itemName + " Nv" + s.Level(u);
                    if (!sum.nightUpgradeBought.ContainsKey(key)) sum.nightUpgradeBought[key] = new List<int>();
                    sum.nightUpgradeBought[key].Add(n);
                }
                r.moneyEnd = s.money;
                sum.nights[n - 1].Add(r);

                // Derrota total: misma regla que ShopSystem.BuildRunStatus / RunEconomyEvaluator.
                var cheapest = d.cuts.Where(c => !c.unlockNightTwo || n + 1 >= 2).Min(c => c.so.basePrice);
                var status = RunEconomyEvaluator.Evaluate(n + 1, d.defeat, s.stock.Values.Sum(), s.coal, s.money,
                    cheapest, true, true, d.coal.basePrice, d.coal.unitsPerBag, d.coalStorageCap);
                bool strikeDefeat = d.defeat != null && d.defeat.strikeStreakDefeatEnabled
                                    && s.strikeNightsInRow >= d.defeat.maxConsecutiveStrikeNights;
                if (!status.CanContinue || strikeDefeat)
                {
                    sum.defeats++;
                    if (strikeDefeat) sum.defeatsByStrikes++; else sum.defeatsByMoney++;
                    sum.defeatNight[n]++;
                    break;
                }
            }
        }
        return sum;
    }

    /// <summary>
    /// Corre todos los perfiles y escribe el informe en <c>Docs/Balance/</c>.
    /// <paramref name="label"/> separa el informe de "antes" del de "después".
    /// </summary>
    public static string RunAll(string label, int singleNights = 10000, int runs = 2000, int nights = 15, int seed = 40)
    {
        var d = LoadData();
        var sb = new StringBuilder();
        var summaries = new List<RunSummary>();
        var night1 = new List<RunSummary>();
        var night6 = new List<RunSummary>();

        foreach (var p in Profiles)
        {
            night1.Add(SimulateFixedNight(d, p, 1, singleNights, seed));
            night6.Add(SimulateFixedNight(d, p, 6, singleNights, seed + 1));
            summaries.Add(SimulateRuns(d, p, runs, nights, seed + 2));
        }
        summaries.Insert(1, SimulateRuns(d, Learner, runs, nights, seed + 3));

        sb.AppendLine($"# Simulación económica — {label}");
        sb.AppendLine();
        sb.AppendLine($"Generado {DateTime.Now:yyyy-MM-dd HH:mm}. Semilla {seed}. {singleNights} noches aisladas por perfil y escenario; {runs} runs de {nights} noches por perfil.");
        sb.AppendLine();
        sb.AppendLine($"Datos leídos: {d.firstNight} clientes la noche 1 (+{d.perNight}/noche, tope {d.maxNight}), {d.maxSim} lugares, jornada {d.dayDuration}s, paciencia base {d.basePatience}s, dinero inicial ${d.startMoney}.");
        sb.AppendLine();
        sb.AppendLine("Cortes: " + string.Join(" · ", d.cuts.Select(c => $"{c.so.itemName} costo ${c.so.basePrice} / plato ${c.so.sellPricePlate} / sándwich ${c.so.sellPriceSandwich} (peso {c.weight}{(c.unlockNightTwo ? ", noche 2+" : "")})")));
        sb.AppendLine();
        sb.AppendLine("Mejoras: " + string.Join(" · ", d.upgrades.Select(u => $"{u.itemName} ${u.basePrice} x{u.MaxLevel} ({u.effectType})")));
        sb.AppendLine();

        WriteFixedNightTable(sb, "Noche 1 aislada (sin mejoras, stock suficiente)", night1);
        WriteFixedNightTable(sb, "Noche 6 aislada (sin mejoras, stock suficiente)", night6);
        WriteTipBreakdown(sb, night6);
        WriteRunTables(sb, summaries, nights);

        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Docs", "Balance");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "economy_sim_" + label + ".md");
        File.WriteAllText(file, sb.ToString());
        return file + "\n\n" + sb;
    }

    static RunSummary SimulateFixedNight(GameData d, Profile p, int night, int count, int seed)
    {
        var sum = new RunSummary { profile = p, nights = new[] { new List<NightResult>() } };
        var rng = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            var s = new RunState { night = night, money = 0f, coal = 999 };
            foreach (var c in d.cuts) s.stock[c.so] = 999;
            var r = SimulateNight(d, p, s, rng);
            sum.nights[0].Add(r);
        }
        return sum;
    }

    // ── Informe ──────────────────────────────────────────────────────────────

    static void WriteFixedNightTable(StringBuilder sb, string title, List<RunSummary> list)
    {
        sb.AppendLine("## " + title);
        sb.AppendLine();
        sb.AppendLine("| Métrica | " + string.Join(" | ", list.Select(x => x.profile.name)) + " |");
        sb.AppendLine("|---|" + string.Join("|", list.Select(_ => "---:")) + "|");
        Row(sb, "Clientes llegados", list, n => n.arrived);
        Row(sb, "Atendidos", list, n => n.served);
        Row(sb, "Perdidos por paciencia", list, n => n.lostPatience);
        Row(sb, "Perdidos sin lugar", list, n => n.lostNoSeat);
        Row(sb, "Entregas crudas/quemadas", list, n => n.badCooking);
        Row(sb, "Entregas imperfectas", list, n => n.wrongish);
        Row(sb, "Cortes desperdiciados", list, n => n.cutsWasted);
        Row(sb, "Ventas $", list, n => n.sales);
        Row(sb, "Propinas $", list, n => n.tips);
        Row(sb, "Costo carne consumida $", list, n => n.cogsMeat);
        Row(sb, "Costo carbón consumido $", list, n => n.cogsCoal);
        Row(sb, "Beneficio operativo $", list, n => n.OperatingProfit);
        RowRatio(sb, "Propinas / ventas", list, n => n.tips, n => n.sales);
        RowRatio(sb, "Propinas / beneficio operativo", list, n => n.tips, n => n.OperatingProfit);
        RowRatio(sb, "Margen operativo / ingresos", list, n => n.OperatingProfit, n => n.sales + n.tips);
        RowRatio(sb, "% entregas con propina", list, n => n.withTip, n => n.served);
        RowRatio(sb, "Propina media por cliente atendido $", list, n => n.tips, n => n.served, false);
        sb.AppendLine("| Propina máx. $ | " + string.Join(" | ", list.Select(x => x.nights[0].Max(n => n.maxTip).ToString("0"))) + " |");
        sb.AppendLine("| Propina mín. (>0) $ | " + string.Join(" | ", list.Select(x =>
        {
            var v = x.nights[0].Where(n => n.withTip > 0).Select(n => n.minTipPositive);
            return v.Any() ? v.Min().ToString("0") : "-";
        })) + " |");
        sb.AppendLine("| Noches cerradas por strikes | " + string.Join(" | ", list.Select(x => (x.nights[0].Count(n => n.endedByStrikes) * 100f / x.nights[0].Count).ToString("0.0") + "%")) + " |");
        sb.AppendLine();
    }

    static void WriteTipBreakdown(StringBuilder sb, List<RunSummary> list)
    {
        sb.AppendLine("## Propinas por tipo de cliente y de pedido (noche 6)");
        sb.AppendLine();
        foreach (string kind in new[] { "type", "order" })
        {
            var keys = list.SelectMany(x => x.nights[0]).SelectMany(n => (kind == "type" ? n.tipByType : n.tipByOrder).Keys).Distinct().OrderBy(k => k).ToList();
            sb.AppendLine("| " + (kind == "type" ? "Cliente" : "Pedido") + " (propina media $ · % sobre lo cobrado) | " + string.Join(" | ", list.Select(x => x.profile.name)) + " |");
            sb.AppendLine("|---|" + string.Join("|", list.Select(_ => "---:")) + "|");
            foreach (var k in keys)
            {
                sb.Append("| " + k + " | ");
                sb.Append(string.Join(" | ", list.Select(x =>
                {
                    float tips = 0, n = 0, sales = 0;
                    foreach (var night in x.nights[0])
                    {
                        var dict = kind == "type" ? night.tipByType : night.tipByOrder;
                        if (dict.TryGetValue(k, out var a)) { tips += a[0]; n += a[1]; sales += a[2]; }
                    }
                    return n > 0 ? $"{tips / n:0.0} · {(sales > 0 ? tips / sales * 100f : 0f):0.0}%" : "-";
                })));
                sb.AppendLine(" |");
            }
            sb.AppendLine();
        }
    }

    static void WriteRunTables(StringBuilder sb, List<RunSummary> list, int nights)
    {
        sb.AppendLine("## Progresión: runs completas (con compras y mejoras)");
        sb.AppendLine();
        sb.AppendLine("Estrategia de tienda igual para todos: primero compra el carbón necesario, después repone la carne que usó la noche anterior (con el colchón del perfil, nunca menos que el mínimo de la run) y compra la mejora más barata si después le queda reserva para reponer media noche (mesas extra solo si la noche anterior no perdió clientes por paciencia).");
        sb.AppendLine();
        sb.AppendLine("| Perfil | Derrotas | por plata | por strikes | Noche media de derrota |");
        sb.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var x in list)
        {
            int runs = x.nights[0].Count;
            float avgDefeat = x.defeats > 0 ? (float)Enumerable.Range(1, nights).Sum(n => n * x.defeatNight[n]) / x.defeats : 0f;
            sb.AppendLine($"| {x.profile.name} | {x.defeats * 100f / runs:0.0}% | {x.defeatsByMoney * 100f / runs:0.0}% | {x.defeatsByStrikes * 100f / runs:0.0}% | {(x.defeats > 0 ? avgDefeat.ToString("0.0") : "-")} |");
        }
        sb.AppendLine();

        foreach (string metric in new[] { "Dinero al cierre (tras compras) $", "Ventas $", "Propinas $", "Beneficio operativo $", "Propinas / beneficio" })
        {
            sb.AppendLine("### " + metric + " por noche (media de runs vivas)");
            sb.AppendLine();
            sb.AppendLine("| Noche | " + string.Join(" | ", list.Select(x => x.profile.name)) + " |");
            sb.AppendLine("|---:|" + string.Join("|", list.Select(_ => "---:")) + "|");
            for (int n = 0; n < nights; n++)
            {
                if (n > 4 && n % 2 == 1 && n != nights - 1) continue;
                sb.Append($"| {n + 1} | ");
                sb.Append(string.Join(" | ", list.Select(x =>
                {
                    var l = x.nights[n];
                    if (l.Count == 0) return "-";
                    switch (metric)
                    {
                        case "Ventas $": return l.Average(r => r.sales).ToString("0");
                        case "Propinas $": return l.Average(r => r.tips).ToString("0");
                        case "Beneficio operativo $": return l.Average(r => r.OperatingProfit).ToString("0");
                        case "Propinas / beneficio":
                            float op = l.Sum(r => r.OperatingProfit);
                            return op > 0 ? (l.Sum(r => r.tips) / op * 100f).ToString("0") + "%" : "beneficio ≤ 0";
                        default: return l.Average(r => r.moneyEnd).ToString("0");
                    }
                })));
                sb.AppendLine(" |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("### Noche media en que se compra cada mejora (runs que la compran)");
        sb.AppendLine();
        var keys = list.SelectMany(x => x.nightUpgradeBought.Keys).Distinct().OrderBy(k => k).ToList();
        sb.AppendLine("| Mejora | " + string.Join(" | ", list.Select(x => x.profile.name)) + " |");
        sb.AppendLine("|---|" + string.Join("|", list.Select(_ => "---:")) + "|");
        foreach (var k in keys)
        {
            sb.AppendLine("| " + k + " | " + string.Join(" | ", list.Select(x =>
            {
                if (!x.nightUpgradeBought.TryGetValue(k, out var v)) return "nunca";
                return $"{v.Average():0.0} ({v.Count * 100f / x.nights[0].Count:0}%)";
            })) + " |");
        }
        sb.AppendLine();
    }

    static void Row(StringBuilder sb, string name, List<RunSummary> list, Func<NightResult, float> f)
    {
        sb.AppendLine("| " + name + " | " + string.Join(" | ", list.Select(x => x.nights[0].Average(f).ToString("0.0"))) + " |");
    }

    static void RowRatio(StringBuilder sb, string name, List<RunSummary> list, Func<NightResult, float> num, Func<NightResult, float> den, bool pct = true)
    {
        sb.AppendLine("| " + name + " | " + string.Join(" | ", list.Select(x =>
        {
            float a = x.nights[0].Sum(num), b = x.nights[0].Sum(den);
            if (b <= 0f) return pct ? "beneficio ≤ 0" : "-";
            return pct ? (a / b * 100f).ToString("0.0") + "%" : (a / b).ToString("0.0");
        })) + " |");
    }

    // ── Utilidades ───────────────────────────────────────────────────────────

    static Order MakeOrder(GameData d, List<CutData> unlocked, System.Random rng)
    {
        // Mismo reparto que OrderSystem.GenerateOrder.
        var order = new Order();
        var cut = Pick(unlocked, c => c.weight, rng).so;
        order.SetSingleCut(cut, (MeatStates)rng.Next((int)MeatStates.Jugoso, (int)MeatStates.Pasado + 1));
        bool sandwich = cut.servingMode == ServingMode.SandwichOnly || (cut.servingMode == ServingMode.Both && rng.NextDouble() < 0.5);
        if (sandwich && cut.requiredBread != null) order.bread = cut.requiredBread;
        if (!order.IsSandwich && d.toppings.Count > 0 && d.maxToppings > 0 && rng.NextDouble() < d.toppingChance)
        {
            int count = rng.Next(1, Mathf.Min(d.maxToppings, d.toppings.Count) + 1);
            foreach (var top in d.toppings.OrderBy(_ => rng.Next()).Take(count)) order.toppings.Add(top);
        }
        return order;
    }

    static float NextDelay(GameData d, float interval, float t01)
    {
        float m = Mathf.Max(0.05f, d.affluence.Evaluate(Mathf.Clamp01(t01)) / d.affluenceAvg);
        return interval / m;
    }

    static MeatStates Shift(MeatStates requested, int offset, System.Random rng)
    {
        if (offset == 0) return requested;
        int lo = (int)MeatStates.Jugoso, hi = (int)MeatStates.Pasado;
        int up = (int)requested + offset, down = (int)requested - offset;
        bool canUp = up <= hi, canDown = down >= lo;
        if (canUp && canDown) return (MeatStates)(rng.NextDouble() < 0.5 ? up : down);
        if (canUp) return (MeatStates)up;
        if (canDown) return (MeatStates)down;
        return (MeatStates)(offset > 0 ? hi : lo);
    }

    static T Pick<T>(List<T> list, Func<T, float> weight, System.Random rng)
    {
        float total = list.Sum(x => Mathf.Max(0f, weight(x)));
        double r = rng.NextDouble() * total, acc = 0;
        foreach (var x in list) { acc += Mathf.Max(0f, weight(x)); if (r <= acc) return x; }
        return list[list.Count - 1];
    }

    static float Gauss(System.Random rng)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
    }

    static void Accumulate(Dictionary<string, float[]> dict, string key, float tip, float sale)
    {
        if (!dict.TryGetValue(key, out var a)) dict[key] = a = new float[3];
        a[0] += tip; a[1] += 1f; a[2] += sale;
    }

    [MenuItem("Parripollo/Balance/Run Economy Simulation")]
    static void RunFromMenu() => Debug.Log(RunAll("manual"));
}
