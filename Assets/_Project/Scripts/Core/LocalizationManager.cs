using Crumble.Events;
using UnityEngine;

namespace Crumble.Core
{
    /// <summary>
    /// Gestiona el idioma del juego. Clase estática — no requiere GameObject en escena.
    /// Llamar <see cref="Init"/> desde GameBootstrap al arrancar.
    /// Idiomas soportados: en, es, pt, fr.
    /// </summary>
    public static class LocalizationManager
    {
        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        public static string CurrentLanguage { get; private set; } = "en";

        public static readonly string[] SupportedLanguages = { "en", "es", "pt", "fr", "de" };

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Inicializa el idioma. Llamar desde GameBootstrap tras cargar SaveManager.
        /// Si el jugador no tiene preferencia guardada, auto-detecta desde el sistema.
        /// </summary>
        public static void Init()
        {
            string saved = SaveManager.Instance?.Data.settings.language ?? "";
            SetLanguage(string.IsNullOrEmpty(saved) ? Detect() : saved, save: false);
        }

        /// <summary>Cambia el idioma activo y dispara OnLanguageChanged.</summary>
        public static void SetLanguage(string code, bool save = true)
        {
            CurrentLanguage = IsSupported(code) ? code : "en";

            if (save)
                SaveManager.Instance?.SaveLanguage(CurrentLanguage);

            GameEvents.RaiseLanguageChanged();
        }

        /// <summary>Cicla al siguiente idioma soportado.</summary>
        public static void CycleNext()
        {
            int idx = System.Array.IndexOf(SupportedLanguages, CurrentLanguage);
            string next = SupportedLanguages[(idx + 1) % SupportedLanguages.Length];
            SetLanguage(next);
        }

        /// <summary>Devuelve el texto localizado para la clave dada.</summary>
        public static string Get(string key)
        {
            return CurrentLanguage switch
            {
                "es" => ES(key),
                "pt" => PT(key),
                "fr" => FR(key),
                "de" => DE(key),
                _    => EN(key),
            };
        }

        /// <summary>Nombre visible del idioma actual.</summary>
        public static string CurrentLanguageName => CurrentLanguage switch
        {
            "es" => "ESPAÑOL",
            "pt" => "PORTUGUÊS",
            "fr" => "FRANÇAIS",
            "de" => "DEUTSCH",
            _    => "ENGLISH",
        };

        // ──────────────────────────────────────────────────────────────────────
        // Auto-detección
        // ──────────────────────────────────────────────────────────────────────

        private static string Detect()
        {
            return Application.systemLanguage switch
            {
                SystemLanguage.Spanish    => "es",
                SystemLanguage.Portuguese => "pt",
                SystemLanguage.French     => "fr",
                SystemLanguage.German     => "de",
                _                         => "en",
            };
        }

