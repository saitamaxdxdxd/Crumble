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
  Core/          ShapeFactory, GameBootstrap (test), GameManager (pendiente Sistema 5)
  Events/        GameEvents.cs — eventos globales estáticos
  Maze/          MazeGenerator, MazeData, CellType, MazeRenderer
                 Editor/MazeDebugVisualizerEditor.cs
  Player/        SphereController, ShrinkMechanic, Crumb, Star
  Enemies/       EnemyController, PatrolEnemy, TrailEnemy  ← pendiente Sistema 3.5
  Movement/      PlayerMovement
  Camera/        CameraFollow
  UI/            HUDController, PauseMapController
  Level/         LevelManager, LevelData (ScriptableObject), LevelLoader  ← pendiente Sistema 5
  Monetization/  AdManager, IAPManager                                    ← pendiente Sistema 6
  Audio/         AudioManager                                              ← pendiente Sistema 7

ScriptableObjects/
  Levels/        30 LevelData assets (Level_01 … Level_30)               ← pendiente Sistema 5
  Colors/        Paletas de color (IAP)

Prefabs/         pendiente
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
| 3 | Movimiento por swipe + cámara | ✅ Completo | Smart Slide + WASD para testing |
| 3.5 | Enemigos | ⬜ Pendiente | Ver sección Enemigos |
| 4 | Mapa en pausa + HUD | ✅ Completo | HUDController + PauseMapController |
| 5 | Sistema de niveles y semillas | ⬜ Pendiente | |
| 6 | Monetización | ⬜ Pendiente | |
| 7 | Juice y sonido | ⬜ Pendiente | |
| 8 | UI completa | ⬜ Pendiente | |

## Generación de maze — MazeStyle

```csharp
public enum MazeStyle { Dungeon, Labyrinth, Hybrid }
```

| Estilo | Algoritmo | Usar para |
|--------|-----------|-----------|
| Dungeon | BSP (cuartos + corredores) | Niveles 1–9 |
| Labyrinth | Recursive Backtracker DFS | Niveles 10–30 |
| Hybrid | Backtracker + cuartos tallados | Alternativa con zonas abiertas |

- **Labyrinth es el modo recomendado** para gameplay real — el Smart Slide funciona mejor con corredores de 1 celda de ancho.
- BSP genera cuartos grandes donde el Smart Slide se degrada a step-by-step.
- El maze siempre se valida con BFS antes de devolverse. `ShortestPathLength` queda en `MazeData`.

## Movimiento — PlayerMovement

Modo activo: **SlideToWall con Smart Slide**

Reglas de parada al deslizar:
1. Siguiente celda es WALL o fuera de límites
2. Pasaje estrecho bloqueado por tamaño actual
3. Celda actual tiene salidas perpendiculares (intersección)
4. Celda actual es EXIT

Campo `Mode` en Inspector permite cambiar a `StepByStep` para testing con teclado.

## Calibración de dificultad — ShrinkMechanic

```
sizePerStep = 0.85 × difficultyFactor ÷ shortestPathLength
```

| difficultyFactor | Significado |
|---|---|
| 0.50 | Tutorial (niveles 1–3) |
| 0.65 | Aprendizaje (niveles 4–7) |
| 0.75 | Normal (niveles 8–10) |
| 0.80 | Exigente (niveles 11–15) |
| 0.85 | Difícil (niveles 16–20) |
| 0.90 | Muy difícil (niveles 21–25) |
| 0.95 | Casi perfecto (niveles 26–30) |
| 0.95→1.0 | Infinito creciente |

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

| Tipo | Comportamiento | Efecto adicional |
|------|---------------|-----------------|
| **PatrolEnemy** | Recorre un segmento fijo de ida y vuelta | Ninguno |
| **TrailEnemy** | Sigue el rastro de migajas del jugador y las devora | Elimina migajas (el jugador no puede recuperar ese tamaño) |
| *(futuros)* | Perseguidor directo, emboscador, etc. | TBD |

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
- Crea en runtime: cámara ortográfica secundaria → `RenderTexture` → asignada al `RawImage` del Inspector
- `FitMapImage()`: ajusta `sizeDelta` del `RawImage` en runtime según aspect ratio del maze y tamaño de pantalla (parámetros: `maxScreenFraction=0.82`, `bottomReserve=120`)
- Dots en world space (visibles solo a la cámara del mapa): jugador (azul), EXIT (rojo)
- `Time.timeScale = 0` al abrir, `= 1` al cerrar
- Botón **CONTINUAR** (verde, centrado abajo)

