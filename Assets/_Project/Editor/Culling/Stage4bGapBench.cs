// Project C — T-CULL01 / Stage 4b (H5b): Play Mode-скамейка gap close 3 031 → ≤2 500.
//
// Что мерит:
//   1. P1 (площадь) при выключенном городском shadow budget — контроль текущего состояния Stage 4.
//   2. P1 с городским shadow budget на 150 / 200 / 250 м: Renderer.shadowCastingMode = Off
//      для городской геометрии дальше порога (двери слоя 10 не трогаются — их ведёт DoorVisibilityBudget).
//   3. C2 / C3 / C3+sb200 / PILOT / ночь — контроль остальных точек.
//   4. Повтор P1 для проверки воспроизводимости (±5 %).
//
// Что НЕ делает: не создаёт GameObject'ов и компонентов, не сохраняет сцену, не пишет в префабы/сеттинги,
// не запускает Play Mode сам (вход только через меню Run), не вызывает SetActive, не использует Camera.main.
// Теневой бюджет — это A/B-полигон внутри бенча, а не игровой компонент: игра не меняется до решения.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ProjectC.Ship;

namespace ProjectC.EditorTools
{
    /// <summary>Stage 4b: P1 с городским shadow budget + контроль остальных точек (T-CULL01 / H5b).</summary>
    public static class Stage4bGapBench
    {
        private const string RunMenu = "Project C/Culling/Stage 4b Bench/Run";
        private const string AbortMenu = "Project C/Culling/Stage 4b Bench/Abort";

        private const string DataDir = "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA";
        private const string CsvName = "P_stage4b_2026-10-31.csv";
        private const string DateTag = "2026-10-31";

        public const string SessionKey = "ProjectC.Stage4bBench.Running";

        private const float BenchFar = 30000f;
        private const float BenchFov = 60f;

        private const int WarmupFrames = 1500;
        private const int SettleFrames = 40;
        private const int SampleGapFrames = 8;
        private const int SampleCount = 3;

        private const double HostTimeoutSeconds = 90.0;
        private const double RigTimeoutSeconds = 240.0;
        private const double WarmupTimeoutSeconds = 900.0;

        private const string PilotChunkName = "PRIMUM_HOUSES_S9_TRD-S2_P1_45H";
        private const float PilotBoundsInflate = 4f;
        private const float PilotCamDistance = 58f;
        private const float PilotCamHeight = 24f;

        private const int DoorLayer = 10;

        private static readonly Vector3 AuthoredWorldOffset = new Vector3(39936.0f, 2560.03f, 39936.0f);

        private enum Scope { City, Pilot }

        private class BenchCase
        {
            public string id;
            public Vector3 local;
            public float yaw;
            public float pitch;
            public float hour;
            public string phase;
            public Scope scope;
            public bool relativeToPilotChunk;
            /// <summary>0 = городской shadow budget выключен; иначе порог в метрах.</summary>
            public float shadowBudget;
        }

        private static List<BenchCase> BuildCases()
        {
            var p1 = new Vector3(-342.90f, -50.43f, -102.20f);
            return new List<BenchCase>
            {
                new BenchCase { id = "P1", local = p1, yaw = 337.5f, hour = 12f, phase = "Midday", scope = Scope.City, shadowBudget = 0f },
                new BenchCase { id = "P1_sb150", local = p1, yaw = 337.5f, hour = 12f, phase = "Midday", scope = Scope.City, shadowBudget = 150f },
                new BenchCase { id = "P1_sb200", local = p1, yaw = 337.5f, hour = 12f, phase = "Midday", scope = Scope.City, shadowBudget = 200f },
                new BenchCase { id = "P1_sb250", local = p1, yaw = 337.5f, hour = 12f, phase = "Midday", scope = Scope.City, shadowBudget = 250f },
                new BenchCase { id = "P1_repeat", local = p1, yaw = 337.5f, hour = 12f, phase = "Midday", scope = Scope.City, shadowBudget = 0f },
                new BenchCase { id = "C2", local = new Vector3(-382.90f, -58.23f, -126.20f), yaw = 225f, hour = 12f, phase = "Midday", scope = Scope.City, shadowBudget = 0f },
                new BenchCase { id = "C3", local = new Vector3(-416.00f, -27.94f, -36.00f), yaw = 132f, pitch = -18f, hour = 12f, phase = "Midday", scope = Scope.City, shadowBudget = 0f },
                new BenchCase { id = "C3_sb200", local = new Vector3(-416.00f, -27.94f, -36.00f), yaw = 132f, pitch = -18f, hour = 12f, phase = "Midday", scope = Scope.City, shadowBudget = 200f },
                new BenchCase { id = "PILOT", local = Vector3.zero, yaw = 0f, hour = 12f, phase = "Midday", scope = Scope.Pilot, relativeToPilotChunk = true, shadowBudget = 0f },
                new BenchCase { id = "PILOT_sb200", local = Vector3.zero, yaw = 0f, hour = 12f, phase = "Midday", scope = Scope.Pilot, relativeToPilotChunk = true, shadowBudget = 200f },
                new BenchCase { id = "C1_night", local = p1, yaw = 337.5f, hour = 23f, phase = "Night", scope = Scope.City, shadowBudget = 0f },
            };
        }

