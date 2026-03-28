# DESIGN_ONLINE.md — Shrink: Features de Retención y Social

> **Objetivo**: aumentar retención diaria y monetización de skins sin multijugador en tiempo real.
> Stack elegido: **Unity Gaming Services (UGS)** + **Unity Mobile Notifications**
>
> **Modo Arena (multijugador)**: documentado al final — idea sólida, implementar después de validar el juego base.

---

## Resumen de features

| Feature | Prioridad | Complejidad | Impacto retención |
|---------|-----------|-------------|-------------------|
| Modo Diario | Alta | Baja | Alto |
| Notificación diaria | Alta | Muy baja | Alto |
| Leaderboard global | Media | Baja | Medio |
| Cloud Save (sync entre devices) | Media | Baja | Medio |
| Skins de esfera | Media | Baja | Medio-alto |
| Leaderboard por amigos | Baja | Media | Alto |
| **Modo Arena (multijugador)** | **Futura** | **Alta** | **Muy alto** |

---

## 1. Modo Diario

### Concepto
Un maze nuevo cada día, igual para todos los jugadores del mundo (misma semilla). El jugador tiene 24 horas para completarlo. Se guarda el mejor resultado del día (tiempo, tamaño restante, estrellas).

### Diseño de gameplay
- **Semilla**: `DateTime.Today.DayOfYear + DateTime.Today.Year` → determinístico, igual en todos los dispositivos sin servidor
- **MazeStyle**: Hybrid (el más interesante visualmente y en gameplay)
- **Tamaño del maze**: fijo 30×18 (balance entre reto y duración)
- **Sin timer de nivel** — el reto es el maze, no el reloj
- **1 intento gratis** por día. Reintentos: ver anuncio rewarded (máximo 2 reintentos/día)
- **Score**: `(tamañoRestante × 1000) + (estrellasRecogidas × 200) - (reintentosUsados × 150)`
- Accesible desde el menú principal — botón propio, no dentro de LevelSelect

### Estados del botón en MenuScene
| Estado | Texto | Condición |
|--------|-------|-----------|
| Disponible | "MODO DIARIO" | No jugado hoy |
| Completado | "✓ HOY COMPLETO — VER RANKING" | Ya completado |
| Sin conexión | "SIN CONEXIÓN" | No hay internet (score se sube al reconectar) |

### Pantalla de resultado (Modo Diario)
- Igual que VictoryPanel / GameOverPanel existentes
- Añade sección de ranking: posición del jugador + top 5 del día
- Botón "Compartir" → genera imagen con score para redes sociales (opcional, fase 2)

### Datos guardados localmente
```
DailyRecord {
    date: string          // "2026-03-27"
    completed: bool
    score: int
    sizeRemaining: float
    starsCollected: int
    retriesUsed: int
    scoreUploaded: bool   // false si no había internet al terminar
}
```

### Flujo de subida de score
1. Al terminar el nivel → guardar `DailyRecord` local inmediatamente
2. Intentar subir a UGS Leaderboard
3. Si falla (sin internet) → `scoreUploaded = false`
4. Al abrir el juego al día siguiente → si hay `scoreUploaded = false` y hay conexión → reintentar subida

---

## 2. Notificaciones

### Tipo: Local (Unity Mobile Notifications)
No requiere servidor ni Firebase. Se programa en el dispositivo al cerrar el juego.

### Notificación diaria — Modo Diario
- **Hora**: 9:00 AM hora local del dispositivo
- **Título**: "Shrink"
- **Body**: varía por día (rotación de mensajes)
- **Condición para programar**: solo si el jugador NO ha completado el modo diario hoy

### Pool de mensajes de notificación
```
"El maze de hoy ya está disponible."
"Nuevo maze diario. ¿Puedes salir primero?"
"El ranking de hoy está vacío. Sé el primero."
"Maze diario disponible. Tu record del ayer fue [N] puntos."  ← si hay record
```

