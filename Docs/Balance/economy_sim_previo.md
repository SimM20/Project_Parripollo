# Simulación económica — previo

Generado 2026-10-15 21:48. Semilla 40. 10000 noches aisladas por perfil y escenario; 2000 runs de 15 noches por perfil.

Datos leídos: 21 clientes la noche 1 (+4/noche, tope 63), 3 lugares, jornada 300s, paciencia base 95s, dinero inicial $1000.

Cortes: Chorizo costo $60 / plato $140 / sándwich $150 (peso 5) · Tira de asado costo $110 / plato $230 / sándwich $0 (peso 4) · Vacío costo $135 / plato $270 / sándwich $270 (peso 2, noche 2+)

Mejoras: Carbon de Quebracho $1200 x1 (CoalBurnTime) · Mesa extra $2500 x3 (MaxSimultaneousCustomers) · Frasco de propinas $700 x3 (TipPercent) · Radio $1500 x1 (CustomerPatience)

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
| Ventas $ | 1183,1 | 2259,3 | 2895,7 | 3335,0 |
| Propinas $ | 65,7 | 211,5 | 381,4 | 556,7 |
| Costo carne consumida $ | 911,0 | 1231,3 | 1407,8 | 1530,5 |
| Costo carbón consumido $ | 292,3 | 227,5 | 199,3 | 173,8 |
| Beneficio operativo $ | 45,5 | 1011,9 | 1669,9 | 2187,4 |
| Propinas / ventas | 5,6% | 9,4% | 13,2% | 16,7% |
| Propinas / beneficio operativo | 144,4% | 20,9% | 22,8% | 25,5% |
| Margen operativo / ingresos | 3,6% | 41,0% | 51,0% | 56,2% |
| % entregas con propina | 66,9% | 82,5% | 93,0% | 98,5% |
| Propina media por cliente atendido $ | 7,9 | 15,2 | 23,0 | 30,2 |
| Propina máx. $ | 61 | 70 | 72 | 73 |
| Propina mín. (>0) $ | 2 | 2 | 2 | 3 |
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
| Ventas $ | 1002,5 | 2358,0 | 3275,3 | 4040,4 |
| Propinas $ | 52,7 | 210,0 | 415,1 | 656,0 |
| Costo carne consumida $ | 954,0 | 1408,9 | 1673,6 | 1916,2 |
| Costo carbón consumido $ | 290,3 | 242,7 | 214,0 | 186,7 |
| Beneficio operativo $ | -189,1 | 916,4 | 1802,8 | 2593,5 |
| Propinas / ventas | 5,3% | 8,9% | 12,7% | 16,2% |
| Propinas / beneficio operativo | beneficio ≤ 0 | 22,9% | 23,0% | 25,3% |
| Margen operativo / ingresos | -17,9% | 35,7% | 48,9% | 55,2% |
| % entregas con propina | 66,8% | 82,5% | 93,1% | 98,5% |
| Propina media por cliente atendido $ | 7,8 | 15,4 | 23,9 | 31,9 |
| Propina máx. $ | 66 | 77 | 80 | 82 |
| Propina mín. (>0) $ | 2 | 2 | 3 | 4 |
| Noches cerradas por strikes | 72,7% | 17,4% | 2,6% | 0,0% |

## Propinas por tipo de cliente y de pedido (noche 6)

| Cliente (propina media $ · % sobre lo cobrado) | Principiante | Promedio | Bueno | Experto |
|---|---:|---:|---:|---:|
| Apurado | 6,8 · 4,7% | 13,8 · 8,1% | 22,0 · 11,8% | 29,9 · 15,3% |
| Camionero | 6,1 · 4,0% | 12,2 · 6,9% | 19,2 · 10,1% | 26,1 · 13,2% |
| Normal | 6,6 · 4,4% | 13,4 · 7,7% | 21,1 · 11,1% | 28,5 · 14,5% |
| Turista | 11,5 · 7,8% | 22,4 · 12,9% | 33,2 · 17,5% | 43,1 · 21,9% |

