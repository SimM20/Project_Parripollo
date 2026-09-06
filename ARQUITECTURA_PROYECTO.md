# ARQUITECTURA — Project_Parripollo

> Documentación técnica de referencia. Objetivo: entender el proyecto sin leer los scripts.

---

## 0. Ficha técnica

| Campo | Valor |
|---|---|
| Motor | Unity **2022.3.62f3**, URP (2D), Input Manager legacy (`Input.GetKeyDown`) |
| Lenguaje | C#, assembly única `Assembly-CSharp` (sin `.asmdef` en `Assets/Scripts`) |
| Código propio | `Assets/Scripts/` — **120 archivos, ~16.2k líneas** |
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
├── Cooler/     Stock persistente (CoolerSystem). Vista Heladera DEPRECADA → StockPanel
├── Build/      Vista Armado: plato, pan/guarniciones/toppings, undo, entrega por arrastre
├── Customers/  Clientes: spawn, paciencia, selección, burbujas de pedido
├── Orders/     Modelo y generación de pedidos
├── Food/       Catálogo (SO), validación de platos, evaluación económica de cocción
├── Shop/       Tienda (post-noche): tabs + breadcrumb, compra individual. Dos capas de UI
└── UI/         ViewManager, Tutorial, notificaciones de parrilla, HUD SO, feedback
```

### Responsabilidades por carpeta

| Carpeta | Responsabilidad | Archivos clave |
|---|---|---|
| **raíz** | Singletons de sesión (`UIManager`, `AudioManager`, `PlayerWallet`, `CoalConsumptionTracker`), modelo base drag&drop (`Item`), grilla (`GridSlot`), entidades físicas (`Meat`, `Coal`), buffer de carbón, **sistema de strikes**, arranque (`Init`), utilidades de escena | `Item.cs`, `GridSlot.cs`, `Meat.cs`, `Coal.cs`, `PlayerWallet.cs`, `CoalConsumptionTracker.cs`, `SceneManagementUtils.cs`, `Strike*.cs` (5 archivos) |
| **Core/** | Bucle de partida e input global (`GameManager`), armado del plato (`BuildStationSystem`), staging de carne entre vistas (`MeatTransferBuffer`), draggables inter-vista, basura | `GameManager.cs` (445), `MeatTransferBuffer.cs` (949), `BuildStationSystem.cs` |
| **Grill/** | Propagación de calor y spawn en grilla (`GrillSystem`), datos de corte (`MeatCutSO` — **está en `MeatType.cs`**), toggle capa carne/carbón, barra y burbuja de cocción por hover, contador de apilado de carbón | `GrillSystem.cs`, `MeatType.cs`, `GrillLayerToggle.cs`, `MeatCookHoverBar.cs`, `CoalStackCounter.cs` |
| **Cooler/** | Stock persistente `ItemDataSO → int` (`CoolerSystem`, DDOL). El resto de la carpeta (visualizadores y draggables de la heladera) está **deprecado** desde el StockPanel | `CoolerSystem.cs` · deprecados: `CoolerStockVisualizer.cs`, `CoalStockVisualizer.cs`, `CoolerDraggableMeat.cs`, `DraggableCoal.cs` |
| **Build/** | Zona de drop del plato (`BuildFoodDropZone`), draggables de pan/side/topping, frascos vertibles con salsa (`ToppingDraggable`), historial de undo (patrón Command), **entrega del plato por arrastre** (`PlateDeliveryDraggable`) | `BuildFoodDropZone.cs`, `ToppingDraggable.cs` (670), `BuildUndoHistory.cs`, `BuildUndoActions.cs`, `PlateDeliveryDraggable.cs` |
| **Customers/** | Spawn ponderado por noche, tick de paciencia, modo selección de entrega, view + burbuja + recuadro | `CustomerSystem.cs` (673), `Customer.cs`, `CustomerView.cs` |
| **Orders/** | `Order` (corte + punto pedido + pan/sides/toppings) y generación aleatoria ponderada | `OrderSystem.cs`, `Order.cs` |
| **Food/** | Catálogo estático (`FoodCatalogSO`), reglas de validez (`DishValidator`), **economía de entrega** (`CookingDeliveryEvaluator`), puente catálogo+stock (`FoodAvailabilityService`) | `CookingDeliveryEvaluator.cs`, `DishValidator.cs`, `FoodCatalogSO.cs` |
| **Shop/** | Lógica de tienda headless (`ShopSystem`) + **dos capas de UI paralelas**: `*UI` (uGUI/Canvas, **la activa** en `EndScene`) y `*2D` (world-space, prefab `ShopRoot` — presente pero **desactivado**) | `ShopSystem.cs`, `ShopGridUI.cs`, `ShopItemCellUI.cs`, `ShopBreadcrumbUI.cs`, `ShopHeaderUI.cs` |
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
├── ShopConfig.asset / CoalData.asset / StrikeConfig.asset
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
        SSF[StrikeSessionFlag]
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
    CUS -->|event OnCustomerLostByPatience| STK[StrikeSystem]
    STK -->|SetStrikes + TriggerStrikeShake| UIM
    STK -->|PlayStrikeSound| AM[AudioManager]
    STK -->|BlockNewSpawns| CUS
    STK -->|MarkClosedByStrikes| SSF
    STK -->|event OnMaxReached| SGN[StrikeGameplayNotice]
    SSF -.->|ClosedByStrikes en EndScene| SCP[StrikeClosurePopupUI]
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
    SHOP -->|events OnTabChanged/OnCartChanged/OnPurchaseResult| SUI["Shop*UI (activa) / Shop*2D (off)"]
    SUI -->|SetTab| SHOP
    SUI -->|TryBuyNow / TryBuyToppingNow| SHOP

    PW -->|event OnMoneyChanged| UIM & WD[WalletDisplay] & SUI
    UIM --> HUD[HudManager] --> HC[HudContainer]

    MEAT -.->|static Notify*| TM
    COAL -.->|static Notify*| TM
    BSS -.-> TM
```

### 2.2 Patrones usados

