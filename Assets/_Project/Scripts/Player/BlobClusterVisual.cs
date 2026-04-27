using UnityEngine;

namespace Crumble.Player
{
    /// <summary>
    /// Visual del jugador: blob central grande + satélites que se despegan y pegan
    /// usando spring physics viscosa (efecto lava lamp / superficie de tensión).
    /// </summary>
    public class BlobClusterVisual : MonoBehaviour
    {
        [Header("Blob central")]
        [SerializeField] private float _centerRadius      = 0.38f;  // fracción de cellSize
        [SerializeField] private float _breatheSpeed      = 1.1f;
        [SerializeField] private float _breatheAmount     = 0.06f;

        [Header("Satélites")]
        [SerializeField] private int   _maxSatellites     = 5;
        [SerializeField] private float _satelliteRadius   = 0.17f;  // fracción de cellSize
        [SerializeField] private float _orbitRadius       = 0.32f;  // fracción de cellSize
        [SerializeField] private float _orbitSpeedMin     = 0.35f;  // rad/s
        [SerializeField] private float _orbitSpeedMax     = 0.70f;
        [SerializeField] private float _orbitPerturbation = 0.07f;  // varía el radio de órbita
        [SerializeField] private float _perturbSpeedMin   = 0.4f;
        [SerializeField] private float _perturbSpeedMax   = 1.0f;

        [Header("Spring (viscosidad)")]
        [SerializeField] private float _springK  = 9f;   // rigidez — más alto = menos lag
        [SerializeField] private float _damping  = 5.5f; // amortiguación — más alto = más viscoso

        [Header("Squish al estirarse")]
        [SerializeField] private float _squishAmount = 0.28f; // cuánto se aplana en dirección de tensión

        [Header("Color")]
        [SerializeField] private Color _colorFull = new Color(1.0f, 0.55f, 0.05f);

        // ──────────────────────────────────────────────────────────────────────
        // Estado interno
        // ──────────────────────────────────────────────────────────────────────

        private GameObject     _centerObject;
        private SpriteRenderer _centerRenderer;

        private GameObject[]     _satObjects;
        private SpriteRenderer[] _satRenderers;

        // Spring physics por satélite
        private Vector3[] _satPos;         // posición actual relativa al centro
        private Vector3[] _satVel;         // velocidad actual
        private float[]   _targetAngle;    // ángulo objetivo en la órbita (avanza con el tiempo)
        private float[]   _orbitSpeed;     // velocidad angular individual
        private float[]   _perturbPhase;
        private float[]   _perturbSpeed;

        private float _cellSize;
        private float _currentSizeT;

        // ──────────────────────────────────────────────────────────────────────
        // Setup
        // ──────────────────────────────────────────────────────────────────────

