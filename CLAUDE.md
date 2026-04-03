# CLAUDE.md — Shrink (Unity 6)

## Proyecto

Juego móvil puzzle maze 2D top-down para iOS y Android.
Motor: Unity 6 | Resolución: 1080x1920 portrait | Estilo: minimalista (líneas y círculos, sin assets externos)
Input System: **New Input System** (Unity package activo — NO usar `UnityEngine.Input` legacy)

## Ruta raíz de código

```
Assets/_Project/
```

Todo el código del juego vive aquí. No tocar Assets/Scenes, Assets/Settings ni carpetas generadas por Unity fuera de _Project.

## Estructura de carpetas

```
Scripts/
  Core/          ShapeFactory, GameManager, SaveManager, GameData, LocalizationManager
  Events/        GameEvents.cs — eventos globales estáticos
  Maze/          MazeGenerator, MazeData, CellType, MazeRenderer
                 Editor/MazeLevelEditor.cs, Editor/MazeDebugVisualizerEditor.cs
  Player/        SphereController, ShrinkMechanic, Crumb, Star
  Enemies/       EnemyController, PatrolEnemy, TrailEnemy, ChaserEnemy
  Movement/      PlayerMovement
  Camera/        CameraFollow
  UI/            HUDController, PauseMapController, GameResultController, LocalizedText,
                 LevelSelectController, DPadController, DPadButton
  Level/         LevelManager, LevelData (ScriptableObject), LevelLoader, LevelTimer,
                 CellOverride, EnemySpawn
  Monetization/  AdManager, IAPManager
  Audio/         AudioManager

ScriptableObjects/
  Levels/        LevelData assets (Level_01 … Level_N, crece con cada mundo)
  Colors/        Paletas de color (IAP)

Prefabs/
  Player.prefab  (SphereController + ShrinkMechanic + PlayerMovement)
Scenes/
Materials/
Sprites/
Fonts/
Audio/
FX/Particles/
```

## Sistemas — estado actual

| #   | Sistema                         | Estado      | Notas                                                                                                                                                 |
| --- | ------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Generación procedural de maze  | ✅ Completo | BSP + Labyrinth + Hybrid                                                                                                                              |
| 2   | Esfera y mecánica de desgaste  | ✅ Completo | Migajas, puertas, estrellas, calibración auto                                                                                                        |
| 3   | Movimiento — joystick flotante | ✅ Completo | Floating joystick invisible, re-anclaje dinámico, velocidad inversamente proporcional al tamaño                                                     |
| 3.5 | Enemigos / Mobs                 | ✅ Completo | PatrolEnemy, TrailEnemy, ChaserEnemy — spawns manuales en editor                                                                                     |
| 4   | Pausa + HUD                     | ✅ Completo | HUDController + PauseMapController (Continuar, Retry, Menú, recompensas, Controles D-pad)                                                            |
| 5   | Sistema de niveles y semillas   | ✅ Completo | LevelData, LevelManager, LevelLoader, LevelTimer, Trampas, Picos                                                                                      |
| 6   | Monetización                   | ✅ Completo | IAPManager (Unity IAP v5) + AdManager (AdMob)                                                                                                         |
| 7   | Juice y sonido                  | ✅ Completo | AudioManager, playlists aleatorias, SFX por eventos                                                                                                   |
| S   | SaveManager                     | ✅ Completo | GameData JSON, LevelRecord, AudioSettings, GameStats                                                                                                  |
| L   | Localización                   | ✅ Completo | EN, ES, PT, FR, DE — auto-detect + guardado en SaveManager                                                                                           |
| 8   | UI completa                     | ✅ Completo | Boot + Menu + LevelSelect (por mundos) + GameResult + Localización                                                                                   |
| D   | D-pad táctil                   | ✅ Completo | DPadController + DPadButton — customizable desde pausa (posición, tamaño, alpha). Input por zona: deslizar sin levantar el dedo cambia dirección. |
| E   | Editor visual de niveles        | ✅ Completo | Ver sección Editor Visual                                                                                                                            |
| 9   | Modo Infinito                   | ✅ Completo | InfiniteGameManager + InfiniteHUDController + InfiniteScene — ver sección Modo Infinito                                                             |
| F   | Mecánicas futuras              | ⬜ Backlog  | Enemigos adicionales, trampas avanzadas, celdas especiales — ver sección Backlog                                                                    |

## Generación de maze — MazeStyle

```csharp
public enum MazeStyle { Dungeon, Labyrinth, Hybrid }
```

| Estilo    | Algoritmo                      | Usar para                          |
| --------- | ------------------------------ | ---------------------------------- |
| Dungeon   | BSP (cuartos + corredores)     | Niveles 1–8, Infinito mazes 1–6  |
| Labyrinth | Recursive Backtracker DFS      | Niveles 9–30+, Infinito mazes 17+ |
| Hybrid    | Backtracker + cuartos tallados | Infinito mazes 7–16               |

