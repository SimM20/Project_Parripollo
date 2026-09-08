# Plan técnico — Integración de BuildView dentro de GrillView

> ## ESTADO: IMPLEMENTADO (etapas 1–5) — pendiente de testing manual
>
> Implementado sobre `GameScene` únicamente. **`TutorialScene` no se tocó** y queda rota
> (ya lo estaba desde el refactor del cooler; este refactor le suma los pasos 20–27).
>
> Desvíos respecto del plan original, todos deliberados:
> - **`ToBuild` conserva su nombre y su posición** (superficie naranja a la derecha de la parrilla).
>   Ya era la zona de destino que el jugador conoce; renombrarla no aportaba nada y rompía lookups.
> - **La bandeja reutiliza `GrillView/MeatHolder`** (superficie gris a la izquierda, que estaba muerta
>   desde el refactor del cooler), renombrada a `MeatTray`. Cero arte nuevo.
> - **`StockPanelTab` se generalizó a `SlidingPanel`** en vez de clonar un `ToppingsPanelTab`.
> - **El panel derecho quedó arriba a la derecha** (espejo exacto del StockPanel), no en la banda
>   vertical original de `FoodItemsContainer`: esa banda es exactamente donde vive el plato.
>   Ver "Ajustes visuales pendientes" abajo.
> - **`ToppingsPanel.prefab` no se creó**: el panel vive como override de instancia dentro de
>   `GameScene`, igual que el resto de los agregados. Se puede extraer a prefab cuando haga falta.
> - **Sin cap en la bandeja** (decisión §6.5): apila sin límite.
> - `GrillNotificationManager` quedó vivo pero sin disparar (decisión §6.6).


> Refactor: eliminar `BuildView` como vista independiente. Cocinar, armar y entregar sin salir de `GrillView`.
> Decisiones ya tomadas con el desarrollador (2026-08-29):
> 1. El panel desplegable derecho contiene **todo** el contenido de `FoodItemsContainer` (panes + guarniciones + frascos).
> 2. La entrega queda **solo por drag & drop**. Se elimina el flujo por teclado (Space/A/D). `Space` = capa parrilla, `R` = cenizas. Limpiar plato pasa a `C`. `X` (descartar quemados) y `M` (informar faltante) se mantienen.
> 3. Carne soltada sobre `ToBuild` va **directo al plato** (`AddCut` inmediato). El undo/devolución deja el corte en una **bandeja** al lado, arrastrable de vuelta a parrilla o plato.

---

## 1. Estado actual

### 1.1 Flujo completo de BuildView

```
GrillView                                    BuildView
─────────                                    ─────────
Meat.OnMouseUp sobre ToBuild                 (tecla E / →)
  → MTB.TryQueueFromGrillToBuild             GameManager.Update detecta transición
  → destruye Meat, guarda BufferedMeatData     → SendMessage("MoveToBuildMeatHolder")
    (cut, tiempos A/B, cara activa, rot)       → cola toBuild → cola buildMeatHolder (máx 2)
  → visual apilado en ToBuildAnchor            → visuales en BuildView/MeatHolder
                                             BuildMeatHolderDraggableMeat → drop en PlateDropZone
                                               → BuildFoodDropZone.TryAcceptMeatAt
                                               → BuildStationSystem.AddCut(cut, state, A, B)
                                               → MTB.ConsumeBuildMeatEntry: visual pasa a
                                                 plateMeatVisuals + AddComponent PlateDeliveryDraggable
                                               → Push AddMeatUndoAction
                                             FoodItemsContainer:
                                               BuildDraggableFoodItem (pan/side) → TryAcceptAt
                                                 → SetBread/AddSide + visual + undo
                                               ToppingDraggable (frascos) → verter sobre PouringZone
                                                 → AddTopping + splatters + undo
                                             Entrega:
                                               A) SPACE → selección teclado → TryDeliverToCustomer
                                               B) PlateDeliveryDraggable → drag a CustomerView
                                                  → GameManager.TryDeliverToCustomer  ← ÚNICA lógica
                                             RollBackButton (RollBackCanvas) → BuildUndoHistory.UndoLast
```

### 1.2 Responsabilidades por objeto/clase

| Objeto / Clase | Responsabilidad hoy |
|---|---|
| `BuildView/PlateDropZone` (+`BuildFoodDropZone`) | Zona de drop del plato: acepta carne (`TryAcceptMeatAt`), pan/side/topping (`TryAcceptAt`), crea visuales de sides/toppings (`SpawnPlateVisual`), push de undo. Registro estático `ActiveZones` |
| `BuildView/FoodItemsContainer` | 2 panes + 3 guarniciones (`BuildDraggableFoodItem`, reutilizables, vuelven al origen) + 2 frascos (`ToppingDraggable`, con salsa finita) + 2 `SplatterSource` |
| `BuildView/PouringZone` | `BoxCollider2D`: zona donde el frasco rota y vierte (`rotationZone` de cada `ToppingDraggable`) |
| `BuildView/MeatHolder` + `MeatList` | Bandeja de cortes traídos de la parrilla (cola `buildMeatHolderCuts`, máx 2). `MeatList`/`MeatHolder` por nombre son el destino de devolución del plato (`IsOverBuildMeatHolder`) |
| `BuildView/RollBackCanvas ` (nombre con espacio final) | Canvas con `RollBackButton` (`RollbackButtonUI`) → `BuildUndoHistory` |
| `BuildView/HudCanvas` (+`HudManager`) | HUD de día/plata/clientes (lo alimenta `UIManager`) |
| `BuildView/DeliveryFeedback` (`DeliveryFeedbackText`) | Mensajes de entrega (singleton) |
| `BuildView/Trash` (`TrashZone`) | Basura de la vista Build (GrillView tiene la suya) |
| `GrillView/ToBuild` (+`ToBuildAnchor`, `BoxCollider2D`, `SpriteRenderer`) | Buffer de staging: recibe carne de la parrilla, apila visuales, `ToBuildDraggableMeat` permite devolverla a la parrilla |
| `MeatTransferBuffer` (raíz de escena) | 4 colas de `BufferedMeatData` + visuales + gestión de visuales del plato (`plateMeatVisuals`/`plateMeatCuts`) + hover de grilla. Colas `toGrill`/`meatHolder` ya están muertas (solo las alimentaba la Cooler View deprecada) |
| `GameManager` | Input global, transiciones Grill↔Build (W/E/flechas + `SendMessage` a buffers), sync de visibilidad de plato/carne por vista, entrega (`TryDeliverToCustomer`), descarte de quemados (`X`), fin de noche |
| `ViewManager` | `Show(view)`: Grill oculta renderers/colliders/canvases (sigue cocinando); Build usa `SetActive`. Evento `OnViewChanged` |
| `[SYSTEMS]/BuildStation` | `BuildStationSystem` (estado del armado) + `BuildUndoHistory` (pila undo) — **independientes de la vista**, no se tocan |
| `StockPanel` (en GrillView.prefab) | Panel deslizante izquierdo: `StockPanelController` (slide con `Time.unscaledDeltaTime`, pool de celdas, gates de drop) + `StockPanelSlot` (drag con ghost) + `StockPanelTab` |