        private static bool IsSupported(string code)
        {
            foreach (var lang in SupportedLanguages)
                if (lang == code) return true;
            return false;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Strings — Inglés
        // ──────────────────────────────────────────────────────────────────────

        private static string EN(string key) => key switch
        {
            // Navegación
            "back"          => "BACK",
            "play"          => "PLAY",
            "settings"      => "SETTINGS",
            "store"         => "STORE",
            // Settings
            "sfx"           => "SFX",
            "music"         => "MUSIC",
            "movement"      => "MOVEMENT",
            "language"      => "LANGUAGE",
            "vibration"     => "VIBRATION",
            "move_slide"    => "◀  SLIDE  ▶",
            "move_cont"     => "◀  CONTINUOUS  ▶",
            "move_step"     => "◀  STEP BY STEP  ▶",
            // Tienda
            "noad_name"     => "No Ads",
            "noad_desc"     => "Remove all ads",
            "full_name"     => "Full Game",
            "full_desc"     => "Unlock everything",
            "buy"           => "BUY",
            "owned"         => "✓ OWNED",
            "restore"       => "Restore Purchases",
            // Resultado
            "gameover"      => "GAME OVER",
            "victory"       => "LEVEL COMPLETE",
            "retry"         => "RETRY",
            "watch_ad"      => "CONTINUE (WATCH AD)",
            "menu"          => "MENU",
            "next"          => "NEXT",
            "cont_btn"      => "CONTINUE",
            // Pausa
            "resume"        => "RESUME",
            "add_size"      => "ADD MASS",
            "add_time"      => "ADD TIME",
            // Modo Infinito — menú y modal bloqueado
            "infinite"               => "INFINITE",
            "infinite_locked"        => "LOCKED",
            "infinite_locked_desc"   => "Finish World 1 to unlock\n{0}/{1} levels · {2}/{3} ⭐",
            // Modo Infinito — HUD y RUN OVER
            "infinite_hud_maze"      => "MAZE {0}",
            "run_over"               => "RUN OVER",
            "run_mazes"              => "MAZES",
            "run_score"              => "SCORE",
            "run_best"               => "BEST",
            "play_again"             => "PLAY AGAIN",
            // Level Select — mundos bloqueados
            "world_name"             => "WORLD {0}",
            "world_locked"           => "UNLOCK WORLD {0}",
            "controls"               => "CONTROLS",
            "adjust_dpad"            => "Drag to reposition · Sliders to resize and adjust opacity",
            "done"                   => "DONE",
            "vibration_on"           => "VIBRATION  ON",
            "vibration_off"          => "VIBRATION  OFF",
            "change_name"            => "YOUR NAME",
            "leaderboard"            => "RANKING",
            "leaderboard_offline"    => "NO CONNECTION",
            "notif_inactivity_body"  => "We miss you! Come back and play.",
            "daily"                  => "DAILY",
            "daily_streak"           => "STREAK",
            "daily_failed"           => "Better luck tomorrow!",
            "daily_completed"        => "Completed today",
            "notif_daily_body"       => "A new daily maze is waiting!",
            "multiplayer_soon"       => "COMING SOON",
            "multiplayer_soon_desc"  => "Multiplayer is on its way.\nVersus · Endless PvP",
            _               => key,
        };

        // ──────────────────────────────────────────────────────────────────────
        // Strings — Español
        // ──────────────────────────────────────────────────────────────────────

        private static string ES(string key) => key switch
        {
            "back"          => "ATRÁS",
            "play"          => "JUGAR",
            "settings"      => "AJUSTES",
            "store"         => "TIENDA",
            "sfx"           => "SFX",
            "music"         => "MÚSICA",
            "movement"      => "MOVIMIENTO",
            "language"      => "IDIOMA",
            "vibration"     => "VIBRACIÓN",
            "move_slide"    => "◀  DESLIZAR  ▶",
            "move_cont"     => "◀  CONTINUO  ▶",
            "move_step"     => "◀  PASO A PASO  ▶",
            "noad_name"     => "Sin Anuncios",
            "noad_desc"     => "Elimina todos los anuncios",
            "full_name"     => "Juego Completo",
            "full_desc"     => "Desbloquea todo",
            "buy"           => "COMPRAR",
            "owned"         => "✓ OBTENIDO",
            "restore"       => "Restaurar Compras",
            "gameover"      => "GAME OVER",
            "victory"       => "NIVEL COMPLETO",
            "retry"         => "REINTENTAR",
            "watch_ad"      => "CONTINUAR (VER ANUNCIO)",
            "menu"          => "MENÚ",
            "next"          => "SIGUIENTE",
            "cont_btn"      => "CONTINUAR",
            "resume"        => "CONTINUAR",
            "add_size"      => "AÑADIR MASA",
            "add_time"      => "AÑADIR TIEMPO",
            "infinite"               => "INFINITO",
            "infinite_locked"        => "BLOQUEADO",
            "infinite_locked_desc"   => "Termina el Mundo 1 para desbloquear\n{0}/{1} niveles · {2}/{3} ⭐",
            "infinite_hud_maze"      => "LABERINTO {0}",
            "run_over"               => "FIN DEL RUN",
            "run_mazes"              => "LABERINTOS",
            "run_score"              => "PUNTUACIÓN",
            "run_best"               => "RÉCORD",
            "play_again"             => "JUGAR DE NUEVO",
            "world_name"             => "MUNDO {0}",
            "world_locked"           => "DESBLOQUEAR MUNDO {0}",
            "controls"               => "CONTROLES",
            "adjust_dpad"            => "Arrastra para mover · Sliders para tamaño y opacidad",
            "done"                   => "LISTO",
            "vibration_on"           => "VIBRACIÓN  ON",
            "vibration_off"          => "VIBRACIÓN  OFF",
            "change_name"            => "TU NOMBRE",
            "leaderboard"            => "RANKING",
            "leaderboard_offline"    => "SIN CONEXIÓN",
            "notif_inactivity_body"  => "¡Te echamos de menos! Vuelve a jugar.",
            "daily"                  => "DIARIO",
            "daily_streak"           => "RACHA",
            "daily_failed"           => "¡Mejor suerte mañana!",
            "daily_completed"        => "Ya completado hoy",
            "notif_daily_body"       => "¡Un nuevo laberinto diario te espera!",
            "multiplayer_soon"       => "PRÓXIMAMENTE",
            "multiplayer_soon_desc"  => "El multijugador está en camino.\nVersus · Endless PvP",
            _               => EN(key),
        };

        // ──────────────────────────────────────────────────────────────────────
        // Strings — Portugués
        // ──────────────────────────────────────────────────────────────────────

        private static string PT(string key) => key switch
        {
            "back"          => "VOLTAR",
            "play"          => "JOGAR",
            "settings"      => "CONFIGURAÇÕES",
            "store"         => "LOJA",
            "sfx"           => "SFX",
            "music"         => "MÚSICA",
            "movement"      => "MOVIMENTO",
            "language"      => "IDIOMA",
            "move_slide"    => "◀  DESLIZAR  ▶",
            "move_cont"     => "◀  CONTÍNUO  ▶",
            "move_step"     => "◀  PASSO A PASSO  ▶",
            "noad_name"     => "Sem Anúncios",
            "noad_desc"     => "Remove todos os anúncios",
            "full_name"     => "Jogo Completo",
            "full_desc"     => "Desbloqueia tudo",
            "buy"           => "COMPRAR",
            "owned"         => "✓ OBTIDO",
            "restore"       => "Restaurar Compras",
            "gameover"      => "GAME OVER",
            "victory"       => "NÍVEL COMPLETO",
            "retry"         => "TENTAR DE NOVO",
            "watch_ad"      => "CONTINUAR (VER ANÚNCIO)",
            "menu"          => "MENU",
            "next"          => "PRÓXIMO",
            "cont_btn"      => "CONTINUAR",
            "resume"        => "CONTINUAR",
            "add_size"      => "ADICIONAR MASSA",
            "add_time"      => "ADICIONAR TEMPO",
            "infinite"               => "INFINITO",
            "infinite_locked"        => "BLOQUEADO",
            "infinite_locked_desc"   => "Termine o Mundo 1 para desbloquear\n{0}/{1} níveis · {2}/{3} ⭐",
            "infinite_hud_maze"      => "LABIRINTO {0}",
            "run_over"               => "FIM DO RUN",
            "run_mazes"              => "LABIRINTOS",
            "run_score"              => "PONTUAÇÃO",
            "run_best"               => "RECORDE",
            "play_again"             => "JOGAR DE NOVO",
            "world_name"             => "MUNDO {0}",
            "world_locked"           => "DESBLOQUEAR MUNDO {0}",
            "leaderboard"            => "RANKING",
            "leaderboard_offline"    => "SEM CONEXÃO",
            "notif_inactivity_body"  => "Sentimos sua falta! Volte a jogar.",
            "daily"                  => "DIÁRIO",
            "daily_streak"           => "SEQUÊNCIA",
            "daily_failed"           => "Tente novamente amanhã!",
            "daily_completed"        => "Concluído hoje",
            "notif_daily_body"       => "Um novo labirinto diário está esperando!",
            "multiplayer_soon"       => "EM BREVE",
            "multiplayer_soon_desc"  => "O multijogador está a caminho.\nVersus · Endless PvP",
            _               => EN(key),
        };

        // ──────────────────────────────────────────────────────────────────────
        // Strings — Alemán
        // ──────────────────────────────────────────────────────────────────────

        private static string DE(string key) => key switch
        {
            "back"          => "ZURÜCK",
            "play"          => "SPIELEN",
            "settings"      => "EINSTELLUNGEN",
            "store"         => "SHOP",
            "sfx"           => "SFX",
            "music"         => "MUSIK",
            "movement"      => "BEWEGUNG",
            "language"      => "SPRACHE",
            "move_slide"    => "◀  GLEITEN  ▶",
            "move_cont"     => "◀  KONTINUIERLICH  ▶",
            "move_step"     => "◀  SCHRITT FÜR SCHRITT  ▶",
            "noad_name"     => "Keine Werbung",
            "noad_desc"     => "Entfernt alle Werbung",
            "full_name"     => "Vollständiges Spiel",
            "full_desc"     => "Alles freischalten",
            "buy"           => "KAUFEN",
            "owned"         => "✓ GEKAUFT",
            "restore"       => "Käufe wiederherstellen",
            "gameover"      => "GAME OVER",
            "victory"       => "LEVEL GESCHAFFT",
            "retry"         => "NOCHMAL",
            "watch_ad"      => "WEITER (WERBUNG ANSEHEN)",
            "menu"          => "MENÜ",
            "next"          => "WEITER",
            "cont_btn"      => "WEITER",
            "resume"        => "FORTSETZEN",
            "add_size"      => "MASSE HINZUFÜGEN",
            "add_time"      => "ZEIT HINZUFÜGEN",
            "infinite"               => "UNENDLICH",
            "infinite_locked"        => "GESPERRT",
            "infinite_locked_desc"   => "Schließe Welt 1 ab, um freizuschalten\n{0}/{1} Level · {2}/{3} ⭐",
            "infinite_hud_maze"      => "LEVEL {0}",
            "run_over"               => "RUN VORBEI",
            "run_mazes"              => "LEVELS",
            "run_score"              => "PUNKTE",
            "run_best"               => "BESTLEISTUNG",
            "play_again"             => "NOCHMAL SPIELEN",
            "world_name"             => "WELT {0}",
            "world_locked"           => "WELT {0} FREISCHALTEN",
            "leaderboard"            => "RANGLISTE",
            "leaderboard_offline"    => "KEINE VERBINDUNG",
            "notif_inactivity_body"  => "Wir vermissen dich! Komm zurück und spiele.",
            "daily"                  => "TÄGLICH",
            "daily_streak"           => "SERIE",
            "daily_failed"           => "Morgen klappt's besser!",
            "daily_completed"        => "Heute abgeschlossen",
            "notif_daily_body"       => "Ein neues Tages-Labyrinth wartet auf dich!",
            "multiplayer_soon"       => "DEMNÄCHST",
            "multiplayer_soon_desc"  => "Mehrspieler kommt bald.\nVersus · Endless PvP",
            _               => EN(key),
        };

        // ──────────────────────────────────────────────────────────────────────
        // Strings — Francés
        // ──────────────────────────────────────────────────────────────────────

        private static string FR(string key) => key switch
        {
            "back"          => "RETOUR",
            "play"          => "JOUER",
            "settings"      => "PARAMÈTRES",
            "store"         => "BOUTIQUE",
            "sfx"           => "SFX",
            "music"         => "MUSIQUE",
            "movement"      => "MOUVEMENT",
            "language"      => "LANGUE",
            "move_slide"    => "◀  GLISSER  ▶",
            "move_cont"     => "◀  CONTINU  ▶",
            "move_step"     => "◀  PAS À PAS  ▶",
            "noad_name"     => "Sans Publicités",
            "noad_desc"     => "Supprime toutes les pubs",
            "full_name"     => "Jeu Complet",
            "full_desc"     => "Débloque tout",
            "buy"           => "ACHETER",
            "owned"         => "✓ OBTENU",
            "restore"       => "Restaurer les Achats",
            "gameover"      => "GAME OVER",
            "victory"       => "NIVEAU COMPLÉTÉ",
            "retry"         => "RÉESSAYER",
            "watch_ad"      => "CONTINUER (VOIR PUB)",
            "menu"          => "MENU",
            "next"          => "SUIVANT",
            "cont_btn"      => "CONTINUER",
            "resume"        => "REPRENDRE",
            "add_size"      => "AJOUTER MASSE",
            "add_time"      => "AJOUTER TEMPS",
            "infinite"               => "INFINI",
            "infinite_locked"        => "VERROUILLÉ",
            "infinite_locked_desc"   => "Termine le Monde 1 pour débloquer\n{0}/{1} niveaux · {2}/{3} ⭐",
            "infinite_hud_maze"      => "LABYRINTHE {0}",
            "run_over"               => "FIN DU RUN",
            "run_mazes"              => "LABYRINTHES",
            "run_score"              => "SCORE",
            "run_best"               => "RECORD",
            "play_again"             => "REJOUER",
            "world_name"             => "MONDE {0}",
            "world_locked"           => "DÉBLOQUER MONDE {0}",
            "leaderboard"            => "CLASSEMENT",
            "leaderboard_offline"    => "SANS CONNEXION",
            "notif_inactivity_body"  => "Tu nous manques ! Reviens jouer.",
            "daily"                  => "QUOTIDIEN",
            "daily_streak"           => "SÉRIE",
            "daily_failed"           => "Bonne chance demain !",
            "daily_completed"        => "Complété aujourd'hui",
            "notif_daily_body"       => "Un nouveau labyrinthe quotidien t'attend !",
            "multiplayer_soon"       => "BIENTÔT DISPONIBLE",
            "multiplayer_soon_desc"  => "Le multijoueur arrive bientôt.\nVersus · Endless PvP",
            _               => EN(key),
        };
    }
}