- **Labyrinth es el modo recomendado** para gameplay real — corredores de 1 celda de ancho.
- BSP genera cuartos grandes donde el joystick se degrada.
- El maze siempre se valida con BFS antes de devolverse. `ShortestPathLength` queda en `MazeData`.

## Movimiento — PlayerMovement

Un solo modo: **joystick flotante invisible**. No hay modos seleccionables.

**Comportamiento:**

- Toca en cualquier punto de la pantalla y arrastra → dirección registrada al superar el deadzone
- Mantener el dedo = la esfera sigue moviéndose. Soltar = para al terminar la celda actual
- Cambiar dirección mientras el dedo está abajo redirige en la próxima celda
- El origen del joystick se re-ancla cada vez que se registra una nueva dirección

**Velocidad dinámica por tamaño:**

- Tamaño 1.0 (lleno) → `moveTimeSlow` segundos/celda (lento, fácil)
- Tamaño 0.15 (mínimo) → `moveTimeFast` segundos/celda (rápido, difícil)
- Fórmula: `duration = Lerp(moveTimeFast, moveTimeSlow, InverseLerp(MinSize, InitialSize, currentSize))`

**Configuración en LevelLoader** (Inspector):

- `Move Time Slow` — velocidad con tamaño máximo (default `0.22`)
- `Move Time Fast` — velocidad con tamaño mínimo (default `0.08`)
- `Joystick Deadzone` — píxeles mínimos para registrar dirección (default `20`)
- `Camera View Cells` — radio de celdas visibles desde el jugador (`3–3.5` recomendado, `0` = todo el maze)

**Teclado (testing):** mantener W/A/S/D o flechas = movimiento continuo. Espacio no detiene — el jugador para al soltar la tecla.

**D-pad táctil (alternativa):** `DPadController` envía `SetDPadDirection(Vector2Int)` a `PlayerMovement`. Tiene prioridad sobre joystick si `_dpadDir != zero`. Se muestra/oculta automáticamente según el estado de pausa.

- Input por zona: todo el input lo gestiona `DPadController`, no los botones hijos. La dirección se calcula por la posición del dedo relativa al centro del D-pad (eje dominante gana). `IPointerMoveHandler` permite deslizar entre direcciones sin levantar el dedo.
- `DPadButton` es solo un marcador visual — el campo `direction` es referencia; no tiene event handlers.

**Player Prefab**: `Assets/_Project/Prefabs/Player.prefab`

- Contiene: `SphereController`, `ShrinkMechanic`, `PlayerMovement`
- `LevelLoader` lo instancia en runtime con `Instantiate(_playerPrefab)`

## Calibración de dificultad — ShrinkMechanic

```
sizePerStep = 0.85 × difficultyFactor ÷ shortestPathLength
```

| difficultyFactor | Significado                                   |
| ---------------- | --------------------------------------------- |
| 0.50             | Tutorial (niveles 1–3)                       |
| 0.65             | Aprendizaje (niveles 4–7)                    |
| 0.75             | Normal (niveles 8–11)                        |
| 0.80             | Exigente (niveles 12–15) — fin Mundo 1      |
| 0.85             | Difícil (niveles 16–20) — inicio Mundo 2   |
| 0.90             | Muy difícil (niveles 21–25)                 |
| 0.95             | Casi perfecto (niveles 26–30) — fin Mundo 2 |
| 0.85→0.95       | Mundo 3 (31–45), escalado progresivo         |
| 0.95→1.0        | Infinito creciente                            |

- `autoCalibrate = true` (default): calcula sizePerStep automáticamente.
- Los callejones sin salida son gratis si el jugador retrocede completo.

## Estrellas (recolectables)

- Visual: diamante (cuadrado rotado 45°), color amarillo
- Recompensa: bonus de tamaño (`starSizeBonus`, default `+0.05`)
- Placement: greedy farthest-point sampling sobre celdas con detour score ≥ 2
- Si hay `manualStarCells` en LevelData, reemplazan el algoritmo greedy completamente

## Enemigos — Sistema 3.5 ✅

Todos los enemigos se mueven celda a celda. Tocar un enemigo = muerte instantánea (`RaiseLevelFail`).
Todo enemigo devora la migaja de la celda al llegar (comportamiento en base class `EnemyController`).

### Tipos implementados

| Tipo                  | Comportamiento                                                                                                                                  | Color                       |
| --------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------- |
| **PatrolEnemy** | Recorre un segmento fijo de ida y vuelta, rebota en paredes                                                                                     | Naranja `(1, 0.30, 0.10)` |
| **TrailEnemy**  | BFS hacia la migaja más reciente (`CrumbOrder.Last`), la devora al llegar                                                                    | Naranja (mismo base)        |
| **ChaserEnemy** | BFS directo hacia la posición actual del jugador. Persigue desde el inicio del nivel. Funciona mejor en Dungeon (rooms). Ideal para nivel 21+. | Azul `(0.10, 0.45, 0.90)` |