### 1.3 Dependencias clave

- Entrega: **dos entradas, una lógica** — `GameManager.TryDeliverToCustomer` (nota 19 de ARQUITECTURA). El `bool` de retorno lo consume el arrastre.
- `PlateDeliveryDraggable` se agrega **en runtime** en `MeatTransferBuffer.ConsumeBuildMeatEntry` (nota 20).
- Los clientes viven en la **raíz de escena** (fuera de los roots de vista): sus colliders están siempre activos → el drag de entrega funciona desde cualquier vista (nota 21). La cámara es una sola y fija.
- `MeatTransferBuffer` resuelve anclas **por nombre**: `"ToBuild"` bajo `"GrillView"`, `"MeatHolderSlot"`/`"MeatHolder"`/`"MeatList"` bajo `"BuildView"` (`ResolveToBuildAnchor`, `ResolveBuildMeatHolderAnchor`, `IsOverBuildMeatHolder`).
- `GameManager` habla con los buffers **solo por `SendMessage`** (nota 2): `MoveToMeatHolder`, `MoveToCoalHolder`, `MoveToBuildMeatHolder`, `ClearPlateMeatVisuals`, `RemovePlateMeatVisualAt`, `SetPlateMeatVisualsVisible`.
- `GrillView` tiene **escala (0.81, 1, 1)** (nota 16): todo hijo nuevo necesita contra-escala. `MeatTransferBuffer.ApplyFixedWorldScale` ya compensa para sus visuales; `StockPanel` ya se contra-escala.
- `GrillView.prefab` y `BuildView.prefab` están instanciados también en **TutorialScene** (fuera de alcance, pero los cambios a prefab y a código compartido le llegan).

### 1.4 Flujo de un pedido (parrilla → entrega)

1. StockPanel → drag directo a la parrilla (`TryTake` + `TrySpawnMeatAtPoint`, rollback si falla).
2. Cocción por frame (`GrillSystem` → `GridSlot` → `Meat.Cook`), flip con click derecho.
3. Drag de la carne a `ToBuild` → buffer conserva tiempos de cocción, destruye el `Meat`.
4. Cambio a Build → cola pasa al `MeatHolder` → drag al `PlateDropZone` → `AddCut`.
5. Pan/side/topping desde `FoodItemsContainer` → `SetBread`/`AddSide`/`AddTopping` + undo.
6. Entrega (teclado o drag) → `TryDeliverToCustomer`: corte correcto → `DishValidator` → `CookingDeliveryEvaluator.Validate` (Crudo/Quemado bloquean; `X` descarta quemados) → `EvaluateCut` por corte → pago + propina → `PlayerWallet.Add`, `CompleteCustomer`, limpieza de plato.
7. Último cliente → `OnNightEnded` → `EndNight` → `EndScene`.

---

## 2. Inventario de impacto

### 2.1 Scripts a editar