### Jerarquía de escena
```
Canvas (Canvas, CanvasScaler, GraphicRaycaster, HUDController, PauseMapController)
  ├── HUDView
  │   ├── TopBar
  │   │   ├── StartIconText  (TMP — _starsLabel)
  │   │   └── PauseButton    (Button — _pauseButton)
  │   └── SizeBarContainer
  │       ├── LifeBar        (Image filled — _sizeBarFill)
  │       └── LifeText       (TMP — _sizeLabel)
  └── PauseView              (GameObject — _mapPanel, desactivado por defecto)
      ├── MapImage           (RawImage — _mapImage)
      └── ResumeButton       (Button — _resumeButton)
```

## Convenciones de código

- Lenguaje: C# exclusivamente
- Comentarios XML en todos los métodos públicos (`/// <summary>`)
- Cero magic numbers — todo valor configurable es `[SerializeField]`
- Singletons: GameManager, AdManager, AudioManager, LevelManager
- Eventos globales estáticos en `Scripts/Events/GameEvents.cs`:
  - `OnLevelComplete`, `OnLevelFail`, `OnDoorOpened`
  - `OnSizeChanged`, `OnMigajaAbsorbed`, `OnNarrowPassageBlocked`
  - `OnStarCollected(int collected, int total)`
- Namespaces: `Shrink.Core`, `Shrink.Maze`, `Shrink.Player`, `Shrink.Enemies`, etc.
- Un archivo = una clase (sin excepciones)
- Nombres de archivo en PascalCase, exactamente igual al nombre de la clase
- Input: `Keyboard.current` y `Touchscreen.current` (New Input System)

## Tipos de celda (MazeData)
```csharp
public enum CellType
{
    WALL, PATH, ROOM, CORRIDOR, DOOR,
    START, EXIT, NARROW_06, NARROW_04
}
```

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
- Mundo 1 (1–10): 20×12, sin tiempo, sin puertas, sin enemigos. NARROW_06 desde nivel 5. MazeStyle: Dungeon
- Mundo 2 (11–20): 25×15, sin tiempo, 1–2 puertas, PatrolEnemy desde nivel 12. NARROW_04 desde nivel 15. MazeStyle: Labyrinth
- Mundo 3 (21–30): 35×20 → 40×24, con timer, 2–4 puertas, TrailEnemy desde nivel 22 (bloqueado tras IAP). MazeStyle: Labyrinth
- Modo infinito: semillas aleatorias, dificultad creciente, todos los tipos de enemigo. MazeStyle: Labyrinth

## Monetización
| Producto | Precio | ID sugerido |
|----------|--------|-------------|
| Sin anuncios | $1.99 | `no_ads` |
| Pack Colores | $0.99 | `color_pack` |
| Mundo 3 | $1.99 | `world_3` |
| Modo Infinito Pro | $2.99 | `infinite_pro` |

- Interstitial AdMob: cada 3 niveles completados, solo en pantalla de nivel completado
- Rewarded AdMob: game over → continuar con 50% tamaño / +30 segundos. Máximo 1 por nivel.

## Rendimiento
- Target: 60 fps estable en Android gama media
- Sin allocations en Update — usar pools para migajas y celdas
- El maze se genera en un hilo separado si supera 30×18 (via `GenerateAsync`)

## Lo que NO hacer
- No usar assets externos (sprites, fuentes de terceros, modelos)
- No joystick visible en pantalla durante gameplay
- No mostrar el maze completo durante gameplay (solo en pausa)
- No añadir sistemas fuera del orden listado sin confirmación
- No resumir ni explicar el código entregado — solo código completo y funcional
- No usar `UnityEngine.Input` (legacy) — siempre `UnityEngine.InputSystem`