| Patrón | Dónde | Detalle |
|---|---|---|
| **Singleton** (`static Instance`) | `GameManager`, `UIManager`, `AudioManager`, `PlayerWallet`*, `CoolerSystem`*, `ToppingStock`*, `CoalConsumptionTracker`*, `StrikeSessionFlag`*, `TutorialManager`, `BuildUndoHistory`, `GrillNotificationManager`, `MeatHoverBubble`, `MeatCookHoverBar`, `CustomerHoverBubble`, `CustomerSelectionFrame`, `DeliveryFeedbackText` | `*` = además `DontDestroyOnLoad`. Los de escena se reasignan en `Awake` sin guard. `GrillLayerToggle` usa `private static instance` |
| **Observer** (`event Action`) | Ver tabla 2.3 | Suscripción en `OnEnable`/`Start`, desuscripción en `OnDisable`/`OnDestroy` |
| **Static notification hub** | `TutorialManager.Notify*(...)` | 11 métodos estáticos no-op si `Instance == null` → en `GameScene` el tutorial no existe y nada cambia |
| **Command** | `IBuildUndoAction` + `BuildUndoHistory` (pila) | `AddSideUndoAction`, `AddToppingUndoAction`, `SetBreadUndoAction`. **La carne nunca es reversible** |
| **Buffer / staging area** | `MeatTransferBuffer`, `CoalTransferBuffer` | Guardan `BufferedMeatData`/`BufferedCoalData` (POCO con tiempos de cocción) y reconstruyen los visuales; permiten mover items entre vistas sin instanciar `Meat`/`Coal` reales |
| **Duck typing por reflexión / `SendMessage`** | `GameManager`→buffers, `MeatHolderDraggableMeat`, `CoolerDraggableMeat`, `*StockVisualizer` | `Type.GetType` sobre todos los assemblies + `MethodInfo.Invoke` / `SendMessage(..., DontRequireReceiver)`. Rompe el binding estático a propósito |
| **Registro estático de instancias** | `Coal.ActiveCoals`, `BuildFoodDropZone.ActiveZones`, `TrashZone.ActiveZones`, `ToppingDraggable.ActiveInstances` | Alta en `OnEnable`, baja en `OnDisable`/`OnDestroy`. Habilita APIs estáticas tipo `TryAcceptAt`, `ClearAllSplatters` |
| **Data-driven (ScriptableObject)** | `ItemDataSO` → `MeatCutSO`, `CoalSO`, `UpgradeSO`; `BreadSO`, `SideSO`, `ToppingSO`, `ProductVariantSO`, `FoodCatalogSO`, `ShopConfigSO`, `TutorialStepSO`, `HudDatabaseSO`, `StrikeConfigSO` | ⚠️ Los SO mutan en runtime (`isUnlocked`, `isPurchased`) → **el estado persiste entre sesiones de Editor** |
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
| `CustomerSystem` | `OnCustomerLostByPatience(Customer)` (campo `Action`) | `StrikeSystem` — se dispara **antes** de `RemoveCustomer` cuando el cliente llega a `IsAngry` |
| `StrikeSystem` | `OnStrikeAdded(int current, int max)` (campo `Action`) | (sin consumidor actual — hook para VFX/HUD extra) |
| `StrikeSystem` | `OnMaxReached` (campo `Action`) | `StrikeGameplayNotice` (cartel "Te clavaron el cartel") |
| `CoolerSystem` | `OnMissingItemRequested(ItemDataSO)` | (sin consumidor actual — hook futuro) |
| `BuildStationSystem` | `OnAssemblyChanged`, `OnAssemblyCleared` | `BuildUndoHistory.Clear` |
| `BuildUndoHistory` | `OnHistoryChanged` | `RollbackButtonUI` (habilita/deshabilita) |
| `PlayerWallet` | `OnMoneyChanged(float)` | `UIManager`, `WalletDisplay`, `ShopHeaderUI`, `ShopGridUI` (→ `RefreshVisuals` de cada celda), (2D: `ShopGrid2D`, `ShopHeader2D`) |
| `ToppingStock` | `OnStockChanged` | `ShopGridUI`, (2D: `ShopGrid2D`, `ShopDetailPanel2D`) |
| `ShopSystem` | `OnTabChanged` | `ShopGridUI` (rebuild), `ShopBreadcrumbUI`, `ShopSubtitleUI`, `ShopNextButtonUI`, (2D: `ShopTabBar2D`, `ShopGrid2D`) |
| `ShopSystem` | `OnCartChanged`, `OnPurchaseResult(bool,string)` | Solo la capa 2D. **La UI activa compra directo y no usa carrito** |
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

Entrega — dos caminos hacia el MISMO método, GameManager.TryDeliverToCustomer(Customer)

  A) Teclado  [SPACE en vista Build]
   │  GameManager.TryEnterDeliverySelection → CustomerSystem.BeginDeliverySelection()
   │  A/D navega clientes · SPACE confirma → ConfirmDeliverySelection() → TryDeliverToCustomer(SelectedCustomer)

  B) Drag & drop  [arrastrar el plato con el mouse, vista Build]
   │  PlateDeliveryDraggable.OnMouseDown → agarra el plato COMPLETO como bloque
   │                                       (visuales de carne + sides/toppings) y guarda sus posiciones
   │  OnMouseDrag → Physics2D.OverlapPointNonAlloc busca CustomerView bajo el mouse
   │              → CustomerSystem.SetDeliveryDragHover(view) → CustomerSelectionFrame + burbuja de pedido
   │  OnMouseUp   → si soltó sobre un cliente: TryDeliverToCustomer(view.Customer)
   │              → si devuelve false (o soltó al vacío): el plato vuelve a la PlateDropZone

  Lógica compartida (TryDeliverToCustomer, devuelve bool):
   │     1. corte armado == order.PrimaryCut ?
   │     2. DishValidator.ValidateSandwich / ValidatePlatedDish
   │     3. CookingDeliveryEvaluator.Validate → Crudo/Quemado BLOQUEAN (X descarta quemados)
   │     4. CookingDeliveryEvaluator.EvaluateCut por corte → pago + propina
   │     5. PlayerWallet.Add · CustomerSystem.CompleteCustomer · limpiar plato → return true
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
| `Customer discardCustomer` | Cliente del intento bloqueado. Necesario porque una entrega **por arrastre** no activa el modo de selección: sin esto, la `X` no sabría contra quién revalidar |

