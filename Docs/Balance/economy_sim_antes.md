# Simulación económica — antes

Generado 2026-10-15 21:00. Semilla 40. 10000 noches aisladas por perfil y escenario; 2000 runs de 15 noches por perfil.

Datos leídos: 21 clientes la noche 1 (+4/noche, tope 63), 3 lugares, jornada 300s, paciencia base 95s, dinero inicial $1000.

Cortes: Chorizo costo $100 / plato $100 / sándwich $100 (peso 5) · Tira de asado costo $100 / plato $100 / sándwich $0 (peso 4) · Vacío costo $130 / plato $120 / sándwich $120 (peso 2, noche 2+)

Mejoras: Carbon de Quebracho $700 x1 (CoalBurnTime) · Mesa extra $300 x3 (MaxSimultaneousCustomers) · Frasco de propinas $250 x3 (TipPercent) · Radio $800 x1 (CustomerPatience)

## Noche 1 aislada (sin mejoras, stock suficiente)

| Métrica | Principiante | Promedio | Bueno | Experto |
|---|---:|---:|---:|---:|
| Clientes llegados | 19,2 | 19,9 | 20,0 | 20,0 |
| Atendidos | 8,3 | 13,9 | 16,6 | 18,4 |
| Perdidos por paciencia | 1,4 | 0,1 | 0,0 | 0,0 |
| Perdidos sin lugar | 9,4 | 5,8 | 3,4 | 1,6 |
| Entregas crudas/quemadas | 0,8 | 0,7 | 0,3 | 0,1 |
| Entregas imperfectas | 4,8 | 5,7 | 3,9 | 2,0 |
| Cortes desperdiciados | 2,5 | 1,0 | 0,5 | 0,2 |
| Ventas $ | 654,8 | 1235,9 | 1585,2 | 1824,9 |
| Propinas $ | 39,3 | 107,3 | 166,2 | 212,3 |
| Costo carne consumida $ | 1109,0 | 1495,9 | 1713,1 | 1862,1 |
| Costo carbón consumido $ | 354,9 | 276,3 | 242,0 | 211,1 |
| Beneficio operativo $ | -769,9 | -429,0 | -203,7 | -36,0 |
| Propinas / ventas | 6,0% | 8,7% | 10,5% | 11,6% |
| Propinas / beneficio operativo | beneficio ≤ 0 | beneficio ≤ 0 | beneficio ≤ 0 | beneficio ≤ 0 |
| Margen operativo / ingresos | -110,9% | -31,9% | -11,6% | -1,8% |
| % entregas con propina | 57,8% | 81,1% | 92,9% | 98,5% |
| Propina media por cliente atendido $ | 4,7 | 7,7 | 10,0 | 11,5 |
| Propina máx. $ | 20 | 20 | 20 | 20 |
| Propina mín. (>0) $ | 5 | 5 | 5 | 5 |
| Noches cerradas por strikes | 43,7% | 4,8% | 0,5% | 0,0% |

## Noche 6 aislada (sin mejoras, stock suficiente)

| Métrica | Principiante | Promedio | Bueno | Experto |
|---|---:|---:|---:|---:|
| Clientes llegados | 35,3 | 38,4 | 38,9 | 39,0 |
| Atendidos | 6,8 | 13,6 | 17,4 | 20,6 |
| Perdidos por paciencia | 2,4 | 0,8 | 0,3 | 0,1 |
| Perdidos sin lugar | 26,1 | 24,0 | 21,2 | 18,4 |
| Entregas crudas/quemadas | 0,7 | 0,7 | 0,3 | 0,1 |
| Entregas imperfectas | 3,9 | 5,6 | 4,1 | 2,2 |
| Cortes desperdiciados | 3,1 | 1,3 | 0,6 | 0,2 |
| Ventas $ | 540,6 | 1242,3 | 1714,8 | 2109,2 |
| Propinas $ | 31,1 | 104,4 | 175,4 | 239,2 |
| Costo carne consumida $ | 1095,0 | 1617,2 | 1922,2 | 2199,7 |
| Costo carbón consumido $ | 352,5 | 294,7 | 259,9 | 226,7 |
| Beneficio operativo $ | -875,9 | -565,2 | -291,8 | -78,0 |
| Propinas / ventas | 5,8% | 8,4% | 10,2% | 11,3% |
| Propinas / beneficio operativo | beneficio ≤ 0 | beneficio ≤ 0 | beneficio ≤ 0 | beneficio ≤ 0 |
| Margen operativo / ingresos | -153,2% | -42,0% | -15,4% | -3,3% |
| % entregas con propina | 55,6% | 79,7% | 92,2% | 98,3% |
| Propina media por cliente atendido $ | 4,6 | 7,7 | 10,1 | 11,6 |
| Propina máx. $ | 24 | 24 | 24 | 24 |
| Propina mín. (>0) $ | 5 | 5 | 5 | 5 |
| Noches cerradas por strikes | 72,7% | 17,4% | 2,6% | 0,0% |

## Propinas por tipo de cliente y de pedido (noche 6)

| Cliente (propina media $ · % sobre lo cobrado) | Principiante | Promedio | Bueno | Experto |
|---|---:|---:|---:|---:|
| Apurado | 2,8 · 3,5% | 5,3 · 5,8% | 7,4 · 7,6% | 8,8 · 8,6% |
| Camionero | 3,9 · 4,9% | 6,6 · 7,2% | 8,4 · 8,5% | 9,6 · 9,3% |
| Normal | 3,3 · 4,1% | 6,2 · 6,8% | 8,1 · 8,2% | 9,2 · 9,0% |
| Turista | 7,8 · 9,7% | 12,5 · 13,7% | 16,3 · 16,5% | 18,9 · 18,4% |

