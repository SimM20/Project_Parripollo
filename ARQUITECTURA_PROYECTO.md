# ARQUITECTURA — Project_Parripollo

> Documentación técnica de referencia. Objetivo: entender el proyecto sin leer los scripts.

---

## 0. Ficha técnica

| Campo | Valor |
|---|---|
| Motor | Unity **2022.3.62f3**, URP (2D), Input Manager legacy (`Input.GetKeyDown`) |
| Lenguaje | C#, assembly única `Assembly-CSharp` (sin `.asmdef` en `Assets/Scripts`) |
| Código propio | `Assets/Scripts/` — **110 archivos, ~13.7k líneas** |
| Third-party | `Assets/AmplifyShaderEditor/` (plugin de shaders, **ignorar**), TextMesh Pro |
| Género | Simulador de parrilla argentina: cocinar cortes, armar platos/sándwiches, entregar a clientes por noche |
| Persistencia | Solo `init.cfg` (resolución/FPS). **No hay savegame**: el progreso vive en objetos `DontDestroyOnLoad` |
| Idioma del dominio | Español (`Crudo`, `Jugoso`, `Hecho`, `Muy_Hecho`, `Pasado`, `Quemado`) |

**Escenas (build order)**: `0 MainMenuScene` → `1 GameScene` → `2 TutorialScene` → `3 EndScene`.
`SampleScene.unity` existe pero **no está en build** (legacy).

---

## 1. Estructura de módulos

```
Assets/Scripts/
├── (raíz)      Managers globales, modelo base de items/grilla, utilidades
├── Core/       Orquestación de partida + transferencia de items entre vistas
├── Grill/      Vista Parrilla: cocción, capas carne/carbón, HUD de hover
├── Cooler/     Vista Heladera: inventario persistente + visualizadores de stock
├── Build/      Vista Armado: plato, pan/guarniciones/toppings, undo
├── Customers/  Clientes: spawn, paciencia, selección, burbujas de pedido
├── Orders/     Modelo y generación de pedidos
├── Food/       Catálogo (SO), validación de platos, evaluación económica de cocción
├── Shop/       Tienda (post-noche): carrito, tabs, compra. Dos capas de UI
└── UI/         ViewManager, Tutorial, notificaciones de parrilla, HUD SO, feedback
```

### Responsabilidades por carpeta