```csharp
public CustomerSystem Customers { get; }          // acceso para PlateDeliveryDraggable
public bool TryDeliverToCustomer(Customer)        // ÚNICO lugar con la lógica de entrega
public void EndNight()   // desuscribe, tracker.RegisterDayCompleted(), carga "EndScene"
// privados relevantes: TryToggleGrillLayer, ClearBuildAssembly, CleanAshes,
//                      TryDiscardBurnedCuts, TryEnterDeliverySelection, ConfirmDeliverySelection
```
`ConfirmDeliverySelection()` es un wrapper de una línea sobre `TryDeliverToCustomer(SelectedCustomer)`:
teclado y arrastre comparten validaciones, mensajes de `DeliveryFeedbackText`, pago y limpieza del plato.
El `bool` de retorno es **solo** para el arrastre: `false` = entrega rechazada → devolver el plato a la
`PlateDropZone`. `TryDiscardBurnedCuts` (`X`) revalida contra `SelectedCustomer` si el modo de selección
está activo, y contra `discardCustomer` si el intento vino de un arrastre.

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

void SetStrikes(int current, int max)   // pinta strikesText + color por proximidad al máximo
void TriggerStrikeShake()               // corrutina de shake sobre el transform de strikesText
```
Instancia `pauseCanvasPrefab` on-demand. Delega el render a `HudManager` → `HudContainer`.

⚠️ El HUD de strikes **no** pasa por `HudManager`: `strikesText` es un `TextMeshProUGUI` asignado
directo por inspector en el `UIManager`, con su propio formato (`strikesFormat`, def. `"Strikes: {0}/{1}"`)
y su propia paleta (`strikesNormalColor` / `strikesWarningColor` en `max−1` / `strikesMaxColor` en `max`).
Si `strikesText` no está asignado, `SetStrikes` y `TriggerStrikeShake` son no-op silenciosos.
`ShakeText` usa `Time.deltaTime`, así que con `Time.timeScale = 0` (pausa, diálogo de tutorial) no avanza.

---

### 3.2 Parrilla y cocción

#### `GrillSystem` — `Grill/GrillSystem.cs`
```csharp
List<GridSlot> slots;  GameObject meatPrefab, coalPrefab;
bool SpawnMeat(MeatCutSO, bool rotateFootprint = false)
bool TrySpawnMeatAtPoint(MeatCutSO, Vector3, out Meat, bool rotateFootprint = false)
bool TrySpawnCoalAtPoint(CoalSO, Vector3, out Coal)   // → CoalConsumptionTracker.ReportConsumption(1)
Meat GetCookedMeat(MeatCutSO)                         // ⚠️ sin llamadores desde que se borró GameManager.TryServe
void RemoveMeat(Meat)                                 // ⚠️ idem
void SetMeatVisualsVisible(bool)
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
Dos entradas para `Toggle()`: el `OnMouseDown` del propio botón en la escena y `Space` desde
`GameManager.TryToggleGrillLayer()` (**solo en la vista `Grill`**). Ambas pasan por `ShowLayer`,
así que el icono del botón y `TutorialManager.NotifyGrillLayerChanged` quedan siempre sincronizados.

#### `CoalStackCounter` — `Grill/CoalStackCounter.cs`

Contiene también `CoalStackCounterStyle`. Feedback visual del stack de carbón: etiqueta `TextMeshPro`
(world-space) con el texto `x{N}` en la esquina **inferior derecha** del sprite de cada `GridSlot`
de tipo `Coal`.

| Aspecto | Detalle |
|---|---|
| Creación | `GrillSystem.Start()` → `SetupCoalStackCounters()` engancha un `CoalStackCounter` a cada slot con `acceptsType == ItemType.Coal` (60 en `GameScene`). No hay setup manual por slot |
| Anclaje | Hijo del slot. `rectTransform.pivot = (1,0)` + `localPosition` calculada desde `spriteRenderer.sprite.bounds` (esquina `max.x`, `min.y`) + offset configurable. Escala heredada del slot |
| Umbral | Solo visible con **2 o más** carbones apilados (`MIN_VISIBLE_COUNT = 2`). Con 1 o 0 no se muestra texto |
| Ocultamiento | También se oculta si la capa activa no es Coal (`GrillLayerToggle.IsItemTypeAllowed`) o si la vista no es Grill (`GrillSystem.SetMeatVisualsVisible` → `SetViewVisible`) |
| Refresco | `LateUpdate` por slot; solo reasigna texto y `SetActive` cuando el conteo o la visibilidad cambian |

**Configuración** (`GrillSystem` → header *Coal Stack Counter*, serializada en `Assets/Prefabs/[SYSTEMS].prefab`,
del que las escenas son instancias):

| Campo | Valor por defecto |
|---|---|
| `font` | `Assets/Fonts/Bungee-Regular SDF.asset` |
| `fontSize` | `4` (slot ≈ 0.52 × 0.55 unidades de mundo) |
| `textColor` | Blanco |
| `offset` | `(-0.06, 0.06)` — unidades locales hacia adentro desde la esquina inferior derecha |
| `sortingOrder` | `50` — por encima del sprite de carbón (`sortingOrder = 2`) |

**Gotcha (TMP 3.0.7)**: el `TextMeshPro` se agrega con el GameObject todavía en la raíz y **después** se
parenta al slot. Los slots pueden estar inactivos al arrancar (la partida abre en la vista Shop); si se
parenta primero, el `Awake` de TMP no corre, `m_renderer` queda null y asignar `.font` tira
`NullReferenceException` en `TMPro_Private.cs:526`. Por el mismo motivo el setup va en `Start()` y no en `Awake()`.

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

#### `PlateDeliveryDraggable` — `Build/PlateDeliveryDraggable.cs`

Entrega del plato **arrastrándolo con el mouse** hasta un cliente. Alternativa al flujo por teclado,
que queda intacto: ambos terminan en `GameManager.TryDeliverToCustomer`.

```csharp
public void RefreshCollider()   // re-mide el BoxCollider2D contra el sprite actual
// resto: OnMouseDown/Drag/Up + helpers estáticos
```

