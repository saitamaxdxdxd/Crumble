using UnityEngine;

namespace Crumble.Core
{
    /// <summary>
    /// Genera nombres aleatorios temáticos para identificar al jugador en el ranking.
    /// Formato: AdjetivoNombre_1234
    /// </summary>
    public static class NameGenerator
    {
        private static readonly string[] Adjectives =
        {
            "Swift", "Tiny", "Bold", "Sly", "Nimble", "Ghost", "Iron",
            "Frost", "Wild", "Sharp", "Dark", "Brave", "Quick", "Clever",
            "Lone", "Crisp", "Keen", "Pale", "Dim", "Rusty"
        };

        private static readonly string[] Nouns =
        {
            "Orb", "Runner", "Seeker", "Drifter", "Shadow", "Spark",
            "Wanderer", "Hunter", "Scout", "Crawler", "Tracer", "Glider",
            "Roamer", "Dasher", "Prowler", "Slider", "Lurker", "Diver"
        };

        /// <summary>Genera un nombre aleatorio. Ejemplo: "SwiftOrb_4821"</summary>
        public static string Generate()
        {
            string adj  = Adjectives[Random.Range(0, Adjectives.Length)];
            string noun = Nouns[Random.Range(0, Nouns.Length)];
            int    num  = Random.Range(100, 9999);
            return $"{adj}{noun}_{num}";
        }
    }
}
