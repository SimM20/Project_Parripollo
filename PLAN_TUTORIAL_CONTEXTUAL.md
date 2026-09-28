# Plan técnico — Tutorial con carteles contextuales

> ## ESTADO: fases 1 a 5 IMPLEMENTADAS (2026-09-27, rama `tutorialREV`) — fases 0, 6 y 7 pendientes
>
> - **Fase 1 (señales):** hecha y probada. Los ~20 `TutorialManager.Notify*` pasaron a `TutorialSignals.Raise`. El
>   `TutorialManager` viejo se suscribe a las señales; se probó en Play que `TutorialScene` avanza igual del paso 7 al 17
>   (stock, carne, capas, carbón, vuelta, punto de cocción, plato y entrega).
> - **Fase 2 (cartel):** hecha y probada en `GameScene` a 1920×1080: ícono + texto, flecha, sin salirse de pantalla,
>   cambio en vivo teclado ↔ joystick e idioma, oculto en pausa, sin raycasts.
> - **Fase 3 (director):** hecha y probada en `GameScene` a 1920×1080, en español: los 11 carteles de la primera noche
>   aparecen solos, en orden, y se aprenden con la acción real (abrir el stock, soltar carne y carbón, capas, hover,
>   vuelta, plato, entrega). También probados: reiniciar y saltear lo aprendido, apagar las ayudas y la E cediendo su lugar.
> - **Fase 4 (opción y progreso):** hecha y probada con los botones reales del menú, en el menú principal y en la pausa.
>   Fila **Ayudas: Sí / No** (`options.hints`). `init.cfg` guarda `TutorialHints` y `TutorialDone`; al reabrir el juego se
>   leen (ayudas apagadas y lo aprendido siguen igual), con las ayudas apagadas no aparece ningún cartel, y al prenderlas
>   `TutorialDone` queda vacío y vuelven la Q y "Ver pedido". El diálogo viejo (`TutorialOffer` y su `Canvas`, instancias
>   de prefabs que ya no existían) salió de `GameScene`: la partida arranca directo.
> - **Fase 5 (la primera vez):** hecha y probada en `GameScene` a 1920×1080, armando cada situación por código y con las
>   teclas y el mouse reales inyectados por el Input System: cenizas + R, rotar un corte del stock + R, pan y salsa (con el
>   panel cerrado y abierto), corte sin stock (elegir al cliente con un click y M), rechazo + C y ↶, carne en Pasado,
>   primer strike y cierre del local. Los 12 aparecen donde tienen que aparecer y se aprenden con la acción o por tiempo.
>
> Desvíos respecto del plan, todos deliberados:
> - **Q y E van debajo de su pestaña**, no al costado: al costado, el de la izquierda le tapa la cara al primer cliente.
> - **Arte provisorio** (`Sprites/UI/Tutorial/* PH.png`) para la fase 0: fondo y flecha del cartel, tecla y botón en blanco,
>   íconos de mouse. `LetraQ` y `LetraE` se usan tal cual (con mipmaps activados); `LetraR` y `LetraSpace` no, porque vienen
>   en lienzos de 1536×1024 casi vacíos. Espacio, R y los botones del joystick salen como tecla en blanco con su nombre.
> - **Las señales nuevas se agregan con el cartel que las usa**: `ToppingsPanelOpened` (fase 2), `CustomerHovered`
>   (fase 3) y 10 en la fase 5. `CustomerSpawned` y `CoalBecameAsh` no hicieron falta: "llegó el cliente" y "hay ceniza"
>   se leen del estado, y leer el estado no se pierde nada si el cartel está ocupado cuando pasa.
> - **Sin componente `HintAnchor`**: las zonas se resuelven por código a partir de un id (`HintAnchorId`) y cada cartel se
>   afina con su offset. Así no hubo que tocar `GrillView.prefab` ni la escena. Si algún cartel necesita un punto puesto a
>   mano, se agrega el componente con un registro estático, como `BuildFoodDropZone`.
> - **Un cartel más, "Q · Sacá carbón" (4b)**: con la capa de carbón activa y el stock cerrado no había forma de llegar al carbón.
> - **"Ver pedido" solo sobre un cliente señalable**: con el stock abierto, el panel tapa a los primeros clientes y el
>   juego les apaga el collider; el cartel apunta al primero que se puede señalar o no aparece.
> - **`completeOnlyIf` es una lista**: `GrillLayerChanged` también sale al arrancar la escena (capa inicial), y "Volver a la
>   carne" se aprendía solo. Ahora pide capa de carne **y** carbón en la parrilla.
> - **Textos cortos de verdad:** "Carne al plato" y "Plato al cliente" (3 palabras) en vez de "Llevá la carne al plato".
> - **Opciones con 7 filas en la misma ventana:** en el menú principal la ventana toca el título y no puede crecer; las
>   filas pasaron a 64 px con 6 de separación (eran 68 + 8).
> - **El 13 son tres carteles**: "Agregá el pan" y "Serví la salsa" (panel abierto, sobre el pan o el frasco que pide el
>   pedido) y una E con prioridad alta cuando el plato necesita algo y el panel está cerrado (13c). Y **"Plato al cliente"
>   (10) espera a que el plato tenga el pan y las salsas**: antes mandaba a entregar un sándwich sin pan.
> - **Dos carteles más**: "Elegí al cliente" (14b), porque la M va al cliente elegido, que con mouse no se ve; y "↶
>   Deshacer" (15b) junto a "C · Vaciar plato". El 15 sale solo si ningún otro cliente espera ese corte: si alguno lo
>   espera, lo que corresponde es "Plato al cliente".
> - **Avisos que se aprenden solos**: `TutorialHintSO.completeAfterSeconds` (16: 4 s, 17 y 18: 5 s) y `TutorialSignal.None`.
>   Rotar (12) también se aprende a los 10 s a la vista: es opcional y no conviene insistir en cada arrastre.
> - **Nunca dos carteles sobre el mismo objeto** (regla nueva del director): gana el de mayor prioridad.
> - **Sin `¡ ! ¿ ?`**: no están en el atlas de la fuente del cartel. "Se quema", sin exclamación.

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
| `TutorialHintSO` + `TutorialHintSetSO` | Un asset por cartel: id, control (`HintPrompt`), clave de texto, zona + lado + offset, prerrequisitos, condiciones, señal (+ condiciones) que lo da por aprendido, prioridad, segundos a la vista para los avisos (`completeAfterSeconds`). El set los ordena | ✅ |
| `TutorialHintDirector` + `TutorialHintContext` | Uno por escena: decide qué carteles se ven (≈5 veces por segundo + en cada señal) a partir de una foto de la partida | ✅ en `GameScene` |
| `TutorialProgress` | Qué se aprendió y si las ayudas están prendidas. Guardado en `init.cfg` a través de `GameSettings` | ✅ |
| Zonas (`HintAnchorId`) | Resueltas por código: pestañas, botón de capa, parrilla, plato, la carne, el cliente que espera, la ceniza, la pieza que se arrastra, el pan y el frasco del panel, el cliente sin stock, el botón ↶, el HUD (reloj, strikes). Tienda: en su fase | ✅ las de la partida |

