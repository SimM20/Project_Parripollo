# Parrilla 40: balance de stock de carne y carbón

Noviembre 2026. Complementa a [economy_balance.md](economy_balance.md) (precios, propinas y mejoras), que no se tocó.

Simulaciones: [stock_sim_antes.md](stock_sim_antes.md) (estado anterior), [stock_sim_despues.md](stock_sim_despues.md) (estado actual), [stock_sim_noche1_barrido.md](stock_sim_noche1_barrido.md) (stock inicial probado para la noche 1) y `stock_sim_var_minimo_*.md` (mínimos de carbón probados y descartados).
Para regenerarlas: abrí la GameScene y usá **Parripollo > Balance > Run Stock Simulation**, o llamá a `ResourceBalanceSimulator.RunAll("etiqueta")`.

## Resumen

- **El problema era la noche 1.** No hay tienda antes de la primera noche, así que el stock inicial es todo lo que el jugador tiene. Con 8 chorizos, 6 tiras y 15 carbones, un jugador promedio pierde pedidos por falta de insumos en el 87% de las primeras noches, casi siempre porque se queda sin carbón (97,5% de las noches).
- **Cambio aplicado:** stock inicial de 10 chorizos, 8 tiras y 22 carbones. No se tocaron precios, mínimos, mecánicas, HUD ni assets.
- **Resultado:** el jugador promedio termina la noche 1 sin perder pedidos en el 98% de los casos (antes 13%). El eficiente, en el 100% (antes 34%). El principiante sigue quedándose corto de carbón, pero por cómo lo maneja: con stock de sobra gasta 27,6 brasas por noche contra las 18 del promedio.
- **Lo que queda abierto** es la reposición. La tienda activa solo muestra el mínimo para pasar de día (11 carbones el día 2) y no sugiere cuánto comprar. Por eso el principiante entra en una espiral de carbón → strikes → derrota. Se resuelve con UI, que quedó fuera de este cambio (ver Riesgos).

## A. Auditoría: cómo funciona hoy

| Parte | Dónde vive | Valor |
|---|---|---|
| Stock inicial | `CoolerSystem.initialStock` (override en GameScene) | Chorizo 8, Tira 6, Carbón 15 (hay además una entrada de Chorizo con 0, inofensiva) |
| Dinero inicial | `PlayerWallet.startingMoney` | $1.000 |
| Clientes | `CustomerSystem` | 21 la noche 1, +4 por noche, tope 63. 3 lugares: si no hay lugar libre, el cliente no entra. Paciencia 95 s × tipo (Apurado 1,0 · Normal 1,4 · Turista 1,5 · Camionero 1,9), 4 tipos con la misma probabilidad |
| Jornada | `DayClock` | 300 s de puertas abiertas; la noche sigue hasta que se va el último cliente |
| Pedidos | `OrderSystem.GenerateOrder` | Corte por peso: Chorizo 5, Tira 4, Vacío 2 (noche 2+), Costillita 2 (noche 3+). Punto entre Jugoso y Pasado. Chorizo 50% sándwich. Al plato: 40% con 1-2 toppings, 40% con 1-2 guarniciones. **El pedido no mira el stock**: si falta el corte, "corte faltante" lo cambia por otro con stock y se pierde la propina |
| Cortes (costo / plato / sándwich, celdas, banda de calor) | `MeatCutSO` | Chorizo $65/$140/$150, 1×2, 40 · Tira $118/$230, 2×2, 100 · Vacío $145/$270, 2×1, 105 · Costillita $120/$240, 2×2, 100 |
| Cocción | `Meat.Cook` (prefab `Meat1`) | Cara apoyada: 1 + 0,07 × estado. Cara de arriba: 0,3 × (1 − 0,3 × estado). Se entrega con las dos caras en el punto |
| Carbón | `CoalSO`, `Coal`, `GridSlot`, `GrillSystem` | Bolsa de 10 a $140 ($14 la brasa). Se descuenta al apoyarla. Dura 100 s y da 6,5 × vida restante. Apiladas rinden 100% / 30,7% / 15,3% según su lugar, y **la ceniza ocupa lugar** hasta que se limpia (mecánica intencional, la enseña el cartel 11b "La ceniza baja el fuego"). A la carne le llega 35% (misma celda u ortogonal), 20% (diagonal) y 20% / 13% desde dos o tres filas más abajo, con tope de 10 por celda |
| Mínimos para pasar de día | `RunDefeatConfigSO` + `ShopNextButtonUI` | Carne 5 + 2 × (día − 1) cortes en total. Carbón 10 + 1 × (día − 1). El botón Siguiente no avanza hasta cumplirlos y la run se pierde si no se pueden pagar |
| Tope de carbón | `CoolerSystem.CoalStorageCap` | 40 |
| Reposición | Tienda en EndScene (`ShopCanvas`) | Muestra stock contra mínimo y lo pinta de verde al alcanzarlo. La sugerencia de carbón (`ShopSystem.GetSuggestedCoalUnits`) y el aviso de stock bajo solo existen en `ShopRoot` (tienda 2D), que está **desactivada** en EndScene |
| Desbloqueos | `CoalConsumptionTracker.cutUnlocks` | Vacío noche 2, Costillita noche 3. Arrancan con stock 0 y se compran en la tienda de la noche anterior |
| Persistencia | DDOL: `CoolerSystem`, `PlayerWallet`, `CoalConsumptionTracker` | El stock pasa de noche a noche y no vence. El carbón que quedó en la parrilla y la carne en la parrilla o el plato al cerrar se pierden (ya estaban descontados). No hay guardado de partida en disco, así que no hay saves que migrar |
| Mejoras de recursos | `UpgradeSO` | Carbón de Quebracho $1.200: brasa de 200 s |

