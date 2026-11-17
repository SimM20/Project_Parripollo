# Balanceo general: economía, cortes y ruta — Parrilla 40

Fecha: 2026-11-17. Sigue al [balanceo de la noche 1](balance_noche1.md). Tablas completas por noche: [economy_antes.md](economy_antes.md) y [economy_despues.md](economy_despues.md) (150 runs × 12 noches por perfil, semilla 40).

## Contexto auditado

- El único documento con economía era `ARQUITECTURA_PROYECTO.md`. Decía que los precios de la ruta estaban "estimados a mano, sin pasar por el simulador" y tenía los precios de las mejoras desactualizados frente a los assets ($500/$100/$100/$400 contra $1200/$2500/$700/$1500). Ya quedó corregido.
- El simulador suponía "una parada cada 2 noches" sin mirar la plata. Ahora compra la próxima parada cuando le alcanza para reponer y cumplir los mínimos (como avisa la tienda real), y respeta que la tienda obliga a cubrir primero los mínimos. Además se agregó el perfil **Aprende** (principiante que llega a promedio en 5 noches), más fiel a un jugador nuevo que el principiante que nunca mejora.

## Diagnóstico

| Problema | Evidencia |
|---|---|
| **El Vacío era una trampa.** Escala de calor 630 en solo 2 celdas: con sus 2 brasas tardaba **63 s** hasta Hecho, nunca llegaba a Pasado (las brasas se apagan antes) y con 1 brasa no se cocinaba. La tira, de escala parecida pero en 4 celdas, tarda 12-27 s | Al desbloquearlo, el promedio pasaba de ~0,5 a ~1,6 strikes por noche y **vendía menos** ($1854 → $1824 por noche). Desbloquear contenido empobrecía |
| **La Costillita era una copia de la tira** (misma escala, mismo tamaño, $10 más) | La parada 3 no traía nada nuevo para jugar |
| **La ruta era muy lenta** | Promedio: Cafayate en la noche 5 y **nunca** Malargüe en 12 noches. Eficiente: Malargüe en la 11, nunca Bariloche |
| **La mejora Propina era un pozo** ($700 por nivel para +10% sobre propinas que son ~8% de las ventas: ~+$15 por noche, se paga en ~47 noches) | Frenaba la compra de paradas |
| **El mínimo de carne crecía +2 por día** (27 cortes el día 12) frente a ~13 usados por noche | Inmovilizaba plata y bloqueaba runs al comprar paradas |
| **Sin presión creciente real.** Los +4 clientes por noche no suman: los 3 lugares ya están siempre llenos | Sin el Vacío roto, las noches 4-12 quedaban planas |

## Cambios implementados

| Asset / lugar | Parámetro | Antes | Después | Por qué |
|---|---|---:|---:|---|
| `Cuts/Vacio` | `timeHeatA` / `timeHeatB` (escala = 3 × A) | 210 / 180 (escala 630) | 130 / 115 (escala 390) | Hecho en 63 → **32 s** con sus 2 brasas; Pasado ahora alcanzable (77 s); 25 s de margen antes de quemar. Sigue siendo el corte "grande" de la parada 2 |
| `Cuts/Costillita de cerdo` | `timeHeatA` / `timeHeatB` | 200 / 150 (escala 600) | 250 / 190 (escala 750) | Pasa a ser el corte lento y rentable de la parada 3: 16 s con 4 brasas, 35 s con 2 |
| `Cuts/Costillita de cerdo` | compra / venta plato | $120 / $240 | $135 / $280 | Paga su cocción más lenta (margen $120 → $145) |
| `Locations/Parada 2 - Cafayate` | precio | $2000 | $1800 | Promedio llega en la noche 4 |
| `Locations/Parada 3 - Malargue` | precio | $4500 | $3000 | Promedio llega en la noche 10 |
| `Locations/Parada 4 - Bariloche` | precio | $8000 | $5000 | Eficiente llega en la noche 11 |
| `Upgrades/TipUpgrade` | precio / bonus por nivel | $700 / +10% | $400 / +25% | Se paga en ~10 noches en vez de ~47 |
| `Config/RunDefeatConfig` | `meatRequirementPerDay` | 2 | 1 | El mínimo para abrir acompaña al consumo real (~13 por noche el día 12) |
| `CustomerSystem.cs` + escena | `patienceLossPerNight` / `minPatienceFactor` (nuevos) | — | 1,5% por noche / piso 0,85 | Presión que crece noche a noche desde la noche 4 (la 3 todavía es neutra tras el bonus de aprendizaje); la Radio (+20%) la compensa |
| Sin cambios | Chorizo ($65 → $140/$150), Tira ($118 → $230), Vacío precios ($145 → $270), **bolsa de carbón $140 × 10**, Radio $1500, Carbón de quebracho $1200, Mesa extra $2500, dinero inicial $1000, mínimo de carbón 10 + 1/día | | | |

**Carbón: se queda en $140.** Probé $170: no cambia la supervivencia y retrasa la ruta (con el promedio, Malargüe pasa del 85% al 60% de las runs). El carbón ya es ~12% de lo que se vende; su tensión real es quedarse sin stock durante la noche, no el precio.