**Señales**: las de la partida ya están todas (fase 5: `AshesCleaned`, `PieceGrabbed`, `PieceRotated`, `BreadAdded`,
`ToppingAdded`, `MissingCutUsed`, `CustomerClicked`, `DeliveryRejected`, `PlateCleared`, `UndoUsed`). Strikes y cierre no
necesitaron señal: se leen de `StrikeSystem` y `DayClock`. En la tienda alcanza con `ShopSystem.OnPurchaseResult` y
`OnTabChanged`; solo T3 necesita un aviso nuevo desde `ShopItemCellUI.RefreshVisuals`.

## 3. Catálogo de carteles

### 3.1 Primera noche (lo básico)

| # | Cartel | Zona | Aparece | Se va |
|---|---|---|---|---|
| 1 | Q · Ver carnes | debajo de la pestaña izquierda | al arrancar, con el panel cerrado | hay carne en la parrilla (se oculta al abrir el panel) |
| 2 | E · Ver panes y toppings | debajo de la pestaña derecha | al arrancar | se abre el panel derecho |
| 3 | 🖱 Arrastrá a la parrilla | parrilla | panel abierto y parrilla vacía | hay carne en la parrilla |
| 4 | Espacio · Capa de carbón | botón de capa | hay carne y ningún carbón | hay carbón |
| 4b | Q · Sacá carbón | debajo de la pestaña izquierda | capa de carbón, sin carbón y con el stock cerrado | hay carbón |
| 5 | 🖱 Poné carbón abajo | la carne | capa de carbón activa y sin carbón | hay carbón |
| 6 | Espacio · Volver a la carne | botón de capa | ya hay carbón y sigue la capa de carbón | capa de carne |
| 7 | 🖱 Ver pedido (pasar el mouse) | el cliente | llega el primer cliente | pasar el mouse sobre el cliente |
| 8 | Click derecho · Dar vuelta | la carne | la cara de abajo dejó de estar cruda | primera vuelta |
| 9 | 🖱 Carne al plato | plato | carne a punto para un pedido (a un punto o menos, como `EvaluateCut`) | carne en el plato |
| 10 | 🖱 Plato al cliente | plato | el plato tiene lo que pidió un cliente que espera (corte, pan y salsas) | entrega aceptada |

Implementados como assets en `ScriptableObjects/TutorialHints/Partida/` (set `CartelesPartida`); el detalle de condiciones
y prioridades está en `ARQUITECTURA_PROYECTO.md`, 3.7 → *Carteles contextuales*.

### 3.2 La primera vez que pasa (cualquier noche)

