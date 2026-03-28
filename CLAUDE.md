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
                 Editor/MazeDebugVisualizerEditor.cs
  Player/        SphereController, ShrinkMechanic, Crumb, Star
  Enemies/       EnemyController, PatrolEnemy, TrailEnemy  ← pendiente Sistema 3.5
  Movement/      PlayerMovement
  Camera/        CameraFollow
  UI/            HUDController, PauseMapController, GameResultController, LocalizedText
  Level/         LevelManager, LevelData (ScriptableObject), LevelLoader, LevelTimer
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
| 3 | Movimiento por swipe + cámara | ✅ Completo | SlideToWall + ContinuousSlide + StepByStep |
| 4 | Pausa + HUD | ✅ Completo | HUDController + PauseMapController (sin mapa) |
| 5 | Sistema de niveles y semillas | ✅ Completo | LevelData, LevelManager, LevelLoader, LevelTimer, Trampas |
| 6 | Monetización | ✅ Completo | IAPManager (Unity IAP v5) + AdManager (AdMob) |
| 7 | Juice y sonido | ✅ Completo | AudioManager, playlists aleatorias, SFX por eventos |
| S | SaveManager | ✅ Completo | GameData JSON, LevelRecord, AudioSettings, GameStats |
| L | Localización | ✅ Completo | EN, ES, PT, FR, DE — auto-detect + guardado en SaveManager |
| 8 | UI completa | ✅ Completo | Boot + Menu + LevelSelect + GameResult + Localización |
| 3.5 | Enemigos | ⬜ Pendiente (al final) | Ver sección Enemigos |

## Generación de maze — MazeStyle

```csharp
public enum MazeStyle { Dungeon, Labyrinth, Hybrid }
```

1. EstiloAlgoritmoUsar paraDungeonBSP (cuartos + corredores)Niveles 1–9LabyrinthRecursive Backtracker DFSNiveles 10–30HybridBacktracker + cuartos talladosAlternativa con zonas abiertas

- **Labyrinth es el modo recomendado** para gameplay real — el Smart Slide funciona mejor con corredores de 1 celda de ancho.
- BSP genera cuartos grandes donde el Smart Slide se degrada a step-by-step.
- El maze siempre se valida con BFS antes de devolverse. `ShortestPathLength` queda en `MazeData`.

## Movimiento — PlayerMovement

Tres modos seleccionables desde `LevelLoader` en el Inspector:

| Modo | Comportamiento | Uso |
|------|---------------|-----|
| `SlideToWall` | Smart Slide: para en pared, NARROW e intersecciones | Producción (default) |
| `ContinuousSlide` | Para solo en pared/NARROW; nuevo swipe redirige en el próximo paso | Alternativa a probar |
| `StepByStep` | Una celda por input | Testing con teclado |

**Configuración en LevelLoader** (sección Movimiento en Inspector):
- `Move Time` — segundos por celda para SlideToWall/StepByStep (default `0.10`)
- `Continuous Move Time` — segundos por celda para ContinuousSlide (default `0.18`)
- `Swipe Min Dist` — píxeles mínimos para reconocer swipe (default `40`)

**Player Prefab**: `Assets/_Project/Prefabs/Player.prefab`
- Contiene: `SphereController`, `ShrinkMechanic`, `PlayerMovement`
- `LevelLoader` lo instancia en runtime con `Instantiate(_playerPrefab)`
- Asignar en Inspector de LevelLoader (sección Prefabs)

## Calibración de dificultad — ShrinkMechanic

```
sizePerStep = 0.85 × difficultyFactor ÷ shortestPathLength
```

| difficultyFactor | Significado                    |
| ---------------- | ------------------------------ |
| 0.50             | Tutorial (niveles 1–3)        |
| 0.65             | Aprendizaje (niveles 4–7)     |
| 0.75             | Normal (niveles 8–10)         |
| 0.80             | Exigente (niveles 11–15)      |
| 0.85             | Difícil (niveles 16–20)      |
| 0.90             | Muy difícil (niveles 21–25)  |
| 0.95             | Casi perfecto (niveles 26–30) |
| 0.95→1.0        | Infinito creciente             |