### Consumo real medido (noche 1 con stock de sobra, 1.000 noches por perfil)

| Perfil | Atendidos | Carne usada (p90) | Brasas usadas (p90) | Brasas por corte cocinado |
|---|---:|---:|---:|---:|
| Principiante | 8,5 | 10,7 (12) | 27,6 (31) | 2,6 |
| Promedio | 12,0 | 12,9 (14) | 18,0 (21) | 1,4 |
| Eficiente | 13,1 | 13,6 (15) | 16,5 (19) | 1,2 |

Con 15 carbones iniciales se quedan sin carbón el 97,5% de los jugadores promedio y el 84,3% de los eficientes.

## B. Diagnóstico

1. **Escasez inevitable en la noche 1.** El stock inicial (14 cortes, 15 carbones) está por debajo del consumo de una noche promedio (13 cortes y 18 brasas, p90 de 14 y 21). Como no hay tienda antes, el jugador no puede hacer nada. Es exactamente la queja de los jugadores. Antes: promedio sin pérdidas en el 13,3% de las noches 1, con 1,74 pedidos perdidos por noche (1,64 por carbón).
2. **Espiral negativa del principiante.** Se queda sin carbón, los clientes se van, suma strikes, se cierra la noche y tres noches seguidas así terminan la run. Antes, el 83% de las runs de principiante morían por strikes y ninguna llegaba a la noche 10.
3. **La tienda no orienta la reposición.** La única cifra de carbón que ve el jugador es el mínimo (11 el día 2) y se pinta de verde, pero una noche consume 16-28. Lo mismo con la carne: el mínimo del día 2 es 7 cortes y se usan unos 12. La sugerencia que existía no está en la tienda activa y además usaba `estimatedCoalConsumption = 8`.
4. **El carbón se gasta aunque no haya carne.** Cada brasa arde 100 s desde que se apoya. Entre el 33% y el 45% del tiempo de brasa del jugador promedio no tiene carne al alcance; el eficiente baja a 23-28%. Es parte del desafío (ver D).
5. **Apilar y la ceniza.** Tres brasas nuevas en una celda cuestan 3 unidades y dan el 146% del calor de una. Una brasa nueva sobre ceniza rinde el 30,7%. Es intencional y el tutorial lo explica.
   > Al principio lo tomé como un bug y lo corregí en `GridSlot.CalculateInternalHeat`. Después encontré que `ARQUITECTURA_PROYECTO.md` y el cartel 11b lo describen como mecánica. **Lo revertí**: `GridSlot.cs` quedó idéntico al commit actual.
