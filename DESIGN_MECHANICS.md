# DESIGN_MECHANICS.md — Shrink

Mecánicas confirmadas, ideas en cola y conceptos para explorar.
No son promesas de implementación — son decisiones de diseño a validar cuando llegue su momento.

---

## Mundo 2 — Confirmado (niveles 16–30)

### WALL_TIMED
Paredes que alternan entre WALL y PATH en un ciclo de tiempo.

- **Parámetros por celda:** `period` (duración del ciclo), `openRatio` (fracción abierta), `phaseOffset` (dónde arranca en el ciclo)
- Parpadean antes de cambiar de estado — el jugador siempre tiene aviso visual
- Si el jugador queda atrapado dentro cuando se cierra → muerte instantánea
- Los enemigos también respetan el estado actual — un PatrolEnemy no puede cruzar una WALL_TIMED cerrada
- **Introducción:** nivel 16 (una sola, ciclo lento 10s) → nivel 18+ en secuencia/cascada → nivel 22+ combinado con AmbushEnemy

### BOUNCE_WALL
Pared que en vez de detener al jugador, lo expulsa una celda en dirección opuesta.

- Si la celda de destino tras el rebote es WALL → el jugador se queda en su lugar
- El rebote **no cuesta masa** (movimiento involuntario)
- Todas las reglas normales aplican en la celda de aterrizaje (trampa, migaja, enemigo)
- Los enemigos también rebotan — crea situaciones interesantes con PatrolEnemy
- **Introducción:** nivel 17 → nivel 20+ combinado con WALL_TIMED

### AmbushEnemy
Enemigo estático (o patrulla corta) que se activa cuando el jugador entra en su radio.

- Persigue brevemente (6–8 celdas). Si el jugador sale del radio → el enemigo vuelve a su posición original y se resetea
- Imposing porque llena los cuartos de tensión — nunca sabes si es seguro entrar
- Diferencia psicológica vs ChaserEnemy: el Chaser siempre te busca, el Ambush *te espera*
- **Introducción:** nivel 22

---

## Mundo 3 — Ideas confirmadas (niveles 31–45)

### PORTAL_A / PORTAL_B
Dos celdas enlazadas. Entrar en A te teletransporta a B (y viceversa).

- No cuesta masa el viaje
- Pueden estar en diferentes zonas del mapa — crea atajos o trampas de orientación
- El reto es recordar dónde está el otro extremo y calcular si conviene usarlo

### GhostEnemy *(backlog original)*
Atraviesa paredes. Solo es bloqueado por celdas NARROW (su tamaño importa).

- Hace que ningún corredor sea 100% seguro
- Requiere que el jugador piense en NARROW como refugio, no solo como obstáculo
- **Introducción sugerida:** nivel 35+

---

## Ideas sin mundo asignado — explorar cuando toque

### INVERT_CONTROLS (trampa o zona)
Una celda o zona que intercambia izquierda↔derecha del D-pad/joystick durante N segundos.

- No stresante si la duración es corta (3–4s) y hay aviso visual claro
- Podría ser solo horizontal (izq↔der) o completo (todas las direcciones invertidas)
- Ideal para niveles de dificultad media — confunde sin matar directamente
- Variante: `TRAP_INVERT` temporal (ya en backlog) o zona permanente marcada en el suelo

### ICE
Celdas de hielo — al pisarlas el jugador se desliza 2–3 celdas adicionales en la misma dirección sin poder frenarse.

- No cuesta masa extra el deslizamiento
- Puede deslizarte hacia una trampa, un enemigo o una BOUNCE_WALL (combo divertido)
- Visual obvio — el jugador entiende el riesgo desde lejos

### CONVEYOR
Celda que empuja al jugador una celda extra en una dirección fija al pisarla.

- Puede trabajar a favor o en contra según la dirección del jugador
- Más predecible que ICE — sabes exactamente a dónde vas
- Interesante para crear "ríos" de celdas que arrastran en una dirección

### BIG_DOOR (puerta de masa mínima)
Inverso exacto de NARROW — solo puedes pasar si tu tamaño actual es **mayor** que un umbral (ej. > 0.7).

- Enseña al jugador a *proteger* su masa en vez de solo gastarla
- Mecánicamente trivial (mismo check que NARROW pero al revés)
- Crea rutas de "llega grande" que requieren tomar el camino corto y eficiente

### SWITCH + WALL_TOGGLE
Una celda SWITCH que al pisarla abre o cierra una WALL_TOGGLE en otro punto del mapa.

- Versión sin costo de masa (solo routing)
- Versión con costo de masa (pagas para abrir la ruta)
- Puede combinarse con WALL_TIMED: el switch resetea la fase del ciclo

### CRUMB_CHAIN bonus
Si absorbes una migaja y hay otra migaja adyacente a donde estás, obtienes +0.03 de bonus por cada crumb en cadena.

- Cambia el orden óptimo de absorción — ya no es lo mismo recoger una a una
- No requiere celda nueva — es lógica en ShrinkMechanic
- Opcional: mostrar el chain visualmente (número flotante pequeño)

### VACUUM zone
Zona de varias celdas marcadas donde las migajas se destruyen pasados 3 segundos.

- Crea zonas "calientes" donde tu trail está en riesgo
- No pierdes masa al moverte, pero sí pierdes las migajas si no vuelves rápido
- Urgencia real sin ser una trampa de muerte

### MirrorEnemy *(backlog original — Modo Infinito)*
Se mueve en la dirección opuesta al jugador.

- Confusión de control indirecta — tus movimientos determinan al enemigo
- Mejor en mapas abiertos (Dungeon) que en laberintos estrechos
- Candidato a Modo Infinito (mazes avanzados) más que a mundo específico

---

## Descartado / aplazado

| Idea | Razón |
|---|---|
| Menú tipo dungeon interactivo | Aplazado al siguiente juego |
| Presión + puerta vinculada | Reemplazado por mecánicas más divertidas de Mundo 2 |