### EnemyType enum

```csharp
public enum EnemyType { Patrol, Trail, Chaser }
```

### Spawning

- **Manual (editor visual)**: `LevelData.manualEnemySpawns` — lista de `EnemySpawn { cell, type, patrolDir }`. Si hay entradas, ignora los contadores.
- **Automático**: `patrolEnemyCount`, `trailEnemyCount` y `chaserEnemyCount` en `LevelData`. Posiciones aleatorias con seed reproducible, distancia Manhattan ≥ 5 del START.
- Todos los enemigos se destruyen en `LevelLoader.UnloadCurrent()`.

### Implementación base

- `EnemyController` abstract: `MoveLoop()` coroutine, `CanEnter()`, `BuildVisual()`, colisión en `Update()`
- `Initialize(MazeRenderer, SphereController, Vector2Int startCell)` — llamar tras instanciar
- `PatrolEnemy.InitializePatrol(renderer, player, cell, dir)` — acepta dirección explícita
- `ChooseNextCell()` abstract — cada subclase define su comportamiento
- `OnArrivedAtCell(cell)` virtual — base devora migaja; override para efectos extra

### Revive tras muerte por enemigo

- Al morir, `OnGameOver()` pone `_active = false` en todos los enemigos y `_isMoving = true` en `PlayerMovement`
- Si el jugador ve un anuncio para revivir, `GameEvents.RaisePlayerRevived()` se lanza desde `GameResultController.ApplyReward()`
- El enemigo que causó la muerte tiene `_wasKiller = true` → se destruye al revivir (evita re-muerte inmediata en misma celda)
- Los demás enemigos escuchan `OnPlayerRevived` y reanudan su `MoveLoop()`
- `PlayerMovement` escucha `OnPlayerRevived` y resetea `_isMoving = false`

### Introducción por nivel

- PatrolEnemy: desde nivel 12 (Mundo 1, últimos niveles)
- TrailEnemy: nivel 19 solo (tutorial del enemigo, sin PatrolEnemy ese nivel), nivel 20 combinado con PatrolEnemy, escala en Mundo 2 desde nivel ~25
- ChaserEnemy: desde nivel 21 (Mundo 2). Colocarlo lejos del START en rooms grandes de Dungeon.

## Trampas — diseño

Celdas de suelo especiales. Siempre visibles — el reto es de planning, no de sorpresa.

### Implementadas

| Tipo             | Comportamiento                                          | Color                              |
| ---------------- | ------------------------------------------------------- | ---------------------------------- |
| `TRAP_DRAIN`   | Drena masa al pisarla. Cobra cada vez. Sin migaja.      | Rojo oscuro `(0.70, 0.10, 0.30)` |
| `TRAP_ONESHOT` | Se pisa una vez → WALL permanente. Bloquea el regreso. | Naranja `(0.95, 0.50, 0.10)`     |
| `SPIKE`        | Muerte instantánea al pisar (igual que tocar enemigo). | Rojo `(0.90, 0.05, 0.05)`        |

- `TRAP_DRAIN`: desde nivel 8. Costo: `trapDrainCost = 0.08`.
- `TRAP_ONESHOT`: desde nivel 12. Destruye base floor + overlay al activarse (fix aplicado).
- BFS de validación usa las trampas en estado inicial → nivel siempre soluble.

### Pendientes de implementar (backlog)

Ver sección **Backlog de mecánicas**.

## Tipos de celda actuales (MazeData)

```csharp
public enum CellType
{
    WALL, PATH, ROOM, CORRIDOR, DOOR,
    START, EXIT, NARROW_06, NARROW_04,
    TRAP_ONESHOT, TRAP_DRAIN,
    SPIKE
}
```

## Mecánica central — reglas inamovibles

- Tamaño inicial esfera: `1.0`
- Tamaño mínimo antes de muerte: `0.15`
- Rango usable: `0.85`
- `NARROW_06`: requiere tamaño < 0.6
- `NARROW_04`: requiere tamaño < 0.4
- Migajas: una por celda, recuperan exactamente el tamaño perdido al ser reabsorbidas
- Puertas: consumen tamaño permanentemente (sin migaja), costo default `0.10`
- Maze siempre tiene solución — validado con BFS
- Tocar un enemigo = muerte instantánea

## Niveles y mundos