- `autoCalibrate = true` (default): calcula sizePerStep automáticamente según el maze generado.
- En Sistema 5, `difficultyFactor` vendrá del `LevelData` ScriptableObject de cada nivel.
- Los callejones sin salida son gratis si el jugador retrocede completo (migajas recuperan el tamaño exacto).

## Estrellas (recolectables)

- Visual: diamante (cuadrado rotado 45°), color amarillo configurable
- Recompensa: bonus de tamaño (`starSizeBonus`, default `+0.05`)
- Placement: greedy farthest-point sampling sobre celdas con detour score ≥ 2
  - Detour score = `distFromStart + distFromExit - shortestPath` (0 = en el camino directo)
  - Primera estrella: máximo detour score. Siguientes: máxima distancia a las ya colocadas
  - Resultado: distribución uniforme por todo el maze, preferentemente en callejones
- Configuración por nivel: `starCount` y `starSizeBonus` → en Sistema 5 vendrán de `LevelData`

### Garantías actuales

- ✅ **Conectividad**: BFS con `size=1.0` — nunca detrás de NARROW ni en islas desconectadas
- ⚠️ **Costeabilidad**: NO garantizada — es riesgo/recompensa intencional

### Pendiente — garantía de estrellas costeables

Implementar filtro opcional `minAffordableStars: int` para cuando `difficultyFactor ≥ 0.85`:

```
presupuesto_desvíos = 0.85 - (sizePerStep × shortestPathLength)
estrella costeable si: distancia_extra × sizePerStep × 2 ≤ presupuesto_desvíos
```

## Enemigos — diseño (pendiente Sistema 3.5)

Los enemigos se introducen a partir de niveles intermedios. Tocar un enemigo = muerte instantánea.

### Tipos definidos

| Tipo                  | Comportamiento                                      | Efecto adicional                                            |
| --------------------- | --------------------------------------------------- | ----------------------------------------------------------- |
| **PatrolEnemy** | Recorre un segmento fijo de ida y vuelta            | Ninguno                                                     |
| **TrailEnemy**  | Sigue el rastro de migajas del jugador y las devora | Elimina migajas (el jugador no puede recuperar ese tamaño) |
| *(futuros)*         | Perseguidor directo, emboscador, etc.               | TBD                                                         |

### Reglas de diseño

- Los enemigos se mueven por celdas (mismo grid que el jugador)
- No pueden pasar por NARROW si son "grandes" (definir tamaño por tipo)
- El TrailEnemy sigue la lista de celdas con migaja ordenada por antigüedad (la más reciente primero)
- Al devorar una migaja: `MazeRenderer.AbsorbCrumb(cell)` pero sin darle el tamaño al jugador
- Los enemigos deben mostrarse en el mapa de pausa
- Velocidad configurable por tipo (`[SerializeField] float moveInterval`)
- Carpeta: `Scripts/Enemies/`
- Namespace: `Shrink.Enemies`

### Por definir al implementar

- ¿En qué niveles aparece cada tipo?
- ¿Cuántos enemigos por nivel?
- ¿El TrailEnemy respawn de migajas o las elimina permanentemente?
- ¿Hay power-up para ralentizarlos / asustarlos?

## HUD + Mapa en pausa — Sistema 4

### Arquitectura UI

- **Un solo Canvas** en escena con dos paneles: `HUDView` (siempre visible) y `PauseView` (se activa/desactiva).
- Los scripts `HUDController` y `PauseMapController` viven en el Canvas.
- `GameBootstrap` referencia ambos via `[SerializeField]` — NO los crea en runtime.
- Textos usan **TextMeshProUGUI** (`TMP_Text`), no `Text` legacy.

### HUDController (`Scripts/UI/HUDController.cs`)

- **Barra de tamaño** (bottom): `Image` filled horizontal, verde → amarillo → rojo según ratio `(size - MinSize) / rango`
- **Label de pasos** (`_sizeLabel`): muestra `~N` pasos restantes = `(size - MinSize) / sizePerStep`
- **Contador de estrellas** (`_starsLabel`): `"recogidas/total"`
- **Botón pausa** (`_pauseButton`): llama `PauseMapController.Open()`
- Recibe `ShrinkMechanic` en `Initialize` para calcular pasos restantes por nivel