### Notificación de racha (bonus retención)
- Si el jugador lleva 3+ días seguidos jugando el Modo Diario → notificación especial a los 2 días de ausencia
- Body: "Llevas [N] días de racha. No la rompas hoy."

### Implementación
Package oficial: `com.unity.mobile.notifications`

```csharp
// Programar al salir del juego (OnApplicationPause / OnApplicationQuit)
void ScheduleDailyNotification()
{
    var tomorrow9am = DateTime.Today.AddDays(1).AddHours(9);
    // Cancelar anterior y reprogramar
    NotificationCenter.ClearAllScheduledNotifications();

    var notification = new AndroidNotification  // idem para iOS
    {
        Title = "Shrink",
        Text = PickDailyMessage(),
        FireTime = tomorrow9am,
        SmallIcon = "app_icon",
    };
    AndroidNotificationCenter.SendNotification(notification, "daily_channel");
}
```

---

## 3. Leaderboard Global

### Servicio: UGS Leaderboards

### Leaderboards a crear en el dashboard de UGS

| ID | Nombre visible | Tipo | Reset |
|----|---------------|------|-------|
| `daily_{fecha}` | Ranking Diario | Score descendente | Por día |
| `alltime_levels` | Mejor Jugador Global | Score acumulado | Nunca |

### Score para `alltime_levels`
```
score += nivelesCompletados × 10
score += estrellasTotal × 5
score += modoDiarioCompletado × 50   // por cada día
```

### UI — Panel de Ranking
- Muestra top 10 + posición del jugador (aunque no esté en top 10)
- Nombre del jugador: nickname guardado en UGS o "Jugador_XXXX" generado al primer login
- Accesible desde: resultado del Modo Diario + botón en MenuScene

### Nombre de jugador
- Al primer inicio online: input de nickname (máx 16 caracteres, sin símbolos especiales)
- Se guarda en UGS Player Data + localmente
- Editable desde Ajustes

---

## 4. Cloud Save

### Servicio: UGS Cloud Save

### Qué se sincroniza
```
GameData completo (JSON) → clave "save_v1"
```

### Cuándo sincronizar
- **Al abrir el juego**: pull si hay conexión, comparar timestamp con local, usar el más reciente
- **Al cerrar / pausar**: push si hubo cambios desde el último push
- **Conflicto**: gana el save con más niveles completados (no el más reciente en tiempo)

### Cuándo NO sincronizar
- Sin conexión → solo local, con flag `pendingSync = true`
- En mitad de un nivel → esperar a que termine

---

## 5. Skins de Esfera

### Concepto
El jugador personaliza la esfera que controla. Las skins son visuales, sin ventaja de gameplay.

### Tipos de skin

| Tipo | Descripción | Obtención |
|------|-------------|-----------|
| **Color** | Color sólido de la esfera | Pack Colores (IAP existente, $0.99) |
| **Patrón** | Textura geométrica sobre la esfera (líneas, puntos, espiral) | Pack Colores ampliado |
| **Trail** | Color/forma de las migajas que deja | Pack Colores ampliado |
| **Modo Diario exclusivas** | Colores especiales desbloqueables por racha | Gratis — racha de 7 días |

### Skins de racha (Modo Diario)
| Racha | Skin desbloqueada |
|-------|------------------|
| 3 días | Esfera dorada |
| 7 días | Esfera con trail de chispas |
| 30 días | Esfera "arcoíris" (color cambia con el tiempo) |

Estas skins son permanentes una vez desbloqueadas — no se pierden si se rompe la racha.

### Implementación
- Sin cambios en `SphereController` — solo cambiar el `Material` o `Color` del renderer
- `SkinManager` (clase estática, Shrink.Core) — guarda skin activa en GameData
- Preview en MenuScene: mini-esfera rotando con la skin seleccionada

---

## 6. Arquitectura técnica