| Mundo             | Niveles | Acceso           | Tamaño        | Timer           | Mecánicas nuevas                                                                  |
| ----------------- | ------- | ---------------- | -------------- | --------------- | ---------------------------------------------------------------------------------- |
| **Mundo 1** | 1–15   | **GRATIS** | 20×12→25×15 | No              | NARROW_06 (5), TRAP_DRAIN (8), TRAP_ONESHOT (12), PatrolEnemy (12), NARROW_04 (15) |
| **Mundo 2** | 16–30  | 💰`full_game`  | 25×15→35×20 | Desde ~nivel 26 | TrailEnemy solo (19→solo en M1 ya), combinaciones, mayor dificultad               |
| **Mundo 3** | 31–45  | 💰 incluido      | 35×20→40×24 | Sí             | Nuevas trampas, portales, power-ups/desventajas, nuevos enemigos                   |
| **Mundo N** | 46+     | 💰 incluido      | escala         | Sí             | Contenido de updates futuros                                                       |

- Los mundos futuros (3, 4…) se incluyen en la misma compra de `full_game` — argumento de venta: "el juego sigue creciendo"
- **Modo Infinito**: al completar los 15 niveles del Mundo 1 (gratis) o IAP `infinite_pro`. Ver sección siguiente.

### MazeStyle por mundo

- **Mundo 1 (1–8)**: `MazeStyle.Dungeon`
- **Mundo 1 (9–15)**: `MazeStyle.Labyrinth`
- **Mundo 2 (16–30)**: `MazeStyle.Labyrinth`
- **Mundo 3 (31–45)**: `MazeStyle.Labyrinth`

## Modo Infinito — Sistema 9 ✅

Se desbloquea completando los **15 niveles del Mundo 1** (gratis) o comprando `infinite_pro` ($2.99).
Escena: `InfiniteScene`. Scripts: `InfiniteGameManager` + `InfiniteHUDController`.

### Mecánica central

- La **masa NO se reinicia** entre mazes — al salir del EXIT entras al siguiente con la masa que tengas
- Cada maze completado da un bonus de masa: `+0.04` (ajustable en Inspector de `InfiniteGameManager`)
- Si la masa llega a `0.15` → fin del run, score guardado en `GameStats.infiniteRecord`
- Score = `mazes × 100 + estrellas × 10` — ejemplo: 12 mazes + 15 estrellas = 1350 pts. Las estrellas desempatan jugadores que llegan al mismo maze.

### Escalado real (implementado en `InfiniteGameManager.BuildLevelData()`)

| Maze   | Tamaño        | Estilo            | Estrellas | Elementos nuevos                            |
| ------ | -------------- | ----------------- | --------- | ------------------------------------------- |
| 1–3   | 20×12         | Dungeon           | 6 × 0.09 | — Solo exploración                        |
| 4–6   | 20×12         | Dungeon           | 6 × 0.09 | PatrolEnemy (1)                             |
| 7–8   | 20×12         | Dungeon           | 6 × 0.09 | TrailEnemy (1) — contrarresta backtracking |
| 9–10  | 25×15         | Hybrid            | 5 × 0.07 | NARROW_06, TRAP_DRAIN, puertas              |
| 11–16 | 25–30×15–18 | Hybrid            | 5 × 0.07 | TRAP_ONESHOT, más enemigos                 |
| 17–21 | 35×20         | Hybrid→Labyrinth | 3 × 0.05 | NARROW_04, SPIKE, TrailEnemy (2)            |
| 22+    | 40–45×24–28 | Labyrinth         | 3 × 0.05 | Timer (90s→45s), dificultad → 1.0         |

### Dificultad

- `difficultyFactor = Clamp(0.55 + mazeIndex × 0.015, 0.55, 1.0)`
- Maze 1: 0.55 (accesible) → Maze 30: 0.97 → Maze 30+: 1.0 (play perfecto)

### PatrolEnemy — validación de espacio

- El spawn requiere ≥ 3 celdas de recorrido total (medidas en ambas direcciones del eje)
- Si no hay celda válida tras 40 intentos, el enemigo no spawna (mejor sin enemigo que imposible)

### Semillas

- `RunBaseSeed = UnityEngine.Random.Range(1, 99999)` — diferente cada run
- `mazeSeed = RunBaseSeed + mazeIndex × 7919` — reproducible dentro del run

### UI de InfiniteScene

```
Canvas
  ├── HUDView                 (HUDController — igual que GameScene)
  ├── PauseView               (PauseMapController — Retry recarga InfiniteScene = nuevo run)
  ├── InfiniteStatsOverlay    (siempre visible: MazeLabel, MassLabel)
  ├── MazeCompleteFlash       (aparece 1.4s al completar cada maze, luego se oculta)
  └── RunOverPanel            (full-screen al morir: mazes, score, récord, Play Again, Menu)
```

- `InfiniteHUDController` va en el Canvas. Gestiona overlay + flash + RunOverPanel.
- `InfiniteGameManager` + `LevelLoader` van en el mismo GameObject (`InfiniteManager`).

## HUD + Pausa — Sistema 4

### PauseMapController (`Scripts/UI/PauseMapController.cs`)