| Script | Cambio |
|---|---|
| [MeatTransferBuffer.cs](Assets/Scripts/Core/MeatTransferBuffer.cs) | **El más afectado.** Nuevo método `TryPlateMeatFromGrill(Meat, Vector3)` (drop sobre ToBuild → visual de plato + `AddCut` + `PlateDeliveryDraggable` + undo, en un paso). Colas `toBuild`/`buildMeatHolder` se fusionan en una cola de **bandeja** anclada en GrillView. `TryReturnPlateMeatToBuildHolder` → devuelve a la bandeja. `IsOverBuildMeatHolder` → bounds de la bandeja (eliminar lookups por nombre bajo `"BuildView"`). Se eliminan `MoveToBuildMeatHolder`, `EnqueueToBuildAtPoint` (semántica vieja), `TryQueueFromGrillToBuild` |
| [Meat.cs](Assets/Scripts/Meat.cs) | `OnMouseUp`: reemplazar `TryQueueFromGrillToBuild` por `TryPlateMeatFromGrill`. Mantener las notificaciones de tutorial (`NotifyMeatDraggedToBuild`) |
| [GameManager.cs](Assets/Scripts/Core/GameManager.cs) | Quitar: W/E/flechas, bloque de transiciones (`lastView`, `MoveToMeatHolder`/`MoveToCoalHolder`/`MoveToBuildMeatHolder`, `SetActivePlateVisualsVisible`/`SetPlateMeatVisualsVisible`), flujo de entrega por teclado (`TryEnterDeliverySelection`, `ConfirmDeliverySelection`, A/D). Mover al contexto Grill: `X` (descartar — ya no depende de selección por teclado, solo de `discardCustomer`), `C` limpiar plato (antes `R`), `M` faltante. `TryDiscardBurnedCuts`: eliminar la rama `IsDeliverySelectionActive`, revalidar siempre contra `discardCustomer` |
| [ViewManager.cs](Assets/Scripts/UI/ViewManager.cs) | `Show(Build)` redirige a Grill con warning (mismo patrón que Cooler). `NextView`/`PreviousView` quedan no-op o se eliminan junto con sus llamadores. `buildRoot` queda sin asignar. **No** quitar miembros del enum `ViewType` (serializado como int en assets de tutorial) |
| [StockPanelController.cs](Assets/Scripts/UI/StockPanel/StockPanelController.cs) | Extraer a clase base reutilizable `SlidingPanel` la mecánica común: slide con `unscaledDeltaTime` + interrupción proporcional, `Open/Close/Toggle/IsOpen/IsAnimating/CanBeginDrag`, apagado de `SlidingRoot` fuera de la vista Grill, cancelación de drag al cerrar. `StockPanelController` hereda; su lógica de stock/celdas no cambia |
| [ToBuildDraggableMeat.cs](Assets/Scripts/Core/ToBuildDraggableMeat.cs) | Pasa a ser el draggable de la **bandeja**: mantiene el drop a parrilla (`TryDropFromToBuildById`); se agrega drop sobre la zona del plato → volver al armado (consume entrada de bandeja + `AddCut` + visual + undo) |
| [PlateDeliveryDraggable.cs](Assets/Scripts/Build/PlateDeliveryDraggable.cs) | `OnMouseUp`: la devolución usa el nuevo `IsOverBuildMeatHolder` (bandeja). Quitar los checks de tutorial de selección por teclado si molestan (mínimo cambio) |
| [BuildFoodDropZone.cs](Assets/Scripts/Build/BuildFoodDropZone.cs) | Sin cambios de lógica (se **reutiliza** montándolo en `ToBuild`). Solo ajustar `EnsureReferences`/valores por inspector |
| [CustomerSystem.cs](Assets/Scripts/Customers/CustomerSystem.cs) | `BeginDeliverySelection`/`EndDeliverySelection`/`SelectAdjacentCustomer`/`ShowSelectedOrderBubble` quedan sin llamadores de gameplay (los llama aún `TryDeliverToCustomer` en ramas de rechazo → limpiar esas llamadas o dejarlas como no-op seguras). `SetDeliveryDragHover` queda igual |
| [ToppingDraggable.cs](Assets/Scripts/Build/ToppingDraggable.cs) / [BuildDraggableFoodItem.cs](Assets/Scripts/Build/BuildDraggableFoodItem.cs) | **Cero cambios de código** previstos: solo se mueven los GameObjects y se re-apuntan referencias serializadas (`rotationZone`, `splatterParent`). Opcional: hook `CancelDrag` para el cierre del panel |

### 2.2 Scripts nuevos

| Script | Rol |
|---|---|
| `Assets/Scripts/UI/SlidingPanel.cs` | Base abstracta del panel deslizante (extraída de `StockPanelController`) |
| `Assets/Scripts/UI/ToppingsPanel/ToppingsPanelController.cs` | Panel derecho: hereda `SlidingPanel`. No poolea celdas ni lee stock — hospeda los GameObjects reales de `FoodItemsContainer`. Cancela drags al cerrar/cambiar de vista |
| `Assets/Scripts/UI/ToppingsPanel/ToppingsPanelTab.cs` | Pestaña (clon de `StockPanelTab`; si `StockPanelTab` se generaliza a `SlidingPanel`, no hace falta) |

### 2.3 Escenas y prefabs

| Asset | Impacto |
|---|---|
| `Assets/Scenes/GameScene.unity` | **Desempaquetar** la instancia de `BuildView.prefab` (Unity no permite reparentar hijos de una instancia como override). Mover hijos, borrar el resto al final |
| `Assets/Prefabs/GrillView.prefab` | **No modificar el prefab** (TutorialScene lo instancia). Todos los agregados (ToppingsPanel, bandeja, componente en ToBuild, objetos reparentados) van como **overrides de instancia** en GameScene |
| `Assets/Prefabs/BuildView.prefab` | Queda intacto en el proyecto (lo usa TutorialScene). Marcar deprecado. Ver §6 |
| `Assets/Prefabs/UI/ToppingsPanel.prefab` (nuevo) | Panel derecho: `SlidingRoot` → `Background` + `ItemsParent` + `Tab` (espejo de `StockPanel.prefab`) |
| `Assets/Scenes/TutorialScene.unity` | **No se toca.** Los cambios de código le llegan igual (ver §5) |

### 2.4 Referencias serializadas a migrar

| Dónde | Campo | De → A |
|---|---|---|
| `ToBuild` (nuevo `BuildFoodDropZone`) | `zoneCollider` | → `BoxCollider2D` propio de ToBuild |
| ídem | `buildStationSystem` / `meatTransferBuffer` | → `[SYSTEMS]/BuildStation` / `MeatTransferBuffer` |
| ídem | `plateVisualDirection/Spacing/Scale/SortingOrder` | Ajustar al espacio de ToBuild (sorting ≥ 500, por encima del fondo de GrillView) |
| `MeatTransferBuffer` | `buildMeatHolderAnchor` | `BuildView/MeatHolder` → nueva `GrillView/MeatTray` (bandeja) |
| ídem | `toBuildAnchor` / `toBuildDropArea` | Quedan en `ToBuild`/`ToBuildAnchor` (sin cambio) |
| `Item_Chimichurri` / `Item_SalsaCriolla` (`ToppingDraggable`) | `rotationZone` | `BuildView/PouringZone` → `PouringZone` reposicionada sobre ToBuild (bajo GrillView) |
| ídem | `splatterParent` | `*SplatterSource` movidos junto con los frascos |
| `UIManager` → HUD | referencia a `HudManager` | Verificar al reparentar `HudCanvas` (si es lookup, nada; si es serializada, re-asignar) |
| `GameManager` | `viewManager` | Se mantiene (ViewManager sobrevive reducido) |