### PauseMapController (`Scripts/UI/PauseMapController.cs`)

- Se activa con botón pausa (HUD) o tecla **Escape**
- **Sin mapa** — el maze es el reto, el jugador usa sus propias migajas como guía
- `Time.timeScale = 0` al abrir, `= 1` al cerrar
- Botón **CONTINUAR** — reanuda el juego
- Botón **AÑADIR MASA** — muestra rewarded ad → da `rewardedSizeBonus` (default `0.15`) de tamaño
- Botón **AÑADIR TIEMPO** — solo visible si el nivel tiene timer → da `rewardedTimeBonus` (default `30s`)
- Los botones de recompensa se desactivan (`interactable = false`) si ya se usó el rewarded en este nivel
- `Initialize(ShrinkMechanic shrink, LevelTimer timer)` — llamar desde LevelLoader

### Jerarquía de escena

```
Canvas (Canvas, CanvasScaler, GraphicRaycaster, HUDController, PauseMapController)
  ├── HUDView
  │   ├── TopBar
  │   │   ├── StarsLabel     (TMP — _starsLabel)
  │   │   └── PauseButton    (Button — _pauseButton)
  │   └── SizeBarContainer
  │       ├── LifeBar        (Image filled — _sizeBarFill)
  │       └── LifeText       (TMP — _sizeLabel)
  └── PauseView              (GameObject — _mapPanel, desactivado por defecto)
      ├── AddSizeButton      (Button — _addSizeButton)
      ├── AddTimeButton      (Button — _addTimeButton, oculto si sin timer)
      └── ResumeButton       (Button — _resumeButton)
```

## Localización — Sistema L

### Arquitectura

- **`LocalizationManager`** — clase estática en `Shrink.Core`. No requiere GameObject. Llamar `Init()` desde `GameBootstrap` tras inicializar `SaveManager`.
- **`LocalizedText`** — componente `[RequireComponent(TMP_Text)]` en `Shrink.UI`. Adjuntar a cualquier TextMeshProUGUI con una `_key` asignada en Inspector. Se suscribe a `GameEvents.OnLanguageChanged` y actualiza automáticamente.

### Idiomas soportados

| Código | Idioma | Auto-detect (SystemLanguage) |
|--------|--------|------------------------------|
| `en` | English (default) | Cualquier otro |
| `es` | Español | `SystemLanguage.Spanish` |
| `pt` | Português | `SystemLanguage.Portuguese` |
| `fr` | Français | `SystemLanguage.French` |
| `de` | Deutsch | `SystemLanguage.German` |

### API pública

```csharp
LocalizationManager.Init();                    // Llamar desde GameBootstrap
LocalizationManager.SetLanguage("fr");         // Cambia idioma y guarda en SaveManager
LocalizationManager.CycleNext();               // Cicla al siguiente idioma
LocalizationManager.Get("play");               // Devuelve texto en idioma activo
LocalizationManager.CurrentLanguageName;       // "FRANÇAIS", "ESPAÑOL", etc.
```

### Claves disponibles

| Clave | Descripción |
|-------|-------------|
| `back`, `play`, `settings`, `store` | Navegación principal |
| `sfx`, `music`, `movement`, `language` | Settings |
| `move_slide`, `move_cont`, `move_step` | Modos de movimiento |
| `noad_name`, `noad_desc`, `full_name`, `full_desc` | Tienda IAP |
| `buy`, `owned`, `restore` | Acciones tienda |
| `gameover`, `victory`, `retry`, `watch_ad`, `menu`, `next`, `cont_btn` | Pantallas resultado |
| `resume`, `add_size`, `add_time` | Pausa |

### Persistencia

- El idioma se guarda en `GameData.settings.language` (string código).
- Al init, si no hay preferencia guardada se auto-detecta desde `Application.systemLanguage`.
- `SaveManager.SaveLanguage(string code)` — método requerido en SaveManager.

### Evento

```csharp
GameEvents.OnLanguageChanged   // Action — emitido por LocalizationManager.SetLanguage()
GameEvents.RaiseLanguageChanged()
```

## GameResultController — Sistema 8 (parcial)

Controla los paneles de victoria y game over en GameScene. Script en `Shrink.UI`.

