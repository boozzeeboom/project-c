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
        public float skyDomeRadius = 900f;
        
        [Tooltip("Height offset above camera for sky dome center")]
        public float skyDomeHeightOffset = 100f;

        [Header("Star Settings")]
        [Tooltip("Base size of star quads in world units")]
        public float baseStarSize = 3f;
        
        [Tooltip("Size variation based on magnitude")]
        public float starSizeMagnitudeScale = 1.5f;

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

        void Awake()
        {
            Debug.Log("[ConstellationController] Awake called");
            
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
            Debug.Log("[ConstellationController] OnEnable - Initializing...");
            Initialize();
        }

        void OnDisable()
        {
            Debug.Log("[ConstellationController] OnDisable - Cleaning up...");
            Cleanup();
        }

        void OnDestroy()
        {
            Debug.Log("[ConstellationController] OnDestroy - Cleaning up...");
            Cleanup();
        }

        private void Initialize()
        {
            if (_isInitialized) return;
            
            if (constellationData == null)
            {
                Debug.LogError("[ConstellationController] No constellation data assigned!");
                return;
            }

            Debug.Log("[ConstellationController] Building sky dome...");
            
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
            // Create sky dome container - ALWAYS at origin in world space, we use local coordinates
            _skyDomeObject = new GameObject("SkyDome_Stars");
            _skyDomeObject.transform.SetParent(transform);
            _skyDomeObject.transform.localPosition = Vector3.zero;
            _skyDomeObject.transform.localRotation = Quaternion.identity;
            
            // Set layer to avoid lighting/raycast issues
            _skyDomeObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            
            Debug.Log("[ConstellationController] SkyDome created at local position (0,0,0)");
        }

        /// <summary>
        /// Converts spherical coordinates (azimuth, altitude) to a LOCAL direction vector.
        /// The LOCAL coordinate system is relative to SkyDome center.
        /// </summary>
        private Vector3 SphericalToLocalDirection(float azimuthDeg, float altitudeDeg)
        {
            float azRad = azimuthDeg * Mathf.Deg2Rad;
            float altRad = altitudeDeg * Mathf.Deg2Rad;
            
            float cosAlt = Mathf.Cos(altRad);
            
            // Local direction from center of dome outward
            return new Vector3(
                cosAlt * Mathf.Cos(azRad),
                Mathf.Sin(altRad),
                cosAlt * Mathf.Sin(azRad)
            ).normalized;
        }

        /// <summary>
        /// Builds the star mesh using LOCAL coordinates.
        /// Stars are positioned at skyDomeRadius distance from center in LOCAL space.
        /// When SkyDome moves, the mesh moves and stars move with it.
        /// </summary>
        private void BuildStarMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> indices = new List<int>();

            int starIndex = 0;
            int totalStars = GetTotalStarCount();
            
            Debug.Log("[ConstellationController] Building mesh for " + totalStars + " stars...");

            foreach (var constellation in constellationData.constellations)
            {
                if (constellation.stars == null) continue;
                
                foreach (var star in constellation.stars)
                {
                    // Get LOCAL position on sky dome surface
                    // The direction is local, multiplied by radius gives local position
                    Vector3 localDir = SphericalToLocalDirection(star.sphericalPosition.x, star.sphericalPosition.y);
                    Vector3 localStarPos = localDir * skyDomeRadius;
                    
                    // Calculate star size based on magnitude
                    float starSize = baseStarSize + (2f - star.magnitude) * starSizeMagnitudeScale;
                    float halfSize = starSize * 0.5f;
                    
                    // Create quad perpendicular to the direction from dome center to star
                    // Use local "up" and "right" vectors for the quad
                    Vector3 right = Vector3.Cross(Vector3.up, localDir);
                    if (right.sqrMagnitude < 0.001f)
                    {
                        right = Vector3.Cross(Vector3.forward, localDir);
                    }
                    right.Normalize();
                    Vector3 up = Vector3.Cross(localDir, right);
                    
                    // Create 4 corners of the star quad in LOCAL coordinates
                    Vector3 p0 = localStarPos + (-right - up) * halfSize;
                    Vector3 p1 = localStarPos + ( right - up) * halfSize;
                    Vector3 p2 = localStarPos + ( right + up) * halfSize;
                    Vector3 p3 = localStarPos + (-right + up) * halfSize;
                    
                    vertices.Add(p0);
                    vertices.Add(p1);
                    vertices.Add(p2);
                    vertices.Add(p3);
                    
                    // Star color with twinkle
                    float twinkle = Mathf.Sin(Time.time * (1f + star.magnitude * 0.5f) + starIndex * 0.1f) * 0.15f + 0.85f;
                    Color starColor = new Color(1f, 1f, 1f, twinkle);
                    colors.Add(starColor);
                    colors.Add(starColor);
                    colors.Add(starColor);
                    colors.Add(starColor);
                    
                    // Two triangles for quad (standard CCW order)
                    // When viewed from INSIDE the sphere, we need to reverse winding
                    // Actually: from inside, the face normal points outward, so standard order works
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
            
            Debug.Log("[ConstellationController] Built mesh: " + starIndex + " stars, " + vertices.Count + " vertices");

            // Create mesh
            Mesh mesh = new Mesh();
            mesh.name = "SkyDomeStars";
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            // Create mesh filter
            _skyDomeMeshFilter = _skyDomeObject.AddComponent<MeshFilter>();
            _skyDomeMeshFilter.sharedMesh = mesh;

            // Create mesh renderer
            _skyDomeMeshRenderer = _skyDomeObject.AddComponent<MeshRenderer>();
            _skyDomeMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _skyDomeMeshRenderer.receiveShadows = false;
            _skyDomeMeshRenderer.sortingOrder = 32767;
            _skyDomeMeshRenderer.lightmapIndex = -1;
            _skyDomeMeshRenderer.realtimeLightmapIndex = -1;

            // Setup material
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
                // Try to load from resources
                matToUse = Resources.Load<Material>("Materials/Stars/StarMaterial");
                if (matToUse == null)
                {
                    // Fallback to sprites default
                    Shader fallbackShader = Shader.Find("Sprites/Default");
                    if (fallbackShader != null)
                    {
                        matToUse = new Material(fallbackShader);
                    }
                    else
                    {
                        // Last resort
                        matToUse = new Material(Shader.Find("Unlit/Color"));
                    }
                    Debug.LogWarning("[ConstellationController] Using fallback material with shader: " + matToUse.shader.name);
                }
            }
            
            _skyDomeMeshRenderer.sharedMaterial = matToUse;
            
            if (_skyDomeMeshRenderer.sharedMaterial != null)
            {
                _skyDomeMeshRenderer.sharedMaterial.doubleSidedGI = true;
                _skyDomeMeshRenderer.sharedMaterial.renderQueue = 3000;
                
                Debug.Log("[ConstellationController] Material set: " + _skyDomeMeshRenderer.sharedMaterial.shader.name);
            }
        }

        /// <summary>
        /// Creates line renderers for constellation connections.
        /// Lines use LOCAL coordinates and will move with SkyDome.
        /// </summary>
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
                    
                    // Get LOCAL positions on sky dome
                    Vector3 localDir1 = SphericalToLocalDirection(star1.sphericalPosition.x, star1.sphericalPosition.y);
                    Vector3 localDir2 = SphericalToLocalDirection(star2.sphericalPosition.x, star2.sphericalPosition.y);
                    Vector3 pos1 = localDir1 * skyDomeRadius;
                    Vector3 pos2 = localDir2 * skyDomeRadius;
                    
                    // Create line object as child of SkyDome
                    GameObject lineObj = new GameObject(constellation.constellationName + "_Line");
                    lineObj.transform.SetParent(_skyDomeObject.transform);
                    lineObj.transform.localPosition = Vector3.zero;
                    lineObj.transform.localRotation = Quaternion.identity;
                    
                    LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                    lr.positionCount = 2;
                    lr.SetPosition(0, pos1);
                    lr.SetPosition(1, pos2);
                    lr.startWidth = 1.5f;
                    lr.endWidth = 1.5f;
                    lr.startColor = new Color(0.6f, 0.7f, 1f, 0.4f);
                    lr.endColor = new Color(0.6f, 0.7f, 1f, 0.4f);
                    lr.useWorldSpace = false; // CRITICAL: local coordinates
                    
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
            Debug.Log("[ConstellationController] Created " + lines.Count + " constellation lines");
        }

        void Update()
        {
            if (!_isInitialized) return;
            
            // Update sky dome position to follow camera
            UpdateSkyDomePosition();
            
            // Force visibility for testing if enabled
            if (forceFullVisibility)
            {
                _starVisibility = 1f;
                UpdateStarVisibility();
            }
            
            // Debug log every 5 seconds
            if (Time.frameCount % 300 == 0)
            {
                LogDebugInfo();
            }
        }

        /// <summary>
        /// Updates sky dome world position to follow the camera.
        /// The mesh uses LOCAL coordinates, so moving SkyDome moves all stars.
        /// </summary>
        private void UpdateSkyDomePosition()
        {
            if (Camera.main == null || _skyDomeObject == null) return;
            
            Vector3 cameraPos = Camera.main.transform.position;
            Vector3 domeWorldPos = new Vector3(
                cameraPos.x,
                cameraPos.y + skyDomeHeightOffset,
                cameraPos.z
            );
            
            // Move SkyDome to camera position in WORLD space
            // Since stars are in LOCAL coordinates, they move with it
            _skyDomeObject.transform.position = domeWorldPos;
        }

        private void LogDebugInfo()
        {
            if (_skyDomeObject == null || _skyDomeMeshRenderer == null) return;
            
            Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            Vector3 domePos = _skyDomeObject.transform.position;
            float dist = Vector3.Distance(camPos, domePos);
            
            Debug.Log("[ConstellationController] DEBUG: " +
                      "Camera=" + camPos +
                      " | SkyDome=" + domePos +
                      " | Dist=" + dist.ToString("F1") +
                      " | Visibility=" + _starVisibility +
                      " | MeshEnabled=" + _skyDomeMeshRenderer.enabled +
                      " | Active=" + _skyDomeObject.activeSelf +
                      " | Shader=" + (_skyDomeMeshRenderer.sharedMaterial?.shader?.name ?? "NULL"));
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

        /// <summary>
        /// Updates star visibility based on time of day.
        /// </summary>
        public void SetStarVisibility(float timeOfDay)
        {
            if (forceFullVisibility) return; // Override for testing
            
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
                    // Handle both custom _StarColor and standard _Color
                    Color color = new Color(1f, 1f, 1f, _starVisibility);
                    _skyDomeMeshRenderer.sharedMaterial.color = color;
                    
                    if (_skyDomeMeshRenderer.sharedMaterial.HasProperty("_StarColor"))
                    {
                        _skyDomeMeshRenderer.sharedMaterial.SetColor("_StarColor", color);
                    }
                }
            }
            
            if (_constellationLines != null)
            {
                foreach (var line in _constellationLines)
                {
                    if (line != null)
                    {
                        line.enabled = _starVisibility > 0.01f;
                    }
                }
            }
        }

        /// <summary>
        /// Rebuilds the sky dome (call if constellation data changes).
        /// </summary>
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
            
            // Draw sky dome wire sphere at current position
            Vector3 domePos = _skyDomeObject != null 
                ? _skyDomeObject.transform.position 
                : (Camera.main != null ? Camera.main.transform.position + Vector3.up * skyDomeHeightOffset : Vector3.zero);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(domePos, skyDomeRadius);
            
            // Draw line from camera to sky dome center
            if (Camera.main != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(Camera.main.transform.position, domePos);
            }
        }
    }
}
