# Parrilla 40: balance de la economía y de las propinas

Simulaciones completas: [economy_sim_antes.md](economy_sim_antes.md) (economía original), [economy_sim_previo.md](economy_sim_previo.md) (primer rebalanceo) y [economy_sim_despues.md](economy_sim_despues.md) (balance actual, con el ajuste 2).
Para regenerarlas, abrí la GameScene y usá el menú **Parripollo > Balance > Run Economy Simulation**, o llamá a `EconomySimulator.RunAll("etiqueta")`.

## A. Diagnóstico inicial

### Cómo funcionaba la economía

| Parte | Dónde vive | Valor original |
|---|---|---|
| Dinero inicial | `PlayerWallet.startingMoney` (GameScene) | $1000 |
| Stock inicial | `CoolerSystem.initialStock` (GameScene) | 20 chorizos, 20 tiras, 30 carbones |
| Costo y precio de cada corte | `MeatCutSO.basePrice` / `sellPricePlate` / `sellPriceSandwich` | Chorizo 100/100/100 · Tira 100/100 · Vacío 130/120 |
| Carbón | `CoalSO` (CoalData) | $170 la bolsa de 10; cada brasa dura 100 s |
| Pan y toppings | `BreadSO.purchasePrice`, `ToppingSO.purchasePrice` | $100 c/u. El pan nunca se cobra y el stock de toppings nunca se consume |
| Cobro de la entrega | `GameManager.EvaluateDelivery` → `CookingDeliveryEvaluator.EvaluateCut/EvaluateExtras` | Desfase 0-1: 100%. Desfase ≥2: 50%. Crudo o quemado: $0 + strike |
| Propina | `CookingDeliveryEvaluator.EvaluateDeliveryFeedback` | Escalones fijos: turista 20%; 10% si paciencia ≥50%; 5% si no; 0% con desfase ≥2 |
| Clientes | `CustomerSystem` (GameScene) | 21 la noche 1, +4 por noche, tope 63. 3 lugares. 4 tipos con la misma probabilidad (pesos en 0). Paciencia base 95 s × tipo (Apurado 1.0, Normal 1.4, Turista 1.5, Camionero 1.9) |
| Jornada | `DayClock` | 300 s reales |
| Mejoras | `UpgradeSO` | Mesa extra $300 ×3 · Frasco de propinas $250 ×3 (+50% por nivel) · Radio $800 (+15% paciencia) · Quebracho $700 (brasa de 200 s) |
| Derrota | `RunEconomyEvaluator` + `StrikeSystem` | No poder comprar el mínimo de carne y carbón, o 3 noches seguidas cerradas por strikes |

### Problemas encontrados

1. **Toda venta perdía plata.** El chorizo y la tira costaban lo mismo que se cobraban, y el vacío se vendía por debajo de su costo ($130 → $120). Encima, cada pedido consume carbón. Ninguna habilidad compensaba eso: en la simulación, los perfiles principiante, promedio y bueno perdían el 100% de las runs, y el experto el 90,5%.
2. **Las propinas eran el único ingreso neto.** Para el experto llegaban a ser 2-3 veces el beneficio (un 200-360%). No eran un premio: eran lo único que mantenía viva la partida.
3. **La propina casi no medía la habilidad.** Eran escalones fijos con tope $20-24: un experto ganaba $11,5 por cliente y un principiante $4,7. La velocidad solo importaba al cruzar el 50% de paciencia, no se reconocía la complejidad ni la constancia, y el turista daba 20% sin importar cuánto esperó.
4. **El Frasco de propinas a +50% por nivel** (+150% a nivel 3) era, por lejos, la única mejora que valía la pena.
5. **Exploit en la regla de derrota:** cinco cortes de relleno (Chinchulín, Costillita, Matambre, Paty y Pechuga) estaban desbloqueados en la tienda con costo $0. Comprándolos se llegaba al mínimo de carne sin gastar nada.
6. **Fuera de este cambio (ver Riesgos):** el stock de toppings nunca se consume, el pan es gratis y la capacidad de atención está fijada por los 3 lugares.

