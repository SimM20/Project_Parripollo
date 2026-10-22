# Simulación económica — despues

Generado 2026-10-15 22:13. Semilla 40. 10000 noches aisladas por perfil y escenario; 2000 runs de 15 noches por perfil.

Datos leídos: 21 clientes la noche 1 (+4/noche, tope 63), 3 lugares, jornada 300s, paciencia base 95s, dinero inicial $1000.

Cortes: Chorizo costo $65 / plato $140 / sándwich $150 (peso 5) · Tira de asado costo $118 / plato $230 / sándwich $0 (peso 4) · Vacío costo $145 / plato $270 / sándwich $270 (peso 2, noche 2+)

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
| Costo carne consumida $ | 981,2 | 1326,2 | 1516,3 | 1648,4 |
| Costo carbón consumido $ | 292,3 | 227,5 | 199,3 | 173,8 |
| Beneficio operativo $ | -24,7 | 917,1 | 1561,5 | 2069,5 |
| Propinas / ventas | 5,6% | 9,4% | 13,2% | 16,7% |
| Propinas / beneficio operativo | beneficio ≤ 0 | 23,1% | 24,4% | 26,9% |
| Margen operativo / ingresos | -2,0% | 37,1% | 47,6% | 53,2% |
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
| Costo carne consumida $ | 1026,7 | 1516,3 | 1801,2 | 2062,2 |
| Costo carbón consumido $ | 290,3 | 242,7 | 214,0 | 186,7 |
| Beneficio operativo $ | -261,9 | 809,0 | 1675,2 | 2447,5 |
| Propinas / ventas | 5,3% | 8,9% | 12,7% | 16,2% |
| Propinas / beneficio operativo | beneficio ≤ 0 | 26,0% | 24,8% | 26,8% |
| Margen operativo / ingresos | -24,8% | 31,5% | 45,4% | 52,1% |
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
| Principiante | 100,0% | 18,8% | 81,3% | 3,9 |
| Aprendiz | 40,6% | 0,5% | 40,2% | 3,6 |
| Promedio | 10,5% | 0,0% | 10,5% | 10,1 |
| Bueno | 1,6% | 0,0% | 1,6% | 10,4 |
| Experto | 0,1% | 0,0% | 0,1% | 9,0 |

### Dinero al cierre (tras compras) $ por noche (media de runs vivas)

| Noche | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|---:|
| 1 | 200 | 196 | 914 | 1288 | 1333 |
| 2 | 183 | 256 | 1230 | 1368 | 1532 |
| 3 | 142 | 333 | 1287 | 1629 | 1968 |
| 4 | 158 | 625 | 1425 | 1852 | 2664 |
| 5 | 135 | 1004 | 1547 | 2430 | 2952 |
| 7 | 137 | 1378 | 1925 | 3331 | 5406 |
| 9 | 95 | 1604 | 2428 | 5181 | 13221 |
| 11 | 79 | 1989 | 2969 | 11051 | 23670 |
| 13 | - | 2465 | 3344 | 17659 | 34403 |
| 15 | - | 2814 | 4136 | 24187 | 45121 |

### Ventas $ por noche (media de runs vivas)

| Noche | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|---:|
| 1 | 830 | 833 | 1999 | 2364 | 2490 |
| 2 | 949 | 1129 | 2196 | 2994 | 3570 |
| 3 | 961 | 1307 | 2283 | 3097 | 3738 |
| 4 | 974 | 1904 | 2268 | 3190 | 3889 |
| 5 | 982 | 2082 | 2289 | 3225 | 4149 |
| 7 | 1001 | 2351 | 2405 | 4085 | 5695 |
| 9 | 1108 | 2389 | 2539 | 5128 | 7378 |
| 11 | 1240 | 2488 | 2782 | 5581 | 7876 |
| 13 | - | 2566 | 2922 | 5573 | 7855 |
| 15 | - | 2795 | 2974 | 5548 | 7849 |

