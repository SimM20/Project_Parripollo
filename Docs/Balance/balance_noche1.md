# Balanceo de la primera noche — Parrilla 40

Fecha: 2026-11-17. Datos completos de antes y después: [difficulty_baseline.md](difficulty_baseline.md) y [difficulty_despues.md](difficulty_despues.md) (400 runs × 6 noches por perfil, semilla 40).

## 1. Diagnóstico

Medido con `ResourceBalanceSimulator`, que copia las reglas reales: grilla de la GameScene, calor de brasas, cocción por caras, cobro con `CookingDeliveryEvaluator`, tienda y derrota con `RunEconomyEvaluator`.

- **El principiante pierde la noche 1 por carbón y por paciencia, no por la carne.** Cierra por strikes el 62,5% de las noches: se gasta los 22 carbones y en el 80% de las noches pierde pedidos por falta de calor. Los 18 cortes alcanzan, porque llega a usar unos 10.
- **La presión arranca de entrada.** Los clientes llegaban a los ~16, 32 y 48 s: a menos de un minuto ya había 3 esperando. Las pistas del tutorial no pausan el tiempo, así que el jugador lee mientras corre la paciencia. El 10% recibía un strike antes de los 2 minutos.
- **El primer pedido podía ser el más difícil:** una tira (2,5 veces más lenta que el chorizo) con 2 salsas y guarnición.
- **La transición castiga dos veces.** Tres noches seguidas cerradas por strikes terminan la run. El principiante perdía la run en la noche 3 en el 49,5% de los casos.
- **Promedio y eficiente no tenían problemas en la noche 1** (0,2-0,35 strikes de media).

## 2. Cambios implementados

| Dónde | Qué |
|---|---|
| `CustomerSystem.cs` | 3 parámetros nuevos en "Primeras noches": bonus de paciencia de la noche 1 que baja en línea recta durante N noches, y cantidad de pedidos simples al abrir la noche 1. Solo aplican con `DayClock` (jornada real). |
| `OrderSystem.cs` | `GenerateSimpleOrder()`: el corte desbloqueado que más rápido se cocina, sin toppings ni guarniciones (el pan sigue saliendo al azar). `GenerateOrder()` queda igual. |
| `GameScene` › CustomerSystem | Valores de los parámetros nuevos y curva de afluencia con mañana tranquila. |
| `GameScene` › CoolerSystem | Carbón inicial 22 → 24. |
| `ResourceBalanceSimulator.cs` (Editor) | Lee los parámetros nuevos. Agrega un modelo de aprendizaje en la noche 1, métricas de dificultad (primer strike, pico de espera, rechazados sin lugar), `DifficultyReport`, `SweepEarlyNights` y `SelfCheckEarlyNights`. |

## 3. Tabla comparativa

| Parámetro | Antes | Después | Justificación |
|---|---|---|---|
| Carbón inicial | 22 | 24 | Saca al principiante del "sin carbón a mitad de noche" sin regalar: igual gasta los 24, y el que derrocha sigue perdiendo ~1 pedido por calor. Promedio y eficiente cierran con ~8 (antes ~5). |
| Paciencia extra noche 1 (`firstNightPatienceBonus`) | — (no existía) | +25% | Es la palanca más fuerte contra los strikes por espera sin tocar la economía. |
| Noches del bonus (`patienceBonusNights`) | — | 2 (noche 2: +12,5%; noche 3: 0%) | Evita que la noche 2 sea un escalón. |
| Pedidos simples al abrir la noche 1 (`simpleOpeningOrdersFirstNight`) | — | 2 | El primer pedido es para aprender: chorizo, sin extras. Del tercero en adelante, pedidos normales. |
| Curva de afluencia al abrir (t=0 / t=0,1) | 1,50 / 1,60 | 0,80 / 1,10 | Mañana tranquila: el 1.er cliente llega a los ~30 s y el 3.º a los ~69 s (medido en Play Mode). Aplica a todas las noches, y no cambia cuánta gente viene en el día, solo cuándo. |
| Curva al cierre (t=0,93 / t=1) | 2,00 / 1,50 | 2,10 / 1,70 | El cierre queda como el tramo más exigente de la noche. |
| Resto de la curva | valores con decimales y tangentes manuales | mismos picos, redondeados y con tangentes suaves | Los valores originales están en el diff de `GameScene.unity` para revertir. |
| Sin cambios | 21 clientes (+4/noche), 3 lugares, paciencia base 95 s, 3 strikes, carne 10 chorizos + 8 tiras, toppings y guarniciones 40%, mínimos de carbón 10 + 1/día, dinero inicial $1000 | | |

## 4. Resultados de pruebas

Pruebas ejecutadas de verdad:

1. **Simulación Monte Carlo** (Unity Editor, datos leídos de la escena): 400 runs × 6 noches × 4 perfiles, antes y después, más barridos de 300-400 runs por variante (carbón 22/24/26/28/30, paciencia 0/20/25/30%, pedidos simples, curva, 18 clientes, mínimo de carbón 10/16/18).
2. **Play Mode real, noche 1**: paciencia ×1,25 (Camionero 226 s = 95 × 1,9 × 1,25); clientes a los ~30, 52 y 69 s; los dos primeros pidieron choripán sin extras y el tercero, tira con 2 salsas y guarnición; carbón 24.
3. **`SelfCheckEarlyNights` en Play Mode**: el pedido simple es siempre chorizo sin extras (200/200), y la paciencia por noche del juego coincide con la del simulador (N1 1,25 · N2 1,125 · N3 1 · N4 1).
4. **Test runner EditMode**: el proyecto no tiene tests (0 ejecutados).
5. **Consola**: sin errores después de compilar, entrar y salir de Play Mode.

