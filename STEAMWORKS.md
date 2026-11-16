# Steam en Parrilla 40

Integración con Steamworks: wrapper, arranque, verificación de propiedad, logros y subida de builds.
El código está en `Assets/Scripts/Platform/Steam/`, `Assets/Scripts/Achievements/` y `Assets/Scripts/Editor/Steam/`.

> **App ID: 5636520** (confirmado en el panel de Steamworks), cargado en
> `Assets/Scripts/Platform/Steam/SteamConfig.cs`. El editor genera `steam_appid.txt` con ese número
> al abrir Unity. El depot por defecto es el App ID + 1 (**5636521**).

---

## 1. Wrapper: Steamworks.NET

Está instalado como paquete de git en `Packages/manifest.json`, fijado a una versión:

```
"com.rlabrecque.steamworks.net": "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.165.0"
```

**Por qué Steamworks.NET y no Facepunch.Steamworks:**

| | Steamworks.NET | Facepunch.Steamworks |
|---|---|---|
| Mantenimiento | Sigue cada SDK de Valve (la 2025.165.0 es de octubre de 2026) | Actualizaciones más espaciadas |
| Instalación | Paquete UPM por git, con versión fija | DLLs sueltas que se copian a mano |
| API | 1 a 1 con la de Valve en C++: la documentación oficial de Steamworks sirve tal cual | Más cómoda en C# (async, propiedades), pero propia |
| Lo que usamos (Init, logros, stats, propiedad) | Completo | Completo |

Para un equipo que va a aprender Steamworks leyendo la documentación de Valve, lo práctico es que
el código se lea igual que esa documentación. Facepunch tiene una API más linda, pero va a la
zaga del SDK y no tiene paquete oficial para UPM.

**Requisitos para el equipo:** git instalado (Unity baja el paquete de GitHub la primera vez).
La primera vez que se abre el proyecto Steamworks.NET agrega el define `STEAMWORKS_NET` a
Player Settings (`ProjectSettings.asset`). Ese cambio se commitea una sola vez.

Para actualizar el paquete hay que cambiar el tag en el manifest. Conviene hacerlo solo cuando
Valve saque algo que necesitemos.

---

## 2. Arranque: `SteamBootstrap`

No hay que ponerlo en ninguna escena: se crea solo antes de la primera escena
(`RuntimeInitializeOnLoadMethod`), queda en `DontDestroyOnLoad` y hace esto:

1. Chequea que el wrapper y la DLL coincidan (`Packsize.Test`, `DllCheck.Test`).
2. **Solo en builds:** `SteamAPI.RestartAppIfNecessary(AppId)`. Si el .exe se abrió por fuera de
   Steam, Steam lo vuelve a lanzar como corresponde y esta copia se cierra.
3. `SteamAPI.InitEx`. Si falla:
   - **Editor y Development Build:** aviso en consola y el juego sigue. Los logros van a un backend
     local que solo los muestra en la consola.
   - **Release:** cartel de error ("necesita Steam abierto…") y se cierra.
4. Verificación de propiedad (sección 4).
5. `SteamAPI.RunCallbacks()` en cada `Update`.
6. Al salir: guarda los stats pendientes (evento `OnBeforeShutdown`) y llama a `SteamAPI.Shutdown()`.

**El juego anda sin Steam abierto en el editor.** Es lo que pasa siempre que Steam está cerrado.

### `steam_appid.txt`

Le dice a Steam qué juego es cuando el proceso no se abrió desde Steam (el editor, o un .exe que
se abre con doble clic). Lo maneja `SteamBuildGuard`:

| Dónde | Qué pasa |
|---|---|
| Raíz del proyecto (editor) | Se genera solo con el `AppId` de `SteamConfig` al abrir Unity. Está en `.gitignore`. Mientras el App ID esté en 0, Steamworks.NET deja `480` (Spacewar, el juego de prueba de Valve). |
| Development Build | Se copia junto al .exe, así se puede probar con doble clic y Steam abierto. |
| Build de release | Se borra si quedó. **Nunca se sube a Steam:** con ese archivo el juego se abre por fuera de Steam y `RestartAppIfNecessary` no hace nada. |