- Se activa con botón pausa (HUD) o tecla Escape
- `Time.timeScale = 0` al abrir, `= 1` al cerrar
- Botón **CONTINUAR** (`_resumeButton`) — reanuda el juego
- Botón **REINTENTAR** (`_retryButton`) — recarga la escena activa (nuevo intento del mismo nivel)
- Botón **MENÚ** (`_menuButton`) — va a MenuScene
- Botón **AÑADIR MASA** (`_addSizeButton`) — rewarded ad → `+rewardedSizeBonus` (default `0.15`)
- Botón **AÑADIR TIEMPO** (`_addTimeButton`) — solo visible si el nivel tiene timer → `+rewardedTimeBonus` (default `30s`)
- Botón **CONTROLES** (`_controlsButton`) — abre ControlsView para ajustar el D-pad
- Los botones de recompensa se desactivan si ya se usó el rewarded en este nivel

### Jerarquía de escena

```
Canvas
  ├── HUDView
  │   ├── TopBar
  │   │   ├── StarsLabel     (TMP — _starsLabel)
  │   │   └── PauseButton    (Button — _pauseButton)
  │   └── SizeBarContainer
  │       ├── LifeBar        (Image filled — _sizeBarFill)
  │       └── LifeText       (TMP — _sizeLabel)
  ├── PauseView              (GameObject — _mapPanel, desactivado por defecto)
  │   ├── ResumeButton       (Button — _resumeButton)
  │   ├── RetryButton        (Button — _retryButton)
  │   ├── MenuButton         (Button — _menuButton)
  │   ├── ControlsButton     (Button — _controlsButton)
  │   ├── AddSizeButton      (Button — _addSizeButton)
  │   └── AddTimeButton      (Button — _addTimeButton, oculto si sin timer)
  ├── ControlsView           (GameObject — _controlsPanel, desactivado por defecto)
  │   ├── Label              ("Drag to reposition · Sliders to resize and adjust opacity")
  │   └── DoneButton         → llama PauseMapController.OnControlsDone()
  └── DPad                   (DPadController — al fondo del Canvas para renderizar sobre todo)
      ├── ButtonsGroup       (CanvasGroup — controla alpha solo de los 4 botones)
      │   ├── UpButton       (DPadButton, direction=(0,1))
      │   ├── DownButton     (DPadButton, direction=(0,-1))
      │   ├── LeftButton     (DPadButton, direction=(-1,0))
      │   └── RightButton    (DPadButton, direction=(1,0))
      └── EditOverlay        (desactivado por defecto — contiene ScaleSlider, AlphaSlider, ResetButton)
```

**Notas del D-pad:**

- `DPadController` se asigna a `LevelLoader._dpad` y a `PauseMapController._dpad`
- `SetPaused(true)` oculta el DPad; `SetPaused(false)` lo muestra
- `OnControlsPressed()` oculta PauseView, muestra ControlsView y llama `SetEditMode(true)`
- `OnControlsDone()` llama `SetEditMode(false)`, `SaveSettings()` y restaura PauseView
- Posición/escala/alpha se guardan en `SaveManager.Data.dpad` (`DPadSettings`)
- `DPadSettings.initialized = false` en primer arranque → se usa la posición del editor como default. Se marca `true` al primer `SaveSettings()`. Así la posición colocada en el editor es el default real sin hardcodear coordenadas.

## GameResultController — Sistema 8

```
Canvas
  ├── HUDView, PauseView (ver arriba)
  ├── VictoryPanel
  │   ├── Stars[]       (3× Image — _victoryStars)
  │   ├── NextButton
  │   └── MenuButton
  ├── GameOverPanel
  │   ├── RetryButton
  │   ├── WatchAdButton
  │   └── MenuButton
  └── ReadyPanel
      ├── BonusText     (TMP — _bonusText)
      └── ContinueButton
```

- `_continueBonus = 0.30f` — masa que se añade al continuar tras anuncio
- Usa flag `_applyRewardNextFrame` para callbacks de AdMob (hilo secundario)

## Editor Visual de Niveles — Sistema E ✅

Ventana: **Window → Shrink → Level Editor**

### Qué se puede editar

- **Estrellas**: colocar/quitar. Si hay manuales, reemplazan el algoritmo greedy.
- **Trampas**: `TRAP_DRAIN`, `TRAP_ONESHOT`, `SPIKE`
- **Puertas**: `DOOR`
- **NARROW**: `NARROW_06`, `NARROW_04`
- **Estructura**: abrir muros (WALL→PATH) o cerrar celdas (PATH→WALL)
- **Enemigos**: `→ Patrulla H`, `↑ Patrulla V`, `◎ Rastreador`, `⬤ Perseguidor (ChaserEnemy)`
- **Dificultad**: slider `difficultyFactor` con sizePerStep y margen en vivo
- **Masa por estrella**: slider `starSizeBonus` con balance total en vivo
- **Camino óptimo**: toggle overlay azul del BFS START→EXIT (se recalcula al editar estructura)

### Datos en LevelData