| Pedido (propina media $ · % sobre lo cobrado) | Principiante | Promedio | Bueno | Experto |
|---|---:|---:|---:|---:|
| Chorizo | 6,1 · 5,5% | 11,6 · 9,3% | 17,1 · 12,8% | 22,4 · 16,2% |
| Chorizo + toppings | 5,9 · 5,4% | 12,3 · 9,9% | 19,2 · 14,4% | 25,5 · 18,4% |
| Sándwich de Chorizo | 6,6 · 5,6% | 12,4 · 9,3% | 18,5 · 12,9% | 24,1 · 16,2% |
| Tira de asado | 9,5 · 5,2% | 18,3 · 8,9% | 27,5 · 12,5% | 35,9 · 15,8% |
| Tira de asado + toppings | 9,5 · 5,3% | 19,3 · 9,5% | 30,7 · 13,9% | 41,0 · 18,0% |
| Vacío | 9,2 · 4,3% | 17,2 · 7,2% | 27,4 · 10,6% | 37,6 · 14,1% |
| Vacío + toppings | 9,3 · 4,5% | 18,8 · 7,9% | 31,1 · 12,1% | 43,6 · 16,3% |

## Progresión: runs completas (con compras y mejoras)

Estrategia de tienda igual para todos: primero compra el carbón necesario, después repone la carne que usó la noche anterior (con el colchón del perfil, nunca menos que el mínimo de la run) y compra la mejora más barata si después le queda reserva para reponer media noche (mesas extra solo si la noche anterior no perdió clientes por paciencia).

| Perfil | Derrotas | por plata | por strikes | Noche media de derrota |
|---|---:|---:|---:|---:|
| Principiante | 100,0% | 5,3% | 94,7% | 5,2 |
| Aprendiz | 32,5% | 0,0% | 32,5% | 5,7 |
| Promedio | 31,9% | 0,0% | 31,9% | 12,8 |
| Bueno | 2,1% | 0,0% | 2,1% | 10,6 |
| Experto | 0,2% | 0,0% | 0,2% | 10,7 |

### Dinero al cierre (tras compras) $ por noche (media de runs vivas)

| Noche | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|---:|
| 1 | 1079 | 1086 | 1233 | 1581 | 1650 |
| 2 | 1069 | 1086 | 1495 | 1622 | 2363 |
| 3 | 1034 | 1165 | 1611 | 2495 | 2538 |
| 4 | 934 | 1345 | 2096 | 2828 | 4262 |
| 5 | 826 | 1497 | 2421 | 3371 | 4366 |
| 7 | 589 | 2041 | 2922 | 5373 | 11508 |
| 9 | 377 | 2544 | 3437 | 11691 | 22018 |
| 11 | 378 | 3190 | 4620 | 18635 | 33061 |
| 13 | 475 | 3801 | 6370 | 25688 | 44382 |
| 15 | 1109 | 5111 | 8588 | 32730 | 55638 |

### Ventas $ por noche (media de runs vivas)

| Noche | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|---:|
| 1 | 1180 | 1176 | 2253 | 2893 | 3343 |
| 2 | 969 | 1133 | 2204 | 2961 | 3588 |
| 3 | 965 | 1300 | 2282 | 3138 | 3791 |
| 4 | 958 | 1900 | 2355 | 3393 | 4515 |
| 5 | 991 | 2089 | 2390 | 3931 | 5099 |
| 7 | 991 | 2425 | 2726 | 5058 | 6999 |
| 9 | 985 | 2590 | 2895 | 5425 | 7530 |
| 11 | 961 | 2847 | 3034 | 5591 | 7884 |
| 13 | 874 | 2960 | 3006 | 5574 | 7832 |
| 15 | 960 | 2976 | 2992 | 5595 | 7850 |