## B. Comparación numérica

Las cifras son medias de 10.000 noches simuladas por perfil (noche 1, sin mejoras) y de 2.000 runs de 15 noches por perfil.

### Noche 1 aislada

| Métrica | Principiante antes | Principiante después | Promedio antes | Promedio después | Bueno antes | Bueno después | Experto antes | Experto después |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Ventas | $655 | $1.183 | $1.236 | $2.259 | $1.585 | $2.896 | $1.825 | $3.335 |
| Propinas | $39 | $66 | $107 | $212 | $166 | $381 | $212 | $557 |
| Costos operativos (carne + carbón) | $1.464 | $1.203 | $1.772 | $1.259 | $1.955 | $1.607 | $2.073 | $1.704 |
| Beneficio operativo | −$770 | +$46 | −$429 | +$1.012 | −$204 | +$1.670 | −$36 | +$2.187 |
| Propinas / ventas | 6,0% | 5,6% | 8,7% | 9,4% | 10,5% | 13,2% | 11,6% | 16,7% |
| Propinas / beneficio | — (beneficio ≤ 0) | 144% | — | 21% | — | 23% | — | 25% |
| Propina media por cliente | $4,7 | $7,9 | $7,7 | $15,2 | $10,0 | $23,0 | $11,5 | $30,2 |
| Entregas con propina | 58% | 67% | 81% | 83% | 93% | 93% | 98% | 99% |

### Runs de 15 noches

| Métrica | Principiante antes → después | Promedio antes → después | Bueno antes → después | Experto antes → después |
|---|---|---|---|---|
| Runs perdidas | 100% → 100% | 100% → 5,6% | 100% → 0,2% | 90,5% → 0% |
| …por plata / por strikes | 98,5% / 1,6% → 45% / 55% | 99,8% / 0,3% → 0% / 5,6% | 100% / 0% → 0% / 0,2% | 90,5% / 0% → 0% / 0% |
| Dinero tras la noche 5 | perdió → $172 | perdió → $2.501 | $59 → $3.326 | $600 → $3.554 |
| Dinero tras la noche 15 | perdió → perdió | perdió → $5.654 | perdió → $28.830 | $1.477 → $55.727 |
| Propinas / beneficio (noches 5-15) | — → negativo | — → 24-27% | — → 26-27% | 210-330% → 30% |
| Noche en que compra la 1ª Mesa extra | nunca → nunca | nunca → 12,1 | nunca → 5,4 | nunca → 3,8 |
| Noche en que tiene todas las mejoras | nunca → nunca | nunca → >15 (22% completa la Mesa 3) | nunca → 7,9 | nunca → 5,5 |

Perfil nuevo **Aprendiz**: empieza como principiante y llega al nivel promedio en 5 noches. Pierde solo el 4,8% de las runs por plata (y el 28,5% por strikes). Pasa de −$73 de beneficio en la noche 2 a +$500 en la noche 4. La economía deja salir del pozo.

## C. Cambios implementados

### Código

| Archivo | Cambio |
|---|---|
| `Assets/Scripts/Food/TipRulesSO.cs` (nuevo) + `Assets/Resources/TipRules.asset` | Todos los números de la propina en un solo lugar, editables en el inspector. |
| `Assets/Scripts/Food/CookingDeliveryEvaluator.cs` | Nueva fórmula de propina (abajo). Agregué `BuildTipMessage` (explica la propina) y borré `CalculateTip`, `tipEligible` y dos constantes que no se usaban. |
| `Assets/Scripts/Core/DayStats.cs` | Ventas, propinas, racha actual y mejor racha de la jornada. |
| `Assets/Scripts/Core/GameManager.cs` | Pasa la racha a la evaluación, registra cada cobro y muestra por qué la propina salió así (en `DeliveryFeedbackText`, el mismo canal que ya explicaba los extras). |
| `Assets/Scripts/Customers/CustomerSystem.cs` | Un cliente que se va enojado, o un plato crudo o quemado, corta la racha. |
| `Assets/Scripts/EndScreen.cs` + `EndScene.unity` | Cajas nuevas VENTAS y PROPINAS ("$420 (15% de lo cobrado)"), clonadas de la caja CLIENTES. |
| `Assets/Scripts/Customers/CustomerFeedbackSelfCheck.cs` | 44 validaciones: las de antes más las de la fórmula nueva, la racha, los totales del día y "ningún corte se vende por debajo de su costo". |
| `Assets/Scripts/Editor/EconomySimulator.cs` (nuevo) | Simulador Monte Carlo, detallado en D. |
| Localización (`Gameplay.csv`, `Shop.csv`, `Items.csv`) | Textos de los motivos de la propina, de la pantalla de fin y de las descripciones del Frasco y la Radio. |