| Pedido (propina media $ · % sobre lo cobrado) | Principiante | Promedio | Bueno | Experto |
|---|---:|---:|---:|---:|
| Chorizo | 5,0 · 6,3% | 8,0 · 9,0% | 10,2 · 10,7% | 11,6 · 11,7% |
| Chorizo + toppings | 4,0 · 5,1% | 7,3 · 8,2% | 9,8 · 10,3% | 11,5 · 11,6% |
| Sándwich de Chorizo | 4,9 · 6,3% | 8,0 · 9,0% | 10,3 · 10,7% | 11,6 · 11,7% |
| Tira de asado | 4,7 · 6,0% | 7,8 · 8,7% | 10,1 · 10,5% | 11,5 · 11,6% |
| Tira de asado + toppings | 3,9 · 5,0% | 7,0 · 7,9% | 9,6 · 10,0% | 11,3 · 11,4% |
| Vacío | 4,8 · 5,1% | 8,0 · 7,5% | 10,7 · 9,3% | 12,3 · 10,3% |
| Vacío + toppings | 3,8 · 4,2% | 7,0 · 6,7% | 10,0 · 8,8% | 12,0 · 10,1% |

## Progresión: runs completas (con compras y mejoras)

Estrategia de tienda igual para todos: repone la demanda esperada de la próxima noche con el colchón del perfil, el carbón necesario y compra la mejora más barata si después le queda reserva para reponer media noche.

| Perfil | Derrotas | por plata | por strikes | Noche media de derrota |
|---|---:|---:|---:|---:|
| Principiante | 100,0% | 98,5% | 1,6% | 2,1 |
| Promedio | 100,0% | 99,8% | 0,3% | 3,1 |
| Bueno | 100,0% | 100,0% | 0,0% | 4,1 |
| Experto | 90,5% | 90,5% | 0,0% | 7,3 |

### Dinero al cierre (tras compras) $ por noche (media de runs vivas)

| Noche | Principiante | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|
| 1 | 508 | 1143 | 1591 | 1611 |
| 2 | 65 | 459 | 1058 | 1470 |
| 3 | 47 | 74 | 405 | 1099 |
| 4 | - | 52 | 79 | 753 |
| 5 | - | - | 59 | 600 |
| 7 | - | - | - | 257 |
| 9 | - | - | - | 312 |
| 11 | - | - | - | 254 |
| 13 | - | - | - | 881 |
| 15 | - | - | - | 1477 |

### Ventas $ por noche (media de runs vivas)

| Noche | Principiante | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|
| 1 | 646 | 1240 | 1587 | 1824 |
| 2 | 521 | 1159 | 1549 | 1875 |
| 3 | 424 | 1156 | 1629 | 1968 |
| 4 | - | 1181 | 1654 | 2032 |
| 5 | - | - | 1668 | 2057 |
| 7 | - | - | - | 2148 |
| 9 | - | - | - | 2193 |
| 11 | - | - | - | 2250 |
| 13 | - | - | - | 2249 |
| 15 | - | - | - | 2253 |

### Propinas $ por noche (media de runs vivas)

| Noche | Principiante | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|
| 1 | 39 | 108 | 166 | 212 |
| 2 | 30 | 98 | 229 | 420 |
| 3 | 23 | 98 | 240 | 443 |
| 4 | - | 99 | 244 | 458 |
| 5 | - | - | 270 | 464 |
| 7 | - | - | - | 520 |
| 9 | - | - | - | 606 |
| 11 | - | - | - | 631 |
| 13 | - | - | - | 630 |
| 15 | - | - | - | 646 |

### Beneficio operativo $ por noche (media de runs vivas)

| Noche | Principiante | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|
| 1 | -781 | -431 | -204 | -37 |
| 2 | -869 | -537 | -226 | 117 |
| 3 | -882 | -582 | -218 | 123 |
| 4 | - | -584 | -236 | 139 |
| 5 | - | - | -197 | 150 |
| 7 | - | - | - | 203 |
| 9 | - | - | - | 283 |
| 11 | - | - | - | 299 |
| 13 | - | - | - | 302 |
| 15 | - | - | - | 306 |

### Propinas / beneficio por noche (media de runs vivas)

| Noche | Principiante | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|
| 1 | beneficio ≤ 0 | beneficio ≤ 0 | beneficio ≤ 0 | beneficio ≤ 0 |
| 2 | beneficio ≤ 0 | beneficio ≤ 0 | beneficio ≤ 0 | 359% |
| 3 | beneficio ≤ 0 | beneficio ≤ 0 | beneficio ≤ 0 | 361% |
| 4 | - | beneficio ≤ 0 | beneficio ≤ 0 | 331% |
| 5 | - | - | beneficio ≤ 0 | 309% |
| 7 | - | - | - | 257% |
| 9 | - | - | - | 215% |
| 11 | - | - | - | 211% |
| 13 | - | - | - | 208% |
| 15 | - | - | - | 211% |

### Noche media en que se compra cada mejora (runs que la compran)

| Mejora | Principiante | Promedio | Bueno | Experto |
|---|---:|---:|---:|---:|
| Frasco de propinas Nv1 | nunca | 1,0 (0%) | 1,0 (44%) | 1,0 (23%) |
| Frasco de propinas Nv2 | nunca | nunca | 1,0 (21%) | 1,0 (47%) |
| Frasco de propinas Nv3 | nunca | nunca | 1,0 (1%) | 1,0 (27%) |

