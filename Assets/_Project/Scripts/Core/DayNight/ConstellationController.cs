using UnityEngine;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// Manages star field and constellations for the day-night cycle.
    /// Stars are rendered on a SKY DOME that follows the player's camera.
    /// This ensures all players see the same stars regardless of their world position.
    /// 
    /// IMPORTANT: Star positions are stored in LOCAL coordinates relative to SkyDome center.
    /// When SkyDome moves, the mesh moves and stars move with it.
    /// 
    /// WORKING PARAMETERS for MMO:
    /// - skyDomeRadius: 900000 (creates proper scale for large world)
    /// - baseStarSize: 3000
    /// - starSizeMagnitudeScale: 1500
    /// </summary>
    public class ConstellationController : MonoBehaviour
    {
        [Header("Data")]
        public ConstellationData constellationData;
        
        [Header("Materials")]
        public Material starMaterial;
        public Material constellationLineMaterial;
        
        [Header("Sky Dome Settings")]
        [Tooltip("Radius of the sky dome sphere around the player")]
        public float skyDomeRadius = 900000f;
        
        [Tooltip("Height offset above camera for sky dome center")]
        public float skyDomeHeightOffset = 100f;

        [Header("Star Settings")]
        [Tooltip("Base size of star quads in world units. Working value for MMO: 3000")]
        public float baseStarSize = 3000f;
        
        [Tooltip("Size variation based on magnitude. Working value for MMO: 1500")]
        public float starSizeMagnitudeScale = 1500f;
        
        [Header("Sky Dome Rotation")]
        [Tooltip("Enable sky dome rotation over time (Earth rotation simulation)")]
        public bool enableRotation = true;
        
        [Tooltip("Rotation speed multiplier. 1.0 = full Earth rotation (24h sidereal). Adjust for game pacing")]
        public float rotationSpeedMultiplier = 1.0f;
        
        [Tooltip("Server time of day reference for rotation. Set by ServerWeatherController")]
        public float serverTimeOfDay = 12f;

        [Header("Constellation Lines")]
        [Tooltip("Enable or disable constellation connection lines")]
        public bool showConstellationLines = true;
        
        [Tooltip("Width of constellation lines")]
        public float constellationLineWidth = 1.5f;

        [Header("Debug")]
        public bool forceFullVisibility = true;
        public bool showDebugGizmos = true;

        private GameObject _skyDomeObject;
        private MeshFilter _skyDomeMeshFilter;
        private MeshRenderer _skyDomeMeshRenderer;
        private LineRenderer[] _constellationLines;
        
        private float _starVisibility = 0f;
        private bool _isInitialized = false;
        
        private const float NIGHT_START_HOUR = 21f;
        private const float NIGHT_END_HOUR = 5f;
        private const float TWILIGHT_DURATION_HOURS = 1.5f;
        
        private const float EARTH_ROTATION_DEGREES_PER_GAME_HOUR = 15f; // 360 / 24

        void Awake()
        {
            // CRITICAL: Make this object persist across scene loads
            // This ensures stars remain visible when loading world scenes
            DontDestroyOnLoad(gameObject);
            
            // Also make SkyDome persist if it exists
            if (_skyDomeObject != null)
            {
                DontDestroyOnLoad(_skyDomeObject);
            }
        }

        void OnEnable()
        {
            Initialize();
            
            // Subscribe to server time updates
            if (ServerWeatherController.Instance != null)
            {
                ServerWeatherController.Instance.OnTimeOfDayChanged += OnServerTimeChanged;
                // Get initial time
                SetServerTimeOfDay(ServerWeatherController.Instance.TimeOfDay);
            }
        }

        void OnDisable()
        {
            // Unsubscribe from server time updates
            if (ServerWeatherController.Instance != null)
            {
                ServerWeatherController.Instance.OnTimeOfDayChanged -= OnServerTimeChanged;
            }
            
            Cleanup();
        }

        void OnDestroy()
        {
            // Unsubscribe from server time updates
            if (ServerWeatherController.Instance != null)
            {
                ServerWeatherController.Instance.OnTimeOfDayChanged -= OnServerTimeChanged;
            }
            
            Cleanup();
        }
        
        private void OnServerTimeChanged(float time)
        {
            SetServerTimeOfDay(time);
        }

        private void Initialize()
        {
            if (_isInitialized) return;
            
            if (constellationData == null)
            {
                Debug.LogError("[ConstellationController] No constellation data assigned!");
                return;
            }

            CreateSkyDome();
            BuildStarMesh();
            CreateConstellationLines();
            
            _starVisibility = forceFullVisibility ? 1f : 0f;
            UpdateStarVisibility();
            
            _isInitialized = true;
            
            Debug.Log("[ConstellationController] Initialization complete. Stars: " + GetTotalStarCount());
        }

        private void CreateSkyDome()
        {
            _skyDomeObject = new GameObject("SkyDome_Stars");
            _skyDomeObject.transform.SetParent(transform);
            _skyDomeObject.transform.localPosition = Vector3.zero;
            _skyDomeObject.transform.localRotation = Quaternion.identity;
            
            _skyDomeObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            _skyDomeObject.transform.SetParent(null);
            DontDestroyOnLoad(_skyDomeObject);
        }

        private Vector3 SphericalToLocalDirection(float azimuthDeg, float altitudeDeg)
        {
            float azRad = azimuthDeg * Mathf.Deg2Rad;
            float altRad = altitudeDeg * Mathf.Deg2Rad;
            
            float cosAlt = Mathf.Cos(altRad);
            
            return new Vector3(
                cosAlt * Mathf.Cos(azRad),
                Mathf.Sin(altRad),
                cosAlt * Mathf.Sin(azRad)
            ).normalized;
        }

        private void BuildStarMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> indices = new List<int>();

            int starIndex = 0;
            int totalStars = GetTotalStarCount();

            foreach (var constellation in constellationData.constellations)
            {
                if (constellation.stars == null) continue;
                
                foreach (var star in constellation.stars)
                {
                    Vector3 localDir = SphericalToLocalDirection(star.sphericalPosition.x, star.sphericalPosition.y);
                    Vector3 localStarPos = localDir * skyDomeRadius;
                    
                    float starSize = baseStarSize + (2f - star.magnitude) * starSizeMagnitudeScale;
                    float halfSize = starSize * 0.5f;
                    
                    Vector3 right = Vector3.Cross(Vector3.up, localDir);
                    if (right.sqrMagnitude < 0.001f)
                    {
                        right = Vector3.Cross(Vector3.forward, localDir);
                    }
                    right.Normalize();
                    Vector3 up = Vector3.Cross(localDir, right);
                    
                    Vector3 p0 = localStarPos + (-right - up) * halfSize;
                    Vector3 p1 = localStarPos + ( right - up) * halfSize;
                    Vector3 p2 = localStarPos + ( right + up) * halfSize;
                    Vector3 p3 = localStarPos + (-right + up) * halfSize;
                    
                    vertices.Add(p0);
                    vertices.Add(p1);
                    vertices.Add(p2);
                    vertices.Add(p3);
                    
                    float twinkle = Mathf.Sin(Time.time * (1f + star.magnitude * 0.5f) + starIndex * 0.1f) * 0.15f + 0.85f;
                    Color starColor = new Color(1f, 1f, 1f, twinkle);
                    colors.Add(starColor);
                    colors.Add(starColor);
                    colors.Add(starColor);
                    colors.Add(starColor);
                    
                    int baseIdx = starIndex * 4;
                    indices.Add(baseIdx + 0);
                    indices.Add(baseIdx + 1);
                    indices.Add(baseIdx + 2);
                    indices.Add(baseIdx + 0);
                    indices.Add(baseIdx + 2);
                    indices.Add(baseIdx + 3);
                    
                    starIndex++;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "SkyDomeStars";
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            _skyDomeMeshFilter = _skyDomeObject.AddComponent<MeshFilter>();
            _skyDomeMeshFilter.sharedMesh = mesh;

            _skyDomeMeshRenderer = _skyDomeObject.AddComponent<MeshRenderer>();
            _skyDomeMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _skyDomeMeshRenderer.receiveShadows = false;
            _skyDomeMeshRenderer.sortingOrder = 32767;
            _skyDomeMeshRenderer.lightmapIndex = -1;
            _skyDomeMeshRenderer.realtimeLightmapIndex = -1;

            SetupMaterial();
        }

        private void SetupMaterial()
        {
            Material matToUse = null;
            
            if (starMaterial != null)
            {
                matToUse = starMaterial;
            }
            else
            {
                matToUse = Resources.Load<Material>("Materials/Stars/StarMaterial");
                if (matToUse == null)
                {
                    Shader fallbackShader = Shader.Find("Sprites/Default");
                    if (fallbackShader != null)
                    {
                        matToUse = new Material(fallbackShader);
                    }
                    else
                    {
                        matToUse = new Material(Shader.Find("Unlit/Color"));
                    }
                }
            }
            
            _skyDomeMeshRenderer.sharedMaterial = matToUse;
            
            if (_skyDomeMeshRenderer.sharedMaterial != null)
            {
                _skyDomeMeshRenderer.sharedMaterial.doubleSidedGI = true;
                _skyDomeMeshRenderer.sharedMaterial.renderQueue = 3000;
            }
        }

        private void CreateConstellationLines()
        {
            if (constellationData == null || constellationData.constellations == null) return;
            
            List<LineRenderer> lines = new List<LineRenderer>();
            
            foreach (var constellation in constellationData.constellations)
            {
                if (constellation.linePairs == null || constellation.stars == null) continue;
                
                for (int i = 0; i < constellation.linePairs.Length; i += 2)
                {
                    if (i + 1 >= constellation.linePairs.Length) break;
                    
                    int starA = constellation.linePairs[i];
                    int starB = constellation.linePairs[i + 1];
                    
                    if (starA >= constellation.stars.Length || starB >= constellation.stars.Length) continue;
                    
                    var star1 = constellation.stars[starA];
                    var star2 = constellation.stars[starB];
                    
                    Vector3 localDir1 = SphericalToLocalDirection(star1.sphericalPosition.x, star1.sphericalPosition.y);
                    Vector3 localDir2 = SphericalToLocalDirection(star2.sphericalPosition.x, star2.sphericalPosition.y);
                    Vector3 pos1 = localDir1 * skyDomeRadius;
                    Vector3 pos2 = localDir2 * skyDomeRadius;
                    
                    GameObject lineObj = new GameObject(constellation.constellationName + "_Line");
                    lineObj.transform.SetParent(_skyDomeObject.transform);
                    lineObj.transform.localPosition = Vector3.zero;
                    lineObj.transform.localRotation = Quaternion.identity;
                    
                    LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                    lr.positionCount = 2;
                    lr.SetPosition(0, pos1);
                    lr.SetPosition(1, pos2);
                    lr.startWidth = constellationLineWidth;
                    lr.endWidth = constellationLineWidth;
                    lr.startColor = new Color(0.6f, 0.7f, 1f, 0.4f);
                    lr.endColor = new Color(0.6f, 0.7f, 1f, 0.4f);
                    lr.useWorldSpace = false;
                    lr.enabled = showConstellationLines;
                    
                    if (constellationLineMaterial != null)
                    {
                        lr.material = constellationLineMaterial;
                    }
                    else
                    {
                        lr.material = new Material(Shader.Find("Sprites/Default"));
                    }
                    
                    lines.Add(lr);
                }
            }
            
            _constellationLines = lines.ToArray();
        }

        void Update()
        {
            if (!_isInitialized) return;
            
            UpdateSkyDomePosition();
            
            // Update time from ServerWeatherController if available
            if (ServerWeatherController.Instance != null)
            {
                serverTimeOfDay = ServerWeatherController.Instance.TimeOfDay;
            }
            
            UpdateSkyDomeRotation();
            
            if (forceFullVisibility)
            {
                _starVisibility = 1f;
                UpdateStarVisibility();
            }
        }

        private void UpdateSkyDomePosition()
        {
            if (Camera.main == null || _skyDomeObject == null) return;
            
            Vector3 cameraPos = Camera.main.transform.position;
            Vector3 domeWorldPos = new Vector3(
                cameraPos.x,
                cameraPos.y + skyDomeHeightOffset,
                cameraPos.z
            );
            
            _skyDomeObject.transform.position = domeWorldPos;
            
            // After setting position, apply rotation
            // Rotation is around the SkyDome's own center (since localPosition is 0,0,0)
            if (enableRotation)
            {
                float rotationPerGameHour = EARTH_ROTATION_DEGREES_PER_GAME_HOUR * rotationSpeedMultiplier;
                float currentRotation = serverTimeOfDay * rotationPerGameHour;
                _skyDomeObject.transform.rotation = Quaternion.Euler(0f, currentRotation, 0f);
            }
        }

        private void UpdateSkyDomeRotation()
        {
            if (!enableRotation) return;
            if (_skyDomeObject == null) return;
            
            float rotationPerGameHour = EARTH_ROTATION_DEGREES_PER_GAME_HOUR * rotationSpeedMultiplier;
            float currentRotation = serverTimeOfDay * rotationPerGameHour;
            
            _skyDomeObject.transform.rotation = Quaternion.Euler(0f, currentRotation, 0f);
        }

        /// <summary>
        /// Called by ServerWeatherController to update the server time.
        /// This drives the sky dome rotation.
        /// </summary>
        public void SetServerTimeOfDay(float time)
        {
            serverTimeOfDay = time;
        }

        /// <summary>
        /// Enable or disable constellation lines.
        /// Can be called from gameplay systems (e.g., character perks).
        /// </summary>
        public void SetConstellationLinesVisible(bool visible)
        {
            showConstellationLines = visible;
            
            if (_constellationLines != null)
            {
                foreach (var line in _constellationLines)
                {
                    if (line != null)
                    {
                        line.enabled = visible;
                    }
                }
            }
        }

        /// <summary>
        /// Set the width of constellation lines.
        /// </summary>
        public void SetConstellationLineWidth(float width)
        {
            constellationLineWidth = width;
            
            if (_constellationLines != null)
            {
                foreach (var line in _constellationLines)
                {
                    if (line != null)
                    {
                        line.startWidth = width;
                        line.endWidth = width;
                    }
                }
            }
        }

        private int GetTotalStarCount()
        {
            int count = 0;
            if (constellationData?.constellations == null) return 0;
            
            foreach (var constellation in constellationData.constellations)
            {
                if (constellation.stars != null)
                    count += constellation.stars.Length;
            }
            return count;
        }

        public void SetStarVisibility(float timeOfDay)
        {
            if (forceFullVisibility) return;
            
            _starVisibility = CalculateStarVisibility(timeOfDay);
            UpdateStarVisibility();
        }

        private float CalculateStarVisibility(float timeOfDay)
        {
            if (timeOfDay >= NIGHT_START_HOUR || timeOfDay < NIGHT_END_HOUR)
            {
                return 1f;
            }
            
            if (timeOfDay >= (NIGHT_START_HOUR - TWILIGHT_DURATION_HOURS) && 
                timeOfDay < NIGHT_START_HOUR)
            {
                float elapsed = timeOfDay - (NIGHT_START_HOUR - TWILIGHT_DURATION_HOURS);
                return Mathf.InverseLerp(0f, TWILIGHT_DURATION_HOURS, elapsed) * 0.7f;
            }
            
            if (timeOfDay >= NIGHT_END_HOUR && 
                timeOfDay < (NIGHT_END_HOUR + TWILIGHT_DURATION_HOURS))
            {
                float elapsed = timeOfDay - NIGHT_END_HOUR;
                return 1f - Mathf.InverseLerp(0f, TWILIGHT_DURATION_HOURS, elapsed);
            }
            
            return 0f;
        }

        private void UpdateStarVisibility()
        {
            if (_skyDomeMeshRenderer != null)
            {
                _skyDomeMeshRenderer.enabled = _starVisibility > 0.01f;
                
                if (_skyDomeMeshRenderer.sharedMaterial != null)
                {
                    Color color = new Color(1f, 1f, 1f, _starVisibility);
                    _skyDomeMeshRenderer.sharedMaterial.color = color;
                    
                    if (_skyDomeMeshRenderer.sharedMaterial.HasProperty("_StarColor"))
                    {
                        _skyDomeMeshRenderer.sharedMaterial.SetColor("_StarColor", color);
                    }
                }
            }
        }

        public void RebuildSkyDome()
        {
            Cleanup();
            _isInitialized = false;
            Initialize();
        }

        private void Cleanup()
        {
            if (_skyDomeObject != null)
            {
                Destroy(_skyDomeObject);
                _skyDomeObject = null;
            }
            
            _skyDomeMeshFilter = null;
            _skyDomeMeshRenderer = null;
            _constellationLines = null;
            _isInitialized = false;
        }

        void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;
            
            Vector3 domePos = _skyDomeObject != null 
                ? _skyDomeObject.transform.position 
                : (Camera.main != null ? Camera.main.transform.position + Vector3.up * skyDomeHeightOffset : Vector3.zero);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(domePos, skyDomeRadius);
            
            if (Camera.main != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(Camera.main.transform.position, domePos);
            }
        }
    }
}