# CLAUDE.md — Shrink (Unity 6)

## Proyecto

Juego móvil puzzle maze 2D top-down para iOS y Android.
Motor: Unity 6 | Resolución: 1920x1080 landscape | Estilo: minimalista (líneas y círculos, sin assets externos)
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
  Enemies/       EnemyController, PatrolEnemy, TrailEnemy
  Movement/      PlayerMovement
  Camera/        CameraFollow
  UI/            HUDController, PauseMapController, GameResultController, LocalizedText
  Level/         LevelManager, LevelData (ScriptableObject), LevelLoader, LevelTimer,
                 CellOverride, EnemySpawn
  Monetization/  AdManager, IAPManager
  Audio/         AudioManager

ScriptableObjects/
  Levels/        30 LevelData assets (Level_01 … Level_30)
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

| # | Sistema | Estado | Notas |
|---|---------|--------|-------|
| 1 | Generación procedural de maze | ✅ Completo | BSP + Labyrinth + Hybrid |
| 2 | Esfera y mecánica de desgaste | ✅ Completo | Migajas, puertas, estrellas, calibración auto |
| 3 | Movimiento — joystick flotante | ✅ Completo | Floating joystick invisible, re-anclaje dinámico, velocidad inversamente proporcional al tamaño |
| 3.5 | Enemigos / Mobs | ✅ Completo | PatrolEnemy, TrailEnemy, spawns manuales en editor |
| 4 | Pausa + HUD | ✅ Completo | HUDController + PauseMapController (Continuar, Retry, Menú, recompensas) |
| 5 | Sistema de niveles y semillas | ✅ Completo | LevelData, LevelManager, LevelLoader, LevelTimer, Trampas, Picos |
| 6 | Monetización | ✅ Completo | IAPManager (Unity IAP v5) + AdManager (AdMob) |
| 7 | Juice y sonido | ✅ Completo | AudioManager, playlists aleatorias, SFX por eventos |
| S | SaveManager | ✅ Completo | GameData JSON, LevelRecord, AudioSettings, GameStats |
| L | Localización | ✅ Completo | EN, ES, PT, FR, DE — auto-detect + guardado en SaveManager |
| 8 | UI completa | ✅ Completo | Boot + Menu + LevelSelect + GameResult + Localización |
| E | Editor visual de niveles | ✅ Completo | Ver sección Editor Visual |
| 9 | Modo Infinito | ✅ Completo | InfiniteGameManager + InfiniteHUDController + InfiniteScene — ver sección Modo Infinito |
| F | Mecánicas futuras | ⬜ Backlog | Enemigos adicionales, trampas avanzadas, celdas especiales — ver sección Backlog |

## Generación de maze — MazeStyle

```csharp
public enum MazeStyle { Dungeon, Labyrinth, Hybrid }
```

| Estilo | Algoritmo | Usar para |
|--------|-----------|-----------|
| Dungeon | BSP (cuartos + corredores) | Niveles 1–9, Infinito mazes 1–6 |
| Labyrinth | Recursive Backtracker DFS | Niveles 10–30, Infinito mazes 17+ |
| Hybrid | Backtracker + cuartos tallados | Infinito mazes 7–16 |

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

**Teclado (testing):** mantener W/A/S/D o flechas = movimiento continuo.

**Player Prefab**: `Assets/_Project/Prefabs/Player.prefab`
- Contiene: `SphereController`, `ShrinkMechanic`, `PlayerMovement`
- `LevelLoader` lo instancia en runtime con `Instantiate(_playerPrefab)`

## Calibración de dificultad — ShrinkMechanic

```
sizePerStep = 0.85 × difficultyFactor ÷ shortestPathLength
```

| difficultyFactor | Significado |
|------------------|-------------|
| 0.50 | Tutorial (niveles 1–3) |
| 0.65 | Aprendizaje (niveles 4–7) |
| 0.75 | Normal (niveles 8–10) |
| 0.80 | Exigente (niveles 11–15) |
| 0.85 | Difícil (niveles 16–20) |
| 0.90 | Muy difícil (niveles 21–25) |
| 0.95 | Casi perfecto (niveles 26–30) |
| 0.95→1.0 | Infinito creciente |

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