- **Inicialización**: `Initialize(ShrinkMechanic shrink)` — llamar desde `LevelLoader` al cargar nivel.
- **Victory panel**: muestra 3 estrellas (Image coloreadas), botones Siguiente y Menú.
- **Game Over panel**: botones Reintentar, Ver Anuncio (rewarded) y Menú.
- **Ready panel**: se muestra tras ver el anuncio rewarded — confirma el bonus y tiene botón CONTINUAR.
- `_continueBonus = 0.30f` — masa que se añade al continuar tras anuncio.
- Usa `_forcesPause = true` + `Update` para mantener `Time.timeScale = 0` mientras los paneles están activos (evita el reset de AdMob).
- El callback de AdMob llega en hilo secundario — usa flag `_applyRewardNextFrame` para diferir al hilo principal.

### Jerarquía sugerida (GameScene Canvas)

```
Canvas
  ├── HUDView           (HUDController)
  ├── PauseView         (PauseMapController)
  ├── VictoryPanel      (GameResultController._victoryPanel)
  │   ├── Stars[]       (3× Image — _victoryStars)
  │   ├── NextButton
  │   └── MenuButton
  ├── GameOverPanel     (GameResultController._gameOverPanel)
  │   ├── RetryButton
  │   ├── WatchAdButton
  │   └── MenuButton
  └── ReadyPanel        (GameResultController._readyPanel)
      ├── BonusText     (TMP — _bonusText)
      └── ContinueButton
```

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
  - `OnLanguageChanged` — emitido por `LocalizationManager.SetLanguage()`
- Namespaces: `Shrink.Core`, `Shrink.Maze`, `Shrink.Player`, `Shrink.Enemies`, etc.
- Un archivo = una clase (sin excepciones)
- Nombres de archivo en PascalCase, exactamente igual al nombre de la clase
- Input: `Keyboard.current` y `Touchscreen.current` (New Input System)

## Tipos de celda (MazeData)

```csharp
public enum CellType
{
    WALL, PATH, ROOM, CORRIDOR, DOOR,
    START, EXIT, NARROW_06, NARROW_04,
    TRAP_ONESHOT, TRAP_DRAIN          // ← Sistema de trampas (pendiente)
}
```

## Trampas — diseño

Celdas de suelo especiales que actúan como trampas al ser pisadas. Se insertan en el maze igual que puertas y narrow.

### Tipos implementados (pendiente)

| Tipo | Comportamiento | Efecto |
|---|---|---|
| `TRAP_ONESHOT` | Se pisa una vez → se convierte en WALL permanentemente | Cierra el camino de regreso. Penaliza no planear la ruta. Interacción clave con migajas: si pisas la trampa, tu ruta de regreso queda cortada |
| `TRAP_DRAIN` | Drena tamaño al pisarla. Se puede volver a pisar (cobra cada vez) | Sin migaja depositada. Costo configurable (`trapDrainCost`, default `0.08`). Penaliza rutas que cruzan la zona repetidamente |

### Tipo pendiente para futuro

| Tipo | Comportamiento |
|---|---|
| `TRAP_TOGGLE` | Al pisarla levanta una WALL temporal durante N segundos bloqueando el paso hacia atrás. Crea puertas de un solo sentido temporales. Para mundos avanzados |

### Reglas de diseño

- Las trampas son siempre visibles (el jugador puede verlas antes de pisarlas) — el reto es de planning, no de sorpresa
- **Colores**: `TRAP_ONESHOT` = naranja `(0.95, 0.50, 0.10)` | `TRAP_DRAIN` = rojo oscuro `(0.70, 0.10, 0.30)`
- `TRAP_ONESHOT` al activarse: cambia a WALL en `MazeData.Grid` y destruye su visual
- `TRAP_DRAIN` no deposita migaja — la pérdida es permanente igual que las puertas. Costo: `trapDrainCost = 0.08`
- El BFS de validación se ejecuta con las trampas en estado inicial (todas walkables) → nivel siempre soluble
- Configuración por nivel: `trapOneshotCount` y `trapDrainCount` en `LevelData`
- Namespace: `Shrink.Maze` (son tipos de celda, no entidades separadas)
- Introducción: `TRAP_DRAIN` desde nivel 8, `TRAP_ONESHOT` desde nivel 12

