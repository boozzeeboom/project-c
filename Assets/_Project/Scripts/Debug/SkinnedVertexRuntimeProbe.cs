// SkinnedVertexRuntimeProbe — T-JITTER16: runtime-проверка вершин после SkinnedMeshRenderer.BakeMesh().
//
// Назначение:
//   Разделяет локальное движение baked-вершин и движение тех же вершин через
//   localToWorldMatrix. Запускать на NetworkPlayer в двух условиях:
//   A) рядом с Unity origin;
//   B) после загрузки WorldScene_0_0 примерно на (40000, 3000, 40000).
//
// Интерпретация:
//   - local max/rms растут в B относительно A — проблема появляется в CPU-side
//     deformation / skinning-пути после BakeMesh.
//   - local стабилен, relative/world растут — проблема ниже локального skinning:
//     transform matrix / world-coordinate precision / render path.
//   - оба стабильны, а визуальная тряска остаётся — BakeMesh не воспроизводит
//     конкретный render artifact; нужны screenshots + отдельная проверка камеры/TAA.
//
// Зонд диагностический и не меняет Animator, SkinnedMeshRenderer или NetworkTransform.
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ProjectC.DebugTools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ProjectC/Debug/Skinned Vertex Runtime Probe")]
    public sealed class SkinnedVertexRuntimeProbe : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Оставьте пустым для автоматического выбора активного SkinnedMeshRenderer с наибольшим мешем.")]
        [SerializeField] private SkinnedMeshRenderer _renderer;

        [Header("Sampling")]
        [SerializeField, Range(8, 512)] private int _sampleCount = 64;
        [SerializeField, Min(0.25f)] private float _reportInterval = 1f;
        [SerializeField] private bool _logging = true;

        private Mesh _bakedMesh;
        private readonly List<Vector3> _bakedVertices = new List<Vector3>(4096);
        private int[] _sampleIndices;
        private Vector3[] _previousLocal;
        private Double3[] _previousRelative;
        private Double3[] _previousWorld;
        private int _vertexCount;
        private bool _initialized;
        private float _windowStart;
        private int _frames;
        private double _localMaxMm;
        private double _relativeMaxMm;
        private double _worldMaxMm;
        private double _localSumSqMm;
        private double _relativeSumSqMm;
        private double _worldSumSqMm;
        private int _deltaCount;
        private string _rendererPath;
        private Animator _animator;
        private readonly StringBuilder _log = new StringBuilder(512);

        private void Awake()
        {
            _bakedMesh = new Mesh
            {
                name = $"{name}_SkinnedVertexProbeBake",
                hideFlags = HideFlags.HideAndDontSave
            };
            _bakedMesh.MarkDynamic();
        }

        private void OnDestroy()
        {
            if (_bakedMesh != null)
            {
                Destroy(_bakedMesh);
                _bakedMesh = null;
            }
        }

        private void LateUpdate()
        {
            if (!TryResolveRenderer()) return;

            try
            {
                _renderer.BakeMesh(_bakedMesh, true);
                _bakedMesh.GetVertices(_bakedVertices);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SkinnedVertexProbe] {name}: BakeMesh failed: {ex.Message}", this);
                enabled = false;
                return;
            }

            if (_bakedVertices.Count == 0) return;

            if (_vertexCount != _bakedVertices.Count || _sampleIndices == null)
            {
                RebuildSampling(_bakedVertices.Count);
                return;
            }

            Matrix4x4 localToWorld = _renderer.localToWorldMatrix;
            if (!_initialized)
            {
                CaptureBaseline(localToWorld);
                return;
            }

            for (int i = 0; i < _sampleIndices.Length; i++)
            {
                Vector3 local = _bakedVertices[_sampleIndices[i]];
                Double3 relative = MultiplyVector(localToWorld, local);
                Double3 world = MultiplyPoint(localToWorld, local);

                Vector3 localDelta = local - _previousLocal[i];
                Double3 relativeDelta = relative - _previousRelative[i];
                Double3 worldDelta = world - _previousWorld[i];

                double localMm = localDelta.magnitude * 1000.0;
                double relativeMm = relativeDelta.Magnitude * 1000.0;
                double worldMm = worldDelta.Magnitude * 1000.0;

                if (localMm > _localMaxMm) _localMaxMm = localMm;
                if (relativeMm > _relativeMaxMm) _relativeMaxMm = relativeMm;
                if (worldMm > _worldMaxMm) _worldMaxMm = worldMm;

                _localSumSqMm += localMm * localMm;
                _relativeSumSqMm += relativeMm * relativeMm;
                _worldSumSqMm += worldMm * worldMm;
                _deltaCount++;

                _previousLocal[i] = local;
                _previousRelative[i] = relative;
                _previousWorld[i] = world;
            }

            _frames++;
            float now = Time.unscaledTime;
            if (_logging && now - _windowStart >= _reportInterval)
            {
                Report(now - _windowStart);
            }
        }

        private bool TryResolveRenderer()
        {
            if (_renderer != null && _renderer.enabled && _renderer.gameObject.activeInHierarchy && _renderer.sharedMesh != null)
                return true;

            SkinnedMeshRenderer best = null;
            int bestVertexCount = -1;
            foreach (var candidate in GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy || candidate.sharedMesh == null)
                    continue;

                int count = candidate.sharedMesh.vertexCount;
                if (count > bestVertexCount)
                {
                    best = candidate;
                    bestVertexCount = count;
                }
            }

            if (best == null) return false;

            bool changed = _renderer != best;
            _renderer = best;
            if (changed)
            {
                _rendererPath = GetPath(_renderer.transform);
                _animator = FindAnimator();
                _initialized = false;
                _sampleIndices = null;
                Debug.Log($"[SkinnedVertexProbe] {name}: renderer='{_rendererPath}' " +
                          $"mesh='{_renderer.sharedMesh.name}' vertices={_renderer.sharedMesh.vertexCount} " +
                          $"distOrigin={transform.position.magnitude:F0}m", this);
            }

            return true;
        }

        private void RebuildSampling(int vertexCount)
        {
            _vertexCount = vertexCount;
            int count = Mathf.Clamp(_sampleCount, 8, Mathf.Min(512, vertexCount));
            _sampleIndices = new int[count];
            _previousLocal = new Vector3[count];
            _previousRelative = new Double3[count];
            _previousWorld = new Double3[count];
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : i / (float)(count - 1);
                _sampleIndices[i] = Mathf.Clamp(Mathf.RoundToInt(t * (vertexCount - 1)), 0, vertexCount - 1);
            }

            _initialized = false;
            Debug.Log($"[SkinnedVertexProbe] {name}: sample set rebuilt — {count}/{vertexCount} vertices.", this);
        }

        private void CaptureBaseline(Matrix4x4 localToWorld)
        {
            for (int i = 0; i < _sampleIndices.Length; i++)
            {
                Vector3 local = _bakedVertices[_sampleIndices[i]];
                _previousLocal[i] = local;
                _previousRelative[i] = MultiplyVector(localToWorld, local);
                _previousWorld[i] = MultiplyPoint(localToWorld, local);
            }

            _initialized = true;
            _windowStart = Time.unscaledTime;
            _frames = 0;
            ResetWindowMetrics();
            Debug.Log($"[SkinnedVertexProbe] {name}: baseline captured at " +
                      $"distOrigin={transform.position.magnitude:F0}m state={GetStateName()}", this);
        }

        private void Report(float elapsed)
        {
            double localRms = SqrtMean(_localSumSqMm, _deltaCount);
            double relativeRms = SqrtMean(_relativeSumSqMm, _deltaCount);
            double worldRms = SqrtMean(_worldSumSqMm, _deltaCount);

            _log.Length = 0;
            _log.Append($"[SkinnedVertexProbe] {name}: ");
            _log.Append($"state={GetStateName()} frames={_frames} ");
            _log.Append($"distOrigin={transform.position.magnitude:F0}m fps={_frames / Mathf.Max(elapsed, 0.001f):F0} ");
            _log.Append($"local max/rms={_localMaxMm:F3}/{localRms:F3}mm ");
            _log.Append($"relative max/rms={_relativeMaxMm:F3}/{relativeRms:F3}mm ");
            _log.Append($"world max/rms={_worldMaxMm:F3}/{worldRms:F3}mm ");
            _log.Append($"samples={_sampleIndices.Length} renderer='{_rendererPath}'");
            Debug.Log(_log.ToString(), this);

            _windowStart = Time.unscaledTime;
            _frames = 0;
            ResetWindowMetrics();
        }

        private void ResetWindowMetrics()
        {
            _localMaxMm = 0.0;
            _relativeMaxMm = 0.0;
            _worldMaxMm = 0.0;
            _localSumSqMm = 0.0;
            _relativeSumSqMm = 0.0;
            _worldSumSqMm = 0.0;
            _deltaCount = 0;
        }

        private Animator FindAnimator()
        {
            foreach (var animator in GetComponentsInChildren<Animator>(false))
            {
                if (animator != null && animator.isHuman)
                    return animator;
            }
            return null;
        }

        private string GetStateName()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return "no-controller";
            return _animator.GetCurrentAnimatorStateInfo(0).shortNameHash.ToString();
        }

        private static double SqrtMean(double sumSq, int count)
        {
            return count > 0 ? System.Math.Sqrt(sumSq / count) : 0.0;
        }

        private static Double3 MultiplyVector(Matrix4x4 matrix, Vector3 value)
        {
            return new Double3(
                matrix.m00 * value.x + matrix.m01 * value.y + matrix.m02 * value.z,
                matrix.m10 * value.x + matrix.m11 * value.y + matrix.m12 * value.z,
                matrix.m20 * value.x + matrix.m21 * value.y + matrix.m22 * value.z);
        }

        private static Double3 MultiplyPoint(Matrix4x4 matrix, Vector3 value)
        {
            return new Double3(
                matrix.m00 * value.x + matrix.m01 * value.y + matrix.m02 * value.z + matrix.m03,
                matrix.m10 * value.x + matrix.m11 * value.y + matrix.m12 * value.z + matrix.m13,
                matrix.m20 * value.x + matrix.m21 * value.y + matrix.m22 * value.z + matrix.m23);
        }

        private static string GetPath(Transform current)
        {
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }
            return path;
        }

        private struct Double3
        {
            private readonly double _x;
            private readonly double _y;
            private readonly double _z;

            public Double3(double x, double y, double z)
            {
                _x = x;
                _y = y;
                _z = z;
            }

            public double Magnitude => System.Math.Sqrt(_x * _x + _y * _y + _z * _z);

            public static Double3 operator -(Double3 left, Double3 right)
            {
                return new Double3(left._x - right._x, left._y - right._y, left._z - right._z);
            }
        }
    }
}