| Aspecto | Detalle |
|---|---|
| Creación | **Cero setup de escena.** `MeatTransferBuffer.ConsumeBuildMeatEntry` lo hace `AddComponent` sobre cada visual de carne que queda en la `PlateDropZone`, justo donde destruye el `BuildMeatHolderDraggableMeat` |
| Qué se arrastra | El plato **completo como bloque**: todas las instancias de `PlateDeliveryDraggable` + los visuales de sides/toppings que devuelve `BuildFoodDropZone.CollectActivePlateVisuals`. Agarrar cualquier sprite mueve todo |
| Estado del arrastre | `static`: posiciones y `sortingOrder` de origen de cada visual (hay un solo mouse, no hay arrastres concurrentes). El `sortingOrder` sube `+5000` mientras dura y se restaura al soltar |
| Hover de cliente | `Physics2D.OverlapPointNonAlloc` sobre un buffer estático de 16 (sin GC por frame) → `GetComponentInParent<CustomerView>()` → `CustomerSystem.SetDeliveryDragHover(view)` |
| Rechazo | Si `TryDeliverToCustomer` devuelve `false`, o si se soltó fuera de un cliente, cada visual vuelve a su posición guardada en la `PlateDropZone`. El mensaje ya lo muestra `GameManager` vía `DeliveryFeedbackText` |
| Tutorial | `OnMouseDown` dispara `TutorialManager.NotifyDeliverySelectionBegun()`: es el equivalente por mouse de entrar en modo selección, y sin eso el paso `25.BeginDelivery` quedaría colgado si el jugador usa el mouse |
| Collider | Se re-mide en `Awake` y cada vez que el pan cambia sprite/escala/rotación del visual (`MeatTransferBuffer.UpdatePlateMeatSprite` y `RestorePlateMeatVisual` llaman a `RefreshCollider()`) |

⚠️ Las salpicaduras de salsa (`SauceSplatter`, creadas por `ToppingDraggable`) **no** siguen al plato
durante el arrastre: quedan en el mostrador y se limpian con `ToppingDraggable.ClearAllSplatters()` en la entrega.

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
Action<Customer> OnCustomerLostByPatience;// campo público — lo consume StrikeSystem
Customer currentCustomer;                 // compat con GameManager
Customer SelectedCustomer { get; }
bool IsDeliverySelectionActive { get; }
bool IsReadyForSpawning { get; }
bool SpawnsBlocked { get; }               // true tras BlockNewSpawns()
int  ActiveCustomerCount { get; }
IReadOnlyList<Customer> ActiveCustomers { get; }
FoodCatalogSO Catalog { get; }            // vía FoodAvailabilityService