### 2.5 Elementos que quedan obsoletos

| Elemento | Momento |
|---|---|
| `BuildView` (objeto de GameScene) y sus hijos no migrados (`PreparationTable`, `Trash`, `MeatHolder`, `MeatList`, `PlateDropZone`) | Etapa 5 |
| [BuildMeatHolderDraggableMeat.cs](Assets/Scripts/Build/BuildMeatHolderDraggableMeat.cs) | Etapa 5 (nadie más lo agrega) |
| Colas `toGrill`/`meatHolder` de `MeatTransferBuffer` + [MeatHolderDraggableMeat.cs](Assets/Scripts/Core/MeatHolderDraggableMeat.cs) + `GrillView/MeatHolder` | **Ya muertas hoy** (solo las alimentaba la Cooler View deprecada). Limpieza opcional, misma etapa |
| `CoalTransferBuffer.MoveToCoalHolder` + cola `toGrill` de carbón | Ídem (opcional) |
| Flujo de entrega por teclado: `TryEnterDeliverySelection`, `ConfirmDeliverySelection`, `SelectAdjacentCustomer` como input | Etapa 4 |
| `GrillNotificationManager` (existía para avisar cocción mientras estabas en otra vista) | Nunca más se dispara. Dejar inactivo o eliminar (ver §6) |
| `SendMessage`s de transición en `GameManager` | Etapa 4 |

---

## 3. Arquitectura objetivo

### 3.1 GrillView (responsabilidades finales)

Única vista de gameplay. Contiene: parrilla + capas carne/carbón (igual), StockPanel izquierdo (igual), **ToppingsPanel derecho** (nuevo), **ToBuild = zona del plato** con `BuildFoodDropZone`, **MeatTray** (bandeja de devolución), `PouringZone` sobre el plato, `RollBackCanvas`, `DeliveryFeedback`. `HudCanvas` pasa a la raíz de escena (HUD global, no pertenece a una vista).

### 3.2 Panel de toppings (desplegable derecho)

- Prefab espejo de `StockPanel.prefab`: `ToppingsPanel` (con `ToppingsPanelController : SlidingPanel`) → `SlidingRoot` → `Background` (SpriteRenderer, gate de drop) + `ItemsParent` + `Tab`.
- Instanciado como override bajo la instancia de GrillView en GameScene, **lado derecho**: `closedLocalX` fuera de pantalla a la derecha, `openLocalX` visible (espejo de los valores del StockPanel). Contra-escala `localScale.x = 1/0.81`.
- `ItemsParent` recibe los 9 hijos actuales de `FoodItemsContainer` **movidos** (no copiados), conservando su **posición Y de mundo actual**; la X se reacomoda dentro del ancho del panel.
- Comportamiento idéntico al StockPanel: mismo slide (`unscaledDeltaTime`, interrupción proporcional), pestaña lateral clickeable, tecla de toggle (propuesta: `T`, gestionada por `GameManager` como `Q`), cierre cancela el drag activo, fuera de la vista Grill se desactiva `SlidingRoot` (con la vista única esto queda latente pero inofensivo).
- Los items **no cambian su lógica**: `BuildDraggableFoodItem` sigue volviendo a su posición de origen tras el drop (que ahora es dentro del panel); los frascos siguen consumiendo salsa contra la `PouringZone`.
- **Reutilización vs abstracción**: se abstrae solo la mecánica de deslizamiento/pestaña/gating (`SlidingPanel`). El contenido NO se abstrae: StockPanel es data-driven (stock → celdas pooleadas), ToppingsPanel es un contenedor de objetos fijos. Forzar una base común de contenido duplicaría complejidad, no la evitaría.

### 3.3 ToBuild → zona del plato

- Recibe el componente `BuildFoodDropZone` (reutilizado tal cual): pan/sides/toppings del panel derecho se sueltan/vierten ahí, con los mismos visuales de plato (`SpawnPlateVisual`) y el mismo undo.
- Carne: `Meat.OnMouseUp` sobre el collider de ToBuild → `MeatTransferBuffer.TryPlateMeatFromGrill`: captura `BufferedMeatData`, destruye el `Meat`, crea el visual del plato (mundo, sin parent, para esquivar la escala 0.81), lo registra en `plateMeatCuts`/`plateMeatVisuals`, hace `AddCut(cut, state, A, B)`, agrega `PlateDeliveryDraggable` y pushea `AddMeatUndoAction`. Un solo gesto: parrilla → plato.
- El viejo `PlateDropZone` de BuildView pierde su componente en la misma etapa (nunca dos `BuildFoodDropZone` activos).
- **Nombre**: conservar `"ToBuild"` durante toda la migración (los lookups por nombre de `MeatTransferBuffer` dependen de él). Renombrar a `PlateZone` recién en la Etapa 5, actualizando `ResolveToBuildDropArea`/`ResolveToBuildAnchor` en el mismo commit. Recomendado: sí renombrar (el nombre viejo describe un rol que ya no existe).

### 3.4 MeatTray (bandeja)

- Nuevo GameObject bajo GrillView al lado de ToBuild (hereda el rol de `BuildMeatHolder` + cola `toBuild`, fusionadas en una sola cola en `MeatTransferBuffer`).
- Destino de: undo de carne (`AddMeatUndoAction`) y devolución por arrastre del plato (`IsOverBuildMeatHolder` pasa a medir sus bounds).
- Sus visuales llevan `ToBuildDraggableMeat`: arrastrables de vuelta a la **parrilla** (ya implementado) o al **plato** (extensión menor).

