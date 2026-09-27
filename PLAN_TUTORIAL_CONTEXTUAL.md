# Plan técnico — Tutorial con carteles contextuales

> ## ESTADO: fases 1 y 2 IMPLEMENTADAS (2026-09-27, rama `tutorialREV`) — fases 0 y 3 a 7 pendientes
>
> - **Fase 1 (señales):** hecha y probada. Los ~20 `TutorialManager.Notify*` pasaron a `TutorialSignals.Raise`. El
>   `TutorialManager` viejo se suscribe a las señales; se probó en Play que `TutorialScene` avanza igual del paso 7 al 17
>   (stock, carne, capas, carbón, vuelta, punto de cocción, plato y entrega).
> - **Fase 2 (cartel):** hecha y probada en `GameScene` a 1920×1080: ícono + texto, flecha, sin salirse de pantalla,
>   cambio en vivo teclado ↔ joystick e idioma, oculto en pausa, sin raycasts. Se ve desde el QA (`TutorialHintDebug`):
>   todavía no hay director.
>
> Desvíos respecto del plan, todos deliberados:
> - **Q y E van debajo de su pestaña**, no al costado: al costado, el de la izquierda le tapa la cara al primer cliente.
> - **Arte provisorio** (`Sprites/UI/Tutorial/* PH.png`) para la fase 0: fondo y flecha del cartel, tecla y botón en blanco,
>   íconos de mouse. `LetraQ` y `LetraE` se usan tal cual (con mipmaps activados); `LetraR` y `LetraSpace` no, porque vienen
>   en lienzos de 1536×1024 casi vacíos. Espacio, R y los botones del joystick salen como tecla en blanco con su nombre.
> - **De las señales nuevas solo se agregó `ToppingsPanelOpened`** (la usa el QA de la E). Las demás se suman en la fase
>   3 o 5, junto con el cartel que las usa, para no dejar avisos que nadie escucha.

> Decisiones tomadas con el desarrollador (2026-09-27):
> 1. El tutorial pasa a la partida real (`GameScene`), sin escena aparte. **Nada bloquea ni pausa.**
> 2. La primera noche es el juego tal cual: sin cambios de ritmo ni de reglas. Lo único distinto son los carteles.
> 3. La E aparece al arrancar, junto con la Q (izquierda carne, derecha extras), más el cartel 13 cuando llega el
>    primer pedido con extras.
> 4. La tienda también lleva carteles, con el mismo criterio de poco texto, y aparecen al terminar el día.

---

## 1. La idea

Hoy el tutorial son 28 pasos lineales con paneles de texto, en dos escenas aparte (`TutorialScene`, `ShopTutorial`). Todo
lo que no es el paso actual queda bloqueado (`Check*Allowed`) y la cocción se pausa.

El nuevo son **ayudas dentro de la partida**, en la primera noche y la primera vez que pasa algo. Cada cartel es el ícono
del control y 2 a 4 palabras, en la zona de la acción:

1. **Nada bloquea ni pausa.**
2. **El cartel reacciona al resultado, no a la tecla.** "Q · Ver carnes" se va cuando *se abre el panel*, sea con Q, LB
   o un click en la pestaña.
3. **Si ya lo hiciste, no aparece.** Hacer la acción antes de ver el cartel lo da por aprendido.
4. **Objetivo + contexto.** Un cartel se muestra mientras su contexto vale y se completa con su objetivo. Con el panel
   cerrado se ve "Q · Ver carnes"; abierto, "Arrastrá a la parrilla"; si se cierra sin poner carne, vuelve la Q.
5. **Como máximo 2 a la vez**, por prioridad. La E tiene la más baja: cede su lugar y vuelve cuando se libera.

## 2. Piezas (`Assets/Scripts/Tutorial/`)

| Pieza | Qué hace | Estado |
|---|---|---|
| `TutorialSignals` | Hub estático: `Raise(señal, cut, coal, meat, layer, target)` + evento `Raised`. `target` es lo que conviene señalar (la carne, el carbón, la carne ya en el plato) | ✅ |
| `TutorialHintLayer` + `Prefabs/UI/TutorialHints.prefab` | Canvas overlay (orden 15, sin raycaster, oculto en pausa). Crea y recicla carteles: `Show(prompt, textKey, target, placement, offset)` | ✅ en `GameScene` |
| `TutorialHintView` + `Prefabs/UI/TutorialHint.prefab` | Un cartel: ícono + texto + flecha, pegado a un objeto del mundo o de UI | ✅ |
| `InputGlyphSetSO` + `ScriptableObjects/TutorialHints/InputGlyphs.asset` | Ícono por control físico del binding. Sin dibujo: tecla o botón en blanco con el nombre | ✅ |
| `TutorialHintDebug` | QA en Play: Q y E, cartel de prueba configurable, ocultar todos | ✅ |
| `TutorialHintSO` | Un asset por cartel: id, control (`HintPrompt`), clave de texto, ancla + lado + offset, prerrequisitos, contexto, señal que lo completa, prioridad, auto-ocultar | fase 3 |
| `TutorialHintDirector` | Uno por escena: decide qué carteles se ven (≈5 veces por segundo + en cada señal) y guarda los completados | fase 3 |
| `HintAnchor` | Marca zonas fijas (`StockTab`, `ToppingsTab`, `LayerButton`, `Grill`, `Plate`, `Undo`, `Trash`, `Clock`, `Strikes`) con registro estático | fase 3 |