void StartNight()
void BlockNewSpawns()                     // corta SpawnLoop; lo llama StrikeSystem al 3er strike
void SpawnCustomer(bool ignoreNightLimit = false)
void SelectCustomer(Customer), SelectAdjacentCustomer(int direction)
bool BeginDeliverySelection();  void EndDeliverySelection()
void CompleteCustomer(Customer), ShowSelectedOrderBubble()
bool IsCustomerActive(Customer)             // sigue esperando (no se fue ni fue atendido)
void SetDeliveryDragHover(CustomerView)     // resaltado durante el arrastre del plato; null limpia
```
`SetDeliveryDragHover` reusa `CustomerSelectionFrame` + `CustomerHoverBubble` sin tocar
`SelectedCustomer` ni `IsDeliverySelectionActive`: el arrastre y la selección por teclado conviven.
Al pasar `null` restaura lo que corresponda al modo teclado. Si el cliente resaltado se va enojado
a mitad del arrastre, `RemoveCustomer` suelta el recuadro antes de destruir la view.
Clientes por noche: `min(customersFirstNight + (noche−1) × customersAddedPerNight, maximumCustomersPerNight)`
(por defecto `20 + 5·(n−1)`, cap `70`; máx. `4` simultáneos).
`Update` descuenta paciencia y, al llegar a `IsAngry`, dispara `OnCustomerLostByPatience(c)` **antes** de
`RemoveCustomer(c, "Se fue enojado")`. `CompactSlots()` corre las views a la izquierda al liberarse un slot.

**Fin de noche — cambió con el sistema de strikes.** Hoy `OnNightEnded` se dispara desde dos lugares:

| Origen | Condición |
|---|---|
| `RemoveCustomer` | `SpawnsBlocked && activeCustomers.Count == 0` — es decir, **solo** tras el cierre por strikes, una vez atendidos/expulsados los que quedaban en el mostrador |
| `SpawnCustomer` | `spawnedTonight >= maximumCustomersPerNight` (tope duro, def. `70`) |

`SpawnLoop` ahora itera `while (spawnedTonight <= maximumCustomersPerNight && !SpawnsBlocked)` y chequea
`SpawnsBlocked` de nuevo después del `WaitForSeconds`. `customersTargetTonight` sigue existiendo y sigue
frenando los spawns dentro de `SpawnCustomer` (`return` silencioso), pero **ya no cierra la noche** — ver
nota 22 en §5.

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

### 3.6 Strikes (cierre anticipado de la jornada)

Regla de diseño: **si se van 3 clientes por falta de paciencia, dejan de llegar clientes nuevos.**
No es un game over — la noche sigue hasta atender a los que ya estaban, y después se pasa a la tienda
con un popup que explica qué pasó.

Cinco archivos, todos en la raíz de `Assets/Scripts/`, ninguno depende de `GameManager`:

| Archivo | Escena | Rol |
|---|---|---|
| `StrikeConfigSO.cs` | asset | Config + **todos los textos** (`Assets/ScriptableObjects/StrikeConfig.asset`) |
| `StrikeSystem.cs` | `GameScene` (GO `StrikeSystem`) | Lógica: cuenta strikes, corta spawns, marca el flag |
| `StrikeSessionFlag.cs` | `GameScene` (GO `StrikeSessionFlag`) · **DDOL** | Único puente `GameScene` → `EndScene` |
| `StrikeGameplayNotice.cs` | `GameScene` (GO `StrikeGameplayNotice`, bajo `StrikeCanvas`) | Cartel temporal in-game al 3er strike |
| `StrikeClosurePopupUI.cs` | `EndScene` (GO `StrikeClosurePopUp`) | Popup explicativo al llegar a la tienda |

#### `StrikeConfigSO : ScriptableObject` — menú `Parrilla/Strike Config`
```csharp
int   maxStrikes = 3;                 // [Min(1)]
float gameplayNoticeDuration = 5f;    // [Min(0.1)] — segundos que dura el cartel in-game
string noticeTitle, noticeBody;       // cartel in-game
string popupTitle, popupBody, popupButtonText;   // popup de la tienda
```
El copy vive **acá**, no en los prefabs ni en los scripts: `StrikeSystem` reexpone
`MaxStrikes` / `NoticeDuration` / `NoticeTitle` / `NoticeBody` con fallbacks (`3`, `5f`, `""`) por si
falta el asset, y `StrikeClosurePopupUI` lee el SO directo.

#### `StrikeSystem` — `StrikeSystem.cs` (NO singleton, instancia de escena)
```csharp
[SF] CustomerSystem customerSystem;  [SF] StrikeConfigSO config;
Action<int,int> OnStrikeAdded;   Action OnMaxReached;
int CurrentStrikes { get; }      int MaxStrikes { get; }
float NoticeDuration { get; }    string NoticeTitle { get; }  string NoticeBody { get; }
```
`Start()` — si falta cualquiera de las dos referencias, `LogError` y **no se suscribe a nada**
(el sistema queda inerte, la partida sigue). Si están: `StrikeSessionFlag.Reset()`, `CurrentStrikes = 0`,
suscripción a `CustomerSystem.OnCustomerLostByPatience` y primer `UIManager.SetStrikes(0, max)`.
Se desuscribe en `OnDestroy`.

Por cada cliente perdido (`HandleCustomerLostByPatience`), en orden:

```
1. CurrentStrikes = min(CurrentStrikes + 1, MaxStrikes)   // sin efecto si maxReached
2. OnStrikeAdded(current, max)
3. UIManager.SetStrikes(current, max) + UIManager.TriggerStrikeShake()
4. AudioManager.PlayStrikeSound()
5. si current >= MaxStrikes → HandleMaxReached()
```

`HandleMaxReached()`: `maxReached = true` (guard idempotente) → `CustomerSystem.BlockNewSpawns()` →
`StrikeSessionFlag.MarkClosedByStrikes()` → `OnMaxReached()`.
**No** carga escena ni llama a `GameManager.EndNight`: la noche termina sola cuando
`RemoveCustomer` ve `SpawnsBlocked && activeCustomers == 0`.

Los strikes son **por jornada**: no hay contador acumulado entre noches y `StrikeSessionFlag` solo
transporta un `bool`, no el conteo.

#### `StrikeSessionFlag` — Singleton + DDOL
```csharp
static StrikeSessionFlag Instance { get; }
bool ClosedByStrikes { get; }
void MarkClosedByStrikes(), Reset()
```
Guard de singleton estándar + `transform.SetParent(null)` antes del `DontDestroyOnLoad` (necesario
porque en `GameScene` cuelga de un padre). Ciclo del flag:

```
GameScene: StrikeSystem.Start()      → Reset()                 (arranca la jornada limpia)
GameScene: 3er strike                → MarkClosedByStrikes()
EndScene:  StrikeClosurePopupUI.Start() lee ClosedByStrikes → muestra el popup
EndScene:  click en "Ir a la tienda" → Reset()                 (consume el flag)
```
⚠️ No lo destruye `SceneManagementUtils.ReturnToMainMenu()` — a diferencia de los otros cuatro DDOL,
`StrikeSessionFlag` sobrevive al volver al menú. Como `StrikeSystem.Start()` lo resetea al entrar a
`GameScene`, en la práctica no se filtra, pero es la excepción a la regla de §4.5.

#### `StrikeGameplayNotice` — cartel in-game
Se suscribe a `StrikeSystem.OnMaxReached` en `OnEnable` (y apaga `noticeRoot`), se desuscribe en
`OnDisable`. Al dispararse rellena `titleText`/`bodyText` desde el `StrikeSystem`, activa `noticeRoot`
y programa el apagado con una corrutina de `WaitForSeconds(NoticeDuration)`, cancelando la anterior si
la hubiera. Usa tiempo escalado: **con el juego en pausa el cartel no se va**.

#### `StrikeClosurePopupUI` — popup en `EndScene`
`Awake` engancha `continueButton.onClick` y apaga `popupRoot`. `Start` consulta
`StrikeSessionFlag.Instance?.ClosedByStrikes`: si es `false` o no hay flag, no hace nada — o sea, en una
noche terminada normalmente el popup nunca aparece. Si es `true`, vuelca los tres textos del
`StrikeConfigSO` y activa `popupRoot`. El botón cierra el popup y llama a `Reset()` del flag.

⚠️ Es puramente informativo: **no** bloquea la tienda ni cambia de escena, solo tapa la UI mientras está
abierto. Y conserva ~10 `Debug.Log` de diagnóstico — sacarlos antes de release (ver nota 24 en §5).

---

### 3.7 Progresión, economía y tienda

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
ShopTabType CurrentTab { get; } = Coal   // Coal → Meat → Upgrades → Toppings
event Action OnCartChanged, OnTabChanged;  event Action<bool,string> OnPurchaseResult;

void SetTab(ShopTabType)                                // no-op si ya es el tab actual
IReadOnlyList<ItemDataSO> GetItemsForCurrentTab() / GetItemsForTab(ShopTabType)
IReadOnlyList<ToppingSO>  GetToppings()
bool IsPurchasable(ItemDataSO), IsToppingPurchasable(ToppingSO)
int  GetSuggestedCoalUnits(), GetSuggestedCoalBags()    // max(0, consumoPromedio − stock)
int  GetTotalCoalUnits()                                // suma TODO el stock CoalSO del cooler
int  GetCartQty(ItemDataSO);  void SetQty(...), IncrementQty(...), ClearCart()
float CartTotal(), MoneyAfterPurchase();  int CartCoalBags()
List<MeatCutSO> GetLowStockCuts()
bool TryConfirmPurchase(out string)                      // carrito completo — solo la capa 2D
bool TryBuyNow(ItemDataSO, int qty, out string)          // ← compra individual (capa activa)
bool TryBuyToppingNow(ToppingSO, int qty, out string)    // ← compra individual (capa activa)
```

**Fuentes de items por tab** (`GetItemsForTab`):

| Tab | Origen | Filtro `IsPurchasable` |
|---|---|---|
| `Coal` | `ShopConfigSO.coal` (un solo item) | siempre `true` |
| `Meat` | `FoodCatalogSO.GetAllCuts()` | `cut.isUnlocked` |
| `Upgrades` | `FoodCatalogSO.GetAllUpgrades()` | `up.isUnlocked && !up.isPurchased` |
| `Toppings` | `GetToppings()` → `catalog.GetAvailableToppings()` (devuelve `ToppingSO`, **no** `ItemDataSO`) | siempre `true` |