### 3.5 RollBackCanvas

- Se reparenta bajo GrillView (misma posición de pantalla). `RollbackButtonUI` resuelve `BuildUndoHistory.Instance` solo → sin recableo. Nota: `ViewManager.SetVisualVisibility` apaga los `Canvas` de GrillView al salir de la vista; como la vista es única, no afecta. Aprovechar el reparent para quitar el espacio final del nombre `"RollBackCanvas "`.

### 3.6 Nuevo flujo de armado y entrega

```
StockPanel → parrilla → cocinar/flip → drag a ToBuild(plato) [AddCut directo]
  → pan/side desde ToppingsPanel → drop en plato · frascos → verter sobre PouringZone
  → entrega: arrastrar el plato hasta el cliente (PlateDeliveryDraggable, sin cambios de fondo)
  → rechazo: plato vuelve a ToBuild · X descarta quemados y revalida contra discardCustomer
  → undo (botón): última acción; carne vuelve a MeatTray
  → C: limpiar plato completo
```

### 3.7 GameManager (responsabilidades que conserva)

Pausa (`Esc`), toggle StockPanel (`Q`) y ToppingsPanel (`T`), capa parrilla (`Space`), cenizas (`R`), limpiar plato (`C`), descarte de quemados (`X`), informar faltante (`M`), **`TryDeliverToCustomer` intacto como única lógica de entrega**, `EndNight`. Pierde: navegación de vistas, sincronización por transición, entrega por teclado.

---

## 4. Plan de implementación por etapas

> Cada etapa deja el proyecto compilando. El estado jugable esperado se indica al final de cada una. Validar consola vía MCP tras cada etapa.

### Etapa 1 — Extraer `SlidingPanel` (refactor puro)

**Archivos**: nuevo `Assets/Scripts/UI/SlidingPanel.cs`; editar `StockPanelController.cs`.
1. Crear base abstracta con: `slidingRoot`, `openLocalX`, `closedLocalX`, `slideDuration`, `Open/Close/Toggle`, `IsOpen/IsAnimating/CanBeginDrag`, corrutina de slide (idéntica, `unscaledDeltaTime`), manejo de `OnViewChanged` (activar/desactivar `SlidingRoot`), hook virtual `OnPanelClosing()` para cancelar drags.
2. `StockPanelController : SlidingPanel`; borrar los miembros movidos; conservar intactos gates de tutorial, binding a `CoolerSystem`, celdas.
**Estado esperado**: juego idéntico al actual. **Nada obsoleto aún.**

### Etapa 2 — ToBuild asume el plato (solo carne)

**Archivos**: `MeatTransferBuffer.cs`, `Meat.cs`, `PlateDeliveryDraggable.cs`, `ToBuildDraggableMeat.cs`, `GameManager.cs` (parcial), GameScene.
1. GameScene: desempaquetar la instancia de `BuildView.prefab` (Unpack Completely).
2. Agregar `BuildFoodDropZone` a `GrillView/ToBuild` (override de instancia): `zoneCollider` = su `BoxCollider2D`; refs a BuildStation y MeatTransferBuffer; params de visuales de plato ajustados. **Quitar el componente `BuildFoodDropZone` de `BuildView/PlateDropZone` en el mismo paso.**
3. Crear `GrillView/MeatTray` (override) al lado de ToBuild; en `MeatTransferBuffer`: fusionar colas `toBuild`+`buildMeatHolder` → `trayCuts` ancladas en MeatTray; `buildMeatHolderAnchor` → MeatTray.
4. `MeatTransferBuffer.TryPlateMeatFromGrill(Meat, point)` (ver §3.3). `Meat.OnMouseUp` lo llama en lugar de `TryQueueFromGrillToBuild`. Conservar `NotifyMeatDraggedToBuild` + `NotifyMeatPlacedOnBuildZone`.
5. `TryReturnPlateMeatToBuildHolder` → devuelve a `trayCuts`; `IsOverBuildMeatHolder` → bounds de MeatTray (borrar lookups `"MeatList"`/`"MeatHolder"` bajo `"BuildView"`).
6. `ToBuildDraggableMeat`: agregar rama de drop sobre la zona del plato → re-emplatar.
7. Reparentar bajo GrillView: `RollBackCanvas`, `DeliveryFeedback`, `PouringZone` (reposicionada sobre ToBuild). `HudCanvas` → raíz de escena.
8. `GameManager`: habilitar `X`/`C`/`M` también en vista Grill (los checks de Build se quitan recién en Etapa 4); quitar el sync `SetActivePlateVisualsVisible`/`SetPlateMeatVisualsVisible` (el plato queda siempre visible).
**Estado esperado**: ciclo completo jugable desde GrillView **solo con platos sin pan/sides/toppings** (los items siguen en BuildView pero su drop zone vieja ya no existe → pedidos sándwich no completables). Rama de trabajo, estado transitorio aceptado.
**Obsoleto ya**: `BuildMeatHolderDraggableMeat` (nadie lo agrega), `MoveToBuildMeatHolder`.

### Etapa 3 — ToppingsPanel derecho

**Archivos**: nuevos `ToppingsPanelController.cs` (+Tab si aplica), `Assets/Prefabs/UI/ToppingsPanel.prefab`; GameScene; `GameManager.cs` (tecla `T`).
1. Construir el prefab (espejo de StockPanel, lado derecho, contra-escala 1/0.81) e instanciarlo como override bajo GrillView.
2. **Mover** los 9 hijos de `BuildView/FoodItemsContainer` a `ItemsParent`, conservando Y de mundo; re-apuntar `rotationZone` → PouringZone nueva y `splatterParent` → los SplatterSource movidos.
3. `GameManager`: tecla `T` (`toppingsPanelToggleKey`, serializada como `Q`).
4. Cierre del panel: cancelar drag/vertida activa (hook `OnPanelClosing`; si un frasco está vertiendo, `OnMouseUp` natural del jugador basta — evaluar si hace falta forzarlo).
**Estado esperado**: armado completo (carne + pan + sides + toppings) y entrega, todo desde GrillView. BuildView ya no aporta nada, pero sigue siendo navegable con `E`.