6. **Bugs y datos desactualizados encontrados (sin cambios):**
   - `CoalSO.heatPower` (3) no se usa: el calor está fijo en 6,5 dentro de `Coal.GetCurrentHeatOutput`.
   - Las brasas siguen quemándose cuando `TutorialManager.IsCookingPaused` congela la carne (solo afecta al tutorial viejo).
   - `ShopConfigSO.estimatedCoalConsumption = 8` y `GetSuggestedCoalUnits` no tienen en cuenta que, si el jugador se quedó sin carbón, su consumo real fue mayor que lo que puso.
   - Tope de 40 contra un mínimo que crece +1 por día: desde el día 32 el mínimo (41) supera el tope y la run termina sí o sí (`requirementAboveStorageCap`).

No encontré ningún bug que duplique o adelante el consumo: cada brasa se descuenta una vez al apoyarla (`StockPlacementMode` y `CoalTransferBuffer` devuelven la unidad si no entra) y cada corte una vez al sacarlo del stock.

## C. Valores originales y nuevos

| Parámetro | Dónde | Antes | Después | Por qué |
|---|---|---:|---:|---|
| Chorizo inicial | GameScene → `CoolerSystem.initialStock[0]` | 8 | 10 | Cubre el p95 del chorizo de la noche 1 del jugador promedio (6,8 pedidos de media) |
| Tira inicial | GameScene → `CoolerSystem.initialStock[2]` | 6 | 8 | Ídem para la tira (5,3 pedidos de media). Con 9/7 el promedio quedaba justo en 95,2% y el escenario adverso en 80,9% |
| Carbón inicial | GameScene → `CoolerSystem.initialStock[3]` | 15 | 22 | p90 del promedio = 21. Con 20 se quedaba sin carbón el 26% de las noches; con 22, el 4,5% y casi siempre al final; con 24 no mejora (98,0% → 98,4%) |

Valor del stock inicial regalado: de $1.438 a $1.902 (+$366 en carne y +$98 en carbón). El jugador promedio llega a la noche 5 con $1.154 (antes $1.077). La economía temprana sigue ajustada, como pedía el Ajuste 2 de `economy_balance.md`.

### Probado y descartado

| Alternativa | Resultado | Por qué no |
|---|---|---|
| Ceniza sin lugar de calor | Principiante noche 1: 18,9% → 49,4% sin pérdidas | Es una mecánica intencional que enseña el tutorial |
| Mínimo de carbón 18 + 1/día | Principiante: derrota por strikes 86% → 71%, runs bloqueadas 13,7% → 23,6% | Pasa el problema del carbón a la plata |
| Mínimo de carbón 21 + 0/día | Strikes 58%, bloqueadas 32,9% | Ídem |
| Mínimo de carbón 21 + 1/día | Strikes 47%, bloqueadas 38,5% | Ídem, y además adelanta al día 21 el corte por el tope de 40 |
| Cambiar la duración de la brasa | Sin simular como cambio final | El calor total de una brasa es proporcional a su duración: cambiarla acelera la cocción y le quita valor al Quebracho |
| Bajar el precio del carbón | — | El carbón es el 10-17% de lo que factura el promedio; no es lo que rompe la economía |

## D. Simulaciones antes y después

**Método.** `Assets/Scripts/Editor/ResourceBalanceSimulator.cs` lee la escena y los ScriptableObjects. Simula la parrilla celda por celda con las reglas de la sección A. Cobra con `CookingDeliveryEvaluator` y decide la derrota con `RunEconomyEvaluator`. Antes de cada corrida arma una celda real (`GridSlot` + `Coal`) y compara su calor con el del modelo; las cuatro pruebas dan idéntico.

