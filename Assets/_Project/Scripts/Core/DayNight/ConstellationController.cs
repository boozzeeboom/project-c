using UnityEngine;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public class ConstellationController : MonoBehaviour
    {
        public ConstellationData constellationData;
        public Material starMaterial;

        private GameObject _starFieldObject;
        private GameObject _linesObject;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private LineRenderer[] _lineRenderers;
        private float _starVisibility = 0f;
        private const float NIGHT_START_HOUR = 21f;
        private const float NIGHT_END_HOUR = 5f;
        private const float TWILIGHT_DURATION_HOURS = 1.5f;

        void Start()
        {
            if (constellationData != null)
            {
                CreateStarField();
                CreateConstellationLines();
            }
        }

        void Update()
        {
            if (ServerWeatherController.Instance != null)
            {
                float timeOfDay = ServerWeatherController.Instance.TimeOfDay;
                float visibility = CalculateStarVisibility(timeOfDay);

                if (Mathf.Abs(visibility - _starVisibility) > 0.01f)
                {
                    _starVisibility = visibility;
                    UpdateStarVisibility();
                }
            }
        }

        public void SetStarVisibility(float timeOfDay)
        {
            _starVisibility = CalculateStarVisibility(timeOfDay);
            UpdateStarVisibility();
        }

        private float CalculateStarVisibility(float timeOfDay)
        {
            if (timeOfDay >= NIGHT_START_HOUR)
            {
                return 1f;
            }

            if (timeOfDay >= (NIGHT_START_HOUR - TWILIGHT_DURATION_HOURS) && timeOfDay < NIGHT_START_HOUR)
            {
                float elapsed = timeOfDay - (NIGHT_START_HOUR - TWILIGHT_DURATION_HOURS);
                return Mathf.InverseLerp(0f, TWILIGHT_DURATION_HOURS, elapsed) * 0.7f;
            }

            if (timeOfDay >= 5f && timeOfDay < NIGHT_END_HOUR)
            {
                float remaining = NIGHT_END_HOUR - timeOfDay;
                if (remaining < 1.5f)
                {
                    return remaining / 1.5f;
                }
                return 0.7f;
            }

            if (timeOfDay >= NIGHT_END_HOUR && timeOfDay < (NIGHT_END_HOUR + TWILIGHT_DURATION_HOURS))
            {
                float elapsed = timeOfDay - NIGHT_END_HOUR;
                return 1f - Mathf.InverseLerp(0f, TWILIGHT_DURATION_HOURS, elapsed);
            }

            return 0f;
        }

        private void UpdateStarVisibility()
        {
            if (_meshRenderer != null)
            {
                Color c = _meshRenderer.material.color;
                c.a = _starVisibility;
                _meshRenderer.material.color = c;
            }

            if (_lineRenderers != null)
            {
                for (int i = 0; i < _lineRenderers.Length; i++)
                {
                    if (_lineRenderers[i] != null)
                    {
                        _lineRenderers[i].startColor = new Color(1f, 1f, 1f, _starVisibility * 0.5f);
                        _lineRenderers[i].endColor = new Color(1f, 1f, 1f, _starVisibility * 0.5f);
                    }
                }
            }
        }

        private void CreateStarField()
        {
            _starFieldObject = new GameObject("StarField");
            _starFieldObject.transform.SetParent(transform);
            _starFieldObject.transform.localPosition = Vector3.zero;
            _starFieldObject.transform.localRotation = Quaternion.identity;

            Mesh mesh = new Mesh();
            mesh.name = "StarFieldMesh";

            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> indices = new List<int>();

            int starIndex = 0;
            foreach (var constellation in constellationData.constellations)
            {
                if (constellation.stars == null) continue;

                foreach (var star in constellation.stars)
                {
                    Vector3 pos = SphericalToCartesian(star.sphericalPosition.x, star.sphericalPosition.y, 10000f);

                    float twinkle = Mathf.Sin(Time.time * (1f + star.magnitude * 0.1f) + starIndex) * 0.2f + 0.8f;
                    float size = (2f - star.magnitude) * 2f;

                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * 60f * Mathf.Deg2Rad;
                        float x = Mathf.Cos(angle) * size;
                        float z = Mathf.Sin(angle) * size;

                        vertices.Add(pos + new Vector3(x, 0f, z));
                        colors.Add(new Color(1f, 1f, 1f, twinkle));
                    }

                    indices.Add(starIndex * 6);
                    indices.Add(starIndex * 6 + 1);
                    indices.Add(starIndex * 6 + 2);
                    indices.Add(starIndex * 6);
                    indices.Add(starIndex * 6 + 2);
                    indices.Add(starIndex * 6 + 3);
                    indices.Add(starIndex * 6 + 3);
                    indices.Add(starIndex * 6 + 4);
                    indices.Add(starIndex * 6 + 5);

                    starIndex++;
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();

            MeshFilter mf = _starFieldObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            _meshRenderer = _starFieldObject.AddComponent<MeshRenderer>();
            if (starMaterial != null)
            {
                _meshRenderer.material = starMaterial;
            }
            else
            {
                Shader starShader = Shader.Find("Project C/Stars/Stars");
                if (starShader != null)
                {
                    _meshRenderer.material = new Material(starShader);
                }
            }
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.sortingOrder = 1000;
        }

        private void CreateConstellationLines()
        {
            if (constellationData == null || constellationData.constellations == null) return;

            _linesObject = new GameObject("ConstellationLines");
            _linesObject.transform.SetParent(transform);
            _linesObject.transform.localPosition = Vector3.zero;

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

                    Vector3 pos1 = SphericalToCartesian(star1.sphericalPosition.x, star1.sphericalPosition.y, 10000f);
                    Vector3 pos2 = SphericalToCartesian(star2.sphericalPosition.x, star2.sphericalPosition.y, 10000f);

                    GameObject lineObj = new GameObject(constellation.constellationName + "_Line_" + i);
                    lineObj.transform.SetParent(_linesObject.transform);

                    LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                    lr.positionCount = 2;
                    lr.SetPosition(0, pos1);
                    lr.SetPosition(1, pos2);
                    lr.startWidth = 0.5f;
                    lr.endWidth = 0.5f;
                    lr.startColor = new Color(1f, 1f, 1f, 0.3f);
                    lr.endColor = new Color(1f, 1f, 1f, 0.3f);

                    Material lineMat = new Material(Shader.Find("Sprites/Default"));
                    lr.material = lineMat;

                    lines.Add(lr);
                }
            }

            _lineRenderers = lines.ToArray();
        }

        private Vector3 SphericalToCartesian(float azimuth, float altitude, float radius)
        {
            float azRad = azimuth * Mathf.Deg2Rad;
            float altRad = altitude * Mathf.Deg2Rad;

            float x = radius * Mathf.Cos(altRad) * Mathf.Cos(azRad);
            float y = radius * Mathf.Sin(altRad);
            float z = radius * Mathf.Cos(altRad) * Mathf.Sin(azRad);

            return new Vector3(x, y, z);
        }

        public float GetStarVisibility(float timeOfDay)
        {
            return CalculateStarVisibility(timeOfDay);
        }
    }
}