> El tab `Toppings` es el único que **no** pasa por `GetItemsForTab`: la UI llama a `GetToppings()` y bindea `ToppingSO`. Por eso `ShopGridUI` y `ShopItemCellUI` tienen una rama y un `Bind` por cada tipo.

**Compra individual vs. carrito** — coexisten dos caminos:

```
Compra individual (capa uGUI, la activa)
   ShopItemCellUI: −/+ ajustan un `pendingQty` LOCAL de la celda (no toca el carrito)
   → botón Comprar → ShopSystem.TryBuyNow(item, qty) / TryBuyToppingNow(topping, qty)
   → valida IsPurchasable → Wallet.CanAfford → Wallet.TrySpend
   → CoalSO: Cooler.Add(coal, unitsPerBag × qty) · UpgradeSO: up.isPurchased = true · resto: Cooler.Add(item, qty)
   → OnPurchaseResult(true, msg) + pendingQty vuelve a 1
   ⚠️ NO emite OnCartChanged. El refresco lo disparan Wallet.OnMoneyChanged y Cooler.OnInventoryChanged

Carrito (capa 2D, desactivada)
   SetQty/IncrementQty → cart / toppingCart → TryConfirmPurchase() paga todo junto
```
Dos carritos separados: `cart` (`ItemDataSO`) y `toppingCart` (`ToppingSO` → `ToppingStock`).
`UpgradeSO` está capado a cantidad 1 en ambos caminos.

#### UI de tienda (capa uGUI activa) — `Shop/*UI.cs` · `EndScene`

Todos los componentes cuelgan de un `ShopSystem` asignado por inspector y usan el mismo patrón
`OnEnable` (suscribir) / `Start` (`started = true` + `Refresh`) / `OnDisable` (desuscribir);
el flag `started` evita refrescar antes del primer `Start`.

| Componente | Rol |
|---|---|
| `ShopBreadcrumbUI` | Puente entre los 4 `ShopTabButtonUI` y el `ShopSystem`. Se suscribe a `OnTabClicked` de cada botón → `shop.SetTab(tab)`; con `OnTabChanged` repinta cuál está activo (`SetActiveState`) |
| `ShopTabButtonUI` | `[RequireComponent(Button)]`. Expone `ShopTabType Tab` y `Action<ShopTabType> OnTabClicked`. `SetActiveState(bool)` cambia color de `background` y `label` (activo/inactivo) |
| `ShopHeaderUI` | Header: nombre de la tienda, plata (`$N0`) y **total de carbón** (`"Carbon: {GetTotalCoalUnits()} u."`). Se suscribe a `Wallet.OnMoneyChanged` **y** `Cooler.OnInventoryChanged` |
| `ShopGridUI` | Reconstruye la grilla al cambiar de tab. `AdjustCellCount` instancia/destruye celdas (`ShopItemCell 1.prefab`) bajo el `Content` del ScrollView y las bindea. Ante cambios de stock/plata solo llama `RefreshVisuals()` de cada celda (no reconstruye) |
| `ShopItemCellUI` | Celda: icono, nombre, descripción, precio, `pendingQty`, subtotal. Dos `Bind` (`ItemDataSO` / `ToppingSO`). Deshabilita `−` en `qty == 1`, y `Comprar` si el item no es comprable o no alcanza la plata. `lockedOverlay` + icono atenuado para lo bloqueado |
| `ShopSubtitleUI` | Título + detalle por tab. En `Coal` el detalle es dinámico: `"USASTE {AverageCoalPerDay} UNIDADES DE CARBÓN"`, o `"PRIMERA NOCHE — SIN DATOS DE CONSUMO"` si `DaysPlayed == 0` |
| `ShopNextButtonUI` | Avanza `Coal → Meat → Upgrades → Toppings` cambiando el label; en `Toppings` el botón carga `GameScene` |

Navegación por tabs: **dos entradas** — el breadcrumb (salto directo a cualquier tab) y el botón
"Siguiente" (avance secuencial). Ambas terminan en `ShopSystem.SetTab`, así que el estado visual
queda sincronizado por el evento `OnTabChanged`.

---

### 3.8 Tutorial

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
| `Awake` | Singletons se registran (`GameManager`, `UIManager`, `AudioManager`, `CoolerSystem`+DDOL, `PlayerWallet`+DDOL, `ToppingStock`+DDOL, `CoalConsumptionTracker`+DDOL, `StrikeSessionFlag`+DDOL, `BuildUndoHistory`). `GrillSystem.AssignGridCoordinates`. `CoolerSystem.BuildInitialStockRuntime()` (o restaura backup) |
| `OnEnable` | Visualizadores y UI se suscriben a `OnInventoryChanged` / `OnMoneyChanged`; `StrikeGameplayNotice` se suscribe a `StrikeSystem.OnMaxReached` |
| `Start` | `ViewManager.Show(startView)` · `CustomerSystem` calcula la noche, aplica desbloqueos, crea `OrderSystem` y lanza `StartNight()` · `GameManager` se suscribe a `OnNightEnded` y publica el día en el HUD · `StrikeSystem` resetea el flag, pone el contador en `0/max` y se suscribe a `OnCustomerLostByPatience` · buffers resuelven tipos por reflexión y llaman `RefreshVisuals()` |
| `Update` | Ver 4.3 |

> ⚠️ `TutorialManager.Start` y `CustomerSystem.Start` pueden correr en cualquier orden: el spawn forzado del tutorial espera un frame y reintenta hasta 5 s.

### 4.3 Game loop (por frame)

| Componente | `Update` |
|---|---|
| `GameManager` | Input global; sincroniza visibilidad de carne/plato al cambiar de vista; dispara `MoveToMeatHolder` / `MoveToCoalHolder` / `MoveToBuildMeatHolder` en las transiciones |
| `GrillSystem` | `UpdateHeatPropagation()` — recalcula el calor de todos los slots |
| `GridSlot` (×N) | Quema carbones → calcula calor interno → `meat.Cook(totalHeatReceived)` |
| `Meat` | Efectos (calor/humo); si está agarrado: `HandleHeldInput` + `UpdateHoverPreview` |
| `CustomerSystem` | Descuenta paciencia y expulsa clientes enojados → `OnCustomerLostByPatience` → `StrikeSystem` |
| `GrillNotificationManager` | Si la vista ≠ `Grill`: reagrupa las carnes por `MeatCutSO` y refresca las burbujas |
| Corrutinas | `CustomerSystem.SpawnLoop` (cada `spawnIntervalSeconds`, def. `6 s`) |
| `CoalStackCounter` (×60) | **`LateUpdate`**: cuenta el stack del slot; solo toca texto/`SetActive` si el conteo o la visibilidad cambiaron |