### Propinas $ por noche (media de runs vivas)

| Noche | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|---:|
| 1 | 67 | 64 | 210 | 383 | 557 |
| 2 | 56 | 70 | 235 | 477 | 725 |
| 3 | 58 | 94 | 253 | 493 | 740 |
| 4 | 61 | 168 | 266 | 523 | 826 |
| 5 | 63 | 211 | 272 | 598 | 989 |
| 7 | 67 | 274 | 301 | 778 | 1363 |
| 9 | 66 | 300 | 312 | 851 | 1544 |
| 11 | 65 | 326 | 320 | 870 | 1608 |
| 13 | 62 | 329 | 313 | 871 | 1608 |
| 15 | 93 | 317 | 310 | 878 | 1616 |

### Beneficio operativo $ por noche (media de runs vivas)

| Noche | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|---:|
| 1 | 49 | 41 | 1006 | 1672 | 2189 |
| 2 | -182 | -40 | 893 | 1714 | 2468 |
| 3 | -197 | 92 | 1024 | 1935 | 2647 |
| 4 | -207 | 541 | 1123 | 2096 | 3111 |
| 5 | -174 | 763 | 1163 | 2445 | 3585 |
| 7 | -164 | 1141 | 1310 | 3153 | 4957 |
| 9 | -157 | 1272 | 1363 | 3405 | 5420 |
| 11 | -147 | 1360 | 1383 | 3499 | 5669 |
| 13 | -262 | 1381 | 1333 | 3500 | 5641 |
| 15 | -84 | 1342 | 1301 | 3522 | 5656 |

### Propinas / beneficio por noche (media de runs vivas)

| Noche | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|---:|
| 1 | 136% | 156% | 21% | 23% | 25% |
| 2 | beneficio ≤ 0 | beneficio ≤ 0 | 26% | 28% | 29% |
| 3 | beneficio ≤ 0 | 102% | 25% | 25% | 28% |
| 4 | beneficio ≤ 0 | 31% | 24% | 25% | 27% |
| 5 | beneficio ≤ 0 | 28% | 23% | 24% | 28% |
| 7 | beneficio ≤ 0 | 24% | 23% | 25% | 27% |
| 9 | beneficio ≤ 0 | 24% | 23% | 25% | 28% |
| 11 | beneficio ≤ 0 | 24% | 23% | 25% | 28% |
| 13 | beneficio ≤ 0 | 24% | 24% | 25% | 29% |
| 15 | beneficio ≤ 0 | 24% | 24% | 25% | 29% |

### Noche media en que se compra cada mejora (runs que la compran)

| Mejora | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---|---:|---:|---:|---:|---:|
| Carbon de Quebracho Nv1 | 4,2 (3%) | 5,1 (76%) | 2,3 (100%) | 2,0 (100%) | 1,6 (100%) |
| Frasco de propinas Nv1 | 1,4 (80%) | 1,4 (79%) | 1,0 (7%) | 1,0 (0%) | nunca |
| Frasco de propinas Nv2 | 2,4 (64%) | 2,3 (68%) | 1,0 (64%) | 1,0 (3%) | 1,0 (0%) |
| Frasco de propinas Nv3 | 3,3 (36%) | 3,4 (82%) | 1,7 (100%) | 1,0 (100%) | 1,0 (100%) |
| Mesa extra Nv1 | nunca | 9,2 (68%) | 5,9 (88%) | 3,7 (89%) | 3,0 (95%) |
| Mesa extra Nv2 | nunca | 11,4 (53%) | 8,1 (70%) | 5,1 (56%) | 4,0 (30%) |
| Mesa extra Nv3 | nunca | 13,4 (45%) | 11,0 (83%) | 6,6 (100%) | 5,4 (100%) |
| Radio Nv1 | 6,8 (0%) | 6,7 (74%) | 3,5 (100%) | 2,3 (100%) | 2,0 (100%) |