```csharp
List<CellOverride>  manualOverrides    // tipo de celda por posición
List<Vector2Int>    manualStarCells    // posiciones manuales de estrellas
List<EnemySpawn>    manualEnemySpawns  // { cell, type, patrolDir }
```

---

## Backlog de mecánicas futuras

Estas mecánicas están diseñadas y aprobadas para implementar después de los 30 niveles base. Se documentan aquí para que el siguiente sprint las tenga como referencia sin tener que rediscutirlas.

### Visual del jugador — Blob cluster (lava lamp)

El código existe en `Assets/_Project/Scripts/Player/BlobClusterVisual.cs` (inactivo).

**Concepto:** reemplazar el círculo único del jugador con un blob central grande + satélites pequeños animados con spring physics viscosa.

- **Blob central**: respira con pulso de escala senoidal (`breatheSpeed`, `breatheAmount`)
- **Satélites**: orbitan usando spring physics — cada uno persigue un target orbital con `vel += toTarget * springK * dt` y amortiguación `vel *= (1 - damping * dt)`. Con springK≈9 y damping≈5.5 el sistema es críticamente amortiguado: se desprenden y pegan sin rebotar.
- **Squish**: cuando un satélite está lejos del target se aplana en esa dirección (tensión superficial visual). Se logra rotando el child y usando `localScale = (scaleAlong, scalePerp)`.
- **Masa → blobs activos**: a masa máxima hay 5 satélites + centro. Conforme baja la masa los satélites desaparecen uno a uno. Color fijo (no cambia con la masa — para eso está la barra de vida).
- **Migajas**: también respiran con pulso senoidal de escala, phase offset aleatorio por migaja para que no estén sincronizadas.

**Para activar**: en `SphereController.Initialize`, sustituir el bloque de `SpriteRenderer` por `BlobClusterVisual.Setup(cellSize)` y delegar `RefreshVisual` a `BlobClusterVisual.Refresh(currentSize, cellSize)`.

### Nuevos tipos de enemigo

| Tipo                  | Comportamiento                                                                                                                                      | Efecto especial                                                                                                                             | Introducción sugerida                                 |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------ |
| **AmbushEnemy** | Estático hasta que el jugador entra en radio N. Persigue brevemente, vuelve a su post.                                                             | Obliga a planificar rutas cerca del ambush. Se distingue visualmente del suelo                                                              | Nivel 22                                               |
| **GhostEnemy**  | Atraviesa paredes (ignora WALL al moverse). Solo el jugador puede bloquearlo usando NARROW.                                                         | Obliga a buscar pasajes estrechos como refugio                                                                                              | Nivel 25                                               |
| **MirrorEnemy** | Se mueve en dirección opuesta al jugador (player va →, mirror va ←). Mismo grid.                                                                 | Crea rutas forzadas simétricas — si vas a un callejón, él también va                                                                   | Modo Infinito                                          |
| **WanderEnemy** | Movimiento aleatorio celda a celda con sesgo suave hacia el jugador (70% random, 30% BFS hacia player). Solo habita y permanece en celdas `ROOM`. | Impredecible — el jugador debe mantener distancia en lugar de memorizar un patrón. El confinamiento a rooms evita bloqueos en corredores. | Infinito mazes 13–14 (Hybrid/Dungeon donde hay rooms) |

**Reglas base (heredadas de EnemyController):**

- Todos devoran migajas al pasar
- Todos matan al contacto
- GhostEnemy necesita `CanEnterGhost()` que ignora WALL
- WanderEnemy: `CanEnter()` solo permite celdas `ROOM` — si el siguiente paso no es ROOM, elige otra dirección aleatoria. Spawn exclusivo en celdas ROOM.

### Nuevos tipos de trampa

| Tipo                | Comportamiento                                                                           | Coste / Duración                                                                     | Introducción |
| ------------------- | ---------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- | ------------- |
| `TRAP_SLOW`       | Al pisarla, el moveTime se multiplica por 2 durante N segundos                           | Duración: 4s. Sin pérdida de masa directa — te hace más lento y fácil de atrapar | Nivel 14      |
| `TRAP_INVERT`     | Invierte los controles del joystick durante N segundos                                   | Duración: 3s. El reto es navegar con izquierda=derecha, arriba=abajo                 | Nivel 19      |
| `TRAP_TELEPORT`   | Teletransporta al jugador a una celda aleatoria walkable                                 | Instant. La celda destino siempre es alcanzable pero puede estar lejos del EXIT       | Nivel 23      |
| `TRAP_CONTINUOUS` | Drena masa por segundo mientras el jugador está PARADO sobre ella. No cobra al moverse. | Rate:`0.02/s`. Penaliza detenerse a pensar en zona peligrosa                        | Nivel 16      |
| `TRAP_TOGGLE`     | Al pisarla, levanta un WALL temporal bloqueando el paso detrás. Dura N segundos.        | Duración: 6s. Crea puertas de un solo sentido temporales                             | Nivel 22      |

