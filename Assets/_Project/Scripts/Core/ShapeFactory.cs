using UnityEngine;

namespace Crumble.Core
{
    /// <summary>
    /// Crea sprites 2D de cuadrado y círculo en tiempo de ejecución.
    /// No requiere assets externos.
    /// </summary>
    public static class ShapeFactory
    {
        private static Sprite _squareSprite;
        private static Sprite _circleSprite;

        /// <summary>Devuelve un sprite cuadrado blanco de 1×1 pixel (cacheado).</summary>
        public static Sprite GetSquare()
        {
            if (_squareSprite != null) return _squareSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            _squareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            return _squareSprite;
        }

        /// <summary>Devuelve un sprite circular blanco de <paramref name="resolution"/> píxeles (cacheado).</summary>
        public static Sprite GetCircle(int resolution = 64)
        {
            if (_circleSprite != null) return _circleSprite;

            var tex  = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear
            };

            float center = resolution * 0.5f;
            float radius = center - 0.5f;

            for (int x = 0; x < resolution; x++)
            for (int y = 0; y < resolution; y++)
            {
                float dx   = x - center + 0.5f;
                float dy   = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Antialiasing de un píxel en el borde
                float alpha = Mathf.Clamp01(radius - dist + 1f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }

            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, resolution, resolution),
                                          Vector2.one * 0.5f, resolution);
            return _circleSprite;
        }

        /// <summary>
        /// Crea un GameObject 2D con SpriteRenderer listo para usar.
        /// </summary>
        private static Material _unlitMaterial;

        /// <summary>
        /// Crea un GameObject 2D con SpriteRenderer usando Sprite-Unlit-Default.
        /// Evita artefactos visuales del shader lit en escenas sin luces 2D.
        /// </summary>
        public static GameObject CreateSprite(string name, Sprite sprite, Color color,
                                              Transform parent = null, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);

            var sr          = go.AddComponent<SpriteRenderer>();
            sr.sprite        = sprite;
            sr.color         = color;
            sr.sortingOrder  = sortingOrder;
            sr.sharedMaterial = GetUnlitMaterial();

            return go;
        }

        public static Material GetUnlitMaterial()
        {
            if (_unlitMaterial != null) return _unlitMaterial;
            _unlitMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            return _unlitMaterial;
        }
    }
}
