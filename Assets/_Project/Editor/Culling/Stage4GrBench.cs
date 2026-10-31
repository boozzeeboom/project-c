// Project C — T-CULL01 / Stage 4 (H5): рантайм-скамейка city DC для GRD + GPU occlusion.
// Методика — та же, что Stage 2 (Stage2CullingBench): Play Mode → Host, прогрев 1500 кадров,
// фаза Midday forced, камера рига far 30000 / fov 60, 40 кадров успокоения на точку,
// 3 замера с шагом 8 кадров, медиана, метрики UnityStats.
//
// Отличия Stage 4:
//   1. Корабли НЕ оптимизируются. Все рендереры под корнями с ShipRootReference (heavy / medium / light /
//      20 NPC) выключаются runtime-ом на время city-замера. Отдельный контрольный кейс — heavy включён.
//   2. Пилотный кейс: включён только один чанк (PRIMUM_HOUSES_S9_TRD-S2) + геометрия в его габаритах,
//      остальной мир выключен. Камера ставится относительно чанка.
//   3. В конфиг-строку пишутся BrgStripping / GPUResidentDrawerMode / occlusion / тени, поэтому
//      файл сам подписывается как baseline или grd.
//
// Чего НЕ делает: не сохраняет сцену, не правит ассеты и код, не трогает DoorController,
// коллайдеры, материалы и SetActive. Все правки живут только внутри Play Mode.
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
    /// <summary>Stage 4: city DC до/после GPU Resident Drawer + GPU occlusion (T-CULL01 / H5).</summary>
    public static class Stage4GrBench
    {
        private const string RunMenu = "Project C/Culling/Stage 4 Bench/Run";
        private const string AbortMenu = "Project C/Culling/Stage 4 Bench/Abort";

        private const string DataDir = "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA";
        private const string CsvName = "O_stage4_2026-10-31.csv";
        private const string DateTag = "2026-10-31";

        public const string SessionKey = "ProjectC.Stage4Bench.Running";

        private const float BenchFar = 30000f;
        private const float BenchFov = 60f;

        private const int WarmupFrames = 1500;
        private const int SettleFrames = 40;
        private const int SampleGapFrames = 8;
        private const int SampleCount = 3;

        private const double HostTimeoutSeconds = 90.0;
        private const double RigTimeoutSeconds = 240.0;
        private const double WarmupTimeoutSeconds = 900.0;

        /// <summary>Чанк-пилот Stage 5 (H4): PRIMUM_HOUSES_S9_TRD-S2.</summary>
        private const string PilotChunkName = "PRIMUM_HOUSES_S9_TRD-S2_P1_45H";
        private const float PilotBoundsInflate = 4f;
        private const float PilotCamDistance = 58f;
        private const float PilotCamHeight = 24f;

        private static readonly Vector3 AuthoredWorldOffset = new Vector3(39936.0f, 2560.03f, 39936.0f);

        private enum Scope { City, CityWithHeavy, Pilot }

        private class BenchCase
        {
            public string id;
            public string name;
            public Vector3 local;
            public float yaw;
            public float pitch;
            public float hour;
            public string phase;
            public Scope scope;
            public bool relativeToPilotChunk;
        }

        private static List<BenchCase> BuildCases()
        {
            var p1 = new Vector3(-342.90f, -50.43f, -102.20f);
            return new List<BenchCase>
            {
                new BenchCase { id = "C1_heavy", name = "tsentr_ploshhadi_heavy_on", local = p1, yaw = 337.5f, hour = 12f, phase = "Midday", scope = Scope.CityWithHeavy },
                new BenchCase { id = "PILOT", name = "chunk_S9_TRD-S2", local = Vector3.zero, yaw = 0f, hour = 12f, phase = "Midday", scope = Scope.Pilot, relativeToPilotChunk = true },
                new BenchCase { id = "C2", name = "uzkaya_ulitsa", local = new Vector3(-382.90f, -58.23f, -126.20f), yaw = 225f, hour = 12f, phase = "Midday", scope = Scope.City },
                new BenchCase { id = "C3", name = "krysha", local = new Vector3(-416.00f, -27.94f, -36.00f), yaw = 132f, pitch = -18f, hour = 12f, phase = "Midday", scope = Scope.City },
                new BenchCase { id = "C1_night", name = "tsentr_ploshhadi_night", local = p1, yaw = 337.5f, hour = 23f, phase = "Night", scope = Scope.City },
                new BenchCase { id = "C1_repeat", name = "tsentr_ploshhadi_repeat", local = p1, yaw = 337.5f, hour = 12f, phase = "Midday", scope = Scope.City },
                new BenchCase { id = "C1", name = "tsentr_ploshhadi", local = p1, yaw = 337.5f, hour = 12f, phase = "Midday", scope = Scope.City },
            };
        }

        // ------------------------------------------------------------------ состояние

        private enum State { Off, WaitPlayMode, WaitHost, WaitRig, Warmup, Point, Hold }

        private static State _state = State.Off;
        private static readonly List<BenchCase> _cases = BuildCases();
        private static int _caseIndex;

        private static readonly List<int> _dc = new List<int>();
        private static readonly List<int> _spc = new List<int>();
        private static readonly List<int> _tris = new List<int>();
        private static readonly List<int> _sc = new List<int>();
        private static readonly List<int> _hybridDC = new List<int>();
        private static readonly List<int> _hybridInst = new List<int>();
        private static readonly List<int> _indirectDC = new List<int>();
        private static readonly List<int> _srpDC = new List<int>();
        private static readonly List<double> _ms = new List<double>();
        private static readonly List<int> _frames = new List<int>();

        private static Camera _cam;
        private static Transform _worldRoot;
        private static Renderer[] _allRenderers;
        private static Renderer[] _shipRenderers;
        private static Renderer[] _heavyShipRenderers;
        private static Renderer[] _pilotRenderers;
        private static readonly HashSet<Renderer> _shipSet = new HashSet<Renderer>();
        private static readonly HashSet<Renderer> _heavySet = new HashSet<Renderer>();
        private static Bounds _pilotBounds;
        private static readonly List<Renderer> _forcedOff = new List<Renderer>();

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
        private static string _configLine = "";
        private static readonly StringBuilder _notes = new StringBuilder();

        private static double Now { get { return EditorApplication.timeSinceStartup; } }
        private static readonly Plane[] _planes = new Plane[6];

        // --------------------------------------------------------------------- меню

        [MenuItem(RunMenu)]
        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Stage4Bench] Уже в Play Mode / вход выполняется. Сначала Abort.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dataDir = Path.Combine(projectRoot, DataDir);
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

            _phaseLabel = ReadGrdPhaseLabel();
            _runTag = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
            _outRawPath = Path.Combine(dataDir, "O_stage4_" + _phaseLabel + "_" + DateTag + "_raw.txt");
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
            _heavyShipRenderers = null;
            _pilotRenderers = null;
            _forcedOff.Clear();
            _notes.Length = 0;
            _stateSince = Now;
            _configLine = ReadRenderConfigLine();

            SessionState.SetBool(SessionKey, true);
            SessionState.SetInt(SessionKey + ".reloads", 0);

            Append("NOTE;bench=Stage4GrBench;unity=" + Application.unityVersion +
                   ";date=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                   ";phase=" + _phaseLabel);
            Append(_configLine);
            Append("NOTE;method=PlayMode→Host;warmup=" + WarmupFrames + "fr;settle=" + SettleFrames +
                   "fr;samples=" + SampleCount + "x gap " + SampleGapFrames + "fr;metric=UnityStats median;far=" +
                   BenchFar.ToString("F0", CultureInfo.InvariantCulture) + ";fov=" + BenchFov.ToString("F0", CultureInfo.InvariantCulture));
            Append("NOTE;ships=all_ship_roots(ShipRootReference)_renderers_disabled_runtime;heavy_kept_only_in_C1_heavy;prefab_fbx_scene_untouched");

            Debug.Log("[Stage4Bench] Старт (" + _phaseLabel + "), " + _cases.Count + " точек. Файл: " + _outRawPath);

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

        private static void UpdatePhaseLabelAfterPlay()
        {
            _phaseLabel = ReadGrdPhaseLabel();
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
            int renderMode = ReadSerializedInt(urp, "m_RenderingMode");
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
                   ";renderingMode=" + renderMode +
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
                Debug.LogError("[Stage4Bench] Исключение: " + e);
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
                    UpdatePhaseLabelAfterPlay();
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

                case State.Hold:
                    // Держим Play Mode на последней точке для ручного Frame Debugger.
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
                        Debug.Log("[Stage4Bench] Host запрошен через MainMenuWindow.OnHostClicked()");
                    }
                    else
                    {
                        var start = nmc.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
                        if (start != null) start.Invoke(nmc, null);
                        Debug.Log("[Stage4Bench] Host запрошен напрямую через NetworkManagerController.StartHost()");
                    }
                }
                else
                {
                    var start = nmc.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
                    if (start != null) start.Invoke(nmc, null);
                    Debug.Log("[Stage4Bench] MainMenuWindow не найден: StartHost() напрямую");
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

            Debug.Log("[Stage4Bench] Риг: " + _cam.name + " (far " + _cam.farClipPlane + ", fov " + _cam.fieldOfView +
                      "), отключено компонентов-двигателей камеры: " + disabled +
                      ", WorldRoot_0_0=" + (_worldRoot != null ? _worldRoot.position.ToString("F2") : "нет"));

            Append("NOTE;camera;name=" + _cam.name + ";occlusionCulling=" + _cam.useOcclusionCulling +
                   ";allowMSAA=" + _cam.allowMSAA + ";allowHDR=" + _cam.allowHDR +
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
                Debug.LogWarning("[Stage4Bench] DayNightController не найден — фаза суток не форсирована");
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
            var heavy = new List<Renderer>();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "WorldRoot_0_0") continue;
                var rootRef = go.GetComponentInChildren<ShipRootReference>(true);
                if (rootRef == null) continue;
                var rs = go.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rs.Length; i++)
                {
                    ships.Add(rs[i]);
                    if (go.name == "Ship_havyII_base") heavy.Add(rs[i]);
                }
            }
            _shipRenderers = ships.ToArray();
            _heavyShipRenderers = heavy.ToArray();
            _shipSet.Clear();
            _heavySet.Clear();
            for (int i = 0; i < _shipRenderers.Length; i++) _shipSet.Add(_shipRenderers[i]);
            for (int i = 0; i < _heavyShipRenderers.Length; i++) _heavySet.Add(_heavyShipRenderers[i]);

            // Пилот: габариты одного чанка + всё, что в них попадает (включая двери этого чанка).
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

            Debug.Log("[Stage4Bench] Рендереров всего=" + _allRenderers.Length +
                      ", корабельных=" + _shipRenderers.Length + " (heavy=" + _heavyShipRenderers.Length + ")" +
                      ", пилотных=" + _pilotRenderers.Length +
                      ", чанк=" + (chunk != null ? "найден" : "НЕ НАЙДЕН"));
            Append("NOTE;renderers;all=" + _allRenderers.Length + ";ships=" + _shipRenderers.Length +
                   ";heavy=" + _heavyShipRenderers.Length + ";pilot=" + _pilotRenderers.Length +
                   ";pilotChunk=" + PilotChunkName +
                   ";pilotBoundsCenter=" + _pilotBounds.center.ToString("F1") +
                   ";pilotBoundsSize=" + _pilotBounds.size.ToString("F1"));
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

            switch (scope)
            {
                case Scope.City:
                case Scope.CityWithHeavy:
                {
                    for (int i = 0; i < _shipRenderers.Length; i++)
                    {
                        var r = _shipRenderers[i];
                        if (r == null || !r.enabled) continue;
                        if (scope == Scope.CityWithHeavy && IsHeavyRenderer(r)) continue;
                        r.enabled = false;
                        _forcedOff.Add(r);
                    }
                    break;
                }
                case Scope.Pilot:
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
                    break;
                }
            }
        }

        private static bool IsHeavyRenderer(Renderer r)
        {
            return r != null && _heavySet.Contains(r);
        }

        // ------------------------------------------------------------------- точки

        private static void BeginCase(int index)
        {
            _caseIndex = index;
            if (_caseIndex >= _cases.Count) { Finish("OK", true); return; }

            var c = _cases[_caseIndex];
            ApplyScope(c.scope);
            ApplyPose(c);
            ForcePhase(c.hour);

            _dc.Clear(); _spc.Clear(); _tris.Clear(); _sc.Clear();
            _hybridDC.Clear(); _hybridInst.Clear(); _indirectDC.Clear(); _srpDC.Clear(); _ms.Clear(); _frames.Clear();
            _censusDone = false;
            _settleUntilFrame = Time.frameCount + SettleFrames;
            _nextSampleFrame = _settleUntilFrame;
            _caseWindowFrame = Time.frameCount;
            _caseWindowStart = Now;
            Debug.Log("[Stage4Bench] Точка " + c.id + " (" + c.name + ") scope=" + c.scope + " — " + c.phase);
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
            _indirectDC.Add(UnityStats.totalIndirectDrawCalls);
            _srpDC.Add(UnityStats.srpBatcherDrawCalls);
            _ms.Add((double)Time.deltaTime * 1000.0);
            _frames.Add(Time.frameCount);

            if (_dc.Count == 1 && UnityStats.drawCalls == 0)
            {
                Debug.LogWarning("[Stage4Bench] UnityStats.drawCalls=0 (screenRes=" + UnityStats.screenRes +
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
                if (r.gameObject.layer == 10) _censusDoorsInFrustum++;
                if (!IsShipRenderer(r)) _censusScope++;
            }

            if (_censusScope == 0)
            {
                Debug.LogError("[Stage4Bench] В кадре 0 городских рендереров: поза/область неверны, замер остановлен.");
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
            int indirectDC = Median(_indirectDC);
            int srpDC = Median(_srpDC);
            int frame = Median(_frames);
            double ms = MedianD(_ms);
            double seconds = Math.Max(0.0001, Now - _caseWindowStart);
            int framesInWindow = Math.Max(1, Time.frameCount - _caseWindowFrame);
            double fps = framesInWindow / seconds;

            float lcd = 0f;
            if (_cam != null)
            {
                var arr = _cam.layerCullDistances;
                if (arr != null && arr.Length > 10) lcd = arr[10];
            }

            Vector3 shift = _worldRoot != null ? _worldRoot.position : Vector3.zero;

            var sb = new StringBuilder();
            sb.Append("CASE;id=").Append(c.id);
            sb.Append(";name=").Append(c.name);
            sb.Append(";phase=").Append(_phaseLabel);
            sb.Append(";scope=").Append(c.scope);
            sb.Append(";dayPhase=").Append(c.phase);
            sb.Append(";far=").Append(BenchFar.ToString("F0", CultureInfo.InvariantCulture));
            sb.Append(";fov=").Append(BenchFov.ToString("F0", CultureInfo.InvariantCulture));
            sb.Append(";pos=").Append(c.relativeToPilotChunk ? "relative_to_chunk" : c.local.ToString("F2"));
            sb.Append(";worldpos=").Append(_targetPos.ToString("F2"));
            sb.Append(";yaw=").Append(c.relativeToPilotChunk ? 0f : c.yaw);
            sb.Append(";pitch=").Append(c.relativeToPilotChunk ? 0f : c.pitch);
            sb.Append(";worldrootshift=").Append(shift.ToString("F2"));
            sb.Append(";layerCull10=").Append(lcd.ToString("F1", CultureInfo.InvariantCulture));
            sb.Append(";dc=").Append(dc);
            sb.Append(";spc=").Append(spc);
            sb.Append(";tris=").Append(tris);
            sb.Append(";shadowCasters=").Append(sc);
            sb.Append(";hybridDC=").Append(hybridDC);
            sb.Append(";hybridInstances=").Append(hybridInst);
            sb.Append(";indirectDC=").Append(indirectDC);
            sb.Append(";srpBatcherDC=").Append(srpDC);
            sb.Append(";ms=").Append(ms.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(";fps=").Append(fps.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(";frame=").Append(frame);
            sb.Append(";enabledRenderers=").Append(_censusEnabledTotal);
            sb.Append(";inFrustum=").Append(_censusInFrustum);
            sb.Append(";cityInFrustum=").Append(_censusScope);
            sb.Append(";doorsInFrustum=").Append(_censusDoorsInFrustum);
            sb.Append(";rawDc=").Append(string.Join("|", _dc.ConvertAll(v => v.ToString()).ToArray()));
            sb.Append(";rawSpc=").Append(string.Join("|", _spc.ConvertAll(v => v.ToString()).ToArray()));
            sb.Append(";rawHybridDC=").Append(string.Join("|", _hybridDC.ConvertAll(v => v.ToString()).ToArray()));
            Append(sb.ToString());
            AppendCsv(c, dc, spc, tris, sc, hybridDC, hybridInst, indirectDC, srpDC, ms, fps, frame);

            Debug.Log("[Stage4Bench] " + c.id + " " + c.phase + ": dc=" + dc + " spc=" + spc + " tris=" + tris +
                      " shadowCasters=" + sc + " hybridDC=" + hybridDC + " hybridInstances=" + hybridInst +
                      " indirectDC=" + indirectDC + " cityInFrustum=" + _censusScope);
        }

        private static void AppendCsv(BenchCase c, int dc, int spc, int tris, int sc, int hybridDC, int hybridInst,
                                      int indirectDC, int srpDC, double ms, double fps, int frame)
        {
            bool writeHeader = !File.Exists(_outCsvPath);
            var sb = new StringBuilder();
            if (writeHeader)
                sb.AppendLine("run,date,phase,case,scope,dayPhase,far,fov,worldpos,yaw,pitch,dc,spc,tris,shadowCasters,hybridDC,hybridInstances,indirectDC,srpBatcherDC,ms,fps,frame,enabledRenderers,inFrustum,cityInFrustum,doorsInFrustum");
            sb.Append(_runTag).Append(',')
              .Append(DateTag).Append(',')
              .Append(_phaseLabel).Append(',')
              .Append(c.id).Append(',')
              .Append(c.scope).Append(',')
              .Append(c.phase).Append(',')
              .Append(BenchFar.ToString("F0", CultureInfo.InvariantCulture)).Append(',')
              .Append(BenchFov.ToString("F0", CultureInfo.InvariantCulture)).Append(',')
              .Append('"').Append(_targetPos.ToString("F2")).Append('"').Append(',')
              .Append(c.relativeToPilotChunk ? 0f : c.yaw).Append(',')
              .Append(c.relativeToPilotChunk ? 0f : c.pitch).Append(',')
              .Append(dc).Append(',').Append(spc).Append(',').Append(tris).Append(',').Append(sc).Append(',')
              .Append(hybridDC).Append(',').Append(hybridInst).Append(',').Append(indirectDC).Append(',')
              .Append(srpDC).Append(',')
              .Append(ms.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
              .Append(fps.ToString("F2", CultureInfo.InvariantCulture)).Append(',').Append(frame).Append(',')
              .Append(_censusEnabledTotal).Append(',').Append(_censusInFrustum).Append(',')
              .Append(_censusScope).Append(',').Append(_censusDoorsInFrustum);
            sb.AppendLine();
            try { File.AppendAllText(_outCsvPath, sb.ToString(), Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[Stage4Bench] CSV: " + e.Message); }
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

            if (_state != State.Off)
            {
                Append("BENCH_DONE;result=" + result + ";phase=" + _phaseLabel +
                       ";casesCompleted=" + _caseIndex + "/" + _cases.Count +
                       ";sceneSaved=no;push=no;hold=" + (keepPlaying ? "yes" : "no"));
                if (_notes.Length > 0) Append("NOTE;bench_notes=" + _notes);
            }

            Debug.Log("[Stage4Bench] Завершено: " + result + " (" + _phaseLabel + "). Файл: " + _outRawPath);

            if (keepPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _state = State.Hold;
                Debug.Log("[Stage4Bench] Play Mode удерживается на точке C1 для ручного Frame Debugger. " +
                          "Выход: Project C → Culling → Stage 4 Bench → Abort.");
                return;
            }

            _state = State.Off;
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        }

        private static void Append(string line)
        {
            if (string.IsNullOrEmpty(_outRawPath)) return;
            try { File.AppendAllText(_outRawPath, line + Environment.NewLine, Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[Stage4Bench] Не удалось записать строку: " + e.Message); }
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
                Debug.LogError("[Stage4Bench] Слишком много доменных перезагрузок подряд — прогон отменён.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _outRawPath = Path.Combine(projectRoot, DataDir, "O_stage4_" + _phaseLabel + "_" + DateTag + "_raw.txt");
            _outCsvPath = Path.Combine(projectRoot, DataDir, CsvName);
            _state = State.WaitPlayMode;
            _stateSince = Now;
            _hostRequested = false;
            _menuHidden = false;
            _cam = null;
            _worldRoot = null;
            _allRenderers = null;
            _shipRenderers = null;
            _heavyShipRenderers = null;
            _pilotRenderers = null;
            _forcedOff.Clear();
            _caseIndex = 0;

            Append("NOTE;domain_reload=true;restart=" + reloads);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[Stage4Bench] Доменная перезагрузка (#" + reloads + ") — прогон продолжен из SessionState.");
        }
    }

    [InitializeOnLoad]
    internal static class Stage4GrBenchBootstrap
    {
        static Stage4GrBenchBootstrap()
        {
            Stage4GrBench.ResumeIfRunning();
        }
    }
}