Lo que no sale del juego son las manos del jugador, que se modelan con perfiles:

| Perfil | Cómo juega |
|---|---|
| Principiante | Reacción lenta. 2 cortes a la vez. Mantiene 4 brasas siempre prendidas y pone 2 por corte. Limpia la ceniza el 20% de las veces. Compra el carbón que la tienda marca como listo y repone la carne que usó |
| Promedio | 3 cortes a la vez. 1-2 brasas por corte. Limpia la ceniza el 60% de las veces. Compra 1,26 veces lo que gastó |
| Eficiente | 4 cortes a la vez. Reaprovecha las brasas vivas y siempre limpia la ceniza. Compra 1,15-1,2 veces lo que gastó |
| Promedio + pedidos adversos | El promedio, pero la mitad de los pedidos van al corte del que tiene menos stock en proporción a su demanda: la peor racha razonable |

**Calibración.** Con el stock del playtest documentado (20/20/30), el promedio atiende 12 clientes y factura $2.001 en la noche 1. A mano se midieron 10 clientes y $1.818: es un ~10% más.

1.000 runs de 10 noches por escenario, semillas fijas (base 40). "Sin pérdidas" = ningún cliente perdido por falta de carne o carbón; los cambios por faltante no cuentan como pérdida, porque la venta se hace.

### Resumen

| Escenario | Noches sin pérdidas (todas) | Noche 1 sin pérdidas | Runs bloqueadas por plata | Derrota por strikes | Runs vivas en la noche 10 |
|---|---|---|---|---|---|
| Principiante | 4,7% → 10,1% | 0,6% → 18,9% | 16,8% → 13,7% | 83,2% → 86,0% | 0,0% → 0,3% |
| Promedio | 82,7% → **91,9%** | 13,3% → **98,0%** | 0,1% → 0,0% | 6,4% → 4,3% | 93,5% → 95,7% |
| Eficiente | 90,0% → **96,2%** | 33,7% → **100%** | 0% → 0% | 3,6% → 2,4% | 96,4% → 97,6% |
| Promedio + pedidos adversos | 61,8% → 70,5% | 9,1% → 89,5% | 0,2% → 0,0% | 7,9% → 4,2% | 91,9% → 95,8% |

### Noche 1 (antes → después)

| Perfil | Atendidos | Perdidos por carne | Perdidos por carbón | Cambios por faltante | Brasas puestas | Ventas + propinas |
|---|---|---|---|---|---|---|
| Principiante | 5,0 → 7,3 | 0,07 → 0,02 | 3,17 → 1,35 | 0,07 → 0,01 | 15,0 → 21,9 | $801 → $1.178 |
| Promedio | 10,1 → 11,9 | 0,10 → 0,02 | 1,64 → 0,00 | 0,81 → 0,15 | 15,0 → 18,0 | $1.682 → $1.991 |
| Eficiente | 12,0 → 13,1 | 0,17 → 0,00 | 0,88 → 0,00 | 1,18 → 0,22 | 14,8 → 16,4 | $2.287 → $2.505 |

### Promedio, noche a noche (después)

| Noche | Sin pérdidas | Perdidos por carne / carbón | Cambios por faltante | Brasas | Carbón al empezar la siguiente (p10) | Carne al empezar la siguiente | Facturación | Compras carne + carbón | Plata al cierre |
|---:|---:|---|---:|---:|---|---:|---:|---:|---:|
| 1 | 98,0% | 0,02 / 0,00 | 0,15 | 18,0 | 27,7 (24) | 17,3 | $1.991 | $1.579 | $1.049 |
| 2 | 94,8% | 0,05 / 0,01 | 0,40 | 17,2 | 26,8 (21) | 16,9 | $1.665 | $1.305 | $1.033 |
| 3 | 86,1% | 0,11 / 0,06 | 0,84 | 18,8 | 28,8 (23) | 17,3 | $1.833 | $1.479 | $1.005 |
| 5 | 88,3% | 0,10 / 0,03 | 0,87 | 19,3 | 29,3 (23) | 17,8 | $1.916 | $1.468 | $1.154 |
| 10 | 94,6% | 0,06 / 0,00 | 0,59 | 15,5 | 26,2 (21) | 25,0 | $2.171 | $1.645 | $1.650 |