### 4.4 Controles

| Tecla | Contexto | Acción |
|---|---|---|
| `Esc` | Global | Pausa / reanudar |
| `Q` | Grill | Abre / cierra el **StockPanel** (`stockPanelToggleKey`, configurable en `GameManager`) |
| `W` / `E` | Global | Grill / Build |
| `←` / `→` | Global | Vista anterior / siguiente (**solo Grill ↔ Build**) |
| `Space` | Grill | Cambia la capa carne ↔ carbón (`TryToggleGrillLayer` → `GrillLayerToggle.Toggle`; ignorado si hay un drag activo) |
| `R` | Grill | `CleanAshes()` — destruye carbones en `Ceniza` |
| `R` | mientras se arrastra | Rotar footprint del corte |
| Click derecho | sobre carne en parrilla | `Meat.Flip()` |
| `Space` | Build | Entrar a selección de cliente / confirmar entrega |
| Arrastrar el plato | Build | Entrega por **drag & drop**: soltar sobre un cliente entrega; soltar al vacío o entrega rechazada → el plato vuelve a la `PlateDropZone` |
| `A` / `D` | Build, seleccionando | Cliente anterior / siguiente |
| `X` | Build, entrega bloqueada | Descartar cortes quemados y revalidar |
| `R` | Build, sin seleccionar | Limpiar el plato entero |
| `M` | Build | Informar corte faltante |

### 4.5 Ciclo de noche