### Fórmula de propina

```
propina = precio × (base del tipo × calidad × velocidad + complejidad + racha) × mejoras
```

| Parámetro | Valor | Por qué |
|---|---|---|
| Base: Normal / Camionero / Apurado / Turista | 10% / 8% / 12% / 18% | Se mantiene el 10% de referencia y el turista generoso. El camionero es paciente y deja menos; el apurado paga mejor si lo atendés rápido. |
| Calidad: desfase 0 / 1 / ≥2 | 100% / 40% / 0% | Un error chico cuesta más de la mitad de la propina; un error grande la anula. |
| Velocidad (según la paciencia que le queda al cliente) | ×0,35 con 0% de paciencia → ×1,2 con el 100% | Curva continua en vez de un escalón: cada segundo cuenta. |
| Complejidad | +2% del precio por topping, solo si el pedido llega perfecto | Premia los pedidos difíciles bien hechos. |
| Racha | +2% por cada entrega perfecta previa, hasta +8% | Recompensa la constancia y es la principal diferencia entre bueno y experto. |
| Frasco de propinas | +10% por nivel (antes +50%) | Que mejore la propina sin reemplazar la habilidad. |

Rango medido en la noche 6, sin mejoras: de $2 (desfase 1 y lento) a $82 (turista perfecto, con toppings y racha, sobre un vacío de $270). Con el Frasco completo, el máximo teórico es ~1,3× eso.

### Datos (ScriptableObjects)

| Asset | Antes | Después | Justificación |
|---|---|---|---|
| Chorizo: costo / plato / sándwich | 100 / 100 / 100 | 60 / 140 / 150 | El corte más rápido. Margen de ~57% para que el principiante pueda sostenerse. |
| Tira de asado: costo / plato | 100 / 100 | 110 / 230 | ~2× el tiempo de cocción del chorizo, ~1,6× el precio. |
| Vacío: costo / plato | 130 / 120 | 135 / 270 | El corte más lento (noche 2+); deja la propina más grande. |
| Carbón (bolsa de 10) | $170 | $140 | Bajaba sobre todo el costo del principiante, que gasta más brasas. |
| Mesa extra | $300 ×3 | $2.500 ×3 | Es la mejora más fuerte (+33% de capacidad por nivel). Con $300 se compraba la noche 1. |
| Frasco de propinas | $250, +50% | $700, +10% | Antes era la única compra con sentido. |
| Radio | $800, +15% | $1.500, +20% | Más paciencia significa menos strikes y más propina por velocidad. El jugador promedio la compra en la noche ~9. |
| Carbón de Quebracho | $700 | $1.200 | Ahorra ~$90-145 de carbón por noche; recupera la inversión en 8-13 noches. Es una decisión, no un automático. |
| Chinchulín, Costillita, Matambre, Paty, Pechuga | Desbloqueados, $0 | `isUnlocked = false` | Cierra el exploit del mínimo de carne. Hay que reactivarlos con precio cuando tengan contenido real. |

## D. Simulación

`EconomySimulator` lee de la GameScene abierta y de los ScriptableObjects los cortes y sus pesos, los tipos de cliente, la paciencia, la curva de afluencia, los lugares, el stock inicial, la regla de derrota y las mejoras. Cobra cada entrega con las funciones reales: `EvaluateCut`, `EvaluateExtras`, `ApplyReducedPrice`, `EvaluateDeliveryFeedback` y `RunEconomyEvaluator.Evaluate`.