**Lectura.**
- La noche 1, la única donde la escasez es inevitable, pasa a cumplir el objetivo del 95%.
- Desde la noche 2, las pérdidas del promedio (0,06 a 0,17 clientes por noche) vienen de cómo repone. Compra en proporción a la demanda y le quedan unas 3 unidades de vacío y de costillita, que tienen 1,8 pedidos por noche. Cuando se agota uno y no usa "corte faltante", pierde al cliente.
- No es escasez inevitable: el eficiente, que repone con criterio, llega al 96-98% en las noches 2-5. Que se note la gestión es lo que se buscaba.

## E. Carbón

- **Por tiempo.** Una brasa entrega 325 de calor en 100 s (6,5 → 0), el 75% en los primeros 50 s. A la carne de arriba le llega el 35%: unos 114 por brasa.
- **Por cocción.** Medido con stock de sobra: 1,2 brasas por corte cocinado (eficiente), 1,4 (promedio) y 2,6 (principiante).
- **Por acción.** Cada brasa apoyada es una unidad. Apilar 3 da el 146% del calor de una. Una brasa sobre ceniza rinde el 30,7%, y sobre dos cenizas el 15,3%.
- **Tiempos muertos.** Del tiempo de brasa, sin carne al alcance: promedio 33-45%, eficiente 23-28%, principiante 18-25%. El principiante tiene menos tiempo muerto pero gasta más, porque cocina sobre una cama de 4 brasas y recarga encima de la ceniza.
- **Costo.** $14 por brasa. El promedio gasta $215-$330 por noche, el 10-17% de lo que factura.
- **Decisión.** Solo se ajustó el stock inicial. Duración, costo y consumo quedan como están, porque ahí está la ventaja del que maneja bien la parrilla: 16,4 brasas contra 18,0 y 27,6.

## F. Cada carne (promedio, después)

Formato: pedidos / usados / stock al empezar la noche siguiente.

| Corte | Noche 1 | Noche 2 | Noche 3 | Noche 5 | Observación |
|---|---|---|---|---|---|
| Chorizo | 6,8 / 7,2 / 7,8 | 5,1 / 5,4 / 6,1 | 4,4 / 4,7 / 6,3 | 4,6 / 4,9 / 6,6 | El corte de la noche 1. Barato ($65) y rápido |
| Tira de asado | 5,3 / 5,6 / 6,2 | 3,9 / 4,1 / 5,1 | 3,6 / 3,8 / 5,3 | 3,6 / 3,9 / 5,4 | Ocupa 4 celdas. Con más tiras de arranque (11/9) el promedio se queda sin carbón más seguido (6,7% contra 4,5%) |
| Vacío (noche 2+) | — | 2,0 / 2,0 / 2,8 | 1,8 / 1,8 / 2,9 | 1,8 / 1,8 / 2,9 | Arranca en 0; se compra en la tienda de la noche 1 |
| Costillita (noche 3+) | — | — | 1,7 / 1,6 / 2,9 | 1,8 / 1,7 / 2,9 | Ídem. Con vacío, es el corte que más se agota |

Usados > pedidos por los cortes quemados y los de clientes que se fueron (0,8-1,2 por noche para el promedio).

## G. Economía entre días

Ciclo: stock → cocción → ventas → ingresos → gastos → reposición → día siguiente.