### Packages a añadir
```
com.unity.services.leaderboards        → UGS Leaderboards
com.unity.services.cloudsave           → UGS Cloud Save
com.unity.mobile.notifications        → Notificaciones locales
com.unity.services.authentication     → UGS Auth (requerido por UGS)
```

### Autenticación
UGS requiere un player ID. Usar **Anonymous Authentication** — sin login, sin registro, sin fricción.

```csharp
// GameBootstrap — añadir tras SaveManager.Init()
await UnityServices.InitializeAsync();
await AuthenticationService.Instance.SignInAnonymouslyAsync();
// El player ID queda en AuthenticationService.Instance.PlayerId
// Guardarlo en GameData para asociar con el save cloud
```

Si el jugador reinstala → nuevo ID anónimo → pierde su posición en rankings (aceptable para v1)
Mejora futura: login con Apple/Google para persistencia de ID.

### Orden de init en GameBootstrap
```
1. SaveManager.Init()
2. LocalizationManager.Init()
3. UnityServices.InitializeAsync()          ← nuevo
4. AuthenticationService.SignInAnonymously() ← nuevo
5. SyncCloudSave()                           ← nuevo (async, no bloquea)
6. AudioManager / AdManager / IAPManager
7. Cargar MenuScene
```

---

## Roadmap de implementación

### Fase 1 — Modo Diario + Notificaciones (prioridad)
- [ ] `DailyModeManager.cs` — genera maze con semilla del día, guarda DailyRecord
- [ ] Botón Modo Diario en MenuScene
- [ ] Panel de resultado con score
- [ ] Unity Mobile Notifications — notificación diaria a las 9am
- [ ] Racha local (sin servidor aún)

### Fase 2 — UGS + Leaderboard
- [ ] Setup UGS en dashboard (proyecto ya existe en Unity)
- [ ] Anonymous Auth en GameBootstrap
- [ ] Leaderboard diario — subir score, mostrar top 10
- [ ] Input de nickname
- [ ] Panel de ranking en resultado del Modo Diario

### Fase 3 — Cloud Save + Skins
- [ ] Cloud Save sync en GameBootstrap y OnApplicationPause
- [ ] `SkinManager.cs` + UI de selección de skins
- [ ] Ampliar Pack Colores IAP con patrones y trails
- [ ] Skins de racha del Modo Diario

### Fase 4 — Polish
- [ ] Notificación de racha rota
- [ ] Leaderboard alltime acumulado
- [ ] Botón Compartir resultado (screenshot + score)
- [ ] Login con Apple/Google para persistencia de ID (opcional)

---

## 8. Modo Infinito

### Concepto
Runs procedurales encadenadas. El tamaño **no se resetea entre mazos** — terminas un maze con 0.40 → entras al siguiente con 0.40. La presión es acumulada y creciente.

### Mecánica central
- Cada run es una secuencia de mazes con dificultad escalante
- El tamaño residual del maze anterior es tu punto de partida en el siguiente
- La mecánica de velocidad-al-encoger hace que cada maze sea más frenético que el anterior
- Las estrellas son la válvula de escape: recogerlas recupera tamaño, pero requieren desvíos arriesgados
- Al morir: run terminada, se muestra el número de mazes superados y el récord personal

### Escalada de dificultad
| Mazes | Tamaño | Especiales | Enemigos |
|-------|--------|------------|----------|
| 1–5 | 20×12 | Sin trampas ni puertas | Sin enemigos |
| 6–10 | 25×15 | NARROW_06, TRAP_DRAIN | Sin enemigos |
| 11–20 | 30×18 | NARROW_04, TRAP_ONESHOT, puertas | PatrolEnemy |
| 21+ | 35×20 | Todo activo, timer opcional | TrailEnemy |

### Gancho de retención
- Marcador visible durante el juego: *"Maze 7 — récord: 12"*
- Al morir se muestra exactamente qué te mató (trampa, NARROW, enemigo) → fácil culpar a la mala suerte → "una más"
- Sin leaderboard global — solo récord personal. Para móvil casual es suficiente y evita frustración
- Rewarded ad = +0.20 de tamaño, máximo 1 por run — válvula de escape ocasional

