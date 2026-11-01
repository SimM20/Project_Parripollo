using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Simulador Monte Carlo de stock de carne y carbón de Parrilla 40.
///
/// A diferencia de <see cref="EconomySimulator"/> (que modela el carbón como un gasto fijo por
/// segundo de cocción), acá la parrilla se simula con sus reglas reales:
/// <list type="bullet">
/// <item>la grilla real de la GameScene (celdas de carbón y de carne con sus coordenadas);</item>
/// <item>cada brasa se quema desde que se apoya hasta hacerse ceniza, haya o no carne arriba
///   (<c>GridSlot.Update → Coal.Burn</c>), y da <c>6,5 × vida restante</c> de calor
///   (<c>Coal.GetCurrentHeatOutput</c>);</item>
/// <item>apiladas, cuentan 100% / 30,7% / 15,3% según su lugar en la pila
///   (<c>GridSlot.CalculateInternalHeat</c>), y la ceniza ocupa lugar hasta que se limpia;</item>
/// <item>el calor pasa a las celdas de carne con los factores de <c>GrillSystem.UpdateHeatPropagation</c>
///   (tope 10 por celda);</item>
/// <item>cada corte cocina sus dos caras con las tasas del prefab de carne (<c>Meat.Cook</c>) y
///   se cobra con <see cref="CookingDeliveryEvaluator"/>;</item>
/// <item>la tienda, los mínimos y la derrota usan <see cref="RunEconomyEvaluator"/>.</item>
/// </list>
/// Las manos del jugador se modelan con perfiles. Todas las corridas usan semillas fijas.
/// Menú: Parripollo/Balance/Run Stock Simulation (con la GameScene abierta).
/// </summary>
public static class ResourceBalanceSimulator
{
    // ── Perfiles ─────────────────────────────────────────────────────────────

    public class Profile
    {
        public string name;
        public float reaction;          // s desde que llega el pedido hasta poner la carne
        public int maxParallel;         // cortes en la parrilla a la vez
        public float aimNoise;          // desvío relativo al apuntar el punto de cada cara
        public float actDelay;          // s medios entre ver el punto y dar vuelta / sacar
        public float forgetProb;        // por corte: se distrae 20-60 s (suele quemarse)
        public float assembly;          // s de armado y entrega
        public float extraError;        // error por topping o guarnición pedida
        public int coalPerSmallCut;     // brasas bajo un corte de 1-2 celdas
        public int coalPerBigCut;       // brasas bajo un corte de 4 celdas
        public int bedSize;             // brasas que mantiene prendidas toda la noche (0 = solo bajo la carne)
        public float cleanAshProb;      // limpia la ceniza antes de recargar una celda
        public float minLifeToReuse;    // vida mínima de una brasa para cocinar encima sin agregar otra
        public float refuelBelow;       // agrega brasa si el calor del corte cae por debajo de esta fracción del de una brasa nueva
        public float knowsSubstitution; // usa "corte faltante" cuando no hay stock
        public float reuseLeftover;     // aprovecha el corte de un cliente que se fue
        public bool trustsShopMinimum;  // compra el carbón que la tienda marca como "listo" (el mínimo del día)
        public float coalBuffer;        // compra carbón = consumo de anoche × esto
        public float meatBuffer;        // compra carne = consumo de anoche × esto
        public bool adversarialOrders;  // pedidos sesgados al corte con menos stock (mala racha)
    }

    public static readonly Profile Beginner = new Profile
    {
        name = "Principiante", reaction = 12f, maxParallel = 2, aimNoise = .22f, actDelay = 8f, forgetProb = .10f,
        assembly = 12f, extraError = .25f, coalPerSmallCut = 2, coalPerBigCut = 2, bedSize = 4, cleanAshProb = .2f,
        minLifeToReuse = .5f, refuelBelow = .6f, knowsSubstitution = .5f, reuseLeftover = .2f,
        trustsShopMinimum = true, coalBuffer = 1f, meatBuffer = 1f
    };

    public static readonly Profile Average = new Profile
    {
        name = "Promedio", reaction = 10f, maxParallel = 3, aimNoise = .16f, actDelay = 7f, forgetProb = .06f,
        assembly = 12f, extraError = .12f, coalPerSmallCut = 1, coalPerBigCut = 2, bedSize = 0, cleanAshProb = .6f,
        minLifeToReuse = .35f, refuelBelow = .4f, knowsSubstitution = .9f, reuseLeftover = .5f,
        coalBuffer = 1.2f, meatBuffer = 1.15f
    };

    public static readonly Profile Efficient = new Profile
    {
        name = "Eficiente", reaction = 4f, maxParallel = 4, aimNoise = .09f, actDelay = 3.5f, forgetProb = .02f,
        assembly = 6f, extraError = .05f, coalPerSmallCut = 1, coalPerBigCut = 2, bedSize = 0, cleanAshProb = 1f,
        minLifeToReuse = .25f, refuelBelow = .3f, knowsSubstitution = 1f, reuseLeftover = .8f,
        coalBuffer = 1.1f, meatBuffer = 1.1f
    };

    public static readonly Profile AverageUnlucky = new Profile
    {
        name = "Promedio + pedidos adversos", reaction = 10f, maxParallel = 3, aimNoise = .16f, actDelay = 7f, forgetProb = .06f,
        assembly = 12f, extraError = .12f, coalPerSmallCut = 1, coalPerBigCut = 2, bedSize = 0, cleanAshProb = .6f,
        minLifeToReuse = .35f, refuelBelow = .4f, knowsSubstitution = .9f, reuseLeftover = .5f,
        coalBuffer = 1.2f, meatBuffer = 1.15f, adversarialOrders = true
    };

    public static Profile[] AllProfiles => new[] { Beginner, Average, Efficient, AverageUnlucky };

    // ── Reglas (espejo del código; ver SelfCheck) ────────────────────────────

    public class Rules
    {
        public float coalPeakHeat = 6.5f;                          // Coal.GetCurrentHeatOutput
        public float[] stackWeights = { 1f, 0.307f, 0.153f };      // GridSlot.CalculateInternalHeat
        public int maxCoalPerSlot = 3;                             // GridSlot.MAX_COAL
        public bool ashKeepsStackRank = true;                      // la ceniza cuenta como lugar de la pila
        public float slotHeatCap = 10f;                            // GridSlot.AddExternalHeat
    }

    /// <summary>Valores que se pueden cambiar para probar alternativas sin tocar la escena.</summary>
    public class Overrides
    {
        public Dictionary<string, int> meatStock;   // por nombre de corte
        public int? coal;
        public float? coalBurnTime;
        public bool? ashKeepsStackRank;
        public int? baseCoalRequirement;
        public int? coalRequirementPerDay;
        public int? estimatedCoalConsumption;
    }

    // ── Datos del proyecto ───────────────────────────────────────────────────

    class CutData
    {
        public MeatCutSO so;
        public string name;
        public float weight;
        public int unlockNight = 1;
        public int w, h;            // footprint en la grilla (columnas, filas)
        public float band;
        public int index;
    }

    class TypeData { public CustomerType type; public float weight, patienceMult; }

