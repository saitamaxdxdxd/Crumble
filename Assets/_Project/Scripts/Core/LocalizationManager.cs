using Shrink.Events;
using UnityEngine;

namespace Shrink.Core
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
            "infinite_locked_desc"   => "Complete {0} levels to unlock\nor purchase Infinite Pro",
            "infinite_locked_buy"    => "UNLOCK — INFINITE PRO",
            // Modo Infinito — HUD y RUN OVER
            "infinite_hud_maze"      => "MAZE {0}",
            "run_over"               => "RUN OVER",
            "run_mazes"              => "MAZES",
            "run_score"              => "SCORE",
            "run_best"               => "BEST",
            "play_again"             => "PLAY AGAIN",
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
            "infinite_locked_desc"   => "Completa {0} niveles para desbloquear\no compra Infinito Pro",
            "infinite_locked_buy"    => "DESBLOQUEAR — INFINITO PRO",
            "infinite_hud_maze"      => "LABERINTO {0}",
            "run_over"               => "FIN DEL RUN",
            "run_mazes"              => "LABERINTOS",
            "run_score"              => "PUNTUACIÓN",
            "run_best"               => "RÉCORD",
            "play_again"             => "JUGAR DE NUEVO",
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
            "infinite_locked_desc"   => "Complete {0} níveis para desbloquear\nou compre Infinito Pro",
            "infinite_locked_buy"    => "DESBLOQUEAR — INFINITO PRO",
            "infinite_hud_maze"      => "LABIRINTO {0}",
            "run_over"               => "FIM DO RUN",
            "run_mazes"              => "LABIRINTOS",
            "run_score"              => "PONTUAÇÃO",
            "run_best"               => "RECORDE",
            "play_again"             => "JOGAR DE NOVO",
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
            "infinite_locked_desc"   => "Schließe {0} Level ab, um freizuschalten\noder kaufe Infinite Pro",
            "infinite_locked_buy"    => "FREISCHALTEN — INFINITE PRO",
            "infinite_hud_maze"      => "LEVEL {0}",
            "run_over"               => "RUN VORBEI",
            "run_mazes"              => "LEVELS",
            "run_score"              => "PUNKTE",
            "run_best"               => "BESTLEISTUNG",
            "play_again"             => "NOCHMAL SPIELEN",
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
            "infinite_locked_desc"   => "Terminez {0} niveaux pour débloquer\nou achetez Infinite Pro",
            "infinite_locked_buy"    => "DÉBLOQUER — INFINITE PRO",
            "infinite_hud_maze"      => "LABYRINTHE {0}",
            "run_over"               => "FIN DU RUN",
            "run_mazes"              => "LABYRINTHES",
            "run_score"              => "SCORE",
            "run_best"               => "RECORD",
            "play_again"             => "REJOUER",
            _               => EN(key),
        };
    }
}