        // ------------------------------------------------------------------ состояние

        private enum State { Off, WaitPlayMode, WaitHost, WaitRig, Warmup, Point }

        private static State _state = State.Off;
        private static readonly List<BenchCase> _cases = BuildCases();
        private static int _caseIndex;

        private static readonly List<int> _dc = new List<int>();
        private static readonly List<int> _spc = new List<int>();
        private static readonly List<int> _tris = new List<int>();
        private static readonly List<int> _sc = new List<int>();
        private static readonly List<int> _hybridDC = new List<int>();
        private static readonly List<int> _hybridInst = new List<int>();
        private static readonly List<int> _srpDC = new List<int>();
        private static readonly List<double> _ms = new List<double>();

        private static Camera _cam;
        private static Transform _worldRoot;
        private static Renderer[] _allRenderers;
        private static Renderer[] _shipRenderers;
        private static Renderer[] _pilotRenderers;
        private static readonly HashSet<Renderer> _shipSet = new HashSet<Renderer>();
        private static Bounds _pilotBounds;
        private static readonly List<Renderer> _forcedOff = new List<Renderer>();

        // Городской shadow budget (A/B внутри бенча).
        private static readonly List<Renderer> _cityShadowCandidates = new List<Renderer>();
        private static readonly List<ShadowCastingMode> _cityShadowBase = new List<ShadowCastingMode>();
        private static int _shadowOffCount;

        private static Vector3 _targetPos;
        private static Quaternion _targetRot;
        private static int _settleUntilFrame;
        private static int _nextSampleFrame;
        private static bool _censusDone;
        private static int _censusScope;
        private static int _censusInFrustum;
        private static int _censusDoorsInFrustum;
        private static int _censusEnabledTotal;
        private static double _stateSince;
        private static double _caseWindowStart;
        private static int _caseWindowFrame;
        private static int _warmStartFrame;
        private static bool _hostRequested;
        private static bool _menuHidden;
        private static string _outRawPath;
        private static string _outCsvPath;
        private static string _phaseLabel = "unknown";
        private static string _runTag = "";
        private static float _smallMeshPct;

        private static double Now { get { return EditorApplication.timeSinceStartup; } }
        private static readonly Plane[] _planes = new Plane[6];

        // --------------------------------------------------------------------- меню

        [MenuItem(RunMenu)]
        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Stage4bBench] Уже в Play Mode / вход выполняется. Сначала Abort.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dataDir = Path.Combine(projectRoot, DataDir);
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

            _smallMeshPct = ReadUrpFloat("m_SmallMeshScreenPercentage");
            _phaseLabel = ReadGrdPhaseLabel() + "_sm" + _smallMeshPct.ToString("0.##", CultureInfo.InvariantCulture).Replace(".", "p");
            _runTag = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
            _outRawPath = Path.Combine(dataDir, "P_stage4b_" + _phaseLabel + "_" + DateTag + "_raw.txt");
            _outCsvPath = Path.Combine(dataDir, CsvName);
            if (File.Exists(_outRawPath)) File.Delete(_outRawPath);

            _state = State.WaitPlayMode;
            _caseIndex = 0;
            _hostRequested = false;
            _menuHidden = false;
            _cam = null;
            _worldRoot = null;
            _allRenderers = null;
            _shipRenderers = null;
            _pilotRenderers = null;
            _forcedOff.Clear();
            _cityShadowCandidates.Clear();
            _cityShadowBase.Clear();
            _shadowOffCount = 0;
            _stateSince = Now;

            SessionState.SetBool(SessionKey, true);
            SessionState.SetInt(SessionKey + ".reloads", 0);

            Append("NOTE;bench=Stage4bGapBench;unity=" + Application.unityVersion +
                   ";date=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                   ";phase=" + _phaseLabel);
            Append(ReadRenderConfigLine());
            Append("NOTE;method=PlayMode→Host;warmup=" + WarmupFrames + "fr;settle=" + SettleFrames +
                   "fr;samples=" + SampleCount + "x gap " + SampleGapFrames + "fr;metric=UnityStats median;far=" +
                   BenchFar.ToString("F0", CultureInfo.InvariantCulture) + ";fov=" + BenchFov.ToString("F0", CultureInfo.InvariantCulture));
            Append("NOTE;ships=all_ship_roots(ShipRootReference)_renderers_disabled_runtime;prefab_fbx_scene_untouched");
            Append("NOTE;shadow_budget=A/B_inside_bench;renderer.shadowCastingMode_off_beyond_threshold;doors_layer10_untouched");

            Debug.Log("[Stage4bBench] Старт (" + _phaseLabel + "), " + _cases.Count + " точек. Файл: " + _outRawPath);

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(AbortMenu)]
        public static void Abort()
        {
            Finish("ABORTED_BY_USER", false);
        }