| Tipo | Comportamiento | Color |
|------|---------------|-------|
| **PatrolEnemy** | Recorre un segmento fijo de ida y vuelta, rebota en paredes | Naranja `(1, 0.30, 0.10)` |
| **TrailEnemy** | BFS hacia la migaja más reciente (`CrumbOrder.Last`), la devora al llegar | Naranja (mismo base) |

### Spawning
- **Manual (editor visual)**: `LevelData.manualEnemySpawns` — lista de `EnemySpawn { cell, type, patrolDir }`. Si hay entradas, ignora los contadores.
- **Automático**: `patrolEnemyCount` y `trailEnemyCount` en `LevelData`. Posiciones aleatorias con seed reproducible, distancia Manhattan ≥ 5 del START.
- Todos los enemigos se destruyen en `LevelLoader.UnloadCurrent()`.

### Implementación base
- `EnemyController` abstract: `MoveLoop()` coroutine, `CanEnter()`, `BuildVisual()`, colisión en `Update()`
- `Initialize(MazeRenderer, SphereController, Vector2Int startCell)` — llamar tras instanciar
- `PatrolEnemy.InitializePatrol(renderer, player, cell, dir)` — acepta dirección explícita
- `ChooseNextCell()` abstract — cada subclase define su comportamiento
- `OnArrivedAtCell(cell)` virtual — base devora migaja; override para efectos extra

### Introducción por nivel
- PatrolEnemy: desde nivel 12 (Mundo 2)
- TrailEnemy: desde nivel 22 (Mundo 3, bloqueado tras IAP `world_3`)

## Trampas — diseño

Celdas de suelo especiales. Siempre visibles — el reto es de planning, no de sorpresa.

### Implementadas

| Tipo | Comportamiento | Color |
|------|---------------|-------|
| `TRAP_DRAIN` | Drena masa al pisarla. Cobra cada vez. Sin migaja. | Rojo oscuro `(0.70, 0.10, 0.30)` |
| `TRAP_ONESHOT` | Se pisa una vez → WALL permanente. Bloquea el regreso. | Naranja `(0.95, 0.50, 0.10)` |
| `SPIKE` | Muerte instantánea al pisar (igual que tocar enemigo). | Rojo `(0.90, 0.05, 0.05)` |

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

## Niveles

- **Mundo 1** (1–10): 20×12, sin timer, sin puertas, sin enemigos. NARROW_06 desde nivel 5. TRAP_DRAIN desde nivel 8. `MazeStyle.Dungeon`
- **Mundo 2** (11–20): 25×15, sin timer, 1–2 puertas. PatrolEnemy desde nivel 12. NARROW_04 desde nivel 15. TRAP_ONESHOT desde nivel 12. `MazeStyle.Labyrinth`
- **Mundo 3** (21–30): 35×20→40×24, con timer, 2–4 puertas. TrailEnemy desde nivel 22. Bloqueado tras IAP `world_3`. `MazeStyle.Labyrinth`
- **Modo Infinito**: tras completar nivel 30 o IAP `infinite_pro`. Ver sección siguiente.

## Modo Infinito — Sistema 9 ✅

Se desbloquea completando **15 niveles** (gratis) o comprando `infinite_pro` ($2.99) o `full_game`.
Escena: `InfiniteScene`. Scripts: `InfiniteGameManager` + `InfiniteHUDController`.

### Mecánica central
- La **masa NO se reinicia** entre mazes — al salir del EXIT entras al siguiente con la masa que tengas
- Cada maze completado da un bonus de masa: `+0.04` (ajustable en Inspector de `InfiniteGameManager`)
- Si la masa llega a `0.15` → fin del run, score guardado en `GameStats.infiniteRecord`
- Score = `mazes × (masaNormalizada 0–100)` — ejemplo: 12 mazes con 60% masa = 720 pts