Además, una build de release con `AppId = 0` falla con un mensaje que explica qué hacer.

### Build sin Steam (muestras, profes, itch.io)

Agregar `DISABLESTEAMWORKS` en *Player Settings → Other Settings → Scripting Define Symbols*.
El bootstrap queda vacío, los logros van al backend local y no se pide App ID. Hay que sacarlo
antes de la build de Steam.

---

## 3. Seguridad: DRM y verificación de propiedad

### Qué es el DRM de Steam (SteamStub)

Es un "envoltorio" que Valve le pone al .exe desde el panel de Steamworks
(*App Admin → SteamPipe → DRM*, o `drm_wrap` en steamcmd). No se configura en Unity:
se sube el .exe, Steam devuelve uno envuelto y ese es el que va al depot. Al abrirlo,
el envoltorio verifica que Steam esté abierto y que la cuenta tenga el juego.

**Qué tan útil es para nosotros:** poco.
- Valve misma lo presenta como una protección liviana. Hay herramientas públicas que lo sacan en segundos.
- En Unity el .exe es apenas un lanzador: el juego está en `UnityPlayer.dll` y en
  `Parrilla 40_Data/Managed/*.dll`, que el envoltorio no toca.
- Hay que volver a envolver el .exe en cada build, y a veces dispara falsos positivos de antivirus.

### Verificación de propiedad con la API

- `SteamAPI.Init` **ya falla si la cuenta no tiene licencia** del App ID. Esa es la verificación principal.
- `SteamApps.BIsSubscribed()` devuelve si la cuenta tiene licencia. Incluye Family Sharing y fin
  de semana gratis, que se distinguen con `BIsSubscribedFromFamilySharing()` y
  `BIsSubscribedFromFreeWeekend()`. `GetAppOwner()` devuelve quién compró el juego cuando es prestado.
- Todo esto lo puede falsear un emulador de Steam, igual que el envoltorio.

### Recomendación para un indie (lo implementado)

1. **`RestartAppIfNecessary` + `InitEx` obligatorios en release** (en `SteamBootstrap`). Con eso
   el juego "necesita Steam", igual que con SteamStub, sin tocar el .exe.
2. **`BIsSubscribed` como segunda línea**: en release, sin licencia el juego se cierra. Los valores
   (suscripto, Family Sharing, fin de semana gratis) quedan en `SteamBootstrap.Ownership` y en el log.
3. **No castigar Family Sharing ni el fin de semana gratis**: son licencias válidas de Steam.
4. **SteamStub: no aplicarlo.** Si igual lo quieren, usen las opciones por defecto y prueben la
   build envuelta en una PC limpia antes de publicarla.
5. **No gastar tiempo en más anti-piratería.** Lo que hace que valga la pena comprar el juego son
   los logros, las actualizaciones y (más adelante) Steam Cloud.

---

## 4. Logros

### Cómo funciona

`AchievementService` (estático) escucha eventos que ya existían o que se agregaron en el embudo
correspondiente, y decide qué se desbloquea. Ningún sistema de la partida sabe que hay logros.

| Evento | De dónde sale |
|---|---|
| `GameManager.DeliveryCompleted` (**nuevo**) | `TryDeliverToCustomer`, al cobrar una entrega (con corte, propina, nivel de propina, racha, si era el cliente especial) |
| `GameManager.NightFinished` (**nuevo**) | `EndNight`, el único cierre de jornada (con strikes, racha de noches, ventas) |
| `TutorialSignals.Raised` | `CoalPlacedOnGrill`, `MeatStateChanged` (quemado), `ShopPurchased` |
| `SceneManagementUtils.OnSceneLoaded` | Manda los stats pendientes |
| `SteamBootstrap.OnBeforeShutdown` | Manda los stats pendientes al cerrar |