### Etapa 4 — Unificación de flujo en GameManager/ViewManager

**Archivos**: `GameManager.cs`, `ViewManager.cs`, `CustomerSystem.cs`, `PlateDeliveryDraggable.cs` (menor).
1. `GameManager.Update`: eliminar W/E/flechas, `lastView` y todo el bloque de transiciones (SendMessages `MoveToMeatHolder`/`MoveToCoalHolder`/`MoveToBuildMeatHolder`, `RefreshVisibility`, `SetMeatVisualsVisible` por transición — dejar la llamada inicial de `Start`), y el bloque `currentView == Build` completo (sus teclas ya viven en el contexto Grill desde la Etapa 2).
2. Eliminar entrega por teclado: `TryEnterDeliverySelection`, `ConfirmDeliverySelection` y los inputs A/D. En `TryDeliverToCustomer`, quitar las llamadas a `EndDeliverySelection` (ya no hay selección activa posible) — revisar cada rama de rechazo.
3. `ViewManager`: `Show(Build)` → redirect a Grill con warning (patrón Cooler); `NextView`/`PreviousView` → no-op.
4. `CustomerSystem`: `BeginDeliverySelection`/`EndDeliverySelection`/`SelectAdjacentCustomer` quedan sin llamadores — no borrarlos todavía (TutorialManager referencia el flujo); marcar deprecados.
**Estado esperado**: imposible salir de GrillView; sin dobles caminos de entrega; teclas: Q/T paneles, Space capa, R cenizas, C plato, X quemados, M faltante, Esc pausa.

### Etapa 5 — Limpieza y renombre

**Archivos**: GameScene, `MeatTransferBuffer.cs`, scripts obsoletos.
1. Eliminar de GameScene el objeto `BuildView` restante (`PreparationTable`, `Trash`, `MeatHolder`, `MeatList`, `PlateDropZone` vacío, `FoodItemsContainer` vacío). `BuildView.prefab` queda en el proyecto (TutorialScene) — ver §6.
2. Borrar `BuildMeatHolderDraggableMeat.cs`. Opcional (dead code preexistente): colas `toGrill`/`meatHolder`, `MeatHolderDraggableMeat.cs`, `GrillView/MeatHolder`, `MoveToCoalHolder`.
3. Renombrar `ToBuild` → `PlateZone` y `ToBuildAnchor` → `PlateAnchor` (decisión recomendada, ver §6): actualizar `ResolveToBuildDropArea`/`ResolveToBuildAnchor` (strings `"ToBuild"`) y renombrar `ToBuildDraggableMeat` → `TrayDraggableMeat` en el mismo commit.
4. `GrillNotificationManager`: decidir (ver §6); mínimo, dejar de suscribirlo si se elimina.
**Estado esperado**: proyecto final limpio; grep de `BuildView` en `Assets/Scripts` solo debe aparecer en código del tutorial.

### Dependencias entre etapas

`E1 → E2 → E3 → E4 → E5` estricto. E2 y E3 podrían invertirse pero E3 sin E2 deja los items del panel sin ninguna zona de drop válida. E4 requiere E3 (si no, se pierde acceso a los items). E5 requiere E4.

---

## 5. Riesgos y casos límite

| Riesgo | Detalle / Mitigación |
|---|---|
| **Referencias nulas por lookups por nombre** | `MeatTransferBuffer` busca `"MeatHolderSlot"`/`"MeatHolder"`/`"MeatList"` bajo `"BuildView"` y `"ToBuild"` bajo `"GrillView"`. Fallan **en silencio** (devuelven null/false). Etapa 2 los elimina/redirige; el renombre de Etapa 5 debe actualizar los strings restantes en el mismo commit |
| **`SendMessage` rotos en silencio** | `ClearPlateMeatVisuals`/`RemovePlateMeatVisualAt` se siguen usando desde `GameManager` por `SendMessage` (nota 2). No renombrarlos. Los `Move*` eliminados: borrar emisor y receptor juntos |
| **Dos zonas de drop activas** | `BuildFoodDropZone.ActiveZones` es registro estático: si ToBuild y PlateDropZone conviven con el componente, un drop puede aceptarse en la zona equivocada. Mitigado: alta y baja en el mismo paso (E2.2) |
| **Entrega duplicada** | Eliminado el flujo por teclado, `TryDeliverToCustomer` solo se llama desde `PlateDeliveryDraggable.OnMouseUp` (una vez por gesto). La limpieza del plato dentro del método sigue siendo atómica |
| **Pérdida de carne en la bandeja** | `TryReturnPlateMeatToBuildHolder` no respeta `maxBuildMeatHolder` (solo lo hacía `MoveToBuildMeatHolder`). Definir cap de `trayCuts` (ver §6); sin cap, el layout de la bandeja debe tolerar N entradas |
| **Rollback incompleto** | `SetBreadUndoAction` captura sprite/escala/rotación del visual de carne: el nuevo visual creado por `TryPlateMeatFromGrill` debe pasar por el mismo pipeline (`UpdatePlateMeatSprite`, `RefreshCollider`) o el undo de pan restaurará un estado incoherente |
| **Splatters** | Siguen sin acompañar el arrastre del plato (comportamiento actual, documentado). `ClearAllSplatters` se mantiene en entrega y limpieza. Verificar que los `SplatterSource` movidos conserven world-position sobre el plato |
| **Escala (0.81,1,1) de GrillView** | Panel derecho y MeatTray necesitan contra-escala; visuales de plato: crearlos sin parent (mundo). `ApplyFixedWorldScale` ya compensa los visuales de bandeja |
| **Sorting orders** | Plato base 500, splatters 5500, drags 6000, +5000 durante arrastre de entrega. El fondo/hijos de GrillView deben quedar por debajo de 500 en la zona del plato. Pasada visual al final de E2/E3 |
| **Cambio de día durante armado** | `OnNightEnded` → carga `EndScene`: el armado y los visuales del plato son objetos de escena → se destruyen. `BuildStationSystem` renace vacío. Igual que hoy; sin persistencia espuria |
| **Estado persistente de BuildView** | No hay: `BuildStationSystem`/`BuildUndoHistory` viven en `[SYSTEMS]` y no dependen de la vista. Los SO mutan en runtime (nota 5) pero nada del armado se serializa |
| **Cross-drop entre paneles** | Soltar un item del StockPanel sobre el ToppingsPanel abierto (o viceversa) intentaría colocarlo igual: cada gate solo chequea su propio `panelBackground`. Edge case menor: agregar chequeo cruzado o aceptar el rollback natural del spawn fallido |
| **Drag activo al cambiar capa** | `Space` ya está gateado por `Input.GetMouseButton(0)`. Los drags del panel derecho quedan cubiertos por el mismo guard |
| **TutorialScene** | Fuera de alcance pero **recibe los cambios de código**: `Meat.OnMouseUp` (pasos 20-22 de arrastre a Build), la eliminación del flujo de selección (paso 25 `BeginDelivery` — `PlateDeliveryDraggable` sigue notificando `NotifyDeliverySelectionBegun`, se mantiene), y las instancias de `BuildView.prefab`/`GrillView.prefab`. El tutorial ya está roto (nota 17); este refactor lo rompe más. Decisión en §6 |
| **`isDirty` de GameScene** | La escena está sucia en el editor al momento de este análisis; antes de empezar, confirmar con el dev qué cambios locales hay sin guardar |