        public void Setup(float cellSize)
        {
            _cellSize = cellSize;

            _centerObject   = CreateBlob("Blob_Center", sortOrder: 6);
            _centerRenderer = _centerObject.GetComponent<SpriteRenderer>();
            _centerRenderer.color = _colorFull;

            _satObjects   = new GameObject[_maxSatellites];
            _satRenderers = new SpriteRenderer[_maxSatellites];
            _satPos       = new Vector3[_maxSatellites];
            _satVel       = new Vector3[_maxSatellites];
            _targetAngle  = new float[_maxSatellites];
            _orbitSpeed   = new float[_maxSatellites];
            _perturbPhase = new float[_maxSatellites];
            _perturbSpeed = new float[_maxSatellites];

            float angleStep = Mathf.PI * 2f / _maxSatellites;
            for (int i = 0; i < _maxSatellites; i++)
            {
                _satObjects[i]   = CreateBlob($"Blob_Sat_{i}", sortOrder: 5);
                _satRenderers[i] = _satObjects[i].GetComponent<SpriteRenderer>();
                _satRenderers[i].color = _colorFull;

                _targetAngle[i]  = angleStep * i + Random.Range(-0.2f, 0.2f);
                _orbitSpeed[i]   = Random.Range(_orbitSpeedMin, _orbitSpeedMax)
                                   * (Random.value > 0.5f ? 1f : -1f);
                _perturbPhase[i] = Random.Range(0f, Mathf.PI * 2f);
                _perturbSpeed[i] = Random.Range(_perturbSpeedMin, _perturbSpeedMax);

                // Inicializar en su posición orbital para evitar el salto inicial
                float r = _orbitRadius * cellSize;
                _satPos[i] = new Vector3(Mathf.Cos(_targetAngle[i]) * r,
                                         Mathf.Sin(_targetAngle[i]) * r, 0f);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Refresh por cambio de masa
        // ──────────────────────────────────────────────────────────────────────

        public void Refresh(float currentSize, float cellSize)
        {
            if (_centerObject == null) return;
            _cellSize     = cellSize;
            _currentSizeT = Mathf.InverseLerp(SphereController.MinSize, SphereController.InitialSize, currentSize);

            // Centro escala con la masa (encoge visualmente cuando queda poco)
            float centerScale = _centerRadius * cellSize * Mathf.Lerp(0.68f, 1.0f, _currentSizeT);
            _centerObject.transform.localScale = Vector3.one * centerScale;

            // Satélites: activos según masa
            int   activeSats = Mathf.RoundToInt(_currentSizeT * _maxSatellites);
            float satScale   = _satelliteRadius * cellSize;

            for (int i = 0; i < _maxSatellites; i++)
            {
                bool on = i < activeSats;
                _satObjects[i].SetActive(on);
                if (on) _satObjects[i].transform.localScale = Vector3.one * satScale;
            }
        }

        public Color CurrentColor => _centerRenderer != null ? _centerRenderer.color : _colorFull;

        // ──────────────────────────────────────────────────────────────────────
        // Animación — spring viscoso + squish
        // ──────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_centerObject == null) return;

            float dt = Time.deltaTime;
            float t  = Time.time;

            // Centro: pulso de respiración
            float breathe    = 1f + Mathf.Sin(t * _breatheSpeed) * _breatheAmount;
            float centerBase = _centerRadius * _cellSize * Mathf.Lerp(0.68f, 1.0f, _currentSizeT);
            _centerObject.transform.localScale    = Vector3.one * (centerBase * breathe);
            _centerObject.transform.localPosition = Vector3.zero;

            // Satélites: spring physics
            float baseOrbit  = _orbitRadius * _cellSize;
            float satBase    = _satelliteRadius * _cellSize;

            for (int i = 0; i < _maxSatellites; i++)
            {
                if (!_satObjects[i].activeSelf) continue;

                // Avanzar ángulo objetivo
                _targetAngle[i] += _orbitSpeed[i] * dt;

                // Radio de órbita con perturbación senoidal
                float perturbed = baseOrbit
                    + Mathf.Sin(t * _perturbSpeed[i] + _perturbPhase[i]) * (_orbitPerturbation * _cellSize);

                Vector3 target = new Vector3(
                    Mathf.Cos(_targetAngle[i]) * perturbed,
                    Mathf.Sin(_targetAngle[i]) * perturbed, 0f);

                // Spring: fuerza hacia target + amortiguación viscosa
                Vector3 toTarget = target - _satPos[i];
                _satVel[i] += toTarget * (_springK * dt);
                _satVel[i] *= Mathf.Clamp01(1f - _damping * dt);
                _satPos[i] += _satVel[i] * dt;

                _satObjects[i].transform.localPosition = _satPos[i];

                // Squish: aplana el blob en la dirección de la tensión
                float   stretch    = Mathf.Clamp01(toTarget.magnitude / baseOrbit);
                float   scaleAlong = satBase * (1f + stretch * _squishAmount);
                float   scalePerp  = satBase * (1f - stretch * _squishAmount * 0.5f);
                float   angle      = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;

                _satObjects[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                _satObjects[i].transform.localScale    = new Vector3(scaleAlong, scalePerp, 1f);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helper
        // ──────────────────────────────────────────────────────────────────────

        private GameObject CreateBlob(string blobName, int sortOrder)
        {
            var go = new GameObject(blobName);
            go.transform.SetParent(transform, worldPositionStays: false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = Core.ShapeFactory.GetCircle();
            sr.sortingOrder = sortOrder;
            return go;
        }
    }
}