### Mejora futura — trampas cerca de estrellas

Colocar `TRAP_DRAIN` preferentemente en celdas adyacentes a estrellas para que recogerlas tenga un coste real. Actualmente no es posible sin refactorizar porque las trampas se insertan en `MazeGenerator` y las estrellas se colocan después en `MazeRenderer` (no comparten contexto). Implementar cuando se revise el pipeline de generación.

## Mecánica central — reglas inamovibles

- Tamaño inicial esfera: `1.0`
- Tamaño mínimo antes de muerte: `0.15`
- Rango usable: `0.85`
- `NARROW_06`: requiere tamaño < 0.6
- `NARROW_04`: requiere tamaño < 0.4
- Migajas: una por celda, recuperan exactamente el tamaño perdido al ser reabsorbidas
- Puertas: consumen tamaño permanentemente (sin migaja depositada), costo default `0.10`
- El maze siempre tiene solución — validado con BFS, `ShortestPathLength` guardado en `MazeData`
- Tocar un enemigo = muerte instantánea (fire `OnLevelFail`)

## Niveles

- Mundo 1 (1–10): 20×12, sin tiempo, sin puertas, sin enemigos. NARROW_06 desde nivel 5. TRAP_DRAIN desde nivel 8. MazeStyle: Dungeon
- Mundo 2 (11–20): 25×15, sin tiempo, 1–2 puertas, PatrolEnemy desde nivel 12. NARROW_04 desde nivel 15. TRAP_ONESHOT desde nivel 12. MazeStyle: Labyrinth
- Mundo 3 (21–30): 35×20 → 40×24, con timer, 2–4 puertas, TrailEnemy desde nivel 22 (bloqueado tras IAP). MazeStyle: Labyrinth
- Modo infinito: semillas aleatorias, dificultad creciente, todos los tipos de enemigo. MazeStyle: Labyrinth

## Monetización

| Producto          | Precio | ID sugerido      |
| ----------------- | ------ | ---------------- |
| Sin anuncios      | $1.99  | `no_ads`       |
| Pack Colores      | $0.99  | `color_pack`   |
| Mundo 3           | $1.99  | `world_3`      |
| Modo Infinito Pro | $2.99  | `infinite_pro` |

- Interstitial AdMob: cada 3 niveles completados (se dispara en `HandleLevelComplete`)
- Rewarded AdMob: disponible desde el panel de pausa → Añadir masa (+0.15) o Añadir tiempo (+30s). Máximo 1 por nivel.
- Si el jugador tiene `no_ads`: `AdsDisabled()` devuelve true y no se muestra ningún anuncio
- `AdManager.IsRewardedAvailable` — consultar antes de mostrar botones de recompensa en UI
- IDs configurados en `AdManager.cs` con `#if UNITY_IOS / #else` para iOS/Android

## Rendimiento

- Target: 60 fps estable en Android gama media
- Sin allocations en Update — usar pools para migajas y celdas
- El maze se genera en un hilo separado si supera 30×18 (via `GenerateAsync`)

## Ideas de diseño visual — pendientes para el final

### Player como puñado de arena (idea pendiente)
- En lugar de esfera sólida, el jugador sería una masa asimétrica de partículas de arena
- Al moverse deja granos de arena (migajas) en lugar de círculos
- El desgaste sería visualmente literal: pierdes granos de arena
- **Alternativa intermedia**: mantener la forma circular base + partículas de arena decorativas alrededor (legibilidad de tamaño sin perder la estética)
- **Bloqueante**: legibilidad de tamaño en pantalla pequeña y rendimiento en mobile
- Revisar cuando todos los sistemas estén completos y el juego sea jugable

## Lo que NO hacer

- No usar assets externos (sprites, fuentes de terceros, modelos)
- No joystick visible en pantalla durante gameplay
- No mostrar el maze completo durante gameplay (solo en pausa)
- No añadir sistemas fuera del orden listado sin confirmación
- No resumir ni explicar el código entregado — solo código completo y funcional
- No usar `UnityEngine.Input` (legacy) — siempre `UnityEngine.InputSystem`