**Reglas comunes:**

- Siempre visibles — colores distintos entre tipos
- Los efectos de tiempo (`SLOW`, `INVERT`) usan una cola de efectos activos, no se cancelan entre sí
- `TRAP_CONTINUOUS` requiere que `PlayerMovement` exponga un flag `IsMoving` — drena solo si `!IsMoving`
- Los efectos de control (`INVERT`) se aplican en `PlayerMovement.ReadJoystickInput()` como multiplicador de dirección `-1`

### Nuevas variantes de pico

| Tipo               | Comportamiento                                                                                     | Implementación                                                                            |
| ------------------ | -------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| `SPIKE_TIMED`    | Aparece y desaparece en ciclo (e.g. 2s activo, 2s inactivo). Visible siempre, color indica estado. | MonoBehaviour con coroutine alterna `Data.Grid[x,y]` entre SPIKE y PATH                  |
| `SPIKE_PRESSURE` | Invisible hasta que el jugador entra en celda adyacente. Entonces revela con animación flash.     | Se registra en MazeRenderer como overlay oculto.`ShrinkMechanic` lo revela al acercarse. |
| `SPIKE_LINKED`   | Par de picos vinculados (A y B) que alternan: cuando A está activo, B está inactivo.             | Comparten un timer. El jugador puede cruzar B cuando A está activo y viceversa.           |

### Nuevas celdas especiales

| Tipo                        | Comportamiento                                                                  | Notas de diseño                                                                                     |
| --------------------------- | ------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `ICE`                     | El jugador desliza 2–3 celdas en la dirección elegida sin poder frenar antes. | Visualmente azul claro. Muy efectivo en Labyrinth — crea momentum forzado.                          |
| `PORTAL_A` / `PORTAL_B` | Celdas enlazadas: entrar en A sale en B (y viceversa).                          | Siempre en pares. El BFS las trata como aristas adicionales para garantizar solubilidad.             |
| `SWITCH`                  | Al pisarlo activa/desactiva una WALL específica en otra celda del maze.        | El par SWITCH↔WALL se asigna en LevelData. El editor visual muestra la vinculación con una línea. |
| `CONVEYOR`                | Empuja al jugador una celda extra en una dirección al terminar el movimiento.  | Dirección fija por celda. Puede empujar hacia paredes (el jugador se detiene) o hacia trampas.      |
| `KEY` / `LOCK_DOOR`     | El jugador recoge KEYs para desbloquear LOCK_DOORs.                             | Alternativa a las puertas normales — requiere exploración activa.                                  |

### Puertas — mecánica definitiva (Opción A implementada)

Las puertas (`DOOR`) actúan como checkpoints obligatorios:

- El EXIT aparece bloqueado hasta que el jugador haya pisado TODAS las puertas del nivel
- HUD muestra contador `🚪 N/N` de puertas activadas
- Cada puerta consume `doorCost = 0.10` de masa al pisarla (sin migaja)
- Diseño visual: la puerta se "abre" visualmente (cambia color) al ser activada
- **Pendiente de implementar**: el sistema de tracking de puertas en `ShrinkMechanic` y el lock visual del EXIT

---

## Localización — Sistema L

### API pública

```csharp
LocalizationManager.Init();
LocalizationManager.SetLanguage("fr");
LocalizationManager.Get("play");
LocalizationManager.CurrentLanguageName;  // "FRANÇAIS", etc.
```

### Claves disponibles

| Clave                                                                                           | Descripción                                                                   |
| ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| `back`, `play`, `settings`, `store`                                                     | Navegación principal                                                          |
| `sfx`, `music`, `movement`, `language`                                                  | Settings                                                                       |
| `noad_name`, `noad_desc`, `full_name`, `full_desc`                                      | Tienda IAP                                                                     |
| `buy`, `owned`, `restore`                                                                 | Acciones tienda                                                                |
| `gameover`, `victory`, `retry`, `watch_ad`, `menu`, `next`, `cont_btn`            | Pantallas resultado                                                            |
| `resume`, `add_size`, `add_time`                                                          | Pausa                                                                          |
| `controls`, `adjust_dpad`, `done`                                                         | Panel de controles D-pad                                                       |
| `world_name`, `world_locked`                                                                | Level Select — mundos (usan `string.Format` con `{0}` = número de mundo) |
| `infinite`, `infinite_locked`, `infinite_locked_desc`, `infinite_locked_buy`            | Modo Infinito bloqueado                                                        |
| `infinite_hud_maze`, `run_over`, `run_mazes`, `run_score`, `run_best`, `play_again` | Modo Infinito HUD y Run Over                                                   |

## Convenciones de código