### Monetización
- Requiere IAP `infinite_pro` ($2.99) para acceso completo
- 5 runs gratis para probar (contador persistente)

### Lo que NO hacer
- No resetear el tamaño al 100% entre mazos — elimina toda la tensión acumulada
- No poner timer en los primeros 10 mazes — el ritmo lo marca el encoger, no el reloj

---

## Lo que NO hacer

- No usar Firebase si UGS cubre las necesidades — evitar dependencias innecesarias
- No poner skins de pago detrás de gameplay (no pay-to-win) — solo cosméticas
- No hacer el Cloud Save bloqueante — siempre async, el juego funciona sin conexión

---

## 7. Modo Arena — Multijugador en Tiempo Real

> **Estado**: idea documentada — implementar después de lanzamiento y validación del juego base.
> La mecánica central del juego es suficientemente original para funcionar en multijugador.

### Concepto

El mismo juego de puzzle, pero 2–6 jugadores en el mismo maze. El reto sigue siendo la planificación y el aprovechamiento de recursos — ahora con la variable de otros jugadores que compiten por los mismos recursos y pueden interferir activamente.

**No es un juego de acción.** Sigue siendo puzzle. La presión viene de la competencia por el maze, no de reflejos.

### Condición de victoria

| Opción | Descripción | Pros | Contras |
|--------|-------------|------|---------|
| **Primera salida** | Gana quien llega al EXIT primero | Simple, claro, tensión de carrera | Favorece rutas cortas, ignora estrellas |
| **Mayor masa al salir** | Gana quien sale con más tamaño | Recompensa eficiencia | Puede premiar quedarse quieto absorbiendo crumbs |
| **Score compuesto** | `masa_al_salir × 100 + estrellas × 50 + bonus_primero × 200` | Equilibrado | Más difícil de comunicar |

**Recomendado**: Score compuesto — mantiene el valor de las estrellas y de llegar primero sin eliminar ninguna estrategia.

### Mecánicas nuevas en Arena

#### Colisión — Embestida
Cuando dos esferas ocupan la misma celda simultáneamente:
- La esfera **más grande absorbe** un porcentaje fijo de masa de la más pequeña (`collisionDrain = 0.08`)
- Si son del mismo tamaño: ambas pierden la mitad de `collisionDrain`
- La esfera más pequeña es empujada una celda hacia atrás (dirección inversa a su movimiento)
- No hay muerte por colisión — solo pérdida de masa

Esto crea una dinámica de poder acumulativo: cuanta más masa tienes, más peligroso eres para chocar, pero también más te buscan.

#### Crumbs ajenos — Absorber migajas de otros
- Las migajas de otros jugadores son visibles en el maze (color distinto al tuyo)
- Al pasar por una celda con migaja ajena: la absorbes y ganas la masa que valía
- El jugador original no la recupera — es pérdida permanente para él
- Esto hace que dejar un rastro largo sea arriesgado: otros pueden "comer" tu camino de regreso

Tensión estratégica clave: ¿backtrack para reabsorber tus propias migajas antes de que otro te las robe, o sigues avanzando hacia el EXIT?

#### Trampas activas — Crumb envenenada
Un jugador puede "envenenar" una de sus propias migajas depositadas:
- Cuesta masa activarla (`poisonCost = 0.05`)
- Visualmente indistinguible de una migaja normal para los demás
- Si un rival la absorbe: pierde `poisonDrain = 0.12` en lugar de ganar masa
- Límite: máximo 2 crumbs envenenadas activas por jugador

Esto da un uso ofensivo al rastro de migajas y recompensa a jugadores que leen los movimientos del rival.