| Carpeta | Responsabilidad | Archivos clave |
|---|---|---|
| **raíz** | Singletons de sesión (`UIManager`, `AudioManager`, `PlayerWallet`, `CoalConsumptionTracker`), modelo base drag&drop (`Item`), grilla (`GridSlot`), entidades físicas (`Meat`, `Coal`), buffer de carbón, arranque (`Init`), utilidades de escena | `Item.cs`, `GridSlot.cs`, `Meat.cs`, `Coal.cs`, `PlayerWallet.cs`, `CoalConsumptionTracker.cs`, `SceneManagementUtils.cs` |
| **Core/** | Bucle de partida e input global (`GameManager`), armado del plato (`BuildStationSystem`), staging de carne entre vistas (`MeatTransferBuffer`), draggables inter-vista, basura | `GameManager.cs` (445), `MeatTransferBuffer.cs` (949), `BuildStationSystem.cs` |
| **Grill/** | Propagación de calor y spawn en grilla (`GrillSystem`), datos de corte (`MeatCutSO` — **está en `MeatType.cs`**), toggle capa carne/carbón, barra y burbuja de cocción por hover | `GrillSystem.cs`, `MeatType.cs`, `GrillLayerToggle.cs`, `MeatCookHoverBar.cs` |
| **Cooler/** | Stock persistente `ItemDataSO → int` (`CoolerSystem`, DDOL). El resto de la carpeta (visualizadores y draggables de la heladera) está **deprecado** desde el StockPanel | `CoolerSystem.cs` · deprecados: `CoolerStockVisualizer.cs`, `CoalStockVisualizer.cs`, `CoolerDraggableMeat.cs`, `DraggableCoal.cs` |
| **Build/** | Zona de drop del plato (`BuildFoodDropZone`), draggables de pan/side/topping, frascos vertibles con salsa (`ToppingDraggable`), historial de undo (patrón Command) | `BuildFoodDropZone.cs`, `ToppingDraggable.cs` (670), `BuildUndoHistory.cs`, `BuildUndoActions.cs` |
| **Customers/** | Spawn ponderado por noche, tick de paciencia, modo selección de entrega, view + burbuja + recuadro | `CustomerSystem.cs` (673), `Customer.cs`, `CustomerView.cs` |
| **Orders/** | `Order` (corte + punto pedido + pan/sides/toppings) y generación aleatoria ponderada | `OrderSystem.cs`, `Order.cs` |
| **Food/** | Catálogo estático (`FoodCatalogSO`), reglas de validez (`DishValidator`), **economía de entrega** (`CookingDeliveryEvaluator`), puente catálogo+stock (`FoodAvailabilityService`) | `CookingDeliveryEvaluator.cs`, `DishValidator.cs`, `FoodCatalogSO.cs` |
| **Shop/** | Lógica de tienda headless (`ShopSystem`) + **dos capas de UI paralelas**: `*2D` (world-space, `SpriteRenderer`/`TextMeshPro`, **la activa**) y `*UI` (uGUI/Canvas, legacy) | `ShopSystem.cs`, `ShopGrid2D.cs`, `ShopItemCell2D.cs` |
| **UI/** | Conmutación de vistas (`ViewManager`), tutorial data-driven (`TutorialManager` + `TutorialStepSO`), notificaciones laterales de parrilla, feedback de entrega | `ViewManager.cs`, `TutorialManager.cs` (749), `GrillNotificationManager.cs` |
| **UI/StockPanel/** | Panel desplegable de stock dentro de la vista Grill: estado abierto/cerrado y layout (`StockPanelController`), celda + arrastre directo a la parrilla (`StockPanelSlot`), pestaña de toggle (`StockPanelTab`) | `StockPanelController.cs`, `StockPanelSlot.cs`, `StockPanelTab.cs` |

### Assets de datos

```
Assets/ScriptableObjects/
├── Cuts/            MeatCutSO   — Chinchulin, Costillita, Matambre, Paty, Pechuga, Tira de asado, Vacio
├── Breads/          BreadSO     — Pan de paty, Pan flauta
├── Sides/           SideSO      — Fritas, Ensalada verde, Ensalada de papa y huevo
├── Toppings/        ToppingSO   — Chimichurri, Salsa criolla
├── Tutorial/        TutorialStepSO ×30 (secuencia ordenada 1..30)
├── FoodCatalog.asset / FoodCatalogTutorial.asset
├── ShopConfig.asset / CoalData.asset
└── Chorizo.asset, ChorizoTutorial.asset
```

---

## 2. Flujo de datos y comunicación

### 2.1 Grafo de dependencias

```mermaid
graph TD
    subgraph DDOL["DontDestroyOnLoad (sobreviven cambio de escena)"]
        PW[PlayerWallet]
        CCT[CoalConsumptionTracker]
        CS_[CoolerSystem]
        TS[ToppingStock]
    end

    GM[GameManager<br/>input + orquestación]
    VM[ViewManager]
    CUS[CustomerSystem]
    GS[GrillSystem]
    BSS[BuildStationSystem]
    MTB[MeatTransferBuffer]
    CTB[CoalTransferBuffer]
    SHOP[ShopSystem]
    TM[TutorialManager]
    UIM[UIManager]

    GM -->|SerializeField| VM & CUS & GS & BSS & SHOP
    GM -.->|SendMessage duck-typed| MTB & CTB
    GM -->|CookingDeliveryEvaluator.Validate/EvaluateCut| EVAL[CookingDeliveryEvaluator]
    GM -->|Add pago+propina| PW
    GM -->|EndNight| CCT

    CUS -->|event OnNightEnded| GM
    CUS --> OS[OrderSystem] --> ORD[Order]
    CUS --> FAS[FoodAvailabilityService] --> CAT[FoodCatalogSO]
    FAS --> CS_

    VM -->|event OnViewChanged| TM & GNM[GrillNotificationManager]

    CS_ -->|event OnInventoryChanged| CSV[Cooler/CoalStockVisualizer]
    CSV -->|AddComponent + SendMessage| DRG[CoolerDraggableMeat / DraggableCoal]
    DRG -->|TryTake + Enqueue*AtPoint| MTB & CTB

    MTB -->|TrySpawnMeatAtPoint| GS
    CTB -->|TrySpawnCoalAtPoint| GS
    GS --> GRID[GridSlot ×N]
    GRID -->|Cook heat| MEAT[Meat] 
    GRID -->|Burn| COAL[Coal]

    MTB -->|BuildMeatHolderDraggableMeat| BDZ[BuildFoodDropZone]
    BDZ -->|AddCut/SetBread/AddSide/AddTopping| BSS
    BDZ -->|Push| BUH[BuildUndoHistory]
    BSS -->|event OnAssemblyCleared| BUH

    SHOP --> PW & CS_ & TS & CAT
    SHOP -->|events OnCartChanged/OnTabChanged/OnPurchaseResult| SUI[Shop*2D / Shop*UI]

    PW -->|event OnMoneyChanged| UIM & WD[WalletDisplay] & SUI
    UIM --> HUD[HudManager] --> HC[HudContainer]

    MEAT -.->|static Notify*| TM
    COAL -.->|static Notify*| TM
    BSS -.-> TM
```

### 2.2 Patrones usados

| Patrón | Dónde | Detalle |
|---|---|---|
| **Singleton** (`static Instance`) | `GameManager`, `UIManager`, `AudioManager`, `PlayerWallet`*, `CoolerSystem`*, `ToppingStock`*, `CoalConsumptionTracker`*, `TutorialManager`, `BuildUndoHistory`, `GrillNotificationManager`, `MeatHoverBubble`, `MeatCookHoverBar`, `CustomerHoverBubble`, `CustomerSelectionFrame`, `DeliveryFeedbackText` | `*` = además `DontDestroyOnLoad`. Los de escena se reasignan en `Awake` sin guard. `GrillLayerToggle` usa `private static instance` |
| **Observer** (`event Action`) | Ver tabla 2.3 | Suscripción en `OnEnable`/`Start`, desuscripción en `OnDisable`/`OnDestroy` |
| **Static notification hub** | `TutorialManager.Notify*(...)` | 11 métodos estáticos no-op si `Instance == null` → en `GameScene` el tutorial no existe y nada cambia |
| **Command** | `IBuildUndoAction` + `BuildUndoHistory` (pila) | `AddSideUndoAction`, `AddToppingUndoAction`, `SetBreadUndoAction`. **La carne nunca es reversible** |
| **Buffer / staging area** | `MeatTransferBuffer`, `CoalTransferBuffer` | Guardan `BufferedMeatData`/`BufferedCoalData` (POCO con tiempos de cocción) y reconstruyen los visuales; permiten mover items entre vistas sin instanciar `Meat`/`Coal` reales |
| **Duck typing por reflexión / `SendMessage`** | `GameManager`→buffers, `MeatHolderDraggableMeat`, `CoolerDraggableMeat`, `*StockVisualizer` | `Type.GetType` sobre todos los assemblies + `MethodInfo.Invoke` / `SendMessage(..., DontRequireReceiver)`. Rompe el binding estático a propósito |
| **Registro estático de instancias** | `Coal.ActiveCoals`, `BuildFoodDropZone.ActiveZones`, `TrashZone.ActiveZones`, `ToppingDraggable.ActiveInstances` | Alta en `OnEnable`, baja en `OnDisable`/`OnDestroy`. Habilita APIs estáticas tipo `TryAcceptAt`, `ClearAllSplatters` |
| **Data-driven (ScriptableObject)** | `ItemDataSO` → `MeatCutSO`, `CoalSO`, `UpgradeSO`; `BreadSO`, `SideSO`, `ToppingSO`, `ProductVariantSO`, `FoodCatalogSO`, `ShopConfigSO`, `TutorialStepSO`, `HudDatabaseSO` | ⚠️ Los SO mutan en runtime (`isUnlocked`, `isPurchased`) → **el estado persiste entre sesiones de Editor** |
| **Service / Facade** | `FoodAvailabilityService` | Cruza `FoodCatalogSO` (estático) con `CoolerSystem` (stock live) |
| **Static utility / Extension methods** | `DishValidator`, `CookingDeliveryEvaluator`, `SceneManagementUtils`, `MeatHoverText.ToHoverString()`, `OrderText.ToHoverString()` | Sin estado, testeables aisladamente |
| **Object pool** | `GrillNotificationManager.groupPool` | Reutiliza grupos de notificación |
| **Construcción procedural de UI** | `GrillNotificationBubbleUI.CreateProceduralBubble`, `GrillNotificationGroupUI.CreateProceduralGroup`, `CustomerSelectionFrame.BuildBars`, `ToppingDraggable.CreateSauceBar` | Generan jerarquía + sprites (`Texture2D`) en runtime si falta prefab |
| **Template Method** | `Item` → `Meat` → `MeatInstance`; `GridSlot` → `GrillSlot` | `virtual OnMouseUp/HandleHeldInput/OnPickedUp/UpdateHoverPreview/Cook` |

### 2.3 Catálogo de eventos

| Emisor | Evento | Consumidores |
|---|---|---|
| `ViewManager` | `OnViewChanged(ViewType)` | `TutorialManager`, `GrillNotificationManager`, `StockPanelController` |
| `CustomerSystem` | `OnNightEnded` (campo `Action`) | `GameManager.EndNight` |
| `CoolerSystem` | `OnInventoryChanged` | `StockPanelController`, `ShopGrid2D`, `ShopDetailPanel2D`, `ShopHeader2D` (+ los visualizadores deprecados del cooler) |
| `CoolerSystem` | `OnMissingItemRequested(ItemDataSO)` | (sin consumidor actual — hook futuro) |
| `BuildStationSystem` | `OnAssemblyChanged`, `OnAssemblyCleared` | `BuildUndoHistory.Clear` |
| `BuildUndoHistory` | `OnHistoryChanged` | `RollbackButtonUI` (habilita/deshabilita) |
| `PlayerWallet` | `OnMoneyChanged(float)` | `UIManager`, `WalletDisplay`, `ShopGrid2D`, `ShopHeader2D`, `ShopHeaderUI` |
| `ToppingStock` | `OnStockChanged` | `ShopGrid2D`, `ShopDetailPanel2D` |
| `ShopSystem` | `OnCartChanged`, `OnTabChanged`, `OnPurchaseResult(bool,string)` | `ShopGrid2D`, `ShopTabBar2D`, `ShopHeader2D`, `ShopNextButtonUI`, `ShopBreadcrumb*` |
| `SceneManagementUtils` | `OnSceneLoaded` (static) | (disponible; suscrito vía `RuntimeInitializeOnLoadMethod`) |
| `ShopButton2D` / `ShopTabButton2D` | `OnClicked`, `OnTabClicked(ShopTabType)` | Celdas, barras de tabs |
| `CoalStock` | `OnChanged(int)` | (clase legacy, sin uso activo) |

### 2.4 Secuencia: del stock al cliente

```
StockPanel (dentro de la vista Grill) ──drag directo──► GridSlot de la parrilla
   │  StockPanelSlot.OnMouseDown  → spawnea un "fantasma" sin collider que sigue al mouse
   │  StockPanelSlot.OnMouseUp    → CoolerSystem.TryTake(item,1)
   │                              → GrillSystem.TrySpawnMeatAtPoint / TrySpawnCoalAtPoint
   │                              → si el spawn falla: CoolerSystem.Add(item,1)  [rollback]
   │  Sin buffer intermedio: el retiro y la colocación ocurren en la misma vista.

MeatHolder ──drag──► GridSlot de la parrilla        [sigue vivo para el round-trip con Build]
   │  MeatHolderDraggableMeat → (reflexión) MTB.TryDropFromMeatHolderById(id, punto, rot)
   │  → GrillSystem.TrySpawnMeatAtPoint → GridSlot.TryFindContiguousPlacement → PlaceMeat

Parrilla (cocción por frame)
   │  GrillSystem.Update      → UpdateHeatPropagation() reparte calor entre slots
   │  GridSlot.Update         → coal.Burn(); CalculateInternalHeat(); meat.Cook(totalHeatReceived)
   │  Meat.Cook               → acumula SOLO la cara activa; clamp en burnThreshold
   │  Click derecho           → MeatClickable → Meat.Flip() (cambia cara activa)

Parrilla ──drag a zona "ToBuild"──► ToBuild (buffer)
   │  Meat.OnMouseUp → MTB.TryQueueFromGrillToBuild(meat, punto)  [conserva tiempos de cocción]
   │  ──cambio a vista Build──► MTB.MoveToBuildMeatHolder()

BuildMeatHolder ──drag──► PlateDropZone
   │  BuildMeatHolderDraggableMeat → BuildFoodDropZone.TryAcceptMeatAt(punto, cut, state, A, B)
   │  → BuildStationSystem.AddCut(cut, state, sideA, sideB)
   │  Pan/Side/Topping → BuildDraggableFoodItem / ToppingDraggable → SetBread/AddSide/AddTopping + Push(undo)

Entrega  [SPACE en vista Build]
   │  GameManager.TryEnterDeliverySelection → CustomerSystem.BeginDeliverySelection()
   │  A/D navega clientes · SPACE confirma → GameManager.ConfirmDeliverySelection()
   │     1. corte armado == order.PrimaryCut ?
   │     2. DishValidator.ValidateSandwich / ValidatePlatedDish
   │     3. CookingDeliveryEvaluator.Validate → Crudo/Quemado BLOQUEAN (X descarta quemados)
   │     4. CookingDeliveryEvaluator.EvaluateCut por corte → pago + propina
   │     5. PlayerWallet.Add · CustomerSystem.CompleteCustomer · limpiar plato
```

---

## 3. Clases centrales

### 3.1 Orquestación

#### `GameManager` — `Core/GameManager.cs` · Singleton
Bucle de input global y árbitro de la entrega. **No** contiene lógica de cocción.

| Campos clave | |
|---|---|
| `[SF] customerSystem, grillSystem, coolerSystem, viewManager, buildStationSystem, shopSystem, wallet, grillLayerToggle, foodAvailabilityService, catalog` | Referencias de inspector |
| `[SF] MonoBehaviour meatTransferBuffer, coalTransferBuffer` | ⚠️ Tipados como `MonoBehaviour`: se invocan **solo por `SendMessage`** |
| `ViewType lastView` | Detecta transiciones de vista |
| `bool discardContextActive` / `List<int> discardBurnedIndices` | Contexto de descarte con `X`; solo activo tras una entrega bloqueada por quemados |

```csharp
public void EndNight()   // desuscribe, tracker.RegisterDayCompleted(), carga "EndScene"
// privados relevantes: TryServe, ClearBuildAssembly, CleanAshes,
//                      TryDiscardBurnedCuts, TryEnterDeliverySelection, ConfirmDeliverySelection
```

#### `ViewManager` — `UI/ViewManager.cs`
```csharp
ViewType CurrentView { get; }
event Action<ViewType> OnViewChanged;
void Show(ViewType), NextView(), PreviousView()
```
`grillRoot` se oculta desactivando **renderers/colliders/canvases** (queda activo, sigue cocinando);
`coolerRoot`/`buildRoot` usan `SetActive` real.

⚠️ **La Cooler View está deprecada.** Las vistas navegables son solo `Grill` ↔ `Build`:
`NextView`/`PreviousView` recorren ese par y `Show(ViewType.Cooler)` **redirige a `Grill`** con un
warning. El miembro `ViewType.Cooler` se conserva a propósito porque el enum se serializa como `int`
en los assets de tutorial (`TutorialStepSO.requiredView`) y quitarlo correría `Build` y `Shop`.
`Toggle()` fue eliminado (no tenía llamadores).

#### `UIManager` — `UIManager.cs` · Singleton
```csharp
bool IsPaused { get; }
void PauseGame(), UnPauseGame()
void SetActualDay(int), SetTotalCustomers(int), SetActualCustomers(int), SetActualMoney(float)
int  GetActualDay(), GetActualMoney(), GetTotalCustomersPerDay(), GetActualCustomers()
```
Instancia `pauseCanvasPrefab` on-demand. Delega el render a `HudManager` → `HudContainer`.

---

### 3.2 Parrilla y cocción

#### `GrillSystem` — `Grill/GrillSystem.cs`
```csharp
List<GridSlot> slots;  GameObject meatPrefab, coalPrefab;
bool SpawnMeat(MeatCutSO, bool rotateFootprint = false)
bool TrySpawnMeatAtPoint(MeatCutSO, Vector3, out Meat, bool rotateFootprint = false)
bool TrySpawnCoalAtPoint(CoalSO, Vector3, out Coal)   // → CoalConsumptionTracker.ReportConsumption(1)
Meat GetCookedMeat(MeatCutSO)
void RemoveMeat(Meat), SetMeatVisualsVisible(bool)
```
`Awake` → `GridSlot.AssignGridCoordinates(slots)`. `Update` → `UpdateHeatPropagation()`:

```
reset:      slot.totalHeatReceived = slot.internalHeat
vecinos:    |dx|<=1 && |dy|<=1  → factor 0.35 (ortogonal) / 0.20 (diagonal)
columna:    |dx|<=1 && source.gridY > target.gridY && dy<=3 → factor 0.4 / dy
clamp:      AddExternalHeat → Mathf.Min(10f, ...)
```

#### `GridSlot` — `GridSlot.cs`
```csharp
ItemType acceptsType;  GameObject currentItem;  List<Coal> stackedCoals;  // MAX_COAL = 3
float internalHeat, totalHeatReceived;  int gridX, gridY;
bool IsOccupied { get; }   Meat currentMeat { get; }

void PlaceItem(GameObject), PlaceMeat(Meat), ClearSlot(), RemoveCoal(Coal)
bool CanPlaceItem(ItemType, GameObject)     // respeta GrillLayerToggle.IsItemTypeAllowed
void SetHoverPreview(bool, bool), ClearHoverPreview(), SetBaseHoverColor(Color)

static List<List<GridSlot>> BuildLogicalRows(IList<GridSlot>)
static void AssignGridCoordinates(IList<GridSlot>)
static bool TryFindContiguousPlacement(IList<GridSlot>, Vector2Int size, Vector3 worldPoint,
                                       ItemType, GameObject incoming, out List<GridSlot>)
```
`Update`: quema carbones → `CalculateInternalHeat()` (pila: `×1.0`, `×0.307`, `×0.153`) → `meat.Cook(totalHeatReceived)`.
Las filas se agrupan por Y mundial (`AXIS_TOLERANCE = 0.1`); **la columna es índice de orden dentro de la fila y por `acceptsType`** — nunca X mundial.

#### `Meat : Item` — `Meat.cs`
```csharp
MeatCutSO cut;  float sideACookTime, sideBCookTime;  bool isSideA;  MeatStates state;

float SideAProgress01, SideBProgress01, CookedPercent01, ActiveSideProgress01
float HeatScale, BurnThreshold
MeatStates SideAState, SideBState, ActiveSideState
bool IsAnySideBurned, IsOnGrill, IsSideAActive, IsGridRotated

virtual void Cook(float heatFromSlot)   // no-op si TutorialManager.IsCookingPaused; 1 vez por frame
void Flip()                             // invierte cara → TutorialManager.NotifyMeatFlipped
void SetCut(MeatCutSO), SetGridRotation(bool), ToggleGridRotation()
void RegisterOccupiedSlot(GridSlot), UnregisterOccupiedSlot(GridSlot), ReleaseOccupiedSlots()
void RefreshState()                     // deriva estado del calor → NotifyMeatStateChanged
```
`OnMouseUp` prioriza: `TrashZone` → `MeatTransferBuffer.TryQueueFromGrillToBuild` → colocación en grilla → volver a origen.
`R` mientras se arrastra rota el footprint. `MeatInstance : Meat` añade audio de chisporroteo.

#### `MeatCutSO : ItemDataSO` — **`Grill/MeatType.cs`** (nombre de archivo ≠ clase)
```csharp
string cutName => itemName;   Vector2Int GrillSpace;   Vector3 visualOffset;
CookingSprites cookingSpritesA, cookingSpritesB;   float timeHeatA, timeHeatB, heatScale;
ServingMode servingMode;   BreadSO requiredBread;   bool isUnlocked;
float sellPricePlate, sellPriceSandwich;

float GetHeatScale()        // heatScale, o timeHeat*3 si es 0
float GetStateBandHeat()    // S / 6
float GetBurnThreshold()    // 5S / 6  ← tope de acumulación por cara
MeatStates GetStateForHeat(float)   // floor(heat / band), clamp 0..5
Sprite GetSpriteForState(MeatStates, bool sideA)
```

**Modelo de cocción — la escala `S` se divide en 6 bandas iguales:**

| Índice | Estado | Rango de calor acumulado |
|---|---|---|
| 0 | `Crudo` | `[0, S/6)` — no entregable |
| 1 | `Jugoso` | `[S/6, 2S/6)` |
| 2 | `Hecho` | `[2S/6, 3S/6)` |
| 3 | `Muy_Hecho` (UI: "Bien Hecho") | `[3S/6, 4S/6)` |
| 4 | `Pasado` | `[4S/6, 5S/6)` — último punto válido |
| 5 | `Quemado` | `>= 5S/6` — irreversible, no entregable |

Cada cara acumula por separado. Solo acumula la cara **apoyada** (`isSideA`).

#### `Coal : Item` — `Coal.cs`
```csharp
static readonly List<Coal> ActiveCoals;   // registro global (usado por CleanAshes)
CoalSO coalData;  float maxBurnTime = 60f, currentBurnTime;  CoalStates state;
float GetCurrentHeatOutput()   // 6.5 * (1 - burnTime/maxBurnTime), solo si Encendido
void Burn(), SetVisualVisibility(bool), RegisterOccupiedSlot(GridSlot), ReleaseOccupiedSlots()
```
`enum CoalStates { Apagado, Encendido, Ceniza }` — `Apagado` pasa a `Encendido` en el primer `Burn()`.

#### `GrillLayerToggle` — `Grill/GrillLayerToggle.cs`
```csharp
enum GrillLayer { Meat, Coal }
GrillLayer CurrentLayer { get; }
static bool IsItemTypeAllowed(ItemType)   // gatea GridSlot.CanPlaceItem
void Toggle(), ShowLayer(GrillLayer), RefreshVisibility()
```
La capa inactiva queda visible con `inactiveAlpha` y colliders desactivados.

---

### 3.3 Inventario y transferencia

#### `CoolerSystem` — `Cooler/CoolerSystem.cs` · Singleton + DDOL
```csharp
event Action OnInventoryChanged;
event Action<ItemDataSO> OnMissingItemRequested;

int  GetCount(ItemDataSO)
IEnumerable<KeyValuePair<ItemDataSO,int>> EnumerateStock()
void Add(ItemDataSO, int = 1)          // carbón capeado a 40
void SetStockDirectly(ItemDataSO, int)
bool TryTake(ItemDataSO, int = 1)
void InformMissingItem(ItemDataSO)
static void PrepareForNewGame()        // invalida el backup estático
```
Guarda un `static stockBackup` en `OnDestroy` para sobrevivir a destrucciones inesperadas del DDOL; `SceneManagementUtils.ReturnToMainMenu()` lo limpia.

#### `StockPanelController` — `UI/StockPanel/StockPanelController.cs` · Singleton
Panel desplegable de stock, **solo dentro de la vista Grill**. Reemplaza a la Cooler View.
Vive anidado en `Prefabs/GrillView.prefab` como instancia de `Prefabs/UI/StockPanel.prefab`.

```csharp
static StockPanelController Instance;
bool IsOpen, IsAnimating, CanBeginDrag;
void Toggle(), Open(), Close(bool instant = false)
void RefreshSlots()
void NotifyDragStarted(StockPanelSlot), NotifyDragEnded(StockPanelSlot), CancelActiveDrag()
void SetViewManager(ViewManager)
bool IsPointOverPanel(Vector3), IsPointInDropArea(Vector3)
```

- **No duplica stock**: se suscribe a `CoolerSystem.OnInventoryChanged` y repinta. Las cantidades salen siempre de `GetCount`.
- **Orden de slots determinista**: `FoodCatalogSO.GetAllCuts()` filtrado por `stock > 0`, después los cortes con stock que no estén en el catálogo (ordenados por `itemName` — así aparece `ChorizoTutorial`), y el carbón **siempre último y siempre visible**, gris cuando está en 0. Nunca usar `EnumerateStock()` directo: el orden del `Dictionary` no es determinista.
- Layout de grilla manual (`columns`, `cellSpacing`, `firstCellLocalOffset`), slots pooleados, sin scroll.
- Se abre/cierra deslizando `SlidingRoot` en X con una corrutina que usa **`Time.unscaledDeltaTime`** (el diálogo de `TutorialOfferController` pone `Time.timeScale = 0` al entrar a `GameScene`).
- Arranca **cerrado** en `Start()`: `OnViewChanged` no dispara en el `Show(startView)` inicial porque `ViewManager.Show` solo invoca el evento si la vista cambió.
- Fuera de la vista Grill hace `SetActive(false)` sobre `SlidingRoot` — necesario porque `ViewManager.SetVisualVisibility` apaga `SpriteRenderer`/`Collider2D`/`Canvas` pero **no** los `MeshRenderer` de TextMeshPro.

#### `StockPanelSlot` — `UI/StockPanel/StockPanelSlot.cs`
Una celda = una variedad. `Bind(ItemDataSO, count, owner)`, `SetSortingOrder(int)`, `CancelDrag()`.
Maneja el arrastre completo desde `OnMouseDown` hasta `OnMouseUp` **en el mismo componente**: Unity
no transfiere `OnMouseDrag`/`OnMouseUp` a otro collider, así que el fantasma que sigue al mouse no
puede hacerse cargo del drag. `FitIconToSlot()` escala el ícono por `sprite.bounds` para que cortes
con sprites de distinto tamaño se vean uniformes. `R` rota el footprint (solo carne; el carbón no
tiene rotación en ninguna parte del proyecto).

Gates del drop, **antes** de cualquier `TryTake`:
1. Soltar sobre el propio panel cancela siempre (`IsPointOverPanel`).
2. `requireDropAreaHit` + `dropArea` (opcional, **off** por defecto) exige soltar dentro de un collider concreto. Apagado, el comportamiento es el mismo que los drags del `MeatHolder`.

#### `StockPanelTab` — `UI/StockPanel/StockPanelTab.cs`
Pestaña lateral con `BoxCollider2D` + `OnMouseDown` → `controller.Toggle()`.

#### `MeatTransferBuffer` — `Core/MeatTransferBuffer.cs` (949 líneas, el archivo más grande)
Cuatro colas paralelas de `BufferedMeatData` (POCO: `cut`, `sideACookTime`, `sideBCookTime`, `isSideA`, `state`, `isGridRotated`) + sus visuales y posiciones locales.

```
ToGrill ──[MoveToMeatHolder]──► MeatHolder ──[TryDropFromMeatHolderById]──► Parrilla
ToBuild ──[MoveToBuildMeatHolder]──► BuildMeatHolder ──[ConsumeBuildMeatEntry]──► Plato
```

```csharp
void EnqueueToGrill(MeatCutSO)
bool EnqueueToGrillAtPoint(MeatCutSO, Vector3)
bool EnqueueToBuildAtPoint(MeatCutSO, Vector3)
void MoveToMeatHolder(), MoveToBuildMeatHolder(), ClearBuildMeatHolder()
bool TryQueueFromGrillToBuild(Meat, Vector3)          // destruye el Meat, conserva la cocción
bool TryDropFromMeatHolder[ById](..., Vector3, bool rotateFootprint)
bool TryDropFromToBuild[ById](..., Vector3, bool rotateFootprint)
void ConsumeBuildMeatEntry(int entryId, GameObject)
void RemovePlateMeatVisualAt(int), ClearPlateMeatVisuals(), SetPlateMeatVisualsVisible(bool)
void UpdatePlateMeatSprite(Sprite)
bool TryCaptureLastPlateMeatVisual(out GameObject, out Sprite, out Vector3, out Vector3)  // undo
void RestorePlateMeatVisual(GameObject, Sprite, Vector3, Vector3)                          // undo
void UpdateMeatHolderHover(MeatCutSO, Vector3, bool), ClearMeatHolderHover(), RefreshVisuals()
```
Resuelve anclas por nombre si faltan en el inspector: `ToBuild` bajo `GrillView`, `MeatHolderSlot`/`MeatHolder` bajo `BuildView`.

#### `CoalTransferBuffer` — `CoalTransferBuffer.cs`
Análogo, dos colas (`ToGrill` → `CoalHolder`): `EnqueueToGrill[AtPoint]`, `MoveToCoalHolder()`, `TryDropFromCoalHolderById(int, Vector3)`, `UpdateCoalHolderHover`, `ClearCoalHolderHover`, `RefreshVisuals`.

---

### 3.4 Armado y entrega

#### `BuildStationSystem` — `Core/BuildStationSystem.cs`
```csharp
struct CutSideStates { MeatStates sideA, sideB; bool IsBurned, IsRaw; }

event Action OnAssemblyChanged, OnAssemblyCleared;
IReadOnlyList<MeatCutSO>     AssembledCuts
IReadOnlyList<MeatStates>    AssembledCutStates
IReadOnlyList<CutSideStates> AssembledCutSideStates
BreadSO AssembledBread;  IReadOnlyList<SideSO> AssembledSides;  IReadOnlyList<ToppingSO> AssembledToppings
bool HasAnyCut, HasBread

void AddCut(MeatCutSO, MeatStates state, MeatStates sideA, MeatStates sideB)
void RemoveLastCut(), RemoveCutAt(int), SetBread(BreadSO), AddSide(SideSO), AddTopping(ToppingSO)
void RemoveLastSide(SideSO), RemoveLastTopping(ToppingSO)   // usados por el undo
void ClearAssembly()
PlatedDish TryBuildPlatedDish(out string reason)
Sandwich   TryBuildSandwich(out string reason)
ProductVariantSO TryResolveVariant()
```

#### `CookingDeliveryEvaluator` — `Food/CookingDeliveryEvaluator.cs` · **static**
Núcleo de la economía. Constantes: `ReducedPriceMultiplier = 0.5`, `TipPercentOfPrice = 0.2`, `MinimumPerfectTip = 1`.

```csharp
struct CutResult          { int worstOffset; float price; bool tipEligible; }
struct DeliveryValidation { int rawCount, burnedCount; List<int> burnedIndices; bool IsBlocked; }

static DeliveryValidation Validate(IReadOnlyList<CutSideStates>,
                                   IReadOnlyList<MeatCutSO>, Func<MeatCutSO,bool> isBurnedExempt)
static CutResult EvaluateCut(MeatStates sideA, MeatStates sideB, MeatStates requested, float basePrice)
static float  CalculateTip(float basePrice, float patience01)
static string BuildBlockedMessage(int rawCount, int burnedCount)
```

| `worstOffset = max(|A−pedido|, |B−pedido|)` | Pago | Propina |
|---|---|---|
| `0` | 100 % | ✅ `max(1, floor(base × 0.2 × patience01))` |
| `1` | 100 % | ❌ |
| `>= 2` | `floor(base × 0.5)` | ❌ |

**Bloqueo**: cualquier cara `Crudo` o `Quemado` bloquea la entrega **completa** (atómica). `Quemado` tiene prioridad sobre `Crudo`. `isBurnedExempt` solo lo usa el tutorial (`TutorialManager.IsBurnedDeliveryExempt`) para evitar un softlock.

#### `DishValidator` — `Food/DishValidator.cs` · **static**
```csharp
static bool ValidatePlatedDish(PlatedDish, out string reason)   // rechaza SandwichOnly al plato
static bool ValidateSandwich(Sandwich, out string reason)       // rechaza PlatedOnly; exige requiredBread
static ProductVariantSO ResolveVariant(PlatedDish|Sandwich, FoodCatalogSO)
```

#### `BuildUndoHistory` — `Build/BuildUndoHistory.cs` · Singleton
```csharp
interface IBuildUndoAction { void Undo(); }
int Count { get; }   event Action OnHistoryChanged;
void Push(IBuildUndoAction), UndoLast(), Clear()
```
Se autolimpia con `BuildStationSystem.OnAssemblyCleared`. **Solo registra pan / guarniciones / toppings — nunca cortes de carne.**

---

### 3.5 Clientes y pedidos

#### `CustomerSystem` — `Customers/CustomerSystem.cs`
```csharp
Action OnNightEnded;                      // campo público, no `event`
Customer currentCustomer;                 // compat con GameManager
Customer SelectedCustomer { get; }
bool IsDeliverySelectionActive { get; }
bool IsReadyForSpawning { get; }
IReadOnlyList<Customer> ActiveCustomers { get; }
FoodCatalogSO Catalog { get; }            // vía FoodAvailabilityService

void StartNight()
void SpawnCustomer(bool ignoreNightLimit = false)
void SelectCustomer(Customer), SelectAdjacentCustomer(int direction)
bool BeginDeliverySelection();  void EndDeliverySelection()
void CompleteCustomer(Customer), ShowSelectedOrderBubble()
```
Clientes por noche: `min(customersFirstNight + (noche−1) × customersAddedPerNight, maximumCustomersPerNight)`
(por defecto `20 + 5·(n−1)`, cap `70`; máx. `4` simultáneos).
`Update` descuenta paciencia y expulsa a los `IsAngry`. Al quedar `spawnedTonight >= target && activeCustomers == 0` → `OnNightEnded`.
`CompactSlots()` corre las views a la izquierda al liberarse un slot.

#### `Customer` — POCO
`type`, `order`, `patience`, `maxPatience`, `slotIndex`; `bool IsAngry`; `float Patience01`; `Init(...)`, `UpdatePatience(float)`.

#### `OrderSystem` / `Order` — `Orders/`
```csharp
OrderSystem(List<WeightedOrderCut> cuts);   Order GenerateOrder();
// punto pedido aleatorio en [Jugoso .. Pasado] — nunca Crudo ni Quemado
// sándwich si servingMode == SandwichOnly, o Both con 50 % de probabilidad
```
`Order`: `meat` (legacy) + `cuts`, `requestedStates`, `bread`, `sides`, `toppings`; `IsSandwich`, `PrimaryCut`, `SetSingleCut(cut, state)`, `GetRequestedState(int)`.

---

### 3.6 Progresión, economía y tienda

#### `CoalConsumptionTracker` — `CoalConsumptionTracker.cs` · Singleton + DDOL
**Fuente de verdad del progreso de la partida.**
```csharp
int   TotalCoalConsumed { get; }
int   DaysPlayed { get; }
int   CurrentNight => DaysPlayed + 1;
float AverageCoalPerDay { get; }
void  ReportConsumption(int), RegisterDayCompleted(), ConfigureNightTwoCut(MeatCutSO), ResetProgress()
```
`ApplyProgressionUnlocks()` fija `nightTwoCut.isUnlocked = (CurrentNight >= 2)`.

#### `PlayerWallet` — Singleton + DDOL
`float Money { get; }` · `event Action<float> OnMoneyChanged` · `CanAfford(float)`, `TrySpend(float)`, `Add(float)`. Arranca en `1000`.

#### `ShopSystem` — `Shop/ShopSystem.cs`
Lógica pura, sin UI. Vive en `EndScene` (también hay una copia en `GameScene`).
```csharp
ShopTabType CurrentTab { get; }        // Coal → Meat → Upgrades → Toppings
event Action OnCartChanged, OnTabChanged;  event Action<bool,string> OnPurchaseResult;

void SetTab(ShopTabType)
IReadOnlyList<ItemDataSO> GetItemsForCurrentTab() / GetItemsForTab(ShopTabType)
IReadOnlyList<ToppingSO>  GetToppings()
bool IsPurchasable(ItemDataSO), IsToppingPurchasable(ToppingSO)
int  GetSuggestedCoalUnits(), GetSuggestedCoalBags()    // max(0, consumoPromedio − stock)
int  GetCartQty(ItemDataSO);  void SetQty(...), IncrementQty(...), ClearCart()
float CartTotal(), MoneyAfterPurchase();  int CartCoalBags()
List<MeatCutSO> GetLowStockCuts()
bool TryConfirmPurchase(out string), TryBuyNow(ItemDataSO,int,out string), TryBuyToppingNow(...)
```
Dos carritos separados: `cart` (`ItemDataSO`) y `toppingCart` (`ToppingSO` → `ToppingStock`).

---

### 3.7 Tutorial

#### `TutorialManager` — `UI/TutorialManager.cs` · Singleton
Máquina de estados data-driven sobre `List<TutorialStepSO>` (30 pasos). **Solo arranca si `SceneManagementUtils.GetCurrentName() == "TutorialScene"`.**

```csharp
static bool IsCookingPaused { get; }      // leído por Meat.Cook
static bool IsBurnedDeliveryExempt(MeatCutSO)
void StartTutorial(), AdvanceStep()

// Hub estático de notificaciones (no-op si Instance == null):
static void NotifyMeatDraggedToGrill/ToBuild(MeatCutSO)
static void NotifyCoalDraggedToGrill(CoalSO), NotifyCoalPlacedOnGrill(CoalSO)
static void NotifyMeatPlacedOnGrill(MeatCutSO), NotifyMeatPlacedOnBuildZone(MeatCutSO)
static void NotifyGrillLayerChanged(GrillLayerToggle.GrillLayer)
static void NotifyMeatFlipped(MeatCutSO), NotifyMeatStateChanged(Meat)
static void NotifyDeliverySelectionBegun(), NotifyProductDelivered()
```
Hace backup del stock real del `CoolerSystem`, lo sustituye por el del tutorial (`ChorizoTutorial=3`, `Coal=3`, resto `0`) y lo restaura al terminar. Al finalizar carga `GameScene`.

`TutorialConditionType`: `ChangeView, ConfirmButton, DragMeatToGrill, DragMeatToBuild, DragCoalToGrill, DragMeatToGrillSlots, ToggleGrillLayer, DragCoalToGrillSlots, FlipMeat, MeatReachesDoneness, DragMeatToMeatHolder, BeginDeliverySelection, DeliverProduct, DragMeatToBuildZone`.

---

## 4. Puntos de entrada e inicialización

### 4.1 Arranque de la aplicación

```
MainMenuScene (build index 0)
  ├── Init.Awake()                        ← ÚNICO punto de config de plataforma
  │     lee %USERPROFILE%/AppData/.../init.cfg  (Application.persistentDataPath)
  │     claves: TargetFPS=120, ResolutionX=1920, ResolutionY=1080, Fullscreen=true
  │     aplica Application.targetFrameRate + Screen.SetResolution
  └── MainMenuPanel.StartNewGame() → LoadSceneByName("GameScene")

[RuntimeInitializeOnLoadMethod]
  SceneManagementUtils.Initialize()          BeforeSceneLoad  → engancha SceneManager.sceneLoaded
  GrillNotificationManager.AutoInitialize()  AfterSceneLoad   → crea el manager si no existe
```

### 4.2 Orden de inicialización en `GameScene`

| Fase | Qué corre |
|---|---|
| `Awake` | Singletons se registran (`GameManager`, `UIManager`, `AudioManager`, `CoolerSystem`+DDOL, `PlayerWallet`+DDOL, `ToppingStock`+DDOL, `CoalConsumptionTracker`+DDOL, `BuildUndoHistory`). `GrillSystem.AssignGridCoordinates`. `CoolerSystem.BuildInitialStockRuntime()` (o restaura backup) |
| `OnEnable` | Visualizadores y UI se suscriben a `OnInventoryChanged` / `OnMoneyChanged` |
| `Start` | `ViewManager.Show(startView)` · `CustomerSystem` calcula la noche, aplica desbloqueos, crea `OrderSystem` y lanza `StartNight()` · `GameManager` se suscribe a `OnNightEnded` y publica el día en el HUD · buffers resuelven tipos por reflexión y llaman `RefreshVisuals()` |
| `Update` | Ver 4.3 |

> ⚠️ `TutorialManager.Start` y `CustomerSystem.Start` pueden correr en cualquier orden: el spawn forzado del tutorial espera un frame y reintenta hasta 5 s.

### 4.3 Game loop (por frame)

| Componente | `Update` |
|---|---|
| `GameManager` | Input global; sincroniza visibilidad de carne/plato al cambiar de vista; dispara `MoveToMeatHolder` / `MoveToCoalHolder` / `MoveToBuildMeatHolder` en las transiciones |
| `GrillSystem` | `UpdateHeatPropagation()` — recalcula el calor de todos los slots |
| `GridSlot` (×N) | Quema carbones → calcula calor interno → `meat.Cook(totalHeatReceived)` |
| `Meat` | Efectos (calor/humo); si está agarrado: `HandleHeldInput` + `UpdateHoverPreview` |
| `CustomerSystem` | Descuenta paciencia y expulsa clientes enojados |
| `GrillNotificationManager` | Si la vista ≠ `Grill`: reagrupa las carnes por `MeatCutSO` y refresca las burbujas |
| Corrutinas | `CustomerSystem.SpawnLoop` (cada `spawnIntervalSeconds`, def. `6 s`) |

### 4.4 Controles

| Tecla | Contexto | Acción |
|---|---|---|
| `Esc` | Global | Pausa / reanudar |
| `Q` | Grill | Abre / cierra el **StockPanel** (`stockPanelToggleKey`, configurable en `GameManager`) |
| `W` / `E` | Global | Grill / Build |
| `←` / `→` | Global | Vista anterior / siguiente (**solo Grill ↔ Build**) |
| `Space` | Grill | `TryServe()` (camino legacy) |
| `R` | Grill | `CleanAshes()` — destruye carbones en `Ceniza` |
| `R` | mientras se arrastra | Rotar footprint del corte |
| Click derecho | sobre carne en parrilla | `Meat.Flip()` |
| `Space` | Build | Entrar a selección de cliente / confirmar entrega |
| `A` / `D` | Build, seleccionando | Cliente anterior / siguiente |
| `X` | Build, entrega bloqueada | Descartar cortes quemados y revalidar |
| `R` | Build, sin seleccionar | Limpiar el plato entero |
| `M` | Build | Informar corte faltante |

### 4.5 Ciclo de noche

```
GameScene ──[último cliente atendido/expulsado]──► CustomerSystem.OnNightEnded
   └─► GameManager.EndNight()
         ├─ CoalConsumptionTracker.RegisterDayCompleted()   // DaysPlayed++, aplica desbloqueos
         └─ LoadSceneByName("EndScene")

EndScene
   ├─ EndScreen        muestra el dinero · botones: MainMenu / Retry / GoShopping
   └─ ShopSystem       tabs Coal → Meat → Upgrades → Toppings
         └─ ShopNextButtonUI: en el último tab → SceneManager.LoadScene("GameScene")

SceneManagementUtils.ReturnToMainMenu()   ← reset total
   destruye PlayerWallet, CoalConsumptionTracker, CoolerSystem, ToppingStock (los 4 DDOL)
   + CoolerSystem.PrepareForNewGame()  (invalida el backup estático de stock)
```

---

## 5. Notas para trabajar sobre el código

| # | Nota |
|---|---|
| 1 | **`MeatCutSO` vive en `Grill/MeatType.cs`**; `GrillSlot` en `Grill/GrillSlots.cs`. Los nombres de archivo no siempre coinciden con la clase. `Grill/GrillSys2.cs` está **vacío** |
| 2 | `GameManager` habla con los buffers **solo por `SendMessage`** (campos tipados `MonoBehaviour`). Renombrar `MoveToMeatHolder`, `MoveToCoalHolder`, `MoveToBuildMeatHolder`, `ClearPlateMeatVisuals`, `RemovePlateMeatVisualAt`, `SetPlateMeatVisualsVisible` **rompe en silencio** |
| 3 | `MeatHolderDraggableMeat` y `CoolerDraggableMeat` invocan al buffer por **reflexión** (`MethodInfo`), probando primero la sobrecarga de 3 parámetros y cayendo a la de 2 |
| 4 | Los visualizadores hacen `AddComponent(Type resuelto por nombre)` + `SendMessage("SetCut"/"SetCoalData"/"SetCoolerSystem"/"SetTransferBuffer"/"SetToGrillDropArea"/"SetTransferEntryId")` |
| 5 | Los `ScriptableObject` **mutan en runtime** (`isUnlocked`, `isPurchased`, `ProductVariantSO.isUnlocked`) → el estado se filtra entre sesiones del Editor |
| 6 | Todos los singletons usan el mismo guard en `Awake`: `if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this;`. Mantener esa forma al agregar nuevos |
| 7 | Vista `Grill`: el root **no se desactiva** — se apagan renderers/colliders/canvases, así que la parrilla **sigue cocinando en segundo plano** (por diseño). Por eso existen las notificaciones de `GrillNotificationManager` |
| 8 | `Shop` tiene **dos capas de UI paralelas**: `*2D` (world-space, la que se usa) y `*UI` (uGUI, legacy). `ShopGlobalBar2D.cs` declara la clase `ShopHeader2D` |
| 9 | `ViewType.Shop` está en el enum pero `ViewManager` no lo maneja — la tienda es una escena aparte |
| 10 | Clases sin uso activo: `ShopCart`, `CoalStock`, `MeatTypes` (enum), `GridTransformGroup` es `[ExecuteAlways]` solo para layout de editor |
| 14 | **Cooler View deprecada.** Sus scripts (`CoolerStockVisualizer`, `CoalStockVisualizer`, `CoolerDraggableMeat`, `DraggableCoal`) y sus assets (`Prefabs/CoolerView.prefab`, `StockPrefab.prefab`) siguen en el proyecto pero **ya no se alcanzan**: `Show(Cooler)` redirige a Grill, así que `coolerRoot` queda desactivado desde el primer `Show`. No borrar sin revisar los overrides de escena |
| 15 | `GrillView` tiene **escala no uniforme `(0.81, 1, 1)`** como override de escena. Cualquier hijo nuevo que deba verse sin deformar necesita contra-escala (`localScale.x = 1/0.81`). Es lo que hace la instancia de `StockPanel` |
| 16 | **`TutorialScene` está rota** a partir de este cambio: los pasos `2.PrimeraParteCooler`, `9.PasarACooler` y `12.VolverGrillView` usan `ChangeView` hacia/desde `Cooler` y ese evento ya no dispara. Los pasos de arrastre (10/11) sí funcionan porque el drop directo del panel emite las notificaciones existentes. Pendiente de decisión: rehacer esos pasos, saltearlos o sacar la escena del build. Además falta wirear en `TutorialScene` la contra-escala del panel y sus refs (`grillSystem`, `viewManager`, buffers) |
| 17 | `TutorialOfferController` pone `Time.timeScale = 0` al entrar a `GameScene` hasta que se responde el diálogo. Cualquier animación de UI que deba correr ahí necesita `Time.unscaledDeltaTime` |
| 11 | Varios archivos tienen **mojibake** por encoding (`carb�n`, `�tem`) en literales y comentarios: `CoalSO.cs`, `CoolerSystem.cs`, `TrashZone.cs`, `HudManager.cs`, `OrderText.cs`, `TutorialStepSO.cs` |
| 12 | El estado de cocción real vive en los `float sideACookTime/sideBCookTime`; `Meat.state` es solo caché visual derivado por `RefreshState()` |
| 13 | `GridSlot.Update`, `GrillSystem.UpdateHeatPropagation` y `Meat.Cook` corren **por frame y por slot**: no agregar `Debug.Log` ni allocations ahí |