- Lenguaje: C# exclusivamente
- Comentarios XML en todos los métodos públicos (`/// <summary>`)
- Cero magic numbers — todo valor configurable es `[SerializeField]`
- Singletons: GameManager, AdManager, AudioManager, LevelManager, SaveManager, IAPManager
- `GameData` incluye `DPadSettings dpad` — persiste posición, escala, alpha e `initialized` del D-pad táctil
- Eventos globales estáticos en `Scripts/Events/GameEvents.cs`:
  - `OnLevelComplete`, `OnLevelFail`, `OnDoorOpened`
  - `OnSizeChanged`, `OnMigajaAbsorbed`, `OnNarrowPassageBlocked`
  - `OnStarCollected(int collected, int total)`
  - `OnTimerTick(float remaining)`, `OnTrapActivated(Vector2Int cell, CellType type)`
  - `OnLanguageChanged`
  - `OnPlayerRevived`
- Namespaces: `Shrink.Core`, `Shrink.Maze`, `Shrink.Player`, `Shrink.Enemies`, `Shrink.Level`, `Shrink.UI`, etc.
- Un archivo = una clase
- Input: `Keyboard.current` y `Touchscreen.current` (New Input System)

## Monetización

| Producto          | Precio          | ID               | Desbloquea                                                 |
| ----------------- | --------------- | ---------------- | ---------------------------------------------------------- |
| Juego Completo    | **$2.99** | `full_game`    | Mundo 2, 3 y mundos futuros (incluidos en la misma compra) |
| Sin anuncios      | $1.99           | `no_ads`       | Elimina interstitials para siempre                         |
| Pack Colores      | $0.99           | `color_pack`   | Paletas de color adicionales                               |
| Modo Infinito Pro | $2.99           | `infinite_pro` | Modo Infinito sin necesitar completar Mundo 1              |

- Interstitial AdMob: cada 3 niveles completados (solo jugadores sin `full_game` ni `no_ads`)
- Rewarded: desde pausa (masa/tiempo), máximo 1 por nivel
- `infinite_pro` y `full_game` pueden venderse juntos como bundle si se quiere
- **Precio a $4.99** cuando se lance con Mundo 2 + Mundo 3 completos simultáneamente

## Rendimiento

- Target: 60 fps estable en Android gama media
- Sin allocations en Update — usar pools para migajas y celdas
- Mazes >30×18 se generan en hilo separado via `GenerateAsync`

## Próximo sprint — UGS Stack

Los tres usan el mismo SDK. Instalar desde Package Manager: `com.unity.services.core`, `com.unity.services.authentication`, `com.unity.services.cloudsave`, `com.unity.services.leaderboards`. Activar proyecto en dashboard.unity.com.

### Paso 1 — UGS Authentication (anónima)

- UUID silencioso en el primer arranque, sin login visible para el jugador
- `AuthenticationService.Instance.SignInAnonymouslyAsync()` desde `GameBootstrap` tras `SaveManager.Load()`
- UUID se guarda en `GameData` para asociar Cloud Save y Leaderboard al mismo jugador

### Paso 2 — UGS Cloud Save

- `GameData` serializado como JSON, asociado al UUID del jugador
- Al iniciar: cargar cloud → comparar con local → usar el más reciente (por `GameStats.levelsPlayed` como versión)
- Al completar nivel y al cerrar app: sincronizar
- Cubre: niveles, estrellas, récord infinito, settings. Las IAP NO — se restauran vía `RestorePurchases()`

### Paso 3 — UGS Leaderboards

- Tabla `infinite_leaderboard` — score = mazes × (masa normalizada 0–100)
- Enviar score al terminar run en `InfiniteGameManager`
- Mostrar top 100 + posición del jugador en `RunOverPanel` de `InfiniteHUDController`
- Tabla futura: `daily_challenge` cuando se implemente el reto diario

---

## Sistemas futuros — post UGS

| #  | Sistema             | Descripción                                                                                                   |
| -- | ------------------- | -------------------------------------------------------------------------------------------------------------- |
| 10 | Notificaciones push | Unity Mobile Notifications (local, sin servidor). Recordatorio a las 24h de inactividad.                       |
| 11 | Reto diario         | Maze con semilla = fecha del día, igual para todos. Score por masa + tiempo. Requiere UGS Leaderboards.       |
| 12 | Logros              | Game Center + Google Play Games. "5 niveles sin morir", "30 mazes en Infinito", "todas las estrellas Mundo 1". |

---

## Lo que NO hacer

- No usar assets externos (sprites, fuentes de terceros, modelos)
- No joystick visible en pantalla — el joystick es siempre invisible/flotante. El D-pad táctil SÍ es visible e intencional; no confundir con el joystick flotante
- No mostrar el maze completo durante gameplay (solo en pausa)
- No añadir sistemas fuera del orden listado sin confirmación
- No resumir ni explicar el código entregado — solo código completo y funcional
- No usar `UnityEngine.Input` (legacy) — siempre `UnityEngine.InputSystem`

BoldCrawler_7271