#### Narrow — ventaja de tamaño pequeño
Las celdas NARROW_06 y NARROW_04 ya existentes funcionan igual.
Un jugador que ha perdido masa por colisiones o trampas puede acceder a zonas del maze que los rivales más grandes no pueden. El mazede juego procedural ya tiene esto resuelto.

### Diseño del maze en Arena

- **MazeStyle**: Hybrid obligatorio — genera zonas abiertas (interacción entre jugadores) y corredores estrechos (rutas alternativas)
- **Tamaño**: 35×20 mínimo para 4 jugadores, 40×24 para 6 jugadores
- **Semilla**: generada por el servidor (host) al crear la sala — igual para todos los jugadores
- **Múltiples salidas**: 1 EXIT principal + 1–2 salidas secundarias con penalización de score (−100) para evitar embotellamiento
- **Puertas**: presentes — crean decisiones de coste/beneficio igual que en solo
- **Estrellas**: distribuidas con el mismo algoritmo actual, contadas globalmente (cada estrella solo la puede recoger el primero que llegue)

### Sesión de juego

```
Lobby (30s) → Countdown (3s) → Partida → Pantalla de resultados → Revanche / Menú
```

- **Tamaño de sesión**: 2–6 jugadores. Si no se llena el lobby: bots rellenan los huecos
- **Tiempo máximo de partida**: 5 minutos. Al acabar el tiempo: gana quien tenga más score acumulado hasta ese momento, sin importar si salió o no
- **Bots**: navegan el maze con A* simple, emisten aleatoriamente, no envenenan crumbs (dificultad configurable: fácil/normal/difícil)
- **Espectador**: al morir (tamaño < MinSize) el jugador puede ver la partida desde arriba con el maze completo visible — no puede interactuar

### Monetización en Arena

| Elemento | Modelo |
|----------|--------|
| Acceso al Modo Arena | Requiere `full_game` IAP ($1.99) — refuerza el valor del pack |
| Skins en Arena | Las mismas del juego base — se ven las de todos los jugadores |
| Entrada sin IAP | 3 partidas gratis/día para probar → incentivo de conversión |
| Tabla de temporada | Rankings de Arena con reset mensual — driver de retención |

### Preguntas de diseño pendientes

- ¿El Modo Arena usa el mismo maze del Modo Diario ese día, o mazes independientes?
- ¿Hay power-ups en el maze (celdas especiales que dan ventaja temporal)?
- ¿Los bots avisan que son bots o se hacen pasar por humanos?
- ¿Hay modo 1v1 además del 2–6?
- ¿La crumb envenenada se desactiva sola tras X segundos o es permanente hasta ser pisada?
- ¿Se pueden ver las crumbs envenenadas propias en tu mapa?

### Stack técnico para Arena

**Opción recomendada: Photon PUN 2**
- SDK maduro, gratis hasta 20 CCU (concurrent users)
- Gestiona rooms, sincronización de estado y matchmaking
- Unity SDK bien documentado
- Alternativa: Unity Netcode for GameObjects (más integrado pero menos maduro en mobile)

**Lo más difícil técnicamente**: sincronizar el movimiento slide-to-wall.
El slide es determinístico si el maze es idéntico en todos los clientes — la semilla garantiza esto. El reto es sincronizar la *dirección del input* (no la posición frame a frame), que es mucho más liviano en red.

```
// Concepto de sync: enviar inputs, no posiciones
// Cada cliente simula el resultado — el maze es idéntico en todos
[PunRPC] void RPC_SendInput(Direction dir, int tick) { ... }
```

### Por qué tiene sentido después del lanzamiento

El Modo Arena no cambia el juego base — lo extiende. Un jugador que conoce el maze solo, las migajas, las NARROW y las trampas ya sabe jugar Arena. La curva de aprendizaje es cero para los jugadores existentes, y Arena puede traer jugadores nuevos que no hubieran probado el solo.

La clave es el orden: primero validar que el juego solo tiene retención, luego Arena tiene una base de jugadores real para llenar los lobbies.