Destino: `SteamAchievementBackend` si Steam está inicializado, si no `LocalAchievementBackend`,
que solo los muestra en la consola. Los logros se mandan en el momento, para que el aviso de Steam
salga al toque. Los stats se juntan y se mandan al terminar la jornada, al cambiar de escena y al cerrar.

Nombres de API y umbrales: `AchievementIds.cs`. Textos en español e inglés:
`Assets/Resources/Localization/Achievements.csv` (claves `achievement.<API>.name` / `.desc`). Esos
textos los muestra Steam, no el juego: hay que cargarlos en el panel. El CSV es la fuente para copiarlos.

### Lista propuesta (20 logros)

| API | Nombre (es / en) | Cómo se gana | Stat | ¿Oculto? |
|---|---|---|---|---|
| `ACH_FIRST_FIRE` | Prendé el fuego / Light It Up | Primer carbón en la parrilla | | No |
| `ACH_FIRST_BURN` | Carbonizado / Charred | Quemar un corte | `STAT_CUTS_BURNED` | No |
| `ACH_FIRST_DELIVERY` | Primer cliente / First Customer | Primera entrega cobrada | | No |
| `ACH_PERFECT_STREAK_5` | En su punto / Cooked Just Right | 5 entregas perfectas seguidas | `STAT_BEST_STREAK` | No |
| `ACH_PERFECT_STREAK_15` | Maestro parrillero / Grill Master | 15 entregas perfectas seguidas | `STAT_BEST_STREAK` | No |
| `ACH_FULL_MENU` | Carta completa / Full Menu | Entregar todos los cortes de la ruta en una partida | | No |
| `ACH_DELIVERIES_100` | Cien platos / A Hundred Plates | 100 entregas en total | `STAT_DELIVERIES` (0–100) | No |
| `ACH_DELIVERIES_1000` | Leyenda de la ruta / Legend of the Road | 1000 entregas en total | `STAT_DELIVERIES` (0–1000) | No |
| `ACH_GREAT_TIP` | Propinón / Big Tipper | Una propina de nivel `Great` | | No |
| `ACH_TIPS_5000` | Pozo de propinas / Tip Jar | $5000 de propinas en total | `STAT_TIPS_TOTAL` (0–5000) | No |
| `ACH_FIRST_NIGHT` | Primera jornada / First Shift | Terminar una jornada (sin cortarla desde la pausa) | `STAT_NIGHTS_COMPLETED` | No |
| `ACH_CLEAN_NIGHT` | Sin quejas / No Complaints | Jornada completa, con ventas y 0 strikes | | No |
| `ACH_NIGHTS_7` | Una semana en la ruta / A Week on the Road | 7 jornadas en una misma partida | | No |
| `ACH_FIRST_STOP` | Arranca el viaje / Hitting the Road | Comprar la segunda parada (Cafayate) | | No |
| `ACH_ROUTE_END` | Fin de la ruta / End of the Road | Llegar a la última parada (Bariloche) | | No |
| `ACH_FIRST_UPGRADE` | A mejorar el puesto / Upgrading the Stand | Comprar una mejora | | No |
| `ACH_ALL_UPGRADES` | Puesto tuneado / Fully Loaded | Todas las mejoras al máximo | | No |
| `ACH_STRIKE_LIMIT` | Te clavaron el cartel / Shut Down | Una jornada cerrada por el límite de strikes | | Sí |
| `ACH_COMEBACK` | Remontada / Comeback | Estar a una noche de perder por strikes y cerrar bien | | Sí |
| `ACH_SPECIAL_CUSTOMER` | La mitad más uno / A Very Special Guest | Entregarle (y cobrarle) al cliente especial, el enano bostero | | Sí |

Stats (todos **INT**, *Set By: Client*): `STAT_DELIVERIES`, `STAT_TIPS_TOTAL`, `STAT_NIGHTS_COMPLETED`,
`STAT_CUTS_BURNED`, `STAT_BEST_STREAK`.