Lo que la escena no puede ejecutar por sí sola (las manos del jugador) se modela con **perfiles de habilidad**: reacción, pedidos en paralelo, calor por celda, precisión del punto, errores de topping, cortes quemados en la parrilla, reaprovechamiento de sobrantes, carbón encendido y colchón de compra.

El tiempo de cocción sale del calor real de cada corte: `GetStateBandHeat × punto pedido ÷ (calor × celdas que ocupa)`. Respeta los 3 lugares, los 4 s de feedback que ocupan el lugar, la paciencia, los strikes, el cierre por strikes, el stock disponible y el cambio por faltante.

**Efecto de las propinas:** el experto cobra 2,6× más propina que el promedio ($557 contra $212 en la noche 1). Eso explica un ~30% de su ventaja de beneficio; el resto viene del volumen. Entre los mismos dos perfiles, en propina por cliente, la diferencia pasó de 1,5× a 2×.

**Riesgos detectados:**
- **Sin sumideros de plata a largo plazo.** El experto termina las mejoras en la noche ~5,5 y llega a $55k en la noche 15, porque el catálogo de mejoras es chico (8 niveles, $12.300 en total). No agregué gastos artificiales (pediste no hacerlo). Esto se va a resolver cuando exista más progresión (nuevos cortes, la ruta a Capital Federal) o cuando las mejoras suban de precio por nivel (hoy la tienda usa un precio fijo por nivel).
- **El principiante estático no se sostiene** (−$220 por noche desde la noche 2). Desperdicia más de lo que vende. El Aprendiz sí se recupera, así que lo tomo como castigo razonable para quien no mejora.
- **Los strikes dominan la derrota.** El 55% de las runs del principiante y el 28,5% de las del Aprendiz se pierden por 3 noches seguidas cerradas por strikes, no por plata. No es economía, pero conviene revisar `maxStrikes` y `maxConsecutiveStrikeNights`.
- **La capacidad está fijada por los lugares.** Desde la noche 3, más de la mitad de los clientes no encuentra lugar. Por eso la dificultad sube en clientes perdidos, no en ingresos, y la Mesa extra es la palanca clave.
- **Toppings y pan:** los toppings se compran pero nunca se descuentan y el pan no se cobra. Hoy comprar toppings es plata tirada. Lo dejé así porque arreglarlo cambia la mecánica de armado.

## E. Validación

- El proyecto compila sin errores.
- `CustomerFeedbackSelfCheck`: 44 validaciones exitosas y 0 fallidas, tanto en el editor como al entrar en Play Mode con la GameScene. Play Mode arrancó sin errores nuevos.
- Layout de la EndScene: lo verifiqué calculando posiciones; las 4 cajas entran sin pisar los botones. No pude sacar una captura de la UI overlay desde el editor.
- **Sin verificar:**
  - Una partida completa jugada a mano: la entrega real, los mensajes de propina en pantalla y la EndScene con datos reales.
  - Los perfiles de habilidad son estimaciones. Conviene calibrarlos con métricas de partidas reales (servidos, desfases, carbón por noche).
- La simulación "antes" no incluye al Aprendiz, porque se corrió antes de agregarlo, con los datos originales.

## Ajuste 2: menos stock inicial y un poco menos de margen

**Por qué.** Probando a mano, el beneficio por noche coincidió con el perfil Promedio/Bueno (noche 1: 10 atendidos, $1.730 en ventas y $88 de propinas; noche 2: 13 atendidos, $2.315 y $234). Aun así sobraba plata: el stock inicial gratis (20 chorizos y 20 tiras, ~$3.400 de mercadería) cubría las primeras 2-3 noches, así que casi todo lo cobrado iba directo a la billetera.

**Cambios.**

| Valor | Antes | Ahora |
|---|---:|---:|
| Stock inicial de chorizo / tira (GameScene, `CoolerSystem.initialStock`) | 20 / 20 | 8 / 6 |
| Costo de chorizo / tira / vacío | $60 / $110 / $135 | $65 / $118 / $145 |
| Carbón inicial (`CoolerSystem.initialStock`) | 30 | 15 |
| Precios de venta | sin cambios | sin cambios |