## Tiempos de cocción (brasas nuevas, sacando en el punto)

| Corte | Celdas | Brasas | Hecho antes → después | Pasado antes → después | Margen antes de quemar |
|---|---|---:|---:|---:|---:|
| Chorizo | 1×2 | 2 | 18 s | 35 s | 13 s |
| Tira | 2×2 | 4 / 2 | 12 s / 27 s | 23 s / 57 s | 8 s / 20 s |
| Vacío | 2×1 | 2 | **63 → 32 s** | **nunca → 77 s** | 74 → 25 s |
| Costillita | 2×2 | 4 / 2 | 12 → 16 s / 27 → 35 s | 23 → 30 s / 57 s → nunca* | 8 → 11 s / 20 → 28 s |

\* Con 2 brasas la costillita en Pasado pide recargar carbón: es la decisión que trae la parada 3.

## Resultados (simulador, mismas semillas)

| Perfil | Run viva tras 12 noches | Cafayate / Malargüe / Bariloche (mediana de noche, % de runs) | Strikes N1-3 / 4-6 / 7-9 / 10-12 | Cierres por strikes N4-12 | Ventas por noche N1 → N12 |
|---|---|---|---|---|---|
| Aprende — antes | 92,0% | N9 (91%) / — / — | 2,0 / 0,9 / 0,4 / 1,3 | 9,7% | $1147 → $1835 |
| Aprende — después | 90,7% | N6 (91%) / N12 (31%) / — | 2,1 / 1,2 / 1,0 / 1,3 | 12,5% | $1147 → $2109 |
| Promedio — antes | 96,0% | N5 / — / — | 0,1 / 0,5 / 1,5 / 1,7 | 16,0% | $1854 → $1824 |
| Promedio — después | 98,0% | N4 / N10 (86%) / — | 0,2 / 0,8 / 1,2 / 1,2 | 8,8% | $1854 → $2229 |
| Eficiente — antes | 94,0% | N2 / N11 (82%) / — | 0,7 / 1,6 / 1,5 / 1,1 | 15,2% | $2461 → $2614 |
| Eficiente — después | 100% | N1 / N5 / N11 (99%) | 0,4 / 0,9 / 0,7 / 0,3 | 3,4% | $2461 → $3606 |
| Principiante que nunca mejora | 0% antes y después | — | ~2,7 → ~4 | ~86% | — |

Lectura: la dificultad ahora **sube de forma pareja** (promedio 0,2 → 0,8 → 1,2) en vez de quedar plana y pegar un salto al comprar el Vacío. **Desbloquear hace ganar más**, la ruta se recorre dentro de las 12 noches y nadie queda bloqueado por plata. El que aprende recorre más ruta y gana más, aunque con algo más de presión media (1,2 strikes en N4-6). El principiante que nunca mejora sigue perdiendo, como debe.

Otras pruebas:
- **Play Mode:** noche 1 con paciencia ×1,25, Vacío con escala 390 cargado, carbón 24.
- **`SelfCheckEarlyNights`:** el juego y el simulador coinciden en la paciencia de las noches 1, 2, 3, 4, 6, 10, 15 y 20 (1,25 / 1,125 / 1 / 0,985 / 0,955 / 0,895 / 0,85 / 0,85).
- **Consola:** sin errores ni advertencias.
- **Tests:** el proyecto no tiene ninguno.

## Riesgos pendientes

1. **Presión tardía de los expertos baja** (eficiente 0,3 strikes en N10-12): con Bariloche y mucha plata la sostiene su habilidad. Si se busca más tensión, la palanca es `patienceLossPerNight` o un corte nuevo para Bariloche, que todavía no tiene ninguno.
2. **Aprende sufre un poco más en N4-6** (0,9 → 1,2 strikes), porque llega antes al Vacío y empieza la pérdida de paciencia. Medirlo en playtest.
3. **Supuestos del simulador:** la política de compra de paradas, las brasas que pone cada perfil y el aprendizaje en 5 noches son modelos. El calor, la cocción, el cobro, la tienda y los mínimos copian el código.
4. **Rotar el Vacío no cambia su tiempo** en la grilla actual (medido igual en 2×1 y 1×2).
5. **Mínimo de carbón** (10 + 1/día): con el promedio gastando ~17-20 por noche, el jugador que compra solo lo que marca la tienda se queda corto (ver el informe de la noche 1).

## Qué medir en el playtest

- Tiempo real de un Vacío desde que se apoya hasta entregarlo, y si piden más carbón para Pasado.
- Strikes en la primera noche con Vacío, y en la primera con Costillita, comparados con la noche anterior.
- En qué noche compran cada parada y si sienten que la plata alcanza o sobra.
- Si compran Propina y Radio, y en qué orden.
- Cierres por strikes desde la noche 6 (objetivo ~5-12% en jugadores medios).

Para repetir (GameScene abierta): `ResourceBalanceSimulator.EconomyReport("etiqueta", 150, 12, 40)`, `EconomySweep(...)`, `CookTimeTable()` y, en Play Mode, `SelfCheckEarlyNights()`.