Para revisar con balance:
- **$5000 de propinas** son unas 150 propinas al precio actual de los cortes (100–270). Ajustar
  `AchievementThresholds.TipsTotal` y el máximo del stat en Steamworks al mismo tiempo.
- **"Puesto tuneado"** pide todas las mejoras del catálogo (`FoodCatalogSO`). Si alguna mejora no
  se puede comprar en la ruta, el logro queda imposible.
- **"Carta completa"** cuenta los cortes de las paradas de la ruta. El chorizo del tutorial no cuenta.

"La mitad más uno" depende de la suerte: el enano viene en el 30 % de las jornadas
(`specialSkinDayChance` en `CustomerSystem`). Lo marca `CustomerSystem` al spawnearlo
(`IsSpecialCustomer`), no por el sprite, y viaja en `DeliveryReport.SpecialCustomer`.

Ideas que quedaron afuera porque necesitan más ganchos: logros de sánguches y "una jornada sin
limpiar ceniza".

### Cargarlos en Steamworks

En partner.steamgames.com → *App Admin* → Parrilla 40 → **Stats & Achievements**:

1. **Stats** → *Add new stat*, uno por stat de arriba:
   - *API Name*: exacto (`STAT_DELIVERIES`…). *Type*: `INT`. *Set By*: `Client`.
   - *Increment Only*: sí (`STAT_BEST_STREAK` también, porque solo sube).
   - *Min*: 0. *Max*: en blanco o un tope generoso. *Default*: 0. *Display Name*: el que quieran.
2. **Achievements** → *Add new achievement*, uno por fila de la tabla:
   - *API Name*: exacto, en mayúsculas. **No se cambia más después de publicar.**
   - *Display Name* y *Description*: los del CSV. Para el inglés y otros idiomas, cada logro tiene
     su texto por idioma en el mismo formulario. También se puede exportar y subir la localización entera.
   - *Set By*: `Client`. *Hidden*: según la tabla.
   - *Progress Stat* (opcional): en los que tienen stat con rango (0–100, 0–1000, 0–5000) elegir el
     stat y el rango. Así Steam muestra la barra de progreso. El código igual lo desbloquea al llegar.
   - *Achieved Icon* y *Unachieved Icon*: un ícono a color y uno apagado (en gris) por logro, JPG
     cuadrado (Valve pide 256×256; revisen el tamaño que indica el formulario). **Son 40 imágenes
     para el artista.**
3. **Publish**: *App Admin* → pestaña **Publish** → *Prepare for Publishing* → *Publish to Steam*.
   **Sin publicar, los logros no existen para el cliente de Steam** y el juego los ignora (avisa en la consola).

### Probarlos

- **En el editor:** Steam abierto con una cuenta que tenga el juego en la biblioteca. Lo más simple
  es que cada integrante esté en el grupo de partner de Steamworks con acceso a la app. Si no, se
  piden claves *Release State Override* desde *Request Steam Product Keys*; Valve exige esperar
  tres semanas desde que se creó el App ID en el primer juego del estudio. `SteamConfig.AppId`
  cargado (genera `steam_appid.txt`). Al dar Play, la consola dice `[Steam] Inicializado` y cada
  logro `[Logros] Desbloqueado (Steam): …`.
- **Antes del lanzamiento los logros ganados NO se ven** ni en la biblioteca ni en el perfil de la
  Comunidad (lo dice Valve). La forma de confirmarlos es la consola de Unity. Desde la consola del
  cliente de Steam (`steam.exe -console`) también se pueden borrar:
  `achievement_clear 5636520 ACH_FIRST_FIRE` o `reset_all_stats 5636520`.
- **Sin Steam:** `[Logros] Desbloqueado (local): …` en la consola. No se guarda nada.
- **Resetear para volver a probar:** menú **Parripollo → Steam → Resetear logros y stats** (en Play).
  **Parripollo → Steam → Estado de Steam** muestra el App ID, si inicializó y el backend.

---

## 5. Builds y SteamPipe

### Una sola vez en el panel