### Escalado real (implementado en `InfiniteGameManager.BuildLevelData()`)
| Maze | Tamaño | Estilo | Estrellas | Elementos nuevos |
|------|--------|--------|-----------|-----------------|
| 1–3 | 20×12 | Dungeon | 6 × 0.09 | — Solo exploración |
| 4–6 | 20×12 | Dungeon | 6 × 0.09 | PatrolEnemy (1) |
| 7–8 | 20×12 | Dungeon | 6 × 0.09 | TrailEnemy (1) — contrarresta backtracking |
| 9–10 | 25×15 | Hybrid | 5 × 0.07 | NARROW_06, TRAP_DRAIN, puertas |
| 11–16 | 25–30×15–18 | Hybrid | 5 × 0.07 | TRAP_ONESHOT, más enemigos |
| 17–21 | 35×20 | Hybrid→Labyrinth | 3 × 0.05 | NARROW_04, SPIKE, TrailEnemy (2) |
| 22+ | 40–45×24–28 | Labyrinth | 3 × 0.05 | Timer (90s→45s), dificultad → 1.0 |

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
  └── PauseView              (GameObject — _mapPanel, desactivado por defecto)
      ├── ResumeButton       (Button — _resumeButton)
      ├── RetryButton        (Button — _retryButton)
      ├── MenuButton         (Button — _menuButton)
      ├── AddSizeButton      (Button — _addSizeButton)
      └── AddTimeButton      (Button — _addTimeButton, oculto si sin timer)