**También corregido.**
- El simulador ahora compra primero el carbón y después repone la carne que usó la noche anterior (nunca menos que el mínimo de la run). Antes reponía para todos los clientes que llegan, incluidos los que no consiguen lugar, y dejaba sin plata para el carbón. Por eso las tablas de arriba (sección B) sobreestiman las derrotas por plata.
- El self-check corre las 4 validaciones de margen sobre el catálogo de la escena y avisa cuando no hay uno (por ejemplo, en el menú). Resultado: 44 de 44.

**Comparación, misma estrategia y semilla (2.000 runs de 15 noches por perfil):**

| Métrica | Principiante (previo → ahora) | Aprendiz | Promedio | Bueno | Experto |
|---|---|---|---|---|---|
| Runs perdidas por plata | 5,3% → 22,5% | 0% → 0% | 0% → 0% | 0% → 0% | 0% → 0% |
| Runs perdidas en total | 100% → 100% | 32,5% → 27,3% | 31,9% → 9,9% | 2,1% → 1,0% | 0,2% → 0,2% |
| Dinero tras la noche 2 | $1.069 → $430 | $1.086 → $524 | $1.495 → $1.241 | $1.622 → $1.361 | $2.363 → $1.718 |
| Dinero tras la noche 5 | $826 → $234 | $1.497 → $1.116 | $2.421 → $1.588 | $3.371 → $2.398 | $4.366 → $3.111 |
| Beneficio de la noche 5 | −$174 → −$253 | $763 → $596 | $1.163 → $868 | $2.445 → $1.894 | $3.585 → $2.853 |
| Noche de la 1ª Mesa extra | nunca → nunca | 9,2 → 13,0 | 5,9 → 9,6 | 3,7 → 5,5 | 3,0 → 4,3 |
| Noche en que tiene todas las mejoras | — | — | 11,0 → 13,8 | 6,6 → 8,6 | 5,4 → 6,9 |
| Propinas / beneficio (noche 5) | — | 28% → 28% | 23% → 29% | 24% → 27% | 28% → 29% |

**Lectura.**
- Las primeras noches ya no regalan plata: el jugador promedio llega a la noche 5 con ~$1.600 en vez de ~$2.400, y las mejoras grandes se postergan 2-4 noches según el perfil.
- Nadie que juegue a nivel promedio o mejor pierde por plata. El Aprendiz tampoco: una mala racha al principio es recuperable.
- Las propinas pesan un poco más en el beneficio (≈27-30%).
- Las derrotas por strikes del promedio bajan (31,9% → 9,9%) porque compra la Mesa extra más tarde, cuando ya puede atender a más gente.
- Lo que sigue pendiente: a partir de la noche ~9 el experto acumula plata sin tener en qué gastarla. Eso se resuelve con contenido, no con números.

**Carbón inicial 30 → 15.** Una noche gasta entre 12 y 20 carbones según qué tan bien se maneje la parrilla. Probé 10 y 15:

| Noche 1 (ventas / beneficio) | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---|---:|---:|---:|---:|---:|
| 30 carbones | $1.185 / −$8 | $1.183 / −$12 | $2.095 / $837 | $2.353 / $1.227 | $2.493 / $1.490 |
| 15 carbones (elegido) | $830 / −$269 | $833 / −$263 | $1.999 / $738 | $2.364 / $1.238 | $2.490 / $1.486 |
| 10 carbones | $522 / −$390 | $517 / −$388 | $1.314 / $225 | $1.949 / $794 | $2.468 / $1.452 |

Con 15, a quien ya maneja la parrilla le alcanza la noche 1 casi igual que con 30. Al principiante se le termina antes de cerrar, y eso le enseña a cuidar el carbón. Desde la noche 2 se compra en la tienda y todo se normaliza. Runs perdidas: Promedio 10,5%, Aprendiz 40,6% (casi todo por strikes).