1. **Depots** (*App Admin → SteamPipe → Depots*): Steam crea uno por defecto (App ID + 1).
   Configurarlo como *Windows*, todos los idiomas. Alcanza con un depot.
2. **Installation → General**: *Install Folder* = `Parrilla 40`. *Launch Option*: ejecutable
   `Parrilla 40.exe`, sistema operativo Windows, 64 bits.
3. **Installation → Redistributables**: en general nada (Unity 2022 no necesita el redistribuible de
   DirectX ni de VC++). Probar en una PC limpia para confirmarlo.
4. **Paquetes asociados** (*App Admin → Paquetes y DLC asociados*): agregar el depot a los paquetes
   del juego, incluido el *Developer Comp*. Si el depot no está en un paquete, nadie lo puede bajar.
5. **Publicar** (pestaña Publish) cada vez que se cambia algo de esto.
6. **Cuenta para builds**: Valve recomienda una cuenta de Steam aparte, con los permisos *Edit App
   Metadata* y *Publish App Changes To Steam* (*Users & Permissions*). Así nadie usa la cuenta del estudio.

### En cada versión

1. Unity → *Build Settings* → Windows, x86_64, **sin** *Development Build* → generar en una carpeta
   vacía. Verificar que **no** quedó `steam_appid.txt` (el guard lo borra y lo avisa en consola).
2. Bajar el **Steamworks SDK** del panel (*SDK* en la página de Steamworks) y usar
   `sdk/tools/ContentBuilder/`:
   - Copiar la build a `ContentBuilder/content/windows/`.
   - Crear `ContentBuilder/scripts/app_build_5636520.vdf`:

```
"AppBuild"
{
	"AppID" "5636520"
	"Desc" "Parrilla 40 v0.1.0"
	"ContentRoot" "..\content\"
	"BuildOutput" "..\output\"
	"SetLive" "beta"
	"Depots"
	{
		"5636521"
		{
			"FileMapping"
			{
				"LocalPath" "windows\*"
				"DepotPath" "."
				"recursive" "1"
			}
			"FileExclusion" "*.pdb"
			"FileExclusion" "*_BurstDebugInformation_DoNotShip*"
			"FileExclusion" "*_BackUpThisFolder_ButDontShipItWithYourGame*"
			"FileExclusion" "steam_appid.txt"
		}
	}
}
```

3. Subir:

```
ContentBuilder\builder\steamcmd.exe +login <cuenta_de_builds> +run_app_build ..\scripts\app_build_5636520.vdf +quit
```

4. En *App Admin → SteamPipe → Builds* aparece la build. Con `"SetLive" "beta"` queda en la rama
   `beta`, que conviene crear con contraseña para que el equipo pruebe primero. Pasarla a `default`
   (la pública) se hace a mano en esa página y hay que publicar.
5. **Antes del lanzamiento** Valve revisa la página de la tienda y la build (*Release checklist* en
   el panel). Calculen unos días hábiles para cada revisión.

---

## 6. Recordatorio: licencia de Unity

El equipo usa el plan **Unity Student**, cuyos términos limitan vender un juego hecho entre varios.
Antes de poner precio en Steam, revisen los términos vigentes y si tienen que pasarse a otro plan.
Esto no bloquea la integración técnica.

---

## Checklist

- [ ] Abrir el proyecto una vez y commitear `packages-lock.json` y el define `STEAMWORKS_NET` de `ProjectSettings.asset`
- [ ] Stats cargados en Steamworks (5)
- [ ] Logros cargados con textos es/en e íconos (20 × 2 íconos)
- [ ] Stats & Achievements **publicados**
- [ ] Probar en el editor con Steam abierto y resetear desde el menú
- [ ] Depot, launch option e install folder configurados y publicados
- [ ] Primera subida con ContentBuilder a la rama `beta`
- [ ] Probar la build de `beta` desde Steam en una PC que no sea de desarrollo
- [ ] Decidir sobre SteamStub (recomendado: no)
- [ ] Revisar la licencia de Unity antes de poner precio