```

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
- **Enemigos**: `→ Patrulla H`, `↑ Patrulla V`, `◎ Rastreador`
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

### Nuevos tipos de enemigo

| Tipo | Comportamiento | Efecto especial | Introducción sugerida |
|------|---------------|-----------------|----------------------|
| **ChaserEnemy** | BFS directo hacia el jugador. Rápido, predecible. | Ninguno — la amenaza es la persecución pura | Nivel 18 |
| **AmbushEnemy** | Estático hasta que el jugador entra en radio N. Persigue brevemente, vuelve a su post. | Obliga a planificar rutas cerca del ambush. Se distingue visualmente del suelo | Nivel 20 |
| **GhostEnemy** | Atraviesa paredes (ignora WALL al moverse). Solo el jugador puede bloquearlo usando NARROW. | Obliga a buscar pasajes estrechos como refugio | Nivel 25 |
| **MirrorEnemy** | Se mueve en dirección opuesta al jugador (player va →, mirror va ←). Mismo grid. | Crea rutas forzadas simétricas — si vas a un callejón, él también va | Modo Infinito |

**Reglas base (heredadas de EnemyController):**
- Todos devoran migajas al pasar
- Todos matan al contacto
- GhostEnemy necesita `CanEnterGhost()` que ignora WALL

### Nuevos tipos de trampa

| Tipo | Comportamiento | Coste / Duración | Introducción |
|------|---------------|-----------------|--------------|
| `TRAP_SLOW` | Al pisarla, el moveTime se multiplica por 2 durante N segundos | Duración: 4s. Sin pérdida de masa directa — te hace más lento y fácil de atrapar | Nivel 14 |
| `TRAP_INVERT` | Invierte los controles del joystick durante N segundos | Duración: 3s. El reto es navegar con izquierda=derecha, arriba=abajo | Nivel 19 |
| `TRAP_TELEPORT` | Teletransporta al jugador a una celda aleatoria walkable | Instant. La celda destino siempre es alcanzable pero puede estar lejos del EXIT | Nivel 23 |
| `TRAP_CONTINUOUS` | Drena masa por segundo mientras el jugador está PARADO sobre ella. No cobra al moverse. | Rate: `0.02/s`. Penaliza detenerse a pensar en zona peligrosa | Nivel 16 |
| `TRAP_TOGGLE` | Al pisarla, levanta un WALL temporal bloqueando el paso detrás. Dura N segundos. | Duración: 6s. Crea puertas de un solo sentido temporales | Nivel 22 |

**Reglas comunes:**
- Siempre visibles — colores distintos entre tipos
- Los efectos de tiempo (`SLOW`, `INVERT`) usan una cola de efectos activos, no se cancelan entre sí
- `TRAP_CONTINUOUS` requiere que `PlayerMovement` exponga un flag `IsMoving` — drena solo si `!IsMoving`
- Los efectos de control (`INVERT`) se aplican en `PlayerMovement.ReadJoystickInput()` como multiplicador de dirección `-1`

### Nuevas variantes de pico

| Tipo | Comportamiento | Implementación |
|------|---------------|---------------|
| `SPIKE_TIMED` | Aparece y desaparece en ciclo (e.g. 2s activo, 2s inactivo). Visible siempre, color indica estado. | MonoBehaviour con coroutine alterna `Data.Grid[x,y]` entre SPIKE y PATH |
| `SPIKE_PRESSURE` | Invisible hasta que el jugador entra en celda adyacente. Entonces revela con animación flash. | Se registra en MazeRenderer como overlay oculto. `ShrinkMechanic` lo revela al acercarse. |
| `SPIKE_LINKED` | Par de picos vinculados (A y B) que alternan: cuando A está activo, B está inactivo. | Comparten un timer. El jugador puede cruzar B cuando A está activo y viceversa. |

### Nuevas celdas especiales

| Tipo | Comportamiento | Notas de diseño |
|------|---------------|-----------------|
| `ICE` | El jugador desliza 2–3 celdas en la dirección elegida sin poder frenar antes. | Visualmente azul claro. Muy efectivo en Labyrinth — crea momentum forzado. |
| `PORTAL_A` / `PORTAL_B` | Celdas enlazadas: entrar en A sale en B (y viceversa). | Siempre en pares. El BFS las trata como aristas adicionales para garantizar solubilidad. |
| `SWITCH` | Al pisarlo activa/desactiva una WALL específica en otra celda del maze. | El par SWITCH↔WALL se asigna en LevelData. El editor visual muestra la vinculación con una línea. |
| `CONVEYOR` | Empuja al jugador una celda extra en una dirección al terminar el movimiento. | Dirección fija por celda. Puede empujar hacia paredes (el jugador se detiene) o hacia trampas. |
| `KEY` / `LOCK_DOOR` | El jugador recoge KEYs para desbloquear LOCK_DOORs. | Alternativa a las puertas normales — requiere exploración activa. |

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
| Clave | Descripción |
|-------|-------------|
| `back`, `play`, `settings`, `store` | Navegación principal |
| `sfx`, `music`, `movement`, `language` | Settings |
| `noad_name`, `noad_desc`, `full_name`, `full_desc` | Tienda IAP |
| `buy`, `owned`, `restore` | Acciones tienda |
| `gameover`, `victory`, `retry`, `watch_ad`, `menu`, `next`, `cont_btn` | Pantallas resultado |
| `resume`, `add_size`, `add_time` | Pausa |

## Convenciones de código

- Lenguaje: C# exclusivamente
- Comentarios XML en todos los métodos públicos (`/// <summary>`)
- Cero magic numbers — todo valor configurable es `[SerializeField]`
- Singletons: GameManager, AdManager, AudioManager, LevelManager, SaveManager, IAPManager
- Eventos globales estáticos en `Scripts/Events/GameEvents.cs`:
  - `OnLevelComplete`, `OnLevelFail`, `OnDoorOpened`
  - `OnSizeChanged`, `OnMigajaAbsorbed`, `OnNarrowPassageBlocked`
  - `OnStarCollected(int collected, int total)`
  - `OnTimerTick(float remaining)`, `OnTrapActivated(Vector2Int cell, CellType type)`
  - `OnLanguageChanged`
- Namespaces: `Shrink.Core`, `Shrink.Maze`, `Shrink.Player`, `Shrink.Enemies`, `Shrink.Level`, `Shrink.UI`, etc.
- Un archivo = una clase
- Input: `Keyboard.current` y `Touchscreen.current` (New Input System)

## Monetización

| Producto | Precio | ID |
|----------|--------|----|
| Sin anuncios | $1.99 | `no_ads` |
| Pack Colores | $0.99 | `color_pack` |
| Mundo 3 | $1.99 | `world_3` |
| Modo Infinito Pro | $2.99 | `infinite_pro` |

