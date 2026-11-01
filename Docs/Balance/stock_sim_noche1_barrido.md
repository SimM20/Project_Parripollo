# Noche 1: barrido de stock inicial

1000 noches por fila, semilla base 40, reglas actuales del juego (la ceniza ocupa lugar de calor). Generado con `ResourceBalanceSimulator.SweepNight1Spec`.

| Variante | Perfil | Noches sin pedidos perdidos por insumos | Pedidos perdidos por insumos (media) | Cambios por faltante | Carbón puesto (media / p90) | Se quedó sin carbón | Carne usada (p90) | Quemados / abandonados | Atendidos | Ventas+propinas |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 8 chorizos, 6 tiras, 15 carbones | Principiante | 0,6% | 3,24 | 0,07 | 15,0 / 15 | 99,9% | 9,4 (11) | 0,5 / 2,9 | 5,0 | $801 |
| 8 chorizos, 6 tiras, 15 carbones | Promedio | 13,3% | 1,74 | 0,81 | 15,0 / 15 | 97,5% | 12,5 (13) | 0,6 / 0,9 | 10,1 | $1682 |
| 8 chorizos, 6 tiras, 15 carbones | Eficiente | 33,7% | 1,05 | 1,18 | 14,8 / 15 | 84,3% | 13,4 (14) | 0,2 / 0,2 | 12,0 | $2287 |
| 8 chorizos, 6 tiras, 15 carbones | Promedio + pedidos adversos | 9,1% | 2,03 | 2,56 | 14,9 / 15 | 95,5% | 12,0 (13) | 0,6 / 0,9 | 9,5 | $1630 |
| 9 chorizos, 7 tiras, 22 carbones | Principiante | 18,3% | 1,38 | 0,06 | 21,9 / 22 | 98,5% | 10,4 (12) | 0,7 / 1,7 | 7,3 | $1178 |
| 9 chorizos, 7 tiras, 22 carbones | Promedio | 95,2% | 0,05 | 0,43 | 17,9 / 20 | 0,9% | 12,8 (14) | 0,8 / 0,0 | 11,9 | $1978 |
| 9 chorizos, 7 tiras, 22 carbones | Eficiente | 99,6% | 0,00 | 0,61 | 16,4 / 19 | 0,0% | 13,6 (15) | 0,2 / 0,0 | 13,1 | $2493 |
| 9 chorizos, 7 tiras, 22 carbones | Promedio + pedidos adversos | 80,9% | 0,21 | 1,96 | 18,1 / 20 | 1,5% | 12,5 (14) | 0,7 / 0,0 | 11,6 | $1960 |
| 10 chorizos, 8 tiras, 20 carbones | Principiante | 6,4% | 2,01 | 0,01 | 20,0 / 20 | 99,1% | 10,3 (12) | 0,7 / 2,1 | 6,6 | $1073 |
| 10 chorizos, 8 tiras, 20 carbones | Promedio | 90,4% | 0,11 | 0,15 | 17,8 / 20 | 26,0% | 12,8 (14) | 0,8 / 0,1 | 11,8 | $1976 |
| 10 chorizos, 8 tiras, 20 carbones | Eficiente | 99,4% | 0,01 | 0,22 | 16,4 / 19 | 4,7% | 13,6 (15) | 0,2 / 0,0 | 13,0 | $2503 |
| 10 chorizos, 8 tiras, 20 carbones | Promedio + pedidos adversos | 80,9% | 0,21 | 1,01 | 17,0 / 20 | 24,8% | 12,8 (14) | 0,8 / 0,1 | 11,7 | $1908 |
| 10 chorizos, 8 tiras, 22 carbones | Principiante | 18,9% | 1,37 | 0,01 | 21,9 / 22 | 98,5% | 10,5 (12) | 0,7 / 1,7 | 7,3 | $1178 |
| 10 chorizos, 8 tiras, 22 carbones | Promedio | 98,0% | 0,02 | 0,15 | 18,0 / 21 | 4,5% | 12,9 (14) | 0,8 / 0,0 | 11,9 | $1991 |
| 10 chorizos, 8 tiras, 22 carbones | Eficiente | 100,0% | 0,00 | 0,22 | 16,4 / 19 | 0,2% | 13,6 (15) | 0,2 / 0,0 | 13,1 | $2505 |
| 10 chorizos, 8 tiras, 22 carbones | Promedio + pedidos adversos | 89,5% | 0,11 | 1,01 | 17,2 / 21 | 3,0% | 12,8 (14) | 0,8 / 0,0 | 11,8 | $1922 |
| 10 chorizos, 8 tiras, 24 carbones | Principiante | 38,5% | 0,84 | 0,01 | 23,8 / 24 | 93,0% | 10,5 (12) | 0,8 / 1,3 | 7,7 | $1253 |
| 10 chorizos, 8 tiras, 24 carbones | Promedio | 98,4% | 0,02 | 0,15 | 18,0 / 21 | 0,0% | 12,9 (14) | 0,8 / 0,0 | 11,9 | $1992 |
| 10 chorizos, 8 tiras, 24 carbones | Eficiente | 100,0% | 0,00 | 0,22 | 16,4 / 19 | 0,0% | 13,6 (15) | 0,2 / 0,0 | 13,1 | $2505 |
| 10 chorizos, 8 tiras, 24 carbones | Promedio + pedidos adversos | 89,9% | 0,11 | 1,01 | 17,2 / 21 | 0,2% | 12,8 (14) | 0,8 / 0,0 | 11,9 | $1923 |
| 20 chorizos, 20 tiras, 30 carbones | Principiante | 97,6% | 0,03 | 0,00 | 27,3 / 30 | 27,1% | 10,7 (12) | 0,9 / 0,7 | 8,4 | $1372 |
| 20 chorizos, 20 tiras, 30 carbones | Promedio | 100,0% | 0,00 | 0,00 | 18,0 / 21 | 0,0% | 12,9 (14) | 0,8 / 0,0 | 12,0 | $2001 |
| 20 chorizos, 20 tiras, 30 carbones | Eficiente | 100,0% | 0,00 | 0,00 | 16,5 / 19 | 0,0% | 13,6 (15) | 0,2 / 0,0 | 13,1 | $2512 |
| 20 chorizos, 20 tiras, 30 carbones | Promedio + pedidos adversos | 100,0% | 0,00 | 0,00 | 15,4 / 18 | 0,0% | 12,9 (14) | 0,7 / 0,0 | 12,1 | $1815 |
| 40 chorizos, 40 tiras, 40 carbones | Principiante | 100,0% | 0,00 | 0,00 | 27,6 / 31 | 0,0% | 10,7 (12) | 0,9 / 0,7 | 8,5 | $1376 |
| 40 chorizos, 40 tiras, 40 carbones | Promedio | 100,0% | 0,00 | 0,00 | 18,0 / 21 | 0,0% | 12,9 (14) | 0,8 / 0,0 | 12,0 | $2001 |
| 40 chorizos, 40 tiras, 40 carbones | Eficiente | 100,0% | 0,00 | 0,00 | 16,5 / 19 | 0,0% | 13,6 (15) | 0,2 / 0,0 | 13,1 | $2512 |
| 40 chorizos, 40 tiras, 40 carbones | Promedio + pedidos adversos | 100,0% | 0,00 | 0,00 | 15,4 / 18 | 0,0% | 12,9 (14) | 0,7 / 0,0 | 12,1 | $1815 |