- **Promedio.** Factura $1.665-$2.171 por noche y gasta $1.305-$1.645 en carne y carbón. Mantiene $1.000-$1.650 de caja y compra mejoras. Sostenible: 0% de runs bloqueadas por plata en 10 noches. La espiral de la noche 1 desaparece: ya no entra a la noche 2 con 1,7 clientes perdidos y strikes acumulados.
- **Eficiente.** Llega a $4.214 en la noche 10. La gestión rinde.
- **Principiante.** Sigue la tienda, que le marca 11 carbones como "listo". Compra 2 bolsas (20) y gasta unas 20 por noche quedándose corto (1,4-1,8 clientes perdidos por carbón).
  - Seguir a la noche siguiente sin bloqueo económico: 99,9% (noche 1), 98,5%, 97,8%, 92,8% (noche 4) y 89,2% (noche 5), sobre las runs que siguen vivas.
  - El objetivo del 90% se cumple las primeras 4 noches. Lo que termina sus runs son los strikes (86%), no la plata.
  - Subir el mínimo de carbón cambiaría strikes por bloqueos (sección C). El arreglo es de UI (sección I).

## H. Archivos modificados

| Archivo | Cambio |
|---|---|
| `Assets/Scenes/GameScene.unity` | Override de `CoolerSystem.initialStock`: 8 → 10, 6 → 8, 15 → 22 (3 valores; nada más en el diff) |
| `Assets/Scripts/Editor/ResourceBalanceSimulator.cs` (nuevo, solo editor) | Simulador de stock; menú Parripollo > Balance > Run Stock Simulation |
| `Docs/Balance/stock_balance.md` (este informe) y `stock_sim_*.md` | Informes |

`GridSlot.cs` se modificó y se revirtió (ver B.5). Sin cambios en HUD, arte, prefabs, ScriptableObjects, `TutorialScene` ni `ShopTutorial`.

## I. Pruebas

- Compilación sin errores después de cada cambio.
- Simulador contra el juego: 4 casos de pila (1 brasa, ceniza + brasa, 2 cenizas + brasa, 3 brasas) medidos con `GridSlot` real e iguales al modelo.
- Play Mode en GameScene: el cooler arranca con Chorizo 10, Tira 8, Carbón 22. Brasa apoyada en la parrilla real: calor de celda y de la carne de arriba como predice el modelo. Sin errores en consola.
- Simulaciones: antes y después (4 × 1.000 runs × 10 noches), barrido de la noche 1 (7 variantes × 4 perfiles × 1.000) y 3 variantes de mínimo de carbón. Las semillas son fijas: correr de nuevo da los mismos números.
- **Sin verificar:** una partida completa jugada a mano con los valores nuevos.

## J. Riesgos para el playtesting

1. **Los perfiles son modelos.** Están calibrados contra una sola partida a mano. Conviene medir en partidas reales los atendidos, las brasas por noche y los cortes quemados, y ajustar `ResourceBalanceSimulator.Beginner/Average/Efficient`.
2. **La tienda no sugiere cuánto comprar.** Es la causa principal de la espiral del principiante desde la noche 2. Recomendación (toca UI, quedó fuera de este cambio):
   - mostrar en `ShopCanvas` el consumo de carbón de la noche anterior (`CoalConsumptionTracker`) con un margen del 25%;
   - avisar el stock bajo por corte (`GetLowStockCuts` ya existe).
3. **Carbón del principiante.** Necesita unas 27 brasas por noche, contra las 22 iniciales y las 20 que suele comprar. Si en el playtest los nuevos se quejan de lo mismo, mirar primero si limpian la ceniza (cartel 11b) antes de subir más el stock.
4. **Vacío y costillita arrancan en 0.** El jugador tiene que acordarse de comprarlos la noche anterior; si no, sus pedidos van a "corte faltante" (sin propina).
5. **Tope de carbón 40 contra mínimo +1 por día:** derrota forzada desde el día 32.
6. **Stock regalado.** Son $464 más de stock inicial. Si las mejoras empiezan a comprarse demasiado temprano, bajar el carbón a 20 antes que la carne (el promedio pierde el 10% de las noches 1 contra el 2% con 22).