- Interstitial AdMob: cada 3 niveles completados
- Rewarded: desde pausa (masa/tiempo), máximo 1 por nivel
- `infinite_pro` desbloquea Modo Infinito sin necesitar completar nivel 30

## Rendimiento

- Target: 60 fps estable en Android gama media
- Sin allocations en Update — usar pools para migajas y celdas
- Mazes >30×18 se generan en hilo separado via `GenerateAsync`

## Diseño visual — tema definitivo: LLAMA DE FUEGO

**Decisión tomada.** El personaje es una llama/bola de fuego. Todo el theming gira alrededor de fuego vs. agua/viento. Implementar cuando los 30 niveles estén curados y el gameplay sea sólido.

### Personaje — Llama
- Círculo naranja/amarillo con glow (segundo círculo concéntrico más grande, color más tenue)
- Al perder masa: color vira hacia rojo oscuro (agonizante). Al mínimo: casi extinguiéndose.

### Migajas — Brasas
- Puntos pequeños naranja/rojo tenue — brasas que quedó la llama al pasar
- Al reabsorberse: flash breve de color cálido

### EXIT — Hoguera / Fogata
- Círculo grande amarillo/naranja con líneas irradiando — la llama busca unirse a algo más grande

### Paredes — Carbón / Piedra oscura
- Fondo negro `(0.05, 0.05, 0.07)`, paredes gris carbón `(0.15, 0.14, 0.13)`
- Contraste alto con la llama naranja — muy legible en pantalla pequeña

### SPIKE — Charco de agua
- Círculo azul con ondas concéntricas (alpha bajo) — el agua apaga la llama
- Muerte instantánea: la llama se apaga

### Recolectables — Reemplazar estrellas
- Las estrellas no tienen sentido temático con fuego
- **Candidatos**: chispas grandes (estrella de fuego), leños/troncos (combustible), velas pequeñas
- **Decisión pendiente**: elegir entre chispa, leño o vela

### Enemigos — Agua y viento
- **PatrolEnemy**: gota de agua o nube de lluvia — azul, patrulla fija
- **TrailEnemy**: corriente de viento — blanco/celeste, persigue brasas
- **ChaserEnemy** (futuro): chorro de agua directo — azul intenso

### Trampas recontextualizadas
- `TRAP_DRAIN`: charco permanente — la llama se moja cada vez que pasa
- `TRAP_ONESHOT`: placa de hielo — se derrite al pisar pero deja hueco (WALL)
- `TRAP_SLOW` (futuro): barro mojado
- `TRAP_INVERT` (futuro): torbellino de viento

### Paleta base
| Elemento | Color |
|----------|-------|
| Fondo | `(0.05, 0.05, 0.07)` |
| Paredes | `(0.15, 0.14, 0.13)` |
| Llama llena | `(1.0, 0.55, 0.05)` |
| Llama mínima | `(0.7, 0.10, 0.05)` |
| Brasas | `(0.9, 0.35, 0.05)` |
| Hoguera EXIT | `(1.0, 0.85, 0.20)` |
| Agua/SPIKE | `(0.10, 0.45, 0.90)` |
| Chispa recolectable | `(1.0, 0.95, 0.60)` |

### Notas de implementación
- Todo con `ShapeFactory` — círculos y cuadrados, sin sprites externos
- Glow de la llama: segundo `SpriteRenderer` color más claro, `sortingOrder` menor, escala 1.3×
- Ondas del charco: dos círculos concéntricos con alpha bajo

---

## Lo que NO hacer

- No usar assets externos (sprites, fuentes de terceros, modelos)
- No joystick visible en pantalla durante gameplay
- No mostrar el maze completo durante gameplay (solo en pausa)
- No añadir sistemas fuera del orden listado sin confirmación
- No resumir ni explicar el código entregado — solo código completo y funcional
- No usar `UnityEngine.Input` (legacy) — siempre `UnityEngine.InputSystem`