        // ------------------------------------------------------------ чтение настроек

        private static string ReadGrdPhaseLabel()
        {
            string urp = FindUrpAssetPath();
            int mode = ReadSerializedInt(urp, "m_GPUResidentDrawerMode");
            if (mode < 1) return "baseline";
            int occ = ReadSerializedInt(urp, "m_GPUResidentDrawerEnableOcclusionCullingInCameras");
            return occ >= 1 ? "grd" : "grd_noocc";
        }

        private static string FindUrpAssetPath()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null) return null;
            string p = AssetDatabase.GetAssetPath(rp);
            return string.IsNullOrEmpty(p) ? null : p;
        }

        private static int ReadSerializedInt(string assetPath, string prop)
        {
            if (string.IsNullOrEmpty(assetPath)) return -1;
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (obj == null) return -1;
            var so = new SerializedObject(obj);
            var p = so.FindProperty(prop);
            return p != null ? p.intValue : -1;
        }

        private static float ReadSerializedFloat(string assetPath, string prop)
        {
            if (string.IsNullOrEmpty(assetPath)) return -1f;
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (obj == null) return -1f;
            var so = new SerializedObject(obj);
            var p = so.FindProperty(prop);
            return p != null ? p.floatValue : -1f;
        }

        private static float ReadUrpFloat(string prop)
        {
            return ReadSerializedFloat(FindUrpAssetPath(), prop);
        }

        private static bool ReadSerializedBool(string assetPath, string prop)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (obj == null) return false;
            var so = new SerializedObject(obj);
            var p = so.FindProperty(prop);
            return p != null && p.boolValue;
        }

        private static string ReadRenderConfigLine()
        {
            string urp = FindUrpAssetPath();
            int grd = ReadSerializedInt(urp, "m_GPUResidentDrawerMode");
            int occ = ReadSerializedInt(urp, "m_GPUResidentDrawerEnableOcclusionCullingInCameras");
            float smallMesh = ReadSerializedFloat(urp, "m_SmallMeshScreenPercentage");
            float shadowDist = ReadSerializedFloat(urp, "m_ShadowDistance");
            int cascades = ReadSerializedInt(urp, "m_ShadowCascadeCount");
            bool soft = ReadSerializedBool(urp, "m_SoftShadowsSupported");
            int res = ReadSerializedInt(urp, "m_MainLightShadowmapResolution");
            int brg = -1;
            var gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs != null)
            {
                var so = new SerializedObject(gs);
                var p = so.FindProperty("m_BrgStripping");
                if (p != null) brg = p.intValue;
            }
            int quality = QualitySettings.GetQualityLevel();
            return "CONFIG;phase=" + _phaseLabel +
                   ";urpAsset=" + (urp ?? "none") +
                   ";grdMode=" + grd +
                   ";grdOcclusionInCameras=" + occ +
                   ";smallMeshScreenPercentage=" + smallMesh.ToString("0.##", CultureInfo.InvariantCulture) +
                   ";brgStripping=" + brg +
                   ";shadowDistance=" + shadowDist.ToString("F0", CultureInfo.InvariantCulture) +
                   ";shadowCascades=" + cascades +
                   ";softShadows=" + soft +
                   ";mainShadowmap=" + res +
                   ";qualityLevel=" + quality + "(" + QualitySettings.names[quality] + ")" +
                   ";lodBias=" + QualitySettings.lodBias.ToString("F2", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------- тик

        private static void Tick()
        {
            try { Step(); }
            catch (Exception e)
            {
                Debug.LogError("[Stage4bBench] Исключение: " + e);
                Finish("EXCEPTION", false);
            }
        }

        private static void Step()
        {
            switch (_state)
            {
                case State.WaitPlayMode:
                    if (!EditorApplication.isPlaying) return;
                    if (Now - _stateSince < 2.0) return;
                    _state = State.WaitHost;
                    _stateSince = Now;
                    break;

                case State.WaitHost:
                    if (!TryStartHost())
                    {
                        if (Now - _stateSince > HostTimeoutSeconds) Finish("HOST_TIMEOUT", false);
                        return;
                    }
                    _state = State.WaitRig;
                    _stateSince = Now;
                    break;

                case State.WaitRig:
                    if (!TryAcquireRig())
                    {
                        if (Now - _stateSince > RigTimeoutSeconds) Finish("RIG_TIMEOUT", false);
                        return;
                    }
                    PrepareBench();
                    _warmStartFrame = Time.frameCount;
                    _state = State.Warmup;
                    _stateSince = Now;
                    break;

                case State.Warmup:
                    if (Time.frameCount - _warmStartFrame < WarmupFrames)
                    {
                        if (Now - _stateSince > WarmupTimeoutSeconds) Finish("WARMUP_TIMEOUT", false);
                        return;
                    }
                    CacheRenderers();
                    _state = State.Point;
                    BeginCase(0);
                    break;

                case State.Point:
                    StepPoint();
                    break;
            }
        }

        // -------------------------------------------------------------- Play Mode → Host

        private static bool TryStartHost()
        {
            if (TryAcquireRig()) { _hostRequested = true; _menuHidden = true; return true; }

            var menu = FindByTypeName("MainMenuWindow");
            var nmc = FindByTypeName("NetworkManagerController");
            if (nmc == null) return false;

            if (!_hostRequested)
            {
                _hostRequested = true;
                if (menu != null)
                {
                    var onClick = menu.GetType().GetMethod("OnHostClicked", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (onClick != null)
                    {
                        onClick.Invoke(menu, null);
                        Debug.Log("[Stage4bBench] Host запрошен через MainMenuWindow.OnHostClicked()");
                    }
                    else
                    {
                        var start = nmc.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
                        if (start != null) start.Invoke(nmc, null);
                        Debug.Log("[Stage4bBench] Host запрошен напрямую через NetworkManagerController.StartHost()");
                    }
                }
                else
                {
                    var start = nmc.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
                    if (start != null) start.Invoke(nmc, null);
                    Debug.Log("[Stage4bBench] MainMenuWindow не найден: StartHost() напрямую");
                }
            }

            if (!_menuHidden && menu != null)
            {
                var hide = menu.GetType().GetMethod("Hide", BindingFlags.Instance | BindingFlags.Public);
                if (hide != null) { hide.Invoke(menu, null); _menuHidden = true; }
            }
            else if (menu == null) _menuHidden = true;

            return _menuHidden;
        }

        private static bool TryAcquireRig()
        {
            var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null || c.cameraType != CameraType.Game) continue;
                if (c.targetTexture != null) continue;
                if (!IsUnderThirdPersonRig(c.transform)) continue;
                _cam = c;
                return true;
            }
            return false;
        }

        private static bool IsUnderThirdPersonRig(Transform t)
        {
            var node = t;
            for (int depth = 0; depth < 6 && node != null; depth++)
            {
                if (node.name.StartsWith("ThirdPersonCamera", StringComparison.OrdinalIgnoreCase)) return true;
                node = node.parent;
            }
            return false;
        }

        private static void PrepareBench()
        {
            var behaviours = _cam.GetComponentsInParent<MonoBehaviour>(true);
            int disabled = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null || !b.enabled) continue;
                string n = b.GetType().Name;
                if (n == "SpringArmCamera" || n == "ThirdPersonCameraDefault_EmptyTEST")
                {
                    b.enabled = false;
                    disabled++;
                }
            }

            _cam.farClipPlane = BenchFar;
            _cam.fieldOfView = BenchFov;
            _cam.nearClipPlane = 0.1f;

            _worldRoot = FindWorldRoot();
            ForcePhase(12f);

            Debug.Log("[Stage4bBench] Риг: " + _cam.name + " (far " + _cam.farClipPlane + ", fov " + _cam.fieldOfView +
                      "), отключено компонентов-двигателей камеры: " + disabled +
                      ", WorldRoot_0_0=" + (_worldRoot != null ? _worldRoot.position.ToString("F2") : "нет"));

            Append("NOTE;camera;name=" + _cam.name + ";occlusionCulling=" + _cam.useOcclusionCulling +
                   ";far=" + _cam.farClipPlane.ToString("F0", CultureInfo.InvariantCulture) +
                   ";fov=" + _cam.fieldOfView.ToString("F0", CultureInfo.InvariantCulture));
        }

        private static Transform FindWorldRoot()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i] != null && roots[i].name == "WorldRoot_0_0") return roots[i].transform;
            return null;
        }

        private static void ForcePhase(float hour)
        {
            var dnc = FindByTypeName("DayNightController");
            if (dnc == null)
            {
                Debug.LogWarning("[Stage4bBench] DayNightController не найден — фаза суток не форсирована");
                return;
            }

            var type = dnc.GetType();
            SetPrivateFloat(type, dnc, "_blendProgress", 1f);
            SetPrivateFloat(type, dnc, "_targetBlendProgress", 1f);

            var setTime = type.GetMethod("SetTimeOfDay", BindingFlags.Instance | BindingFlags.Public);
            var refresh = type.GetMethod("ForceRefresh", BindingFlags.Instance | BindingFlags.Public);
            if (setTime != null) setTime.Invoke(dnc, new object[] { hour });
            if (refresh != null) refresh.Invoke(dnc, null);

            var mb = dnc as MonoBehaviour;
            if (mb != null) mb.enabled = false;

            var phaseProp = type.GetProperty("CurrentPhase", BindingFlags.Instance | BindingFlags.Public);
            string phaseName = "?";
            if (phaseProp != null)
            {
                var phase = phaseProp.GetValue(dnc);
                if (phase != null)
                {
                    var nameField = phase.GetType().GetField("phaseName", BindingFlags.Instance | BindingFlags.Public);
                    if (nameField != null) phaseName = Convert.ToString(nameField.GetValue(phase));
                }
            }

            var sunField = type.GetField("sunLight", BindingFlags.Instance | BindingFlags.Public);
            var sun = sunField != null ? sunField.GetValue(dnc) as Light : null;
            string sunState = sun == null
                ? "none"
                : sun.name + "(enabled=" + sun.enabled + ",shadows=" + sun.shadows + ",intensity=" +
                  sun.intensity.ToString("F2", CultureInfo.InvariantCulture) + ")";

            Append("NOTE;phase_forced=" + hour.ToString("F1", CultureInfo.InvariantCulture) +
                   ";phaseName=" + phaseName + ";sun=" + sunState);
        }

        private static void SetPrivateFloat(Type type, object target, string field, float value)
        {
            var f = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float)) f.SetValue(target, value);
        }

        private static Component FindByTypeName(string typeName)
        {
            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].GetType().Name == typeName) return all[i];
            return null;
        }

        // --------------------------------------------------------------- рендереры

        private static void CacheRenderers()
        {
            _allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            var ships = new List<Renderer>();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "WorldRoot_0_0") continue;
                if (go.GetComponentInChildren<ShipRootReference>(true) == null) continue;
                var rs = go.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rs.Length; i++) ships.Add(rs[i]);
            }
            _shipRenderers = ships.ToArray();
            _shipSet.Clear();
            for (int i = 0; i < _shipRenderers.Length; i++) _shipSet.Add(_shipRenderers[i]);

            // Кандидаты городского shadow budget: не корабли, не двери (слой 10), с включёнными тенями.
            _cityShadowCandidates.Clear();
            _cityShadowBase.Clear();
            for (int i = 0; i < _allRenderers.Length; i++)
            {
                var r = _allRenderers[i];
                if (r == null || !r.enabled) continue;
                if (IsShipRenderer(r)) continue;
                if (r.gameObject.layer == DoorLayer) continue;
                _cityShadowCandidates.Add(r);
                _cityShadowBase.Add(r.shadowCastingMode);
            }

            var pilotList = new List<Renderer>();
            Transform chunk = FindDeep(_worldRoot, PilotChunkName);
            if (chunk != null)
            {
                var chunkRenderers = chunk.GetComponentsInChildren<Renderer>(true);
                Bounds b = new Bounds(chunk.position, Vector3.zero);
                bool init = false;
                for (int i = 0; i < chunkRenderers.Length; i++)
                {
                    if (chunkRenderers[i] == null) continue;
                    if (!init) { b = chunkRenderers[i].bounds; init = true; }
                    else b.Encapsulate(chunkRenderers[i].bounds);
                }
                b.Expand(PilotBoundsInflate);
                _pilotBounds = b;
                for (int i = 0; i < _allRenderers.Length; i++)
                {
                    var r = _allRenderers[i];
                    if (r == null || IsShipRenderer(r)) continue;
                    if (b.Intersects(r.bounds)) pilotList.Add(r);
                }
            }
            _pilotRenderers = pilotList.ToArray();

            Debug.Log("[Stage4bBench] Рендереров всего=" + _allRenderers.Length +
                      ", корабельных=" + _shipRenderers.Length +
                      ", кандидатов теневого бюджета города=" + _cityShadowCandidates.Count +
                      ", пилотных=" + _pilotRenderers.Length);
            Append("NOTE;renderers;all=" + _allRenderers.Length + ";ships=" + _shipRenderers.Length +
                   ";cityShadowCandidates=" + _cityShadowCandidates.Count +
                   ";pilot=" + _pilotRenderers.Length + ";pilotChunk=" + PilotChunkName);
        }

        private static bool IsShipRenderer(Renderer r)
        {
            return r != null && _shipSet.Contains(r);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>Восстанавливает то, что мы сами выключили, и применяет область замера.</summary>
        private static void ApplyScope(Scope scope)
        {
            for (int i = 0; i < _forcedOff.Count; i++)
                if (_forcedOff[i] != null) _forcedOff[i].enabled = true;
            _forcedOff.Clear();

            if (scope == Scope.Pilot)
            {
                var keep = new HashSet<Renderer>();
                for (int i = 0; i < _pilotRenderers.Length; i++) keep.Add(_pilotRenderers[i]);
                for (int i = 0; i < _shipRenderers.Length; i++)
                {
                    var r = _shipRenderers[i];
                    if (r != null && r.enabled) { r.enabled = false; _forcedOff.Add(r); }
                }
                for (int i = 0; i < _allRenderers.Length; i++)
                {
                    var r = _allRenderers[i];
                    if (r == null || keep.Contains(r) || !r.enabled) continue;
                    r.enabled = false;
                    _forcedOff.Add(r);
                }
            }
            else
            {
                for (int i = 0; i < _shipRenderers.Length; i++)
                {
                    var r = _shipRenderers[i];
                    if (r == null || !r.enabled) continue;
                    r.enabled = false;
                    _forcedOff.Add(r);
                }
            }
        }

        /// <summary>Городской shadow budget: тени off для городской геометрии дальше порога. 0 = выключено.</summary>
        private static void ApplyShadowBudget(float threshold)
        {
            for (int i = 0; i < _cityShadowCandidates.Count; i++)
            {
                var r = _cityShadowCandidates[i];
                if (r == null) continue;
                r.shadowCastingMode = _cityShadowBase[i];
            }
            _shadowOffCount = 0;
            if (threshold <= 0f || _cam == null) return;

            Vector3 camPos = _cam.transform.position;
            for (int i = 0; i < _cityShadowCandidates.Count; i++)
            {
                var r = _cityShadowCandidates[i];
                if (r == null || !r.enabled) continue;
                var baseMode = _cityShadowBase[i];
                if (baseMode == ShadowCastingMode.Off) continue;
                if (Vector3.Distance(camPos, r.bounds.center) <= threshold) continue;
                r.shadowCastingMode = ShadowCastingMode.Off;
                _shadowOffCount++;
            }
        }

        private static void RestoreShadowBudget()
        {
            for (int i = 0; i < _cityShadowCandidates.Count; i++)
            {
                var r = _cityShadowCandidates[i];
                if (r == null) continue;
                r.shadowCastingMode = _cityShadowBase[i];
            }
            _shadowOffCount = 0;
        }

        // ------------------------------------------------------------------- точки

        private static void BeginCase(int index)
        {
            _caseIndex = index;
            if (_caseIndex >= _cases.Count) { Finish("OK", false); return; }

            var c = _cases[_caseIndex];
            ApplyScope(c.scope);
            ApplyPose(c);
            ForcePhase(c.hour);
            ApplyShadowBudget(c.shadowBudget);

            _dc.Clear(); _spc.Clear(); _tris.Clear(); _sc.Clear();
            _hybridDC.Clear(); _hybridInst.Clear(); _srpDC.Clear(); _ms.Clear();
            _censusDone = false;
            _settleUntilFrame = Time.frameCount + SettleFrames;
            _nextSampleFrame = _settleUntilFrame;
            _caseWindowFrame = Time.frameCount;
            _caseWindowStart = Now;
            Debug.Log("[Stage4bBench] Точка " + c.id + " scope=" + c.scope +
                      " shadowBudget=" + c.shadowBudget.ToString("F0", CultureInfo.InvariantCulture) +
                      " shadowsOff=" + _shadowOffCount);
        }

        private static void ApplyPose(BenchCase c)
        {
            if (_cam == null) return;

            if (c.relativeToPilotChunk)
            {
                Vector3 target = _pilotBounds.center;
                Vector3 from = target + new Vector3(0f, PilotCamHeight, -PilotCamDistance);
                _targetPos = from;
                _targetRot = Quaternion.LookRotation((target - from).normalized, Vector3.up);
            }
            else
            {
                Vector3 shift = _worldRoot != null ? _worldRoot.position : Vector3.zero;
                _targetPos = c.local + AuthoredWorldOffset + shift;
                _targetRot = Quaternion.Euler(c.pitch, c.yaw, 0f);
            }

            _cam.transform.SetPositionAndRotation(_targetPos, _targetRot);
        }

        private static void StepPoint()
        {
            var c = _cases[_caseIndex];
            ApplyPose(c);

            if (Time.frameCount < _settleUntilFrame) return;

            if (!_censusDone)
            {
                RunCensus(c);
                if (_state == State.Off) return;
                _censusDone = true;
                _nextSampleFrame = Time.frameCount;
            }

            if (Time.frameCount < _nextSampleFrame) return;

            _dc.Add(UnityStats.drawCalls);
            _spc.Add(UnityStats.setPassCalls);
            _tris.Add(UnityStats.triangles);
            _sc.Add(UnityStats.shadowCasters);
            _hybridDC.Add(UnityStats.hybridBatcherDrawCalls);
            _hybridInst.Add(UnityStats.hybridBatcherInstances);
            _srpDC.Add(UnityStats.srpBatcherDrawCalls);
            _ms.Add((double)Time.deltaTime * 1000.0);

            if (_dc.Count == 1 && UnityStats.drawCalls == 0)
            {
                Debug.LogWarning("[Stage4bBench] UnityStats.drawCalls=0 (screenRes=" + UnityStats.screenRes +
                                 "): Game View не рендерит, замер недостоверен.");
                Append("NOTE;warning=drawCalls_0_at_first_sample;screenRes=" + UnityStats.screenRes);
            }

            if (_dc.Count < SampleCount) { _nextSampleFrame = Time.frameCount + SampleGapFrames; return; }

            WriteCaseLine(c);
            BeginCase(_caseIndex + 1);
        }

        private static void RunCensus(BenchCase c)
        {
            _censusScope = 0;
            _censusInFrustum = 0;
            _censusDoorsInFrustum = 0;
            _censusEnabledTotal = 0;

            if (_cam == null || _allRenderers == null) return;

            GeometryUtility.CalculateFrustumPlanes(_cam, _planes);

            for (int i = 0; i < _allRenderers.Length; i++)
            {
                var r = _allRenderers[i];
                if (r == null || !r.enabled) continue;
                _censusEnabledTotal++;
                if (!GeometryUtility.TestPlanesAABB(_planes, r.bounds)) continue;
                _censusInFrustum++;
                if (r.gameObject.layer == DoorLayer) _censusDoorsInFrustum++;
                if (!IsShipRenderer(r)) _censusScope++;
            }

            if (_censusScope == 0)
            {
                Debug.LogError("[Stage4bBench] В кадре 0 городских рендереров: поза/область неверны, замер остановлен.");
                Append("NOTE;error=census_zero;campos=" + _targetPos.ToString("F1") + ";case=" + c.id);
                Finish("CENSUS_ZERO", false);
            }
        }

        private static void WriteCaseLine(BenchCase c)
        {
            int dc = Median(_dc);
            int spc = Median(_spc);
            int tris = Median(_tris);
            int sc = Median(_sc);
            int hybridDC = Median(_hybridDC);
            int hybridInst = Median(_hybridInst);
            int srpDC = Median(_srpDC);
            double ms = MedianD(_ms);
            double seconds = Math.Max(0.0001, Now - _caseWindowStart);
            int framesInWindow = Math.Max(1, Time.frameCount - _caseWindowFrame);
            double fps = framesInWindow / seconds;

            var sb = new StringBuilder();
            sb.Append("CASE;id=").Append(c.id);
            sb.Append(";phase=").Append(_phaseLabel);
            sb.Append(";scope=").Append(c.scope);
            sb.Append(";dayPhase=").Append(c.phase);
            sb.Append(";shadowBudget=").Append(c.shadowBudget.ToString("F0", CultureInfo.InvariantCulture));
            sb.Append(";shadowsOff=").Append(_shadowOffCount);
            sb.Append(";worldpos=").Append(_targetPos.ToString("F2"));
            sb.Append(";dc=").Append(dc);
            sb.Append(";spc=").Append(spc);
            sb.Append(";tris=").Append(tris);
            sb.Append(";shadowCasters=").Append(sc);
            sb.Append(";hybridDC=").Append(hybridDC);
            sb.Append(";hybridInstances=").Append(hybridInst);
            sb.Append(";srpBatcherDC=").Append(srpDC);
            sb.Append(";ms=").Append(ms.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(";fps=").Append(fps.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(";enabledRenderers=").Append(_censusEnabledTotal);
            sb.Append(";inFrustum=").Append(_censusInFrustum);
            sb.Append(";cityInFrustum=").Append(_censusScope);
            sb.Append(";doorsInFrustum=").Append(_censusDoorsInFrustum);
            sb.Append(";rawDc=").Append(string.Join("|", _dc.ConvertAll(v => v.ToString()).ToArray()));
            sb.Append(";rawShadowCasters=").Append(string.Join("|", _sc.ConvertAll(v => v.ToString()).ToArray()));
            sb.Append(";rawHybridDC=").Append(string.Join("|", _hybridDC.ConvertAll(v => v.ToString()).ToArray()));
            Append(sb.ToString());
            AppendCsv(c, dc, spc, tris, sc, hybridDC, hybridInst, srpDC, ms, fps);

            Debug.Log("[Stage4bBench] " + c.id + " (sb=" + c.shadowBudget.ToString("F0") + "): dc=" + dc +
                      " spc=" + spc + " tris=" + tris + " shadowCasters=" + sc +
                      " hybridDC=" + hybridDC + " shadowsOff=" + _shadowOffCount);
        }

        private static void AppendCsv(BenchCase c, int dc, int spc, int tris, int sc, int hybridDC, int hybridInst,
                                      int srpDC, double ms, double fps)
        {
            bool writeHeader = !File.Exists(_outCsvPath);
            var sb = new StringBuilder();
            if (writeHeader)
                sb.AppendLine("run,date,phase,case,scope,dayPhase,shadowBudget,shadowsOff,worldpos,dc,spc,tris,shadowCasters,hybridDC,hybridInstances,srpBatcherDC,ms,fps,enabledRenderers,inFrustum,cityInFrustum,doorsInFrustum");
            sb.Append(_runTag).Append(',')
              .Append(DateTag).Append(',')
              .Append(_phaseLabel).Append(',')
              .Append(c.id).Append(',')
              .Append(c.scope).Append(',')
              .Append(c.phase).Append(',')
              .Append(c.shadowBudget.ToString("F0", CultureInfo.InvariantCulture)).Append(',')
              .Append(_shadowOffCount).Append(',')
              .Append('"').Append(_targetPos.ToString("F2")).Append('"').Append(',')
              .Append(dc).Append(',').Append(spc).Append(',').Append(tris).Append(',').Append(sc).Append(',')
              .Append(hybridDC).Append(',').Append(hybridInst).Append(',').Append(srpDC).Append(',')
              .Append(ms.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
              .Append(fps.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
              .Append(_censusEnabledTotal).Append(',').Append(_censusInFrustum).Append(',')
              .Append(_censusScope).Append(',').Append(_censusDoorsInFrustum);
            sb.AppendLine();
            try { File.AppendAllText(_outCsvPath, sb.ToString(), Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[Stage4bBench] CSV: " + e.Message); }
        }

        private static int Median(List<int> values)
        {
            if (values == null || values.Count == 0) return 0;
            var copy = new List<int>(values);
            copy.Sort();
            return copy[copy.Count / 2];
        }

        private static double MedianD(List<double> values)
        {
            if (values == null || values.Count == 0) return 0.0;
            var copy = new List<double>(values);
            copy.Sort();
            return copy[copy.Count / 2];
        }

        // ------------------------------------------------------------------ финиш

        private static void Finish(string result, bool keepPlaying)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(SessionKey, false);

            RestoreShadowBudget();

            if (_state != State.Off)
            {
                Append("BENCH_DONE;result=" + result + ";phase=" + _phaseLabel +
                       ";casesCompleted=" + _caseIndex + "/" + _cases.Count +
                       ";sceneSaved=no;push=no;hold=" + (keepPlaying ? "yes" : "no"));
            }

            Debug.Log("[Stage4bBench] Завершено: " + result + " (" + _phaseLabel + "). Файл: " + _outRawPath);

            if (keepPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _state = State.Off;
                Debug.Log("[Stage4bBench] Play Mode остаётся на точке C1_night до ручного Abort.");
                return;
            }

            _state = State.Off;
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        }

        private static void Append(string line)
        {
            if (string.IsNullOrEmpty(_outRawPath)) return;
            try { File.AppendAllText(_outRawPath, line + Environment.NewLine, Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[Stage4bBench] Не удалось записать строку: " + e.Message); }
        }

        internal static void ResumeIfRunning()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;

            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(SessionKey, false);
                return;
            }

            int reloads = SessionState.GetInt(SessionKey + ".reloads", 0) + 1;
            SessionState.SetInt(SessionKey + ".reloads", reloads);
            if (reloads > 4)
            {
                SessionState.SetBool(SessionKey, false);
                Debug.LogError("[Stage4bBench] Слишком много доменных перезагрузок подряд — прогон отменён.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _outRawPath = Path.Combine(projectRoot, DataDir, "P_stage4b_" + _phaseLabel + "_" + DateTag + "_raw.txt");
            _outCsvPath = Path.Combine(projectRoot, DataDir, CsvName);
            _state = State.WaitPlayMode;
            _stateSince = Now;
            _hostRequested = false;
            _menuHidden = false;
            _cam = null;
            _worldRoot = null;
            _allRenderers = null;
            _shipRenderers = null;
            _pilotRenderers = null;
            _forcedOff.Clear();
            _cityShadowCandidates.Clear();
            _cityShadowBase.Clear();
            _caseIndex = 0;

            Append("NOTE;domain_reload=true;restart=" + reloads);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[Stage4bBench] Доменная перезагрузка (#" + reloads + ") — прогон продолжен из SessionState.");
        }
    }

    [InitializeOnLoad]
    internal static class Stage4bGapBenchBootstrap
    {
        static Stage4bGapBenchBootstrap()
        {
            Stage4bGapBench.ResumeIfRunning();
        }
    }
}
