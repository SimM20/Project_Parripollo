# EVENTO — rama `event/leaderboard`

> Rama **exclusiva para eventos** (stands, muestras, juntadas): cada persona juega una run corta a su nombre y su
> puntaje va a un ranking público que se ve en la web. **No se mergea a `development` ni a `main`**: cambia reglas del
> juego (la run dura 3 días) que solo tienen sentido acá.
>
> Creada el **2026-10-27** desde `development` (commit `c8de284`).

## Qué cambia respecto del juego normal

| | Juego normal | Rama del evento |
|---|---|---|
| Al tocar **Jugar** | Arranca la run | Pide el nombre (`¿CÓMO TE LLAMÁS?`) y después arranca |
| **Reintentar** (derrota) | Reinicia la run | Vuelve a pedir el nombre: la run siguiente suele ser de otra persona. Viene precargado con el último, así que si juega la misma persona alcanza con Enter |
| Duración de la run | Hasta perder | **3 días como máximo**. Se puede perder antes, igual que siempre (sin recursos o por racha de strikes) |
| HUD | `DÍA 2` | `DÍA 2/3` |
| Pantalla final | Plata final | Plata final **+ Total ganado (ranking)**. Al completar los 3 días el título es `¡SE TERMINÓ LA RUN!` |
| Al terminar la run | — | El puntaje se guarda local y se sube a Supabase |

El tope de 3 días está para que gane el que mejor juega y no el que más horas se queda insistiendo.

## El puntaje

**Total ganado** = toda la plata cobrada en la run (pagos + propinas), **sin restar** lo gastado en la tienda ni en
mejoras. No incluye el dinero inicial. Vive en `PlayerWallet.TotalEarned` y se suma en `PlayerWallet.Add`, que es el
único lugar por donde entra plata.

Ejemplo: arranca con $1000, cobra $2400, gasta $900, cobra $1850 → plata final **$4350**, total ganado **$4250**.

La plata del tutorial también cuenta, porque se cobra en la misma billetera.

## Cuándo se guarda una run

`EventRun.Finish()` cierra la run y la guarda **una sola vez**, en el primero de estos momentos:

- aparece la pantalla final (derrota o 3 días completos);
- se vuelve al menú a mitad de run (abandonar también cuenta, con lo que llevaba ganado);
- arranca la run siguiente;
- se cierra el juego (se guarda local y se sube al próximo arranque).

Ojo en el Editor: salir de Play con una run abierta también la guarda.

## Dónde queda guardado

1. **Local**: `event_leaderboard.json` en `Application.persistentDataPath`
   (en Windows: `%USERPROFILE%\AppData\LocalLow\Placeholder Studios\Ruta 40\`). Tiene **todas** las runs jugadas en esa
   máquina, subidas o no: es el respaldo si algo falla.
2. **Supabase**: tabla `event_leaderboard` del mismo proyecto que usa la encuesta de la web. Si no hay internet, la run
   queda pendiente y se reintenta cada 30 s mientras el juego esté abierto, y otra vez al abrirlo. Cada run lleva su
   propio id, así que un reintento nunca la duplica.

Antes de **cada evento**, borrá `event_leaderboard.json` en la máquina del stand si tiene runs de prueba pendientes:
si no, se suben al ranking.

## El ranking en la web

Repo `Parrilla40-Web`, rama `event/leaderboard`:

- `ranking-evento.html` → **https://parrilla40.com.ar/ranking-evento.html**. No está linkeada en el sitio ni en el
  sitemap y lleva `noindex`: solo se llega con el link. Se actualiza sola cada 20 s, así que se puede dejar abierta
  en una pantalla.
- Muestra la **mejor run de cada nombre** (sin distinguir mayúsculas) y cuántas runs jugó. Top 100.
- `supabase/event-leaderboard.sql` crea la tabla y la función de lectura. **Hay que correrlo una vez** en el SQL
  Editor de Supabase antes del primer evento.

La clave de Supabase del juego es la publicable (la misma de la web): solo permite insertar, no leer ni borrar. Alguien
con conocimientos podría cargar un puntaje falso; la base limita nombres a 20 caracteres y puntajes a 100 millones.
Para borrar runs (pruebas, nombres ofensivos) se hace desde el panel de Supabase → Table Editor → `event_leaderboard`.

Para arrancar un evento con el ranking limpio, vaciar la tabla en el SQL Editor:

```sql
delete from public.event_leaderboard;
```

## Archivos

| Archivo | Qué hace |
|---|---|
| `Scripts/Event/EventRun.cs` | Estado de la run del evento: nombre, `Begin`, `Finish` (idempotente), `MaxDays = 3`, limpieza del nombre |
| `Scripts/Event/EventLeaderboard.cs` | Archivo local + subida a Supabase con reintentos |
| `Scripts/Event/EventNamePrompt.cs` | Cartel del nombre. Se arma por código sobre su propio canvas, sin escenas ni prefabs |
| `PlayerWallet.cs` | `TotalEarned` |
| `MainMenuPanel.cs` | Jugar → pide el nombre |
| `Run/RunDefeatScreen.cs` | Final por 3 días (`RunDefeatReason.EventDaysLimit`), línea de total ganado, guarda la run, Reintentar pide el nombre. QA: menú contextual *Forzar final del evento* |
| `Run/RunEconomyEvaluator.cs` | `RunDefeatReason.EventDaysLimit` |
| `SceneManagementUtils.cs` | Volver al menú guarda la run antes de destruir la billetera |
| `HudManager.cs` | `DÍA x/3` |
| `Scenes/EndScene.unity` | `TotalEarnedText` debajo de la plata final; el panel creció 48 px para hacerle lugar |
| `Resources/Localization/*.csv` | Claves `event.*` (es / en) |

Para cambiar la cantidad de días: `EventRun.MaxDays`. Los textos lo toman solos.

## Si `development` avanza

Traer los cambios con un merge de `development` en esta rama. Los puntos que más chance tienen de chocar son
`RunDefeatScreen.cs`, `EndScene.unity` y las tablas de localización.