---

## 6. Preguntas y decisiones pendientes

**Resueltas con el desarrollador (registradas arriba)**: contenido del panel = todo FoodItemsContainer · entrega solo drag & drop (Space=capa, R=cenizas, C=limpiar plato) · carne directa al plato con bandeja para undo/devolución.

Pendientes:

1. **TutorialScene**: ¿sacarla del build hasta rehacerla, o dejarla rota? (Ya estaba rota por el refactor del cooler — nota 17. Este refactor invalida además los pasos de BuildView 20-27.) Recomendación: sacarla del build en la Etapa 5 y rehacer el tutorial como tarea aparte.
2. **`BuildView.prefab`**: conservar (lo instancia TutorialScene) hasta resolver el punto 1. No borrarlo en la Etapa 5.
3. **Renombrar `ToBuild`**: recomendación **sí**, a `PlateZone`, recién en Etapa 5 (§3.3). Confirmar nombre.
4. **Tecla del panel derecho**: propuesta `T` (espejo de `Q`). Confirmar.
5. **Cap de la bandeja**: `maxBuildMeatHolder` era 2. ¿Mantener 2 (y bloquear undo de carne con bandeja llena, con feedback) o sin límite con layout apilado? Recomendación: sin límite duro, apilado como el ToBuild actual.
6. **`GrillNotificationManager`**: queda muerto (avisaba cocción al estar en otra vista). ¿Eliminar o conservar por si vuelve una segunda vista? Recomendación: conservar desactivado en E5, borrar en una limpieza futura.
7. **`ToppingStock`** (DDOL, compras de toppings en la tienda) **no está conectado** al uso de frascos en el armado (los frascos solo miden salsa por `maxSauceAmount`). Gap preexistente, fuera de alcance — ¿conectar recarga de frascos con stock comprado en una tarea futura?
8. **Posición/tamaño definitivos** de ToBuild(plato), MeatTray y PouringZone en el layout de GrillView: decisión visual del dev durante E2 (el plan solo fija anclas y jerarquía).

---

## 7. Checklist manual para el desarrollador (post-implementación)

Cocción y colocación:
- [ ] StockPanel: drag de corte y carbón a la parrilla; rollback de stock si el spawn falla; `R` rota footprint durante el drag.
- [ ] `Space` cambia capa carne/carbón; `R` limpia cenizas; ninguno dispara nada del armado.
- [ ] Abrir/cerrar StockPanel (Q y pestaña) y ToppingsPanel (T y pestaña); ambos abiertos a la vez; cerrar con drag activo cancela sin perder stock/salsa.

Armado:
- [ ] Carne cocida → drag a la zona del plato: entra al armado con sus dos caras (verificar pago con desfase luego).
- [ ] Pan sobre el plato: sprite de variante (sándwich) aplicado; sides y toppings suman visuales.
- [ ] Frasco: rota y vierte solo sobre la PouringZone; barra de salsa baja; splatters aparecen sobre el plato; frasco vacío no vierte.
- [ ] Botón Rollback: deshace en orden topping/side/pan/carne; la carne vuelve a la bandeja; el pan restaura el sprite anterior.
- [ ] `C` limpia el plato completo (datos + visuales + splatters).
- [ ] Bandeja: corte devuelto es arrastrable de vuelta a la parrilla (conserva cocción — verificar que siga cocinando desde donde quedó) y de vuelta al plato.

Entrega:
- [ ] Arrastre del plato completo (carne + extras como bloque); hover resalta cliente; soltar sobre cliente correcto paga (con propina si el punto es exacto y el cliente esperó poco).
- [ ] Soltar al vacío o entrega rechazada: el plato vuelve a su lugar, sin duplicar ni perder visuales.
- [ ] Corte incorrecto / falta pan en pedido sándwich: mensaje de `DeliveryFeedback` visible en GrillView.
- [ ] Entrega con Crudo: bloqueada. Con Quemado: bloqueada + `X` descarta y revalida contra el mismo cliente (probar vía arrastre, que es el único camino ahora).
- [ ] Descartar todos los cortes con `X` → mensaje "no queda nada", plato coherente.
- [ ] Soltar el plato sobre la bandeja: la carne vuelve a la bandeja (sin extras — verificar qué pasa con pan/sides ya agregados).