**Señales que faltan** (se agregan con su cartel): `CustomerSpawned`, `CustomerHovered` (`CustomerView.OnWorldPointerEnter`),
`CoalBecameAsh` (`Coal.Burn`, solo en la transición), `AshesCleaned`, `PieceRotated`, `PlateCleared`, `UndoUsed`,
`MissingCutUsed`, `DeliveryRejected`. Strikes, cierre y armado del plato ya tienen evento propio. En la tienda alcanza con
`ShopSystem.OnPurchaseResult` y `OnTabChanged`; solo T3 necesita un aviso nuevo desde `ShopItemCellUI.RefreshVisuals`.

## 3. Catálogo de carteles

### 3.1 Primera noche (lo básico)

| # | Cartel | Zona | Aparece | Se va |
|---|---|---|---|---|
| 1 | Q · Ver carnes | debajo de la pestaña izquierda | al arrancar, con el panel cerrado | hay carne en la parrilla (se oculta al abrir el panel) |
| 2 | E · Ver panes y toppings | debajo de la pestaña derecha | al arrancar | se abre el panel derecho |
| 3 | 🖱 Arrastrá a la parrilla | parrilla | panel abierto y parrilla vacía | hay carne en la parrilla |
| 4 | Espacio · Capa de carbón | botón de capa | hay carne y ningún carbón | hay carbón |
| 5 | 🖱 Poné carbón abajo | la carne | capa de carbón activa y sin carbón | hay carbón |
| 6 | Espacio · Volver a la carne | botón de capa | ya hay carbón y sigue la capa de carbón | capa de carne |
| 7 | 🖱 Ver pedido (pasar el mouse) | el cliente | llega el primer cliente | pasar el mouse sobre el cliente |
| 8 | Click derecho · Dar vuelta | la carne | la cara de abajo dejó de estar cruda | primera vuelta |
| 9 | 🖱 Llevá la carne al plato | plato | carne a punto para un pedido (a un punto o menos, como `EvaluateCut`) | carne en el plato |
| 10 | 🖱 Llevá el plato al cliente | plato | carne en el plato y un cliente esperando | entrega aceptada |

### 3.2 La primera vez que pasa (cualquier noche)

| # | Cartel | Cuándo aparece |
|---|---|---|
| 11 | R · Limpiar cenizas (sobre la ceniza) | el primer carbón se hace ceniza |
| 12 | R · Rotar (junto a la pieza) | se arrastra un corte que se puede rotar |
| 13 | 🖱 Agregá el pan / Serví la salsa | pedido con extras + panel derecho abierto + carne en el plato |
| 14 | M · Pedir otro corte (sobre el cliente) | el cliente pide un corte sin stock |
| 15 | C · Vaciar plato (y ↶ Deshacer) | el primer rechazo de una entrega |
| 16 | ⚠ ¡Se quema! (sobre la carne) | la primera carne llega a Pasado |
| 17 | 3 strikes y cerrás (5 s, sobre el HUD) | el primer strike |
| 18 | Cerrado: atendé a los que quedan (5 s) | el primer cierre del local |

Con joystick los íconos cambian solos (LB, RB, Y…): pasar el mouse = seleccionar con el stick; arrastrar = mantener A.

### 3.3 Tienda (al terminar el día)