### Propinas $ por noche (media de runs vivas)

| Noche | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|---:|
| 1 | 47 | 47 | 170 | 272 | 361 |
| 2 | 48 | 64 | 189 | 355 | 531 |
| 3 | 48 | 78 | 205 | 441 | 704 |
| 4 | 49 | 131 | 229 | 485 | 753 |
| 5 | 49 | 162 | 245 | 504 | 811 |
| 7 | 51 | 225 | 268 | 624 | 1091 |
| 9 | 69 | 260 | 298 | 794 | 1468 |
| 11 | 55 | 290 | 322 | 869 | 1607 |
| 13 | - | 308 | 330 | 873 | 1618 |
| 15 | - | 330 | 322 | 873 | 1611 |

### Beneficio operativo $ por noche (media de runs vivas)

| Noche | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|---:|
| 1 | -269 | -263 | 738 | 1238 | 1486 |
| 2 | -265 | -115 | 742 | 1498 | 2083 |
| 3 | -248 | 28 | 790 | 1622 | 2335 |
| 4 | -255 | 430 | 781 | 1769 | 2560 |
| 5 | -231 | 591 | 832 | 1865 | 2758 |
| 7 | -267 | 807 | 1004 | 2385 | 3795 |
| 9 | -114 | 913 | 1136 | 3011 | 4993 |
| 11 | -25 | 1044 | 1229 | 3277 | 5384 |
| 13 | - | 1131 | 1257 | 3284 | 5384 |
| 15 | - | 1246 | 1229 | 3269 | 5374 |

### Propinas / beneficio por noche (media de runs vivas)

| Noche | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---:|---:|---:|---:|---:|---:|
| 1 | beneficio ≤ 0 | beneficio ≤ 0 | 23% | 22% | 24% |
| 2 | beneficio ≤ 0 | beneficio ≤ 0 | 25% | 24% | 25% |
| 3 | beneficio ≤ 0 | 273% | 26% | 27% | 30% |
| 4 | beneficio ≤ 0 | 30% | 29% | 27% | 29% |
| 5 | beneficio ≤ 0 | 27% | 29% | 27% | 29% |
| 7 | beneficio ≤ 0 | 28% | 27% | 26% | 29% |
| 9 | beneficio ≤ 0 | 29% | 26% | 26% | 29% |
| 11 | beneficio ≤ 0 | 28% | 26% | 27% | 30% |
| 13 | - | 27% | 26% | 27% | 30% |
| 15 | - | 26% | 26% | 27% | 30% |

### Noche media en que se compra cada mejora (runs que la compran)

| Mejora | Principiante | Aprendiz | Promedio | Bueno | Experto |
|---|---:|---:|---:|---:|---:|
| Carbon de Quebracho Nv1 | nunca | 9,3 (60%) | 5,6 (97%) | 3,4 (100%) | 3,0 (100%) |
| Frasco de propinas Nv1 | 3,7 (0%) | 5,7 (45%) | 2,3 (71%) | 1,8 (13%) | 1,0 (55%) |
| Frasco de propinas Nv2 | nunca | 6,8 (44%) | 3,1 (68%) | 2,0 (62%) | 2,0 (12%) |
| Frasco de propinas Nv3 | nunca | 7,8 (61%) | 4,1 (98%) | 2,7 (100%) | 2,1 (100%) |
| Mesa extra Nv1 | nunca | 13,5 (47%) | 10,0 (89%) | 5,7 (91%) | 4,6 (79%) |
| Mesa extra Nv2 | nunca | 14,4 (15%) | 12,3 (69%) | 7,2 (62%) | 5,3 (38%) |
| Mesa extra Nv3 | nunca | 14,8 (1%) | 13,9 (41%) | 8,7 (99%) | 7,1 (100%) |
| Radio Nv1 | nunca | 11,1 (59%) | 7,1 (96%) | 4,3 (100%) | 3,7 (100%) |