    class GameData
    {
        public List<CutData> cuts = new List<CutData>();
        public List<CutData> substitutionOrder = new List<CutData>();
        public List<TypeData> types = new List<TypeData>();
        public List<ToppingSO> toppings = new List<ToppingSO>();
        public List<SideSO> sides = new List<SideSO>();
        public List<UpgradeSO> upgrades = new List<UpgradeSO>();
        public CoalSO coal;
        public RunDefeatConfigSO defeat;
        public int baseCoalReq, coalReqPerDay, baseMeatReq, meatReqPerDay, maxStrikeNights;
        public bool strikeStreakDefeat;
        public AnimationCurve affluence;
        public float affluenceAvg = 1f;
        public float basePatience, dayDuration, toppingChance, sideChance, startMoney, feedbackSeconds, coalBurn, coalPrice;
        public int maxSim, firstNight, perNight, maxNight, maxToppings, maxSides, maxStrikes, coalCap, unitsPerBag, estimatedCoal;
        public Dictionary<CutData, int> initialMeat = new Dictionary<CutData, int>();
        public int initialCoal;
        public float sideBonus, idleFactor, idleSlowdown;
        // Grilla
        public int cols, rows;
        public bool[] meatActive, coalActive;  // por (y*cols + x)
        public int[][] coalToMeat;             // celdas de carne que calienta cada celda de carbón
        public float[][] coalToMeatF;
        public int[][] meatFromCoal;           // celdas de carbón que calientan cada celda de carne
        public float[][] meatFromCoalF;
        public Rules rules = new Rules();
    }

    /// <summary>Mismo criterio que GrillSystem.UpdateHeatPropagation.</summary>
    static float HeatFactor(int sx, int sy, int tx, int ty)
    {
        int dx = Mathf.Abs(sx - tx), dy = Mathf.Abs(sy - ty);
        if (dx <= 1 && dy <= 1) return (dx == 0 || dy == 0) ? 0.35f : 0.20f;
        if (dx <= 1 && sy > ty && dy <= 3) return 0.4f / dy;
        return 0f;
    }

    static GameData LoadData(Overrides o)
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
        d.sideChance = so.FindProperty("sideOrderChance").floatValue;
        d.maxSides = so.FindProperty("maxSidesPerOrder").intValue;
        d.affluence = so.FindProperty("affluenceCurve").animationCurveValue;