La tienda ya se explica sola (pestañas numeradas, línea de ayuda por pestaña, botón "Siguiente: X", panel "PARA ARRANCAR
MAÑANA"). Los carteles cubren solo lo que no se ve:

| # | Cartel | Zona | Aparece | Se va |
|---|---|---|---|---|
| T1 | Lo mínimo para mañana | panel de requisitos | al entrar | primera compra, o a los 6 s |
| T2 | 🖱 Comprar | botón Comprar de la primera tarjeta | al entrar | primera compra |
| T3 | Primero, lo mínimo | el Comprar desactivado | primera vez que una compra se bloquea por los mínimos | a los 5 s |
| T4 | Con 3, perdés la partida | línea de la racha de strikes | primera vez que la racha pasa de 0 | a los 5 s |

- Si la noche cerró por strikes, esperan a que se cierre el popup. Con derrota total no hay tienda ni carteles.
- El canvas de carteles (orden 15) queda arriba de la tienda (10) y abajo de los popups (20 y 30).
- Las tarjetas se crean al abrir la tienda: el `HintAnchor` va en el botón Comprar del prefab de tarjeta.

## 4. La primera noche

- `GameScene` arranca con carteles si están prendidos (así vienen en una instalación nueva). Se saca el diálogo de
  `TutorialOfferController`: ya no hay escena a la que mandar al jugador.
- En `init.cfg`, a través de `GameSettings`:
  - `TutorialHints=true|false`, con una fila nueva en Opciones: "Ayudas: Sí / No".
  - `TutorialDone=id1,id2,…`, fuera de `SettingsData`: es progreso, no una opción.
  - Volver a prender las ayudas borra la lista y el tutorial arranca de nuevo.

## 5. Fases

Cada fase deja el juego jugable y se puede mergear sola.

| Fase | Qué | Estado |
|---|---|---|
| 0. Arte | Tecla en blanco, botón de joystick en blanco (idealmente uno por botón), íconos de mouse, fondo y flecha del cartel. Reemplazar los `* PH.png` | ⏳ hay provisorios |
| 1. Señales | `TutorialSignals` + migrar los `Notify*`; el `TutorialManager` viejo escucha las señales | ✅ |
| 2. Cartel | Vista, canvas, íconos, QA | ✅ |
| 3. Director | `TutorialHintSO`, contextos, `HintAnchor` en `GrillView.prefab` y el HUD, carteles 1-10 | ⏳ |
| 4. Primera noche | Opciones, `init.cfg`, sacar el diálogo | ⏳ |
| 5. Primera vez | Carteles 11-18 y sus señales | ⏳ |
| 6. Tienda | T1-T4 en `EndScene` | ⏳ |
| 7. Limpieza | Borrar el tutorial viejo (abajo) y actualizar `ARQUITECTURA_PROYECTO.md` | ⏳ |

**Qué se borra en la fase 7:**
- Las escenas `TutorialScene` y `ShopTutorial`, también de la lista del build.
- `TutorialManager`, `TutorialStepSO`, `TutorialOfferController`.
- Los 28 assets de pasos, los 33 prefabs de `Prefabs/PanelesTutos` y `ClienteTutorial`.
- `FoodCatalogTutorial` y `ChorizoTutorial` (antes, buscar quién usa sus GUID).
- Las claves `tutorial.*` de `Tutorial.csv`.
- Los bloqueos: `Check*Allowed` (8 archivos), `IsCookingPaused` (`Meat.Cook`, `MeatInstance`),
  `IsCookingDeliveryExempt` y `TryExitTutorial` (`GameManager`).
- La nota 17 de la arquitectura (mantener `TutorialScene` como clon a mano).

## 6. Cosas del proyecto a respetar

- **Canvas en pantalla, no en el mundo:** `GrillView` tiene escala `(0.81, 1, 1)` (nota 16).
- **No robar input:** sin `GraphicRaycaster`, sin `raycastTarget`, sin colliders (nota 24). No son `Selectable`.
- **Pausa:** ocultos con `GamePause.IsPaused`, animaciones con tiempo sin escalar (nota 18).
- **Por frame:** nada de `Find*` ni memoria nueva (nota 13); el director va a ~5 Hz + señales.
- **Textos:** claves `hint.*` en `Tutorial.csv` con todas las columnas (nota 37).
- **Enums serializados** (`TutorialSignal`, `HintInput`, `HintPlacement`): valores nuevos siempre al final.

## 7. Checklist manual (fases 1 y 2)

- [ ] `TutorialScene` de punta a punta: cada paso de acción avanza igual que antes.
- [ ] `GameScene`, en Play: seleccionar `TutorialHints` → ⋮ → *QA/Mostrar Q y E*. Aparecen debajo de cada pestaña.
- [ ] Apretar Q (o LB, o click en la pestaña): el cartel de la Q se va con un pop. Lo mismo con E.
- [ ] Con un joystick real (Xbox y PlayStation): los íconos pasan a LB/RB o L1/R1 sin recargar.
- [ ] Opciones → idioma: el texto cambia en vivo.
- [ ] Pausa (Esc): los carteles desaparecen y vuelven al reanudar.
- [ ] Hacer click, arrastrar o pasar el mouse "a través" de un cartel: el juego responde como si no estuviera.
- [ ] Probar en 16:10 y en 4:3: los carteles no se salen de la pantalla.