Noche 1, antes → después (medias de 400 runs):

| Perfil | Atendidos | Strikes | Cierra por strikes | Strike antes de 2 min | 1.er strike (mediana) | Carbón usado / disponible | Ventas+propinas |
|---|---:|---:|---:|---:|---:|---:|---:|
| Principiante | 6,3 → 7,5 | 2,80 → 1,58 | 62,5% → 16,8% | 10,0% → 0,3% | 204 s → 382 s | 21,9/22 → 24,0/24 | $997 → $1144 |
| Promedio | 11,5 → 11,3 | 0,21 → 0,08 | 0% → 0% | 0,8% → 0,8% | 271 s → 282 s | 17,3 → 16,4 | $1898 → $1829 |
| Eficiente | 13,1 → 13,1 | 0,33 → 0,08 | 0,3% → 0% | 0% → 0,5% | 287 s → 370 s | 16,6 → 15,7 | $2518 → $2463 |

Transición y run:

| Perfil | Noche 2 cierra por strikes | Run termina en la noche 3 | Run viva tras 6 noches |
|---|---:|---:|---:|
| Principiante | 84,3% → 69,3% | 49,5% → 15,4% | 4,8% → 6,0% |
| Promedio | 0,5% → 0,3% | 0% → 0% | 97,0% → 99,5% |
| Eficiente | 1,5% → 0,3% | 0% → 0% | 98,0% → 96,5% |

Limitaciones: los perfiles y el modelo de aprendizaje (reacción, armado y respuesta ×2 al abrir para el principiante, ×1,4 para el promedio, decayendo con τ = 90 s) son supuestos. El principiante simulado no mejora entre noches. Los resultados de la noche 3 en adelante suponen que se compra una parada cada 2 noches. No hubo partidas humanas ni una noche completa jugada por un bot en el motor.

## 5. Curva de dificultad

- **Noche 1:** apertura tranquila, 2 pedidos simples, +25% de paciencia. El cierre es el tramo de más presión. El principiante todavía pierde 1 de cada 6 noches.
- **Noche 2:** +12,5% de paciencia, pedidos normales y ritmo 22% más rápido (25 clientes).
- **Noche 3 en adelante:** paciencia normal. **Sin comprar paradas, la dificultad queda plana** (~0,2-0,5 strikes para promedio y eficiente en las noches 1-8): los +4 clientes por noche no presionan más porque los 3 lugares ya están siempre llenos, y solo aumentan los rechazados sin lugar. La subida real llega con el Vacío (parada 2): de ~0,3 a ~1,6 strikes y ~18-21% de noches cerradas por strikes. Ahí los jugadores experimentados encuentran tensión.

## 6. Riesgos pendientes

1. **Noche 2 del principiante que no mejora**: 69% de cierres. Con jugadores reales que aprenden debería ser menos; hay que medirlo.
2. **Carbón en la tienda**: si el jugador solo compra el mínimo en verde (11 para la noche 2) gasta ~20-24 y se queda corto. No pude confirmar qué cartel de carbón ve el jugador en la tienda activa: `ShopRoot` trae una sugerencia por consumo promedio, pero el simulador supone que solo se ve el mínimo. Probar subir el mínimo a 16 no alcanzó (cierres de la noche 2: 68% → 64%).
3. **Escalón del Vacío**: S=630 en 2 celdas, contra la tira con S=600 en 4. Con el mismo carbón tarda cerca del doble que la tira. El simulador puede exagerarlo, porque pone 1 sola brasa debajo de los cortes de 2 celdas.
4. **Progresión plana sin paradas**: si se quiere presión creciente para quien no compra, haría falta otra palanca (por ejemplo, paciencia que baja un poco por noche), no más clientes.
5. **La curva de afluencia es global**: todas las noches arrancan más tranquilas y el pico de la cena es más fuerte. Promedio y eficiente atienden ~0,2-1 pedido menos por noche (≈ −$50-70).
6. **Noche 1 para expertos**: casi sin strikes (0,08). Su tensión ahí pasa por la propina y la racha. Antes también era baja (0,2-0,35).

## 7. Recomendaciones de playtest

Medir por jugador nuevo:

- Segundos hasta la primera entrega, y si el primer pedido (simple) se entiende sin ayuda.
- Strikes en los primeros 2 minutos (objetivo ≈ 0) y momento del primer strike.
- % de noches 1 cerradas por strikes (objetivo 10-25% en novatos) y causa de cada strike: paciencia, crudo/quemado o falta de insumos.
- Carbón al cierre de la noche 1, carbón comprado para la noche 2 frente al mínimo que muestra la tienda, y si leen la sugerencia de consumo.
- Cortes quemados por noche.
- Cierres por strikes en la noche 2 frente a la noche 1 (que no sea un salto).
- Si sienten que la noche "se pone brava" al final (pico de la cena) y no al principio.
- En qué noche compran Cafayate y cuántos strikes tienen en la primera noche con Vacío.
- Pregunta de cierre: "¿querés jugar la noche siguiente?".

Para repetir las mediciones (con la GameScene abierta, en el Editor):
`ResourceBalanceSimulator.DifficultyReport("etiqueta", 400, 6, 40)` y `ResourceBalanceSimulator.SelfCheckEarlyNights()` (en Play Mode).