| # | Cartel | Cuándo aparece | Se va |
|---|---|---|---|
| 11 | R · Limpiar cenizas (sobre la ceniza) | hay ceniza en la parrilla | se limpia |
| 12 | R · Rotar (sobre la pieza) | se arrastra un corte que se puede rotar (después del 3) | se rota, o a los 10 s a la vista |
| 13 | 🖱 Agregá el pan (sobre el pan pedido) | panel derecho abierto + el plato necesita pan | pan en el plato |
| 13b | 🖱 Serví la salsa (sobre el frasco) | panel derecho abierto + al plato le falta una salsa | salsa en el plato |
| 13c | E · Ver panes y toppings (pestaña derecha) | panel cerrado + al plato le falta pan o salsa | se abre el panel |
| 14 | M · Pedir otro corte (sobre el cliente) | el cliente elegido pide un corte sin stock | se usa la M |
| 14b | 🖱 Elegí al cliente | el que pide el corte sin stock no es el elegido | click en ese cliente |
| 15 | C · Vaciar plato | el cliente rechazó el plato y nadie más que espera quiere ese corte | se vacía el plato |
| 15b | 🖱 Deshacer (botón ↶) | lo mismo | se deshace |
| 16 | Se quema (sobre la carne) | la cara de abajo de una carne llega a Pasado | 4 s a la vista |
| 17 | 3 strikes y cerrás (sobre las X del HUD) | el primer strike | 5 s a la vista |
| 18 | Cerrado: atendé a los que quedan (sobre la hora) | el local cierra a la hora con clientes esperando | 5 s a la vista |

Implementados en `ScriptableObjects/TutorialHints/Partida/`, en el mismo set. Con joystick los íconos cambian solos (LB,
RB, Y…): pasar el mouse = seleccionar con el stick; arrastrar = mantener A.

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
| 3. Director | `TutorialHintSO`, contextos, zonas, director y carteles 1-10 (más el 4b) | ✅ |
| 4. Primera noche | Opciones, `init.cfg`, sacar el diálogo | ✅ |
| 5. Primera vez | Carteles 11-18 y sus señales | ✅ |
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
- **Enums serializados** (`TutorialSignal`, `HintInput`, `HintPlacement`, `HintAnchorId`, `HintCondition`): valores
  nuevos siempre al final.
- **Señales que también salen al arrancar** (`GrillLayerChanged`): un cartel que se aprenda con ellas necesita una
  condición que el arranque no cumpla.

## 7. Checklist manual (fases 1 a 5)

- [ ] `TutorialScene` de punta a punta: cada paso de acción avanza igual que antes.
- [ ] `GameScene`, jugando de verdad la primera noche: aparecen Q y E; al apretar Q se va la Q y aparece "Arrastrá a la
      parrilla"; si se cierra el stock sin poner carne, vuelve la Q. Seguir los carteles hasta entregar el primer plato.
- [ ] Hacer las cosas en otro orden (carbón antes que carne, cerrar el stock a mitad): no aparece nada que ya se hizo y
      no queda ningún cartel pidiendo algo imposible.
- [ ] Con el stock abierto, "Ver pedido" no apunta a un cliente tapado por el panel.
- [ ] Noche 2 sin cerrar el juego: no vuelve a aparecer nada de lo aprendido. Para repetir: `TutorialHints` → ⋮ →
      *QA/Reiniciar las ayudas*.
- [ ] Con un joystick real (Xbox y PlayStation): los íconos pasan a LB/RB o L1/R1 sin recargar.
- [ ] Opciones → idioma: el texto cambia en vivo.
- [ ] Pausa (Esc): los carteles desaparecen y vuelven al reanudar.
- [ ] Hacer click, arrastrar o pasar el mouse "a través" de un cartel: el juego responde como si no estuviera.
- [ ] Probar en 16:10 y en 4:3: los carteles no se salen de la pantalla.
- [ ] Opciones → Ayudas: No → Aplicar, en la pausa: al volver al juego no hay carteles. Sí → Aplicar: vuelven desde la Q.
- [ ] Aprender algunos, cerrar el juego (build) y volver a abrirlo: lo aprendido no vuelve a aparecer.
- [ ] Opciones con joystick: la fila Ayudas se alcanza con la cruceta y cambia con las flechas como las demás.
- [ ] Jugando de verdad (fase 5): dejar que un carbón se haga ceniza y limpiarla; arrastrar un chorizo del stock y
      rotarlo; un pedido en sándwich y uno con salsa, con el panel derecho cerrado y abierto; entregar el plato al cliente
      equivocado; dejar que se pase una carne; el primer strike; llegar a las 21:00 con gente esperando.
- [ ] Un pedido con salsa: "Plato al cliente" no aparece hasta servirla (antes mandaba a entregar igual).
- [ ] Corte sin stock con el mouse: si el que lo pide no es el elegido, primero "Elegí al cliente" y después la M.
- [ ] Con el panel derecho reordenado o con otra cantidad de columnas: "Agregá el pan" y "Serví la salsa" no tapan un
      pan o una salsa que haga falta.