        var unlockNights = new Dictionary<MeatCutSO, int>();
        var unlocksProp = so.FindProperty("cutUnlocks");
        for (int i = 0; i < unlocksProp.arraySize; i++)
        {
            var e = unlocksProp.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("cut").objectReferenceValue is MeatCutSO c) unlockNights[c] = e.FindPropertyRelative("night").intValue;
        }
        var cutsProp = so.FindProperty("availableOrderCuts");
        for (int i = 0; i < cutsProp.arraySize; i++)
        {
            var e = cutsProp.GetArrayElementAtIndex(i);
            var cut = e.FindPropertyRelative("cut").objectReferenceValue as MeatCutSO;
            float wgt = e.FindPropertyRelative("weight").floatValue;
            if (cut == null || wgt <= 0f) continue;
            d.cuts.Add(new CutData
            {
                so = cut, name = cut.itemName, weight = wgt, index = d.cuts.Count,
                unlockNight = unlockNights.TryGetValue(cut, out int un) ? un : 1,
                w = Mathf.Max(1, cut.GrillSpace.x), h = Mathf.Max(1, cut.GrillSpace.y), band = cut.GetStateBandHeat()
            });
        }

        var prefabs = so.FindProperty("customerPrefabs");
        for (int i = 0; i < prefabs.arraySize; i++)
        {
            var e = prefabs.GetArrayElementAtIndex(i);
            d.types.Add(new TypeData
            {
                type = (CustomerType)e.FindPropertyRelative("type").intValue,
                weight = e.FindPropertyRelative("spawnWeight").floatValue,
                patienceMult = e.FindPropertyRelative("patienceMultiplier").floatValue
            });
        }
        if (d.types.Sum(t => Mathf.Max(0f, t.weight)) <= 0.0001f) foreach (var t in d.types) t.weight = 1f;

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
        d.baseCoalReq = o?.baseCoalRequirement ?? d.defeat.baseCoalRequirement;
        d.coalReqPerDay = o?.coalRequirementPerDay ?? d.defeat.coalRequirementPerDay;
        d.baseMeatReq = d.defeat.baseMeatRequirement;
        d.meatReqPerDay = d.defeat.meatRequirementPerDay;
        d.strikeStreakDefeat = d.defeat.strikeStreakDefeatEnabled;
        d.maxStrikeNights = d.defeat.MaxConsecutiveStrikeNights;

        var shopCfg = AssetDatabase.LoadAssetAtPath<ShopConfigSO>("Assets/ScriptableObjects/ShopConfig.asset");
        d.coal = shopCfg.coal;
        d.estimatedCoal = o?.estimatedCoalConsumption ?? shopCfg.estimatedCoalConsumption;
        d.coalBurn = o?.coalBurnTime ?? d.coal.maxBurnTime;
        d.coalPrice = d.coal.basePrice;
        d.unitsPerBag = Mathf.Max(1, d.coal.unitsPerBag);
        d.coalCap = CoolerSystem.CoalStorageCap;

        var cooler = UnityEngine.Object.FindFirstObjectByType<CoolerSystem>(FindObjectsInactive.Include);
        var stock = new SerializedObject(cooler).FindProperty("initialStock");
        for (int i = 0; i < stock.arraySize; i++)
        {
            var e = stock.GetArrayElementAtIndex(i);
            var item = e.FindPropertyRelative("item").objectReferenceValue as ItemDataSO;
            int amount = e.FindPropertyRelative("amount").intValue;
            if (item == null || amount <= 0) continue;
            if (item is CoalSO) d.initialCoal = Mathf.Min(d.initialCoal + amount, d.coalCap);
            else if (item is MeatCutSO m)
            {
                var cd = d.cuts.FirstOrDefault(c => c.so == m);
                if (cd != null) d.initialMeat[cd] = (d.initialMeat.TryGetValue(cd, out int n) ? n : 0) + amount;
            }
        }
        if (o?.coal != null) d.initialCoal = o.coal.Value;
        if (o?.meatStock != null)
        {
            d.initialMeat.Clear();
            foreach (var kv in o.meatStock)
            {
                var cd = d.cuts.FirstOrDefault(c => c.name == kv.Key);
                if (cd != null && kv.Value > 0) d.initialMeat[cd] = kv.Value;
            }
        }
        d.rules.ashKeepsStackRank = o?.ashKeepsStackRank ?? DetectAshKeepsStackRank(d.coal, out _);

        var catalog = AssetDatabase.LoadAssetAtPath<FoodCatalogSO>("Assets/ScriptableObjects/FoodCatalog.asset");
        d.toppings = catalog.GetAvailableToppings().Where(t => t != null).ToList();
        d.sides = catalog.GetAvailableSides().Where(t => t != null).ToList();
        d.upgrades = catalog.GetAllUpgrades().Where(u => u != null).ToList();
        foreach (var c in catalog.GetAllCuts())
        {
            var cd = d.cuts.FirstOrDefault(x => x.so == c);
            if (cd != null) d.substitutionOrder.Add(cd);
        }
        d.feedbackSeconds = CustomerFeedbackConfigSO.Instance.FeedbackDuration;

        var grill = UnityEngine.Object.FindFirstObjectByType<GrillSystem>(FindObjectsInactive.Include);
        var meatSo = new SerializedObject(grill.meatPrefab.GetComponent<Meat>());
        d.sideBonus = meatSo.FindProperty("cookSpeedBonusPerState").floatValue;
        d.idleFactor = meatSo.FindProperty("idleSideHeatFactor").floatValue;
        d.idleSlowdown = meatSo.FindProperty("idleSideSlowdownPerState").floatValue;

        // Grilla real: solo las celdas activas, con las coordenadas que asigna el juego.
        var active = grill.slots.Where(s => s != null && s.gameObject.activeInHierarchy).ToList();
        GridSlot.AssignGridCoordinates(active);
        d.cols = active.Max(s => s.gridX) + 1;
        d.rows = active.Max(s => s.gridY) + 1;
        int cells = d.cols * d.rows;
        d.meatActive = new bool[cells];
        d.coalActive = new bool[cells];
        foreach (var s in active)
        {
            int k = s.gridY * d.cols + s.gridX;
            if (s.acceptsType == ItemType.Meat) d.meatActive[k] = true; else if (s.acceptsType == ItemType.Coal) d.coalActive[k] = true;
        }
        var cm = new List<int>[cells]; var cmf = new List<float>[cells];
        var mc = new List<int>[cells]; var mcf = new List<float>[cells];
        for (int k = 0; k < cells; k++) { cm[k] = new List<int>(); cmf[k] = new List<float>(); mc[k] = new List<int>(); mcf[k] = new List<float>(); }
        for (int c = 0; c < cells; c++)
        {
            if (!d.coalActive[c]) continue;
            for (int m = 0; m < cells; m++)
            {
                if (!d.meatActive[m]) continue;
                float f = HeatFactor(c % d.cols, c / d.cols, m % d.cols, m / d.cols);
                if (f <= 0f) continue;
                cm[c].Add(m); cmf[c].Add(f); mc[m].Add(c); mcf[m].Add(f);
            }
        }
        d.coalToMeat = cm.Select(l => l.ToArray()).ToArray(); d.coalToMeatF = cmf.Select(l => l.ToArray()).ToArray();
        d.meatFromCoal = mc.Select(l => l.ToArray()).ToArray(); d.meatFromCoalF = mcf.Select(l => l.ToArray()).ToArray();

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
        public int[] meat;
        public int coal;
        public Dictionary<UpgradeSO, int> levels = new Dictionary<UpgradeSO, int>();
        public int strikeNightsInRow, streak, coalPlacedTotal, nightsPlayed;
        public int Level(UpgradeSO u) => levels.TryGetValue(u, out int l) ? l : 0;
    }

    public class NightResult
    {
        public int night, arrived, served, lostPatience, lostByMeat, lostByCoal, substitutions, stockoutCuts, badCooking, strikes;
        public int coalPlaced, cutsTaken, cutsWasted, cutsBurned, coalShortNights;
        public float coalBurnSeconds, coalIdleSeconds, coalLeftLife;
        public float sales, tips, spentMeat, spentCoal, spentUpgrades, moneyEnd;
        public bool endedByStrikes, blocked, strikeDefeat;
        public int coalEnd, meatEnd;
        public int[] meatEndByCut, takenByCut, ordersByCut;
        public bool AnyScarcityLoss => lostByMeat + lostByCoal > 0;
    }

    class Cust
    {
        public Customer c;
        public float arrive, startAfter;
        public int seat;
        public bool waitingStock, subRolled, canSub, gone;
        public Job job;
    }

    class Job
    {
        public Cust cust;
        public CutData cut;
        public int[] cells;
        public float a, b;           // calor acumulado por cara
        public bool onA = true;
        public int phase;            // 0 cocina A, 1 cocina B, 2 armando, 3 listo
        public float aimA, aimB, actAt = -1f, readyAt;
        public MeatStates req;
        public float forgetUntil;
        public bool forgot;
    }

    // Una celda de carbón: edades de las brasas en la pila (la ceniza queda hasta limpiarse).
    class Grill
    {
        public List<float>[] stacks;
        public Job[] meatOwner;
        public Grill(int cells) { stacks = new List<float>[cells]; meatOwner = new Job[cells]; for (int i = 0; i < cells; i++) stacks[i] = new List<float>(4); }
    }

    // ── Una noche ────────────────────────────────────────────────────────────

    static int StateIndex(float heat, float band) => band <= 0f ? 0 : Mathf.Min(5, Mathf.FloorToInt(Mathf.Max(0f, heat) / band));

    static float CoalPower(GameData d, float age) => age >= d.coalBurn ? 0f : d.rules.coalPeakHeat * Mathf.Clamp01(1f - age / d.coalBurn);

    static float SlotInternal(GameData d, List<float> stack)
    {
        float sum = 0f;
        int rank = 0;
        for (int i = 0; i < stack.Count && i < d.rules.maxCoalPerSlot; i++)
        {
            bool burning = stack[i] < d.coalBurn;
            if (d.rules.ashKeepsStackRank)
            {
                if (burning) sum += CoalPower(d, stack[i]) * d.rules.stackWeights[i];
            }
            else if (burning)
            {
                if (rank < d.rules.stackWeights.Length) sum += CoalPower(d, stack[i]) * d.rules.stackWeights[rank];
                rank++;
            }
        }
        return sum;
    }

    static NightResult SimulateNight(GameData d, Profile p, RunState s, System.Random rng)
    {
        var r = new NightResult { night = s.night, ordersByCut = new int[d.cuts.Count], takenByCut = new int[d.cuts.Count] };
        int cells = d.cols * d.rows;
        var g = new Grill(cells);
        var internalHeat = new float[cells];
        var meatHeat = new float[cells];
        s.streak = 0;

        int target = Mathf.Min(d.firstNight + (s.night - 1) * d.perNight, Mathf.Max(d.firstNight, d.maxNight));
        int seats = d.maxSim + d.upgrades.Where(u => u.effectType == UpgradeEffectType.MaxSimultaneousCustomers).Sum(u => s.Level(u) * Mathf.Max(1, u.customersPerLevel));
        float patienceMult = 1f + d.upgrades.Where(u => u.effectType == UpgradeEffectType.CustomerPatience).Sum(u => s.Level(u) * u.patienceBonusPerLevel);
        float tipMult = 1f + d.upgrades.Where(u => u.effectType == UpgradeEffectType.TipPercent).Sum(u => s.Level(u) * u.tipBonusPerLevel);
        float burnSaved = d.coalBurn;
        foreach (var u in d.upgrades)
            if (u.effectType == UpgradeEffectType.CoalBurnTime && s.Level(u) > 0 && u.upgradedMaxBurnTime > 0f) d.coalBurn = u.upgradedMaxBurnTime;

        var unlocked = d.cuts.Where(c => s.night >= c.unlockNight).ToList();
        float interval = Mathf.Max(0.1f, d.dayDuration / Mathf.Max(1, target));
        const float dt = 0.5f;
        var seatCust = new Cust[seats];
        var feedbackUntil = new float[seats];
        var waiting = new List<Cust>();
        var jobs = new List<Job>();
        float nextSpawn = NextDelay(d, interval, 0f);
        int strikes = 0;
        bool blocked = false;
        float nextAction = 0f;
        var bedCells = new List<int>();
        if (p.bedSize > 0)
        {
            // Cama de brasas fija en la fila de abajo, al centro: el principiante la mantiene toda la noche.
            int y = d.rows - 1, start = Mathf.Max(0, d.cols / 2 - p.bedSize / 2);
            for (int x = start; x < d.cols && bedCells.Count < p.bedSize; x++) if (d.coalActive[y * d.cols + x]) bedCells.Add(y * d.cols + x);
        }

        bool PlaceCoal(int cell)
        {
            if (s.coal <= 0) return false;
            var st = g.stacks[cell];
            bool hasAsh = st.Any(a => a >= d.coalBurn);
            if (hasAsh && (st.Count >= d.rules.maxCoalPerSlot || rng.NextDouble() < p.cleanAshProb))
                for (int k = 0; k < cells; k++) g.stacks[k].RemoveAll(a => a >= d.coalBurn); // GameManager.CleanAshes: limpia toda la parrilla
            if (st.Count >= d.rules.maxCoalPerSlot) return false;
            st.Add(0f);
            s.coal--;
            r.coalPlaced++;
            return true;
        }

        float TopLife(int cell)
        {
            float best = 0f;
            foreach (var a in g.stacks[cell]) if (a < d.coalBurn) best = Mathf.Max(best, 1f - a / d.coalBurn);
            return best;
        }

        bool Burning(int cell) { foreach (var a in g.stacks[cell]) if (a < d.coalBurn) return true; return false; }

        bool EnsureHeat(Job j, bool initial)
        {
            int want = j.cells.Length >= 4 ? p.coalPerBigCut : p.coalPerSmallCut;
            float minLife = initial ? p.minLifeToReuse : 0.05f;
            var coalCells = j.cells.Where(c => d.coalActive[c]).ToList();
            int lit = coalCells.Count(c => TopLife(c) >= minLife);
            bool placed = false;
            foreach (var c in coalCells.OrderBy(c => TopLife(c)))
            {
                if (lit >= want) break;
                if (TopLife(c) >= minLife) continue;
                if (PlaceCoal(c)) { lit++; placed = true; } else if (s.coal <= 0) break;
            }
            if (initial || placed || s.coal <= 0) return placed;

            // Recarga con el corte a medio cocinar y todas sus celdas con brasa débil: el que sabe
            // usa una celda vecina libre (le llega 35%); el resto la apila encima de la débil.
            if (p.cleanAshProb >= 0.5f)
            {
                foreach (var m in j.cells)
                    for (int q = 0; q < d.meatFromCoal[m].Length; q++)
                    {
                        int c = d.meatFromCoal[m][q];
                        if (d.meatFromCoalF[m][q] < 0.35f || Burning(c)) continue;
                        if (PlaceCoal(c)) return true;
                    }
            }
            var weakest = coalCells.OrderBy(c => TopLife(c)).FirstOrDefault();
            return coalCells.Count > 0 && PlaceCoal(weakest);
        }

        Job TryPlaceCut(Cust cu, CutData cut)
        {
            // Busca el hueco: los que reaprovechan miran dónde hay brasa viva.
            int bestX = -1, bestY = -1; float bestScore = -1f;
            int cleanX = -1, cleanY = -1;
            for (int y = 0; y + cut.h <= d.rows; y++)
                for (int x = 0; x + cut.w <= d.cols; x++)
                {
                    bool ok = true, clean = true; float score = 0f;
                    for (int yy = y; yy < y + cut.h && ok; yy++)
                        for (int xx = x; xx < x + cut.w; xx++)
                        {
                            int k = yy * d.cols + xx;
                            if (!d.meatActive[k] || g.meatOwner[k] != null) { ok = false; break; }
                            score += TopLife(k);
                            if (g.stacks[k].Count > 0) clean = false;
                        }
                    if (!ok) continue;
                    // Bajo igualdad, la fila de abajo (recibe además el calor que sube).
                    score += y * 0.001f;
                    if (score > bestScore) { bestScore = score; bestX = x; bestY = y; }
                    if (clean && (cleanX < 0 || y > cleanY)) { cleanX = x; cleanY = y; }
                }
            if (bestX < 0) return null;
            // Si donde hay brasa ya está gastada, el que maneja la parrilla prende en un lugar limpio
            // (la brasa nueva queda abajo de la pila y da el 100%). El principiante cocina sobre su cama.
            if (p.bedSize == 0 && cleanX >= 0 && bestScore / (cut.w * cut.h) < p.minLifeToReuse) { bestX = cleanX; bestY = cleanY; }
            var cellsList = new List<int>();
            for (int yy = bestY; yy < bestY + cut.h; yy++) for (int xx = bestX; xx < bestX + cut.w; xx++) cellsList.Add(yy * d.cols + xx);
            var j = new Job { cust = cu, cut = cut, cells = cellsList.ToArray(), req = cu.c.order.GetRequestedState(0) };
            foreach (var k in j.cells) g.meatOwner[k] = j;
            // Da vuelta / saca cuando ve el punto pedido en el corte (barra y sprite): apunta a la
            // primera parte de la banda, con un error que depende de la mano.
            float AimInBand() => Mathf.Clamp(0.3f + 2f * p.aimNoise * Gauss(rng), -0.6f, 1.4f);
            j.aimA = ((int)j.req + AimInBand()) * cut.band;
            j.aimB = ((int)j.req + AimInBand()) * cut.band;
            if (rng.NextDouble() < p.forgetProb) j.forgot = true;
            return j;
        }

        void FreeJob(Job j) { foreach (var k in j.cells) if (g.meatOwner[k] == j) g.meatOwner[k] = null; }

        for (float t = 0f; ; t += dt)
        {
            bool doorsOpen = t < d.dayDuration && !blocked;
            bool anyone = seatCust.Any(x => x != null);
            if (!doorsOpen && !anyone) break;
            if (t > d.dayDuration * 4f) break;

            // Llegadas (CustomerSystem.SpawnCustomer: sin lugar libre no entra).
            if (doorsOpen && t >= nextSpawn)
            {
                nextSpawn = t + NextDelay(d, interval, t / d.dayDuration);
                int free = -1;
                for (int i = 0; i < seats; i++) if (seatCust[i] == null && feedbackUntil[i] <= t) { free = i; break; }
                if (free >= 0)
                {
                    r.arrived++;
                    var type = Pick(d.types, x => x.weight, rng);
                    var order = MakeOrder(d, p, s, unlocked, rng);
                    r.ordersByCut[d.cuts.First(c => c.so == order.PrimaryCut).index]++;
                    var c = new Customer();
                    c.Init(type.type, order, d.basePatience * type.patienceMult * patienceMult, free);
                    var cu = new Cust { c = c, arrive = t, seat = free, startAfter = t + p.reaction * (0.5f + (float)rng.NextDouble()) };
                    seatCust[free] = cu;
                    waiting.Add(cu);
                }
            }

            // Cama de brasas del principiante: la repone siempre que el local esté abierto o haya gente.
            if (bedCells.Count > 0 && t >= nextAction)
                foreach (var c in bedCells)
                    if (TopLife(c) <= 0.02f && PlaceCoal(c)) { nextAction = t + 1f; break; }

            // Arranca pedidos.
            for (int i = 0; i < waiting.Count && jobs.Count < p.maxParallel; i++)
            {
                var cu = waiting[i];
                if (t < cu.startAfter) continue;
                var cut = d.cuts.First(c => c.so == cu.c.order.PrimaryCut);
                if (s.meat[cut.index] <= 0)
                {
                    if (!cu.waitingStock) { cu.waitingStock = true; r.stockoutCuts++; }
                    if (!cu.subRolled) { cu.subRolled = true; cu.canSub = rng.NextDouble() < p.knowsSubstitution; }
                    if (!cu.canSub) continue;
                    // GameManager: primer corte desbloqueado con stock distinto del que falta (catálogo).
                    var sub = d.substitutionOrder.FirstOrDefault(x => x != cut && s.night >= x.unlockNight && s.meat[x.index] > 0);
                    if (sub == null) continue;
                    var o = cu.c.order;
                    o.SetSingleCut(sub.so, o.GetRequestedState(0));
                    bool sandwich = sub.so.servingMode == ServingMode.SandwichOnly || (sub.so.servingMode == ServingMode.Both && o.IsSandwich);
                    o.bread = sandwich ? sub.so.requiredBread : null;
                    if (o.IsSandwich) { o.toppings.Clear(); o.sides.Clear(); }
                    cu.c.IsTipAnulada = true;
                    r.substitutions++;
                    cu.waitingStock = false;
                    cut = sub;
                }
                var j = TryPlaceCut(cu, cut);
                if (j == null) continue; // sin lugar en la parrilla: espera
                s.meat[cut.index]--;
                r.cutsTaken++; r.takenByCut[cut.index]++;
                EnsureHeat(j, true);
                cu.job = j;
                jobs.Add(j);
                waiting.RemoveAt(i--);
            }

            // Calor de la parrilla (GridSlot.CalculateInternalHeat + GrillSystem.UpdateHeatPropagation).
            for (int k = 0; k < cells; k++) internalHeat[k] = g.stacks[k].Count > 0 ? SlotInternal(d, g.stacks[k]) : 0f;
            foreach (var j in jobs)
                foreach (var m in j.cells)
                {
                    float h = 0f;
                    var src = d.meatFromCoal[m]; var f = d.meatFromCoalF[m];
                    for (int q = 0; q < src.Length; q++) h += internalHeat[src[q]] * f[q];
                    meatHeat[m] = Mathf.Min(d.rules.slotHeatCap, h);
                }

            // Brasas: se queman siempre; se mide cuánto arden sin carne que calentar.
            for (int k = 0; k < cells; k++)
            {
                var st = g.stacks[k];
                if (st.Count == 0) continue;
                bool usefulHeat = false;
                foreach (var m in d.coalToMeat[k]) if (g.meatOwner[m] != null && g.meatOwner[m].phase < 2) { usefulHeat = true; break; }
                for (int q = 0; q < st.Count; q++)
                {
                    if (st[q] >= d.coalBurn) continue;
                    float burn = Mathf.Min(dt, d.coalBurn - st[q]);
                    r.coalBurnSeconds += burn;
                    if (!usefulHeat) r.coalIdleSeconds += burn;
                    st[q] += dt;
                }
            }

            // Cocción y decisiones sobre cada corte.
            for (int ji = 0; ji < jobs.Count; ji++)
            {
                var j = jobs[ji];
                var cu = j.cust;
                if (j.phase < 2)
                {
                    float H = 0f;
                    foreach (var m in j.cells) H += meatHeat[m];
                    float band = j.cut.band, burnAt = 5f * band;
                    int sa = StateIndex(j.a, band), sb = StateIndex(j.b, band);
                    float rateA = j.onA ? 1f + d.sideBonus * sa : d.idleFactor * Mathf.Max(0f, 1f - d.idleSlowdown * sa);
                    float rateB = !j.onA ? 1f + d.sideBonus * sb : d.idleFactor * Mathf.Max(0f, 1f - d.idleSlowdown * sb);
                    j.a = Mathf.Min(burnAt, j.a + H * dt * rateA);
                    j.b = Mathf.Min(burnAt, j.b + H * dt * rateB);

                    // Poco calor y todavía le falta: agrega brasa (si le queda).
                    float freshRef = d.rules.coalPeakHeat * 0.35f * 2f;
                    float remaining = j.phase == 0 ? j.aimA - j.a : j.aimB - j.b;
                    if (H < freshRef * p.refuelBelow && remaining > 0.25f * band && t >= nextAction)
                    {
                        if (EnsureHeat(j, false)) nextAction = t + 1f;
                    }

                    // Se quemó en la parrilla: a la basura y otra pieza.
                    if (j.a >= burnAt || j.b >= burnAt)
                    {
                        r.cutsWasted++; r.cutsBurned++;
                        FreeJob(j);
                        jobs.RemoveAt(ji--);
                        if (!cu.gone)
                        {
                            s.streak = 0;
                            cu.job = null;
                            cu.startAfter = t + p.actDelay;
                            waiting.Insert(0, cu);
                        }
                        continue;
                    }

                    if (j.forgot && j.forgetUntil <= 0f && j.phase == 0 && j.a >= j.aimA * 0.8f)
                        j.forgetUntil = t + 20f + 40f * (float)rng.NextDouble();
                    if (t < j.forgetUntil) continue;

                    float side = j.onA ? j.a : j.b, aim = j.phase == 0 ? j.aimA : j.aimB;
                    if (side >= aim && j.actAt < 0f) j.actAt = t + p.actDelay * (float)(-Math.Log(1.0 - rng.NextDouble()));
                    // MeatBurnAlert avisa antes de que se queme: si está mirando, reacciona rápido.
                    if (side >= burnAt - 0.6f * band) j.actAt = j.actAt >= 0f ? Mathf.Min(j.actAt, t + 1f) : t + 1f;
                    if (j.actAt >= 0f && t >= j.actAt)
                    {
                        j.actAt = -1f;
                        if (j.phase == 0) { j.phase = 1; j.onA = false; }
                        else
                        {
                            j.phase = 2;
                            FreeJob(j);
                            int extras = cu.c.order.toppings.Count + cu.c.order.sides.Count;
                            j.readyAt = t + p.assembly + extras * 1.5f;
                        }
                    }
                }
                else if (j.phase == 2 && t >= j.readyAt)
                {
                    j.phase = 3;
                    jobs.RemoveAt(ji--);
                    if (cu.gone) continue;
                    Deliver(d, p, s, cu, j, tipMult, rng, r, ref strikes);
                    seatCust[cu.seat] = null;
                    feedbackUntil[cu.seat] = t + d.feedbackSeconds;
                }
            }

            // Paciencia.
            for (int i = 0; i < seats; i++)
            {
                var cu = seatCust[i];
                if (cu == null) continue;
                cu.c.UpdatePatience(dt);
                if (!cu.c.IsAngry) continue;
                cu.gone = true;
                strikes++;
                s.streak = 0;
                if (cu.job == null)
                {
                    if (cu.waitingStock) r.lostByMeat++; else r.lostPatience++;
                }
                else
                {
                    var j = cu.job;
                    bool noHeat = j.phase < 2 && s.coal <= 0 && j.cells.Sum(m => meatHeat[m]) < 0.5f;
                    if (noHeat) r.lostByCoal++; else r.lostPatience++;
                    if (j.phase < 2)
                    {
                        FreeJob(j);
                        jobs.Remove(j);
                        if (rng.NextDouble() < p.reuseLeftover) s.meat[j.cut.index]++; else r.cutsWasted++;
                    }
                }
                waiting.Remove(cu);
                seatCust[i] = null;
            }

            if (strikes >= d.maxStrikes && !blocked) { blocked = true; r.endedByStrikes = true; }
        }

        for (int k = 0; k < cells; k++) foreach (var a in g.stacks[k]) if (a < d.coalBurn) r.coalLeftLife += 1f - a / d.coalBurn;
        r.strikes = strikes;
        s.strikeNightsInRow = r.endedByStrikes ? s.strikeNightsInRow + 1 : 0;
        s.coalPlacedTotal += r.coalPlaced;
        d.coalBurn = burnSaved;
        return r;
    }

    static void Deliver(GameData d, Profile p, RunState s, Cust cu, Job j, float tipMult, System.Random rng, NightResult r, ref int strikes)
    {
        var order = cu.c.order;
        MeatStates sa = (MeatStates)StateIndex(j.a, j.cut.band), sb = (MeatStates)StateIndex(j.b, j.cut.band);
        r.served++;
        if (sa == MeatStates.Crudo || sb == MeatStates.Crudo || sa == MeatStates.Quemado || sb == MeatStates.Quemado)
        {
            r.badCooking++; strikes++; s.streak = 0;
            return;
        }
        float basePrice = order.IsSandwich ? j.cut.so.sellPriceSandwich : j.cut.so.sellPricePlate;
        var cutResult = CookingDeliveryEvaluator.EvaluateCut(sa, sb, order.GetRequestedState(0), basePrice);
        float payment = cutResult.price;
        float unreduced = cutResult.worstOffset < 2 ? cutResult.price : 0f;
        int worst = cutResult.worstOffset;
        var tops = new List<ToppingSO>();
        foreach (var tp in order.toppings) if (rng.NextDouble() >= p.extraError) tops.Add(tp);
        var extras = CookingDeliveryEvaluator.EvaluateExtras(order.toppings, tops, order.IsSandwich, order.IsSandwich);
        int extraOff = extras.offset;
        foreach (var _ in order.sides) if (rng.NextDouble() < p.extraError) extraOff++;
        if (extraOff > worst) worst = extraOff;
        if (extraOff >= 2) payment = (payment - unreduced) + CookingDeliveryEvaluator.ApplyReducedPrice(unreduced);
        var eval = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(cu.c, basePrice, worst, tipMult, s.streak);
        if (worst == 0 && !cu.c.IsTipAnulada) s.streak++; else s.streak = 0;
        r.sales += payment;
        r.tips += eval.tipAmount;
    }

    // ── Tienda entre noches ──────────────────────────────────────────────────

    static int CoalReq(GameData d, int day) => d.baseCoalReq + d.coalReqPerDay * (Mathf.Max(1, day) - 1);
    static int MeatReq(GameData d, int day) => d.baseMeatReq + d.meatReqPerDay * (Mathf.Max(1, day) - 1);

    static void BuyCoalUnits(GameData d, RunState s, int units, NightResult r)
    {
        if (units <= 0) return;
        int bags = Mathf.CeilToInt((float)units / d.unitsPerBag);
        for (int i = 0; i < bags && s.money >= d.coalPrice && s.coal < d.coalCap; i++)
        {
            s.money -= d.coalPrice; r.spentCoal += d.coalPrice;
            s.coal = Mathf.Min(d.coalCap, s.coal + d.unitsPerBag);
        }
    }

    static void BuyMeat(RunState s, CutData c, int qty, NightResult r)
    {
        int afford = c.so.basePrice > 0f ? Mathf.FloorToInt(s.money / c.so.basePrice) : qty;
        qty = Mathf.Min(qty, afford);
        if (qty <= 0) return;
        s.meat[c.index] += qty;
        s.money -= qty * c.so.basePrice;
        r.spentMeat += qty * c.so.basePrice;
    }

    static void Shop(GameData d, Profile p, RunState s, NightResult r)
    {
        int next = s.night + 1;
        var unlocked = d.cuts.Where(c => next >= c.unlockNight).ToList();
        float totalW = unlocked.Sum(c => c.weight);

        // Carbón.
        // La tienda activa (ShopCanvas) solo muestra el mínimo del día siguiente y lo pinta de verde
        // al alcanzarlo: el principiante compra eso (lo hace el bloque de mínimos de abajo).
        if (!p.trustsShopMinimum)
        {
            int want = Mathf.CeilToInt(r.coalPlaced * p.coalBuffer * 1.05f);
            BuyCoalUnits(d, s, want - s.coal, r);
        }

        // Carne: repone lo que usó (el principiante, igual que anoche; los demás, con margen).
        int used = r.cutsTaken;
        foreach (var c in unlocked)
        {
            float share = c.weight / totalW;
            int want;
            if (p.trustsShopMinimum)
                want = c.unlockNight == next ? Mathf.CeilToInt(MeatReq(d, next) * share) : r.takenByCut[c.index];
            else
                want = Mathf.CeilToInt(used * 1.1f * share * p.meatBuffer);
            BuyMeat(s, c, Mathf.Max(0, want - s.meat[c.index]), r);
        }

        // Si no llega a los mínimos, el botón "Siguiente" no lo deja avanzar: compra lo que falte.
        int coalDef = CoalReq(d, next) - s.coal;
        if (coalDef > 0) BuyCoalUnits(d, s, coalDef, r);
        int meatDef = MeatReq(d, next) - s.meat.Sum();
        if (meatDef > 0)
        {
            var cheapest = unlocked.OrderBy(c => c.so.basePrice).First();
            BuyMeat(s, cheapest, meatDef, r);
        }

        // Mejoras: la más barata si después le queda una reserva para reponer media noche.
        float reserve = unlocked.Sum(c => c.so.basePrice * used * c.weight / totalW) * 0.5f + d.coalPrice;
        bool bought = true;
        while (bought)
        {
            bought = false;
            foreach (var u in d.upgrades.Where(u => u.isUnlocked && s.Level(u) < u.MaxLevel).OrderBy(u => u.basePrice))
            {
                if (u.effectType == UpgradeEffectType.MaxSimultaneousCustomers && (r.lostPatience > 0 || r.endedByStrikes)) continue;
                if (s.money - u.basePrice < reserve) continue;
                s.money -= u.basePrice; r.spentUpgrades += u.basePrice;
                s.levels[u] = s.Level(u) + 1;
                bought = true;
                break;
            }
        }
    }

    // ── Runs ─────────────────────────────────────────────────────────────────

    public class Summary
    {
        public string label;
        public Profile profile;
        public int runs, nights;
        public List<NightResult>[] perNight;
        public int blockedRuns, strikeDefeatRuns;
        public int[] endNight;
    }

    static Summary SimulateRuns(GameData d, Profile p, int runs, int nights, int seed)
    {
        var sum = new Summary { profile = p, runs = runs, nights = nights, perNight = new List<NightResult>[nights], endNight = new int[nights + 1] };
        for (int n = 0; n < nights; n++) sum.perNight[n] = new List<NightResult>();
        for (int run = 0; run < runs; run++)
        {
            var rng = new System.Random(seed * 100003 + run);
            var s = new RunState { money = d.startMoney, meat = new int[d.cuts.Count], coal = d.initialCoal };
            foreach (var kv in d.initialMeat) s.meat[kv.Key.index] = kv.Value;
            for (int n = 1; n <= nights; n++)
            {
                s.night = n;
                var r = SimulateNight(d, p, s, rng);
                s.nightsPlayed++;
                s.money += r.sales + r.tips;
                Shop(d, p, s, r);
                r.coalEnd = s.coal;
                r.meatEnd = s.meat.Sum();
                r.meatEndByCut = (int[])s.meat.Clone();
                r.moneyEnd = s.money;

                var cheapest = d.cuts.Where(c => n + 1 >= c.unlockNight).Min(c => c.so.basePrice);
                var status = RunEconomyEvaluator.Evaluate(n + 1, ReqConfig(d), s.meat.Sum(), s.coal, s.money, cheapest, true, true,
                    d.coalPrice, d.unitsPerBag, d.coalCap);
                r.blocked = !status.CanContinue;
                r.strikeDefeat = d.strikeStreakDefeat && s.strikeNightsInRow >= d.maxStrikeNights;
                sum.perNight[n - 1].Add(r);
                if (r.blocked || r.strikeDefeat)
                {
                    if (r.blocked) sum.blockedRuns++; else sum.strikeDefeatRuns++;
                    sum.endNight[n]++;
                    break;
                }
            }
        }
        return sum;
    }

    static RunDefeatConfigSO reqConfig;
    static RunDefeatConfigSO ReqConfig(GameData d)
    {
        if (reqConfig == null) reqConfig = ScriptableObject.CreateInstance<RunDefeatConfigSO>();
        reqConfig.hideFlags = HideFlags.HideAndDontSave;
        reqConfig.baseCoalRequirement = d.baseCoalReq;
        reqConfig.coalRequirementPerDay = d.coalReqPerDay;
        reqConfig.baseMeatRequirement = d.baseMeatReq;
        reqConfig.meatRequirementPerDay = d.meatReqPerDay;
        return reqConfig;
    }

    /// <summary>Corre los 4 escenarios y devuelve el informe (y lo guarda en Docs/Balance).</summary>
    public static string RunAll(string label, int runs = 1000, int nights = 10, int seed = 40, Overrides o = null, string profileFilter = null)
    {
        var d = LoadData(o);
        var list = new List<Summary>();
        int i = 0;
        foreach (var p in AllProfiles)
        {
            i++;
            if (profileFilter != null && !p.name.StartsWith(profileFilter)) continue;
            var sm = SimulateRuns(d, p, runs, nights, seed + i);
            sm.label = label;
            list.Add(sm);
        }
        string md = Report(d, list, label, seed);
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Docs", "Balance");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "stock_sim_" + label + ".md"), md);
        return md;
    }

    /// <summary>Solo la noche 1 (sin tienda previa) con varias combinaciones de stock inicial.</summary>
    public static string SweepNight1(int runs, int seed, IEnumerable<(string name, Overrides o)> variants)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Variante | Perfil | Noches sin pedidos perdidos por insumos | Pedidos perdidos por insumos (media) | Cambios por faltante | Carbón puesto (media / p90) | Se quedó sin carbón | Carne usada (p90) | Quemados / abandonados | Atendidos | Ventas+propinas |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var (name, o) in variants)
        {
            var d = LoadData(o);
            int k = 0;
            foreach (var p in AllProfiles)
            {
                k++;
                var res = new List<NightResult>();
                for (int run = 0; run < runs; run++)
                {
                    var rng = new System.Random((seed + k) * 100003 + run);
                    var s = new RunState { money = d.startMoney, meat = new int[d.cuts.Count], coal = d.initialCoal };
                    foreach (var kv in d.initialMeat) s.meat[kv.Key.index] = kv.Value;
                    var r = SimulateNight(d, p, s, rng);
                    r.coalEnd = s.coal; r.meatEnd = s.meat.Sum();
                    res.Add(r);
                }
                var placed = res.Select(x => x.coalPlaced).OrderBy(x => x).ToList();
                sb.AppendLine($"| {name} | {p.name} | {Pct(res.Count(x => !x.AnyScarcityLoss), res.Count)} | {res.Average(x => x.lostByMeat + x.lostByCoal):0.00} | {res.Average(x => x.substitutions):0.00} | {placed.Average():0.0} / {placed[(int)(placed.Count * 0.9)]} | {Pct(res.Count(x => x.coalEnd == 0), res.Count)} | {res.Average(x => x.cutsTaken):0.0} ({res.Select(x => x.cutsTaken).OrderBy(v => v).ElementAt((int)(res.Count * 0.9))}) | {res.Average(x => x.cutsBurned):0.0} / {res.Average(x => x.cutsWasted - x.cutsBurned):0.0} | {res.Average(x => x.served):0.0} | ${res.Average(x => x.sales + x.tips):0} |");
            }
        }
        return sb.ToString();
    }

    /// <summary>Barrido de stock inicial para la noche 1. <paramref name="spec"/>: "chorizo,tira,carbón[,a]" separados por ';' ("a" = ceniza sin lugar de calor).</summary>
    public static string SweepNight1Spec(string spec, int runs = 1000, int seed = 40)
    {
        var variants = new List<(string, Overrides)>();
        foreach (var part in spec.Split(';'))
        {
            var v = part.Trim().Split(',');
            if (v.Length < 3) continue;
            var o = new Overrides
            {
                meatStock = new Dictionary<string, int> { { "Chorizo", int.Parse(v[0]) }, { "Tira de asado", int.Parse(v[1]) } },
                coal = int.Parse(v[2])
            };
            string name = $"{v[0]} chorizos, {v[1]} tiras, {v[2]} carbones";
            if (v.Length > 3 && v[3].Trim() == "a") { o.ashKeepsStackRank = false; name += ", ceniza sin lugar de calor"; }
            if (v.Length > 3 && v[3].Trim() == "b") { o.ashKeepsStackRank = true; name += ", ceniza con lugar de calor"; }
            variants.Add((name, o));
        }
        return SweepNight1(runs, seed, variants);
    }

    /// <summary>Runs completas con una variante. <paramref name="spec"/>: "chorizo,tira,carbón,ceniza(a=sin lugar|b=con lugar|-),mínimoCarbónBase,mínimoCarbónPorDía".</summary>
    public static string RunAllSpec(string label, string spec, int runs = 1000, int nights = 10, int seed = 40, string profileFilter = null)
    {
        var v = spec.Split(',');
        var o = new Overrides();
        if (v.Length >= 3) { o.meatStock = new Dictionary<string, int> { { "Chorizo", int.Parse(v[0]) }, { "Tira de asado", int.Parse(v[1]) } }; o.coal = int.Parse(v[2]); }
        if (v.Length >= 4 && v[3] == "a") o.ashKeepsStackRank = false;
        if (v.Length >= 4 && v[3] == "b") o.ashKeepsStackRank = true;
        if (v.Length >= 5 && v[4] != "-") o.baseCoalRequirement = int.Parse(v[4]);
        if (v.Length >= 6 && v[5] != "-") o.coalRequirementPerDay = int.Parse(v[5]);
        return RunAll(label, runs, nights, seed, o, profileFilter);
    }

    // ── Informe ──────────────────────────────────────────────────────────────

    static string Pct(int a, int b) => b > 0 ? (a * 100f / b).ToString("0.0") + "%" : "-";

    static string Report(GameData d, List<Summary> list, string label, int seed)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Simulación de stock (carne y carbón) — {label}");
        sb.AppendLine();
        sb.AppendLine($"Generado {DateTime.Now:yyyy-MM-dd HH:mm}. Semilla base {seed}. {list.FirstOrDefault()?.runs} runs de {list.FirstOrDefault()?.nights} noches por escenario.");
        sb.AppendLine();
        sb.AppendLine($"Datos leídos: stock inicial {string.Join(", ", d.initialMeat.Select(kv => kv.Key.name + " " + kv.Value))}, carbón {d.initialCoal}; " +
                      $"brasa {d.coalBurn}s, bolsa {d.unitsPerBag} u. a ${d.coalPrice}; mínimos carne {d.baseMeatReq}+{d.meatReqPerDay}/día, carbón {d.baseCoalReq}+{d.coalReqPerDay}/día; " +
                      $"ceniza ocupa lugar de calor en la pila: {(d.rules.ashKeepsStackRank ? "sí" : "no")}; " +
                      $"{d.firstNight} clientes la noche 1 (+{d.perNight}), {d.maxSim} lugares, jornada {d.dayDuration}s, dinero inicial ${d.startMoney}.");
        sb.AppendLine();

        sb.AppendLine("## Resumen por escenario");
        sb.AppendLine();
        sb.AppendLine("| Escenario | Noches sin pedidos perdidos por insumos | Noche 1 sin pérdidas | Runs bloqueadas por plata/insumos | Derrota por strikes | Runs vivas al final |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (var x in list)
        {
            var all = x.perNight.SelectMany(l => l).ToList();
            int alive = x.runs - x.blockedRuns - x.strikeDefeatRuns;
            sb.AppendLine($"| {x.profile.name} | {Pct(all.Count(n => !n.AnyScarcityLoss), all.Count)} | {Pct(x.perNight[0].Count(n => !n.AnyScarcityLoss), x.perNight[0].Count)} | {Pct(x.blockedRuns, x.runs)} | {Pct(x.strikeDefeatRuns, x.runs)} | {Pct(alive, x.runs)} |");
        }
        sb.AppendLine();

        foreach (var x in list)
        {
            sb.AppendLine("## " + x.profile.name);
            sb.AppendLine();
            sb.AppendLine("| Noche | Runs vivas | Llegan | Atendidos | Sin pérdidas por insumos | Perdidos por carne | Perdidos por carbón | Cambios por faltante | Perdidos por paciencia | Carne usada | Desperdicio | Carbón puesto | % brasa sin carne encima | Carbón al cierre (p10) | Carne al cierre | Ventas+propinas | Compras carne | Compras carbón | Mejoras | Plata al cierre | Bloqueadas |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            for (int n = 0; n < x.nights; n++)
            {
                var l = x.perNight[n];
                if (l.Count == 0) { sb.AppendLine($"| {n + 1} | 0 | | | | | | | | | | | | | | | | | | | |"); continue; }
                var coalEnd = l.Select(r => r.coalEnd).OrderBy(v => v).ToList();
                float idle = l.Sum(r => r.coalIdleSeconds) / Mathf.Max(1f, l.Sum(r => r.coalBurnSeconds));
                sb.AppendLine($"| {n + 1} | {l.Count} | {l.Average(r => r.arrived):0.0} | {l.Average(r => r.served):0.0} | {Pct(l.Count(r => !r.AnyScarcityLoss), l.Count)} | {l.Average(r => r.lostByMeat):0.00} | {l.Average(r => r.lostByCoal):0.00} | {l.Average(r => r.substitutions):0.00} | {l.Average(r => r.lostPatience):0.0} | {l.Average(r => r.cutsTaken):0.0} | {l.Average(r => r.cutsWasted):0.0} | {l.Average(r => r.coalPlaced):0.0} | {idle * 100f:0}% | {coalEnd.Average():0.0} ({coalEnd[(int)(coalEnd.Count * 0.1)]}) | {l.Average(r => r.meatEnd):0.0} | ${l.Average(r => r.sales + r.tips):0} | ${l.Average(r => r.spentMeat):0} | ${l.Average(r => r.spentCoal):0} | ${l.Average(r => r.spentUpgrades):0} | ${l.Average(r => r.moneyEnd):0} | {Pct(l.Count(r => r.blocked), l.Count)} |");
            }
            sb.AppendLine();
            // Por corte (noches 1, 2, 3, 5): pedidos y stock al cierre.
            sb.Append("Por corte (pedidos / usados / stock al cierre): ");
            foreach (int n in new[] { 0, 1, 2, 4 })
            {
                if (n >= x.nights || x.perNight[n].Count == 0) continue;
                var l = x.perNight[n];
                sb.Append($"N{n + 1}: " + string.Join(", ", d.cuts.Select(c => $"{c.name} {l.Average(r => r.ordersByCut[c.index]):0.0}/{l.Average(r => r.takenByCut[c.index]):0.0}/{l.Average(r => r.meatEndByCut[c.index]):0.0}")) + ". ");
            }
            sb.AppendLine();
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // ── Verificación contra el juego ─────────────────────────────────────────

    /// <summary>
    /// Arma una celda real (<see cref="GridSlot"/> + <see cref="Coal"/>, fuera de la escena) y mide su
    /// calor con el código del juego en cuatro casos. Devuelve si la ceniza sigue ocupando lugar de
    /// calor en la pila y deja en <paramref name="report"/> la comparación con el modelo del simulador.
    /// </summary>
    public static bool DetectAshKeepsStackRank(CoalSO coalData, out string report)
    {
        var sb = new StringBuilder();
        var created = new List<GameObject>();
        var rules = new Rules();
        bool ashRank = true;
        try
        {
            var slotGo = new GameObject("__simHeatSlot") { hideFlags = HideFlags.HideAndDontSave };
            created.Add(slotGo);
            var slot = slotGo.AddComponent<GridSlot>();
            var calc = typeof(GridSlot).GetMethod("CalculateInternalHeat", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Coal Make(bool ash)
            {
                var go = new GameObject("__simHeatCoal") { hideFlags = HideFlags.HideAndDontSave };
                created.Add(go);
                var c = go.AddComponent<Coal>();
                c.coalData = coalData;
                c.state = ash ? CoalStates.Ceniza : CoalStates.Encendido;
                c.currentBurnTime = ash ? coalData.maxBurnTime : 0f;
                return c;
            }
            float Game(params bool[] ash)
            {
                slot.stackedCoals.Clear();
                foreach (var a in ash) slot.stackedCoals.Add(Make(a));
                calc.Invoke(slot, null);
                return slot.internalHeat;
            }
            float ashThenFresh = Game(true, false);
            ashRank = ashThenFresh < rules.coalPeakHeat * 0.5f;
            var d = new GameData { coalBurn = coalData.maxBurnTime };
            d.rules.ashKeepsStackRank = ashRank;
            float Sim(params bool[] ash) => SlotInternal(d, ash.Select(a => a ? d.coalBurn : 0f).ToList());
            foreach (var (name, c) in new[] { ("1 brasa nueva", new[] { false }), ("ceniza + brasa nueva", new[] { true, false }),
                                               ("2 cenizas + brasa nueva", new[] { true, true, false }), ("3 brasas nuevas", new[] { false, false, false }) })
            {
                float game = Game(c), sim = Sim(c);
                sb.AppendLine($"| {name} | {game:0.000} | {sim:0.000} | {(Mathf.Abs(game - sim) < 0.001f ? "OK" : "DIFIERE")} |");
            }
        }
        finally
        {
            foreach (var go in created) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
        report = sb.ToString();
        return ashRank;
    }

    // ── Utilidades ───────────────────────────────────────────────────────────

    static Order MakeOrder(GameData d, Profile p, RunState s, List<CutData> unlocked, System.Random rng)
    {
        var order = new Order();
        CutData cut;
        if (p.adversarialOrders && rng.NextDouble() < 0.5)
            cut = unlocked.OrderBy(c => s.meat[c.index] / c.weight).First(); // el que menos stock tiene para su demanda
        else
            cut = Pick(unlocked, c => c.weight, rng);
        order.SetSingleCut(cut.so, (MeatStates)rng.Next((int)MeatStates.Jugoso, (int)MeatStates.Pasado + 1));
        bool sandwich = cut.so.servingMode == ServingMode.SandwichOnly || (cut.so.servingMode == ServingMode.Both && rng.NextDouble() < 0.5);
        if (sandwich && cut.so.requiredBread != null) order.bread = cut.so.requiredBread;
        if (!order.IsSandwich)
        {
            AddDistinct(d.toppings, d.toppingChance, d.maxToppings, order.toppings, rng);
            AddDistinct(d.sides, d.sideChance, d.maxSides, order.sides, rng);
        }
        return order;
    }

    static void AddDistinct<T>(List<T> pool, float chance, int max, List<T> target, System.Random rng)
    {
        if (pool.Count == 0 || max <= 0 || rng.NextDouble() >= chance) return;
        int count = rng.Next(1, Mathf.Min(max, pool.Count) + 1);
        foreach (var x in pool.OrderBy(_ => rng.Next()).Take(count)) target.Add(x);
    }

    static float NextDelay(GameData d, float interval, float t01)
    {
        float m = Mathf.Max(0.05f, d.affluence.Evaluate(Mathf.Clamp01(t01)) / d.affluenceAvg);
        return interval / m;
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

    [MenuItem("Parripollo/Balance/Run Stock Simulation")]
    static void RunFromMenu() => Debug.Log(RunAll("manual"));
}