Flujo general:
- [ ] W/E/flechas ya no cambian nada; no existe forma de ver la BuildView.
- [ ] HUD (día/plata/clientes) visible y actualizándose; pausa con Esc; cartel de pausa por encima de los paneles.
- [ ] Cliente se va enojado durante un arrastre de entrega: el recuadro se libera, el plato vuelve, sin NRE en consola.
- [ ] Último cliente atendido con plato a medio armar: transición a EndScene limpia; nueva noche arranca con plato vacío y paneles cerrados.
- [ ] Noche completa + tienda + volver a GameScene: paneles, plato y bandeja en estado inicial; stock consistente.
- [ ] Consola sin errores/warnings nuevos durante una noche completa.


---

## 8. Resultado de la implementación

### Layout final de GrillView

| Zona | Objeto | Rol |
|---|---|---|
| Izquierda (superficie gris) | `GrillView/MeatTray` | Bandeja: cortes devueltos del plato (undo o arrastre). Arrastrables de vuelta al plato o a la parrilla |
| Centro | `GrillView/Grill` + `CoalGrillLayer` | Parrilla, sin cambios |
| Derecha (superficie naranja) | `GrillView/ToBuild` | **Zona del plato** (`BuildFoodDropZone`). Su `BoxCollider2D` medía 0.01×0.01 y se redimensionó al sprite (5.79 × 4.88) |
| Arriba izquierda | `GrillView/StockPanel` | Stock, sin cambios |
| Arriba derecha | `GrillView/ToppingsPanel` | Panes, guarniciones y frascos. Espejo del StockPanel |
| Sobre el plato | `GrillView/PouringZone` | Zona de volcado de salsa, world (6.50, −0.50), box 5.8 × 2.6 |
| Pasto izquierdo | `GrillView/RollBackCanvas` | Botón de undo, world (−6.20, 0.55) |
| Raíz de escena | `HudCanvas` | HUD global, ya no pertenece a ninguna vista |

Los `SplatterSource` quedaron **fuera** del panel deslizante (bajo `GrillView`, fijos sobre el plato):
si viajaran con el panel, las salpicaduras se irían del mostrador al cerrarlo.

### Controles finales

| Tecla | Acción |
|---|---|
| `Q` | Panel de stock |
| `T` | Panel de items de armado |
| `Espacio` | Capa carne ↔ carbón |
| `R` | Limpiar cenizas · rotar footprint mientras se arrastra |
| `C` | Limpiar el plato entero |
| `X` | Descartar cortes quemados y revalidar |
| `M` | Informar corte faltante |
| `Esc` | Pausa |
| Click derecho | Dar vuelta la carne |
| Arrastrar el plato | Entregar (única vía) |

`W`, `E`, `←`, `→`, y la entrega por teclado (`Espacio` + `A`/`D`) ya no existen.

### Archivos

**Nuevos**: `Assets/Scripts/UI/SlidingPanel.cs`, `Assets/Scripts/UI/ToppingsPanel/ToppingsPanelController.cs`
**Eliminados**: `Assets/Scripts/Build/BuildMeatHolderDraggableMeat.cs`, objeto `BuildView` de `GameScene`
**Editados**: `MeatTransferBuffer.cs` (reescrito), `GameManager.cs`, `ViewManager.cs`, `StockPanelController.cs`,
`StockPanelTab.cs`, `ToBuildDraggableMeat.cs`, `ToppingDraggable.cs` (+`CancelDrag`), `Meat.cs`,
`PlateDeliveryDraggable.cs`, `BuildUndoActions.cs`, `DeliveryFeedbackText.cs`

`Assets/Prefabs/BuildView.prefab` **se conservó**: lo instancia `TutorialScene`.

### API de MeatTransferBuffer después del refactor

```
TryPlateMeatFromGrill(Meat, punto)      parrilla → plato, en un solo gesto
TryPlateFromTrayById(id, punto)         bandeja  → plato
TryDropFromTrayById(id, punto, rot)     bandeja  → parrilla
TryReturnPlateMeatToTray(visual)        plato    → bandeja (undo y arrastre)
IsOverMeatTray(punto)                   consulta de la bandeja
```
Se conservan sin renombrar los métodos que `GameManager` invoca por `SendMessage`
(`ClearPlateMeatVisuals`, `RemovePlateMeatVisualAt`, `SetPlateMeatVisualsVisible`).

### Ajustes visuales pendientes (para el desarrollador)

1. **Posición del panel de items**: quedó arriba a la derecha, espejo del StockPanel. El plan pedía
   conservar la banda vertical de `FoodItemsContainer` (Y ≈ −2.4 a −4.0), pero ahí está el plato:
   el panel lo taparía al abrirse. Si se prefiere igual, mover el root `ToppingsPanel` en Y.
2. **Escala de los items**: bajada de `0.08` a `0.05` para que entren en la grilla 3×3 del panel.
3. **Tamaño y grilla del panel**: `columns=3`, `cellSpacing=(1.45, 0.90)`, `firstCellLocalOffset=(-1.45, 0.85)`
   sobre un fondo de 4.60 × 2.90. Todo editable en el inspector de `ToppingsPanelController`.
4. **`PouringZone`**: box 5.8 × 2.6 centrada en (6.50, −0.50). La salsa cae 2 unidades: verificar que
   las salpicaduras aterricen dentro del plato.
5. **`RollBackCanvas` y `DeliveryFeedback`**: reubicados a ojo, conviene afinarlos.