```
GameScene ──[cierre por strikes, o tope duro de maximumCustomersPerNight]──► CustomerSystem.OnNightEnded
   └─► GameManager.EndNight()
         ├─ CoalConsumptionTracker.RegisterDayCompleted()   // DaysPlayed++, aplica desbloqueos
         └─ LoadSceneByName("EndScene")

Cierre por strikes (camino principal hoy)
   3 clientes se van por paciencia
     └─ CustomerSystem.OnCustomerLostByPatience ×3 ──► StrikeSystem.HandleMaxReached()
          ├─ CustomerSystem.BlockNewSpawns()        // corta SpawnLoop, SpawnsBlocked = true
          ├─ StrikeSessionFlag.MarkClosedByStrikes()// viaja a EndScene por DDOL
          └─ OnMaxReached → StrikeGameplayNotice    // cartel in-game, se va solo a los 5 s
   …se sigue jugando con los clientes que quedaban…
     └─ RemoveCustomer: SpawnsBlocked && activeCustomers == 0 → OnNightEnded → EndNight()

EndScene
   ├─ StrikeClosurePopUp  si StrikeSessionFlag.ClosedByStrikes: popup explicativo
   │                      → botón "Ir a la tienda" cierra y hace Reset() del flag
   ├─ EndScreen        muestra el dinero · botones: MainMenu / Retry / GoShopping
   ├─ ShopSystem       tabs Coal → Meat → Upgrades → Toppings  (arranca en Coal)
   │     Header:     nombre de tienda · plata · total de carbón en el cooler
   │     Breadcrumb: 4 ShopTabButtonUI → SetTab (salto directo a cualquier tab)
   │     Grid:       celdas con −/+ y botón Comprar → TryBuyNow / TryBuyToppingNow (pago inmediato)
   │     Next:       avance secuencial de tab; en Toppings → SceneManager.LoadScene("GameScene")
   └─ ShopRoot        prefab de la capa 2D — presente en la escena pero con m_IsActive = 0

SceneManagementUtils.ReturnToMainMenu()   ← reset total
   destruye PlayerWallet, CoalConsumptionTracker, CoolerSystem, ToppingStock (los 4 DDOL)
   + CoolerSystem.PrepareForNewGame()  (invalida el backup estático de stock)
   ⚠️ NO destruye StrikeSessionFlag (5º DDOL) — lo limpia StrikeSystem.Start() al reentrar a GameScene
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
| 8 | `Shop` tiene **dos capas de UI paralelas**. La activa hoy es **`*UI` (uGUI, en `EndScene`)**; la capa `*2D` sigue en la escena dentro del prefab `ShopRoot` pero **desactivada** (`m_IsActive: 0`) — al tocar la tienda, editar los `*UI`. `ShopGlobalBar2D.cs` declara la clase `ShopHeader2D` |
| 8b | La capa activa **no usa el carrito**: cada celda compra por su cuenta con `TryBuyNow`/`TryBuyToppingNow`, que **no emiten `OnCartChanged`**. `cart`/`toppingCart`, `CartTotal()`, `TryConfirmPurchase()` y `OnCartChanged` quedan vivos solo para la capa 2D |
| 8c | `ShopItemCellUI.pendingQty` es estado **local de la celda** y se resetea a 1 tras comprar o rebindear; no sobrevive a un cambio de tab, porque `ShopGridUI.RebuildAll` rebindea todas las celdas |
| 8d | `ShopTabButtonUI` y `ShopGridUI` tienen `Debug.Log` de diagnóstico (`Awake`, `HandleClick`, `RebuildAll`) — quitarlos antes de release |
| 9 | `ViewType.Shop` está en el enum pero `ViewManager` no lo maneja — la tienda es una escena aparte |
| 10 | Clases sin uso activo: `ShopCart`, `CoalStock`, `MeatTypes` (enum), `GridTransformGroup` es `[ExecuteAlways]` solo para layout de editor |
| 11 | Varios archivos tienen **mojibake** por encoding (`carb�n`, `�tem`) en literales y comentarios: `CoalSO.cs`, `CoolerSystem.cs`, `TrashZone.cs`, `HudManager.cs`, `OrderText.cs`, `TutorialStepSO.cs` |
| 12 | El estado de cocción real vive en los `float sideACookTime/sideBCookTime`; `Meat.state` es solo caché visual derivado por `RefreshState()` |
| 13 | `GridSlot.Update`, `GrillSystem.UpdateHeatPropagation` y `Meat.Cook` corren **por frame y por slot**: no agregar `Debug.Log` ni allocations ahí |
| 14 | `CoalSO.unitsPerBag` pasó de `10` a **`1`**: una "bolsa" es una unidad, así que `Cooler.Add(coal, unitsPerBag × qty) == qty`. `GetSuggestedCoalBags()` y `CartCoalBags()` siguen razonando en bolsas — si `unitsPerBag` vuelve a subir, revisar también el texto del header (`GetTotalCoalUnits()` cuenta **unidades**, no bolsas) |
| 15 | **Cooler View deprecada.** Sus scripts (`CoolerStockVisualizer`, `CoalStockVisualizer`, `CoolerDraggableMeat`, `DraggableCoal`) y sus assets (`Prefabs/CoolerView.prefab`, `StockPrefab.prefab`) siguen en el proyecto pero **ya no se alcanzan**: `Show(Cooler)` redirige a Grill, así que `coolerRoot` queda desactivado desde el primer `Show`. Los cuatro scripts llevan cabecera `DEPRECADO`; la de `DraggableCoal` avisa además que descuenta stock **antes** de validar el buffer y **sin rollback**, por si alguien lo copia como referencia. No borrar sin revisar los overrides de escena |
| 16 | `GrillView` tiene **escala no uniforme `(0.81, 1, 1)`** como override de escena. Cualquier hijo nuevo que deba verse sin deformar necesita contra-escala (`localScale.x = 1/0.81`). Es lo que hace la instancia de `StockPanel` |
| 17 | **`TutorialScene` está rota** desde el refactor del cooler: los pasos `2.PrimeraParteCooler`, `9.PasarACooler` y `12.VolverGrillView` usan `ChangeView` hacia/desde `Cooler` y ese evento ya no dispara. Los pasos de arrastre (10/11) sí funcionan porque el drop directo del panel emite las notificaciones existentes. Pendiente de decisión: rehacer esos pasos, saltearlos o sacar la escena del build. Además falta wirear en `TutorialScene` la contra-escala del panel y sus refs (`grillSystem`, `viewManager`, buffers) |
| 18 | `TutorialOfferController` pone `Time.timeScale = 0` al entrar a `GameScene` hasta que se responde el diálogo. Cualquier animación de UI que deba correr ahí necesita `Time.unscaledDeltaTime` |
| 19 | La entrega tiene **dos entradas y una sola lógica**: `GameManager.TryDeliverToCustomer(Customer)`. Al tocar validaciones, pagos o mensajes, editar **solo ahí** — `ConfirmDeliverySelection` (SPACE) y `PlateDeliveryDraggable` (mouse) son cáscaras. El `bool` de retorno lo consume el arrastre para decidir si devuelve el plato a la `PlateDropZone`: si se agrega un camino de rechazo nuevo, tiene que devolver `false` o el plato desaparece del mostrador |
| 20 | `PlateDeliveryDraggable` se agrega **en runtime** desde `MeatTransferBuffer.ConsumeBuildMeatEntry`. Es el único lugar que crea visuales de carne en el plato: si aparece otro camino que ponga un corte en la `PlateDropZone`, tiene que agregar el componente o ese plato no se podrá arrastrar |
| 21 | Los clientes se instancian con `customersParent = null` (raíz de escena), así que **no** los alcanza el toggle de `ViewManager` y sus colliders siguen activos en la vista Build. De eso depende el hover de la entrega por arrastre (`Physics2D.OverlapPointNonAlloc`). Si algún día se cuelgan de un root de vista, se rompe el drag & drop de entrega |
| 22 | **Los strikes son la única fuente de `OnNightEnded` en una partida normal.** `StrikeSystem` no conoce a `GameManager`: corta los spawns y deja que `CustomerSystem.RemoveCustomer` cierre la noche cuando se vacía el mostrador. Si se toca `BlockNewSpawns`, `SpawnsBlocked` o la condición de `RemoveCustomer`, se rompe el fin de noche entero — ver nota 23 |
| 23 | ⚠️ **Regresión abierta en el fin de noche.** El bloque `if (spawnedTonight >= customersTargetTonight && activeCustomers.Count == 0) OnNightEnded()` fue **eliminado** de `RemoveCustomer` y reemplazado por la condición con `SpawnsBlocked`. Como `SpawnCustomer` sigue frenando con un `return` silencioso al llegar a `customersTargetTonight`, una noche jugada **sin llegar a 3 strikes** ya no termina: se atienden los N clientes, `SpawnLoop` sigue girando sin spawnear y `OnNightEnded` nunca dispara (salvo que se alcance `maximumCustomersPerNight`, def. `70`). Si se quiere el cierre normal, hay que volver a chequear `spawnedTonight >= customersTargetTonight` en `RemoveCustomer` |
| 24 | `StrikeSystem` **no es singleton**: es un componente de escena con `customerSystem` y `config` por inspector. Si falta cualquiera de los dos, hace `LogError` en `Start` y **no se suscribe a nada** — no hay strikes, no hay cierre de noche y (por la nota 23) la partida queda sin final. Al duplicar `GameScene` o armar una escena nueva, verificar ese wiring |
| 25 | `StrikeSessionFlag` es el **5º DDOL** y el único que `SceneManagementUtils.ReturnToMainMenu()` **no** destruye. Se limpia solo porque `StrikeSystem.Start()` llama a `Reset()`; si algún día `StrikeSystem` deja de correr al entrar a `GameScene`, el popup de la tienda puede reaparecer sin motivo |
| 26 | `StrikeClosurePopupUI` tiene ~10 `Debug.Log`/`LogWarning` de diagnóstico en `Awake`/`Start`/`ShowPopup` — quitarlos antes de release (mismo caso que la nota 8d) |
| 27 | `CustomerSystem.RemoveCustomer` llama a **`CompactSlots()` dos veces seguidas** (`CustomerSystem.cs:555` y `:557`); es idempotente, así que solo desperdicia trabajo. `SpawnLoop` también quedó **sin indentar** respecto del resto de la clase |
| 28 | Todo el copy de strikes (cartel in-game y popup de la tienda) vive en `Assets/ScriptableObjects/StrikeConfig.asset`, no en los prefabs. Para cambiar textos, umbral (`maxStrikes`) o duración del cartel, editar el asset — no hace falta tocar código |
