// Project C — T-CULL01 / Stage V (final validation): реальная игра с авто-запущенным
// CityShadowBudget + F8/F9 реальным вводом.
//
// Что мерит (только валидация, ничего не оптимизирует):
//   1. Лог самого компонента: `[CityShadowBudget] Рендереров города: N` (ожидается ~1 482).
//   2. P1 (площадь), C2 (улица), C3 (крыша) в реальной игре с включённым компонентом;
//      A/B: те же точки при выключенном компоненте — сверка с методикой бенча Stage 4b
//      (P1 3 047 / C2 573 / C3 1 011 при sb=0).
//   3. P1 в реальной игре с кораблями: A/B кораблей (renderer.enabled) — атрибуция их DC.
//   4. F8 / F9 реальным вводом (InputSystem.QueueStateEvent → wasPressedThisFrame), фокус
//      выведен за порог 256 м телепортом игрока; проверяются сдвиг WorldRoot_0_0, двери,
//      корабли, кандидаты бюджета, ошибки в консоли.
//   5. Hybrid Batch Group (`hybridBatcherDrawCalls > 0`), shader-ошибки — из консоли.
//
// Что НЕ делает: не сохраняет сцену, не пишет в префабы/FBX/ProjectSettings/URP-ассет,
// не меняет игровой код, не делает push. Все правки — runtime (renderer.enabled,
// shadowCastingMode, transform), в конце возвращаются обратно.
//
// Меню: Project C → Culling → Final Validation → Run / Abort.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ProjectC.Ship;
using ProjectC.World;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools
{
    /// <summary>Stage V: фактический CityShadowBudget в игре + F8/F9 реальным вводом (T-CULL01).</summary>
    public static class FinalValidationBench
    {
        private const string RunMenu = "Project C/Culling/Final Validation/Run";
        private const string AbortMenu = "Project C/Culling/Final Validation/Abort";

        private const string DataDir = "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA";
        private const string DateTag = "2026-11-01";

        public const string SessionKey = "ProjectC.FinalValidationBench.Running";

        private const float BenchFar = 30000f;
        private const float BenchFov = 60f;

        private const int WarmupFrames = 1500;
        private const int SettleFrames = 40;
        private const int SampleGapFrames = 8;
        private const int SampleCount = 3;

        private const double HostTimeoutSeconds = 120.0;
        private const double RigTimeoutSeconds = 300.0;
        private const double WarmupTimeoutSeconds = 900.0;
        private const double BudgetWaitSeconds = 120.0;

        private const int DoorLayer = 10;
        private const string WorldRootName = "WorldRoot_0_0";
        private const int RebaseTimeoutFrames = 360;

        private static readonly Vector3 AuthoredWorldOffset = new Vector3(39936.0f, 2560.03f, 39936.0f);

        private enum State { Off, WaitPlayMode, WaitHost, WaitRig, WaitBudget, Warmup, Point, Rebase, Cleanup }

        private class BenchCase
        {
            public string id;
            public Vector3 local;
            public float yaw;
            public float pitch;
            public float hour;
            public bool shipsVisible;
            public bool budgetOn;
        }

        private static List<BenchCase> BuildCases()
        {
            var p1 = new Vector3(-342.90f, -50.43f, -102.20f);
            var c2 = new Vector3(-382.90f, -58.23f, -126.20f);
            var c3 = new Vector3(-416.00f, -27.94f, -36.00f);
            return new List<BenchCase>
            {
                new BenchCase { id = "P1_noship_budget_on", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true },
                new BenchCase { id = "P1_noship_budget_on_repeat", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true },
                new BenchCase { id = "P1_noship_budget_off", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = false },
                new BenchCase { id = "P1_real_budget_on", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = true, budgetOn = true },
                new BenchCase { id = "C2_noship_budget_on", local = c2, yaw = 225f, hour = 12f, shipsVisible = false, budgetOn = true },
                new BenchCase { id = "C2_noship_budget_off", local = c2, yaw = 225f, hour = 12f, shipsVisible = false, budgetOn = false },
                new BenchCase { id = "C3_noship_budget_on", local = c3, yaw = 132f, pitch = -18f, hour = 12f, shipsVisible = false, budgetOn = true },
                new BenchCase { id = "C3_noship_budget_on_repeat", local = c3, yaw = 132f, pitch = -18f, hour = 12f, shipsVisible = false, budgetOn = true },
                new BenchCase { id = "C3_noship_budget_off", local = c3, yaw = 132f, pitch = -18f, hour = 12f, shipsVisible = false, budgetOn = false },
            };
        }

        // ------------------------------------------------------------------ состояние

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
        private static readonly HashSet<Renderer> _shipSet = new HashSet<Renderer>();
        private static readonly List<Renderer> _shipsDisabled = new List<Renderer>();
        private static readonly List<Renderer> _cityCandidates = new List<Renderer>();
        private static readonly List<ShadowCastingMode> _cityBaseModes = new List<ShadowCastingMode>();

        private static CityShadowBudget _budget;
        private static bool _budgetWantedOn = true;
        private static int _budgetEntriesCount = -1;
        private static int _budgetBootEntries = -1;
        private static int _budgetLogCount;

        private static MonoBehaviour _rebaseSlice;
        private static Transform _playerRoot;
        private static GameObject[] _shipRoots = new GameObject[0];

        private static Vector3 _targetPos;
        private static Quaternion _targetRot;
        private static int _settleUntilFrame;
        private static int _nextSampleFrame;
        private static bool _censusDone;
        private static int _censusInFrustum;
        private static int _censusDoorsInFrustum;
        private static int _censusEnabledTotal;
        private static int _censusCity;
        private static int _censusShadowsOff;
        private static double _stateSince;
        private static double _caseWindowStart;
        private static int _caseWindowFrame;
        private static int _warmStartFrame;
        private static bool _hostRequested;
        private static bool _menuHidden;
        private static string _outRawPath;
        private static string _outCsvPath;
        private static string _runTag = "";

        // F8 / F9
        private static int _rebasePhase;
        private static int _rebaseStepFrame;
        private static int _keyReleaseFrame = -1;
        private static int _pressAttempts;
        private static ulong _frameGenBefore;
        private static Vector3 _cumulativeBefore;
        private static Vector3 _rootBefore;
        private static Vector3 _playerBefore;
        private static float _predictedShiftMagnitude;
        private static string _rebasePhaseName = "";
        private static readonly List<string> _rebaseLines = new List<string>();

        // снимки состояния сцены
        private static Transform _camTransformBefore;
        private static Vector3 _camPosBefore;
        private static Quaternion _camRotBefore;
        private static float _camFarBefore;
        private static float _camFovBefore;
        private static float _camNearBefore;
        private static readonly List<MonoBehaviour> _rigBehavioursDisabled = new List<MonoBehaviour>();
        private static MonoBehaviour _dayNight;
        private static bool _dayNightWasEnabled;

        // консоль
        private static readonly List<string> _errorLines = new List<string>();
        private static readonly List<string> _budgetLogLines = new List<string>();
        private static int _errorCount;
        private static int _exceptionCount;
        private static int _baselineErrorCount;
        private static int _shaderErrorCount;
        private static int _warnCount;
        private static bool _logHooked;
        private static bool _dirtyAtStart;

        private static double Now { get { return EditorApplication.timeSinceStartup; } }
        private static readonly Plane[] _planes = new Plane[6];

        // --------------------------------------------------------------------- меню

        [MenuItem(RunMenu)]
        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[FinalValidation] Уже в Play Mode / вход выполняется. Сначала Abort.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dataDir = Path.Combine(projectRoot, DataDir);
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

            _runTag = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
            _outRawPath = Path.Combine(dataDir, "Q_final_validation_" + DateTag + "_raw.txt");
            _outCsvPath = Path.Combine(dataDir, "Q_final_validation_" + DateTag + ".csv");
            if (File.Exists(_outRawPath)) File.Delete(_outRawPath);
            if (File.Exists(_outCsvPath)) File.Delete(_outCsvPath);

            _dirtyAtStart = SceneManager.GetActiveScene().isDirty;

            _state = State.WaitPlayMode;
            _caseIndex = 0;
            _hostRequested = false;
            _menuHidden = false;
            _cam = null;
            _worldRoot = null;
            _allRenderers = null;
            _shipRenderers = null;
            _budget = null;
            _budgetEntriesCount = -1;
            _budgetBootEntries = -1;
            _budgetLogCount = 0;
            _budgetWantedOn = true;
            _rebasePhase = 0;
            _pressAttempts = 0;
            _errorCount = 0;
            _exceptionCount = 0;
            _baselineErrorCount = 0;
            _shaderErrorCount = 0;
            _warnCount = 0;
            _errorLines.Clear();
            _budgetLogLines.Clear();
            _rebaseLines.Clear();
            _shipsDisabled.Clear();
            _cityCandidates.Clear();
            _cityBaseModes.Clear();
            _rigBehavioursDisabled.Clear();
            _shipRoots = new GameObject[0];
            _stateSince = Now;

            HookLog();

            SessionState.SetBool(SessionKey, true);
            SessionState.SetInt(SessionKey + ".reloads", 0);

            Append("NOTE;bench=FinalValidationBench;unity=" + Application.unityVersion +
                   ";date=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                   ";scene=" + SceneManager.GetActiveScene().path + ";sceneDirtyAtStart=" + _dirtyAtStart);
            Append(ReadRenderConfigLine());
            Append("NOTE;method=PlayMode→Host;warmup=" + WarmupFrames + "fr;settle=" + SettleFrames +
                   "fr;samples=" + SampleCount + "x gap " + SampleGapFrames + "fr;metric=UnityStats median;far=" +
                   BenchFar.ToString("F0", CultureInfo.InvariantCulture) + ";fov=" + BenchFov.ToString("F0", CultureInfo.InvariantCulture));
            Append("NOTE;budget=game_component_CityShadowBudget_auto_bootstrap;A/B=component.enabled_toggle");
            Append("NOTE;ships=A/B_renderer.enabled_runtime;prefab_fbx_scene_untouched");
            Append("NOTE;rebase=F8/F9_via_InputSystem.QueueStateEvent(wasPressedThisFrame);player_teleported_past_threshold");

            Debug.Log("[FinalValidation] Старт, " + _cases.Count + " DC-точек + F8/F9. Файл: " + _outRawPath);

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
            return "CONFIG;urpAsset=" + (urp ?? "none") +
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
                Debug.LogError("[FinalValidation] Исключение: " + e);
                Append("NOTE;exception=" + e.GetType().Name + ":" + (e.Message ?? "").Replace(";", ","));
                Finish("EXCEPTION", false);
            }
        }

        private static void Step()
        {
            TickKeyRelease();

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
                    _state = State.WaitBudget;
                    _stateSince = Now;
                    break;

                case State.WaitBudget:
                    _budget = UnityEngine.Object.FindAnyObjectByType<CityShadowBudget>();
                    if (_budget == null)
                    {
                        if (Now - _stateSince > BudgetWaitSeconds)
                        {
                            Append("NOTE;budget=component_not_found_after_" + BudgetWaitSeconds.ToString("F0", CultureInfo.InvariantCulture) + "s");
                            Debug.LogWarning("[FinalValidation] CityShadowBudget не найден — DC в состоянии «бюджета нет».");
                            _warmStartFrame = Time.frameCount;
                            _state = State.Warmup;
                            _stateSince = Now;
                        }
                        return;
                    }
                    _budgetBootEntries = CountPrivate(_budget, "_entries");
                    Append("NOTE;budget_found;enabled=" + _budget.enabled + ";entriesAtFind=" + _budgetBootEntries +
                           ";shadowOffDistance=" + ReadPrivateFloat(_budget, "shadowOffDistance").ToString("F0", CultureInfo.InvariantCulture) +
                           ";shadowOnDistance=" + ReadPrivateFloat(_budget, "shadowOnDistance").ToString("F0", CultureInfo.InvariantCulture));
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
                    _budgetEntriesCount = CountPrivate(_budget, "_entries");
                    Append("NOTE;budget_steady;entries=" + _budgetEntriesCount +
                           ";ourCandidates=" + _cityCandidates.Count +
                           ";componentCameras=" + DescribeCameras(_budget) +
                           ";budgetLogs=" + _budgetLogCount);
                    for (int i = 0; i < _budgetLogLines.Count && i < 6; i++)
                        Append("BUDGET_LOG;" + i + ";" + _budgetLogLines[i]);
                    _state = State.Point;
                    BeginCase(0);
                    break;

                case State.Point:
                    StepPoint();
                    break;

                case State.Rebase:
                    StepRebase();
                    break;

                case State.Cleanup:
                    StepCleanup();
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
                        Debug.Log("[FinalValidation] Host запрошен через MainMenuWindow.OnHostClicked()");
                    }
                    else
                    {
                        var start = nmc.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
                        if (start != null) start.Invoke(nmc, null);
                        Debug.Log("[FinalValidation] Host запрошен напрямую через NetworkManagerController.StartHost()");
                    }
                }
                else
                {
                    var start = nmc.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
                    if (start != null) start.Invoke(nmc, null);
                    Debug.Log("[FinalValidation] MainMenuWindow не найден: StartHost() напрямую");
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
            _camTransformBefore = _cam.transform;
            _camPosBefore = _cam.transform.position;
            _camRotBefore = _cam.transform.rotation;
            _camFarBefore = _cam.farClipPlane;
            _camFovBefore = _cam.fieldOfView;
            _camNearBefore = _cam.nearClipPlane;

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
                    _rigBehavioursDisabled.Add(b);
                    disabled++;
                }
            }

            _cam.farClipPlane = BenchFar;
            _cam.fieldOfView = BenchFov;
            _cam.nearClipPlane = 0.1f;

            _worldRoot = FindWorldRoot();
            ForcePhase(12f);

            Debug.Log("[FinalValidation] Риг: " + _cam.name + " (far " + _cam.farClipPlane + ", fov " + _cam.fieldOfView +
                      "), отключено компонентов-двигателей камеры: " + disabled +
                      ", WorldRoot_0_0=" + (_worldRoot != null ? _worldRoot.position.ToString("F2") : "нет"));

            Append("NOTE;camera;name=" + _cam.name + ";occlusionCulling=" + _cam.useOcclusionCulling +
                   ";far=" + _cam.farClipPlane.ToString("F0", CultureInfo.InvariantCulture) +
                   ";fov=" + _cam.fieldOfView.ToString("F0", CultureInfo.InvariantCulture));
        }

        private static Transform FindWorldRoot()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i] != null && roots[i].name == WorldRootName) return roots[i].transform;
            return null;
        }

        private static void ForcePhase(float hour)
        {
            var dnc = FindByTypeName("DayNightController");
            if (dnc == null)
            {
                Debug.LogWarning("[FinalValidation] DayNightController не найден — фаза суток не форсирована");
                return;
            }

            _dayNight = dnc as MonoBehaviour;
            _dayNightWasEnabled = _dayNight != null && _dayNight.enabled;

            var type = dnc.GetType();
            SetPrivateFloat(type, dnc, "_blendProgress", 1f);
            SetPrivateFloat(type, dnc, "_targetBlendProgress", 1f);

            var setTime = type.GetMethod("SetTimeOfDay", BindingFlags.Instance | BindingFlags.Public);
            var refresh = type.GetMethod("ForceRefresh", BindingFlags.Instance | BindingFlags.Public);
            if (setTime != null) setTime.Invoke(dnc, new object[] { hour });
            if (refresh != null) refresh.Invoke(dnc, null);

            if (_dayNight != null) _dayNight.enabled = false;

            var sunField = type.GetField("sunLight", BindingFlags.Instance | BindingFlags.Public);
            var sun = sunField != null ? sunField.GetValue(dnc) as Light : null;
            string sunState = sun == null
                ? "none"
                : sun.name + "(enabled=" + sun.enabled + ",shadows=" + sun.shadows + ",intensity=" +
                  sun.intensity.ToString("F2", CultureInfo.InvariantCulture) + ")";

            Append("NOTE;phase_forced=" + hour.ToString("F1", CultureInfo.InvariantCulture) + ";sun=" + sunState);
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
            var shipRootList = new List<GameObject>();
            var scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go == null) continue;
                if (go.name == WorldRootName) continue;
                if (go.GetComponentInChildren<ShipRootReference>(true) == null) continue;
                shipRootList.Add(go);
                var rs = go.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rs.Length; i++) ships.Add(rs[i]);
            }
            _shipRenderers = ships.ToArray();
            _shipRoots = shipRootList.ToArray();
            _shipSet.Clear();
            for (int i = 0; i < _shipRenderers.Length; i++) _shipSet.Add(_shipRenderers[i]);

            _cityCandidates.Clear();
            _cityBaseModes.Clear();

            // Базовые режимы берём из самого компонента: он собрал их ДО того, как начал гасить
            // тени, а живой скан на этой фазе уже увидел бы Off и запомнил бы его как «базу».
            CollectCityBudgetFromComponent();

            if (_cityCandidates.Count == 0)
            {
                for (int i = 0; i < _allRenderers.Length; i++)
                {
                    var r = _allRenderers[i];
                    if (r == null || !r.enabled) continue;
                    if (IsShipRenderer(r)) continue;
                    if (r.gameObject.layer == DoorLayer) continue;
                    if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
                    _cityCandidates.Add(r);
                    _cityBaseModes.Add(r.shadowCastingMode);
                }
            }

            Debug.Log("[FinalValidation] Рендереров всего=" + _allRenderers.Length +
                      ", корабельных=" + _shipRenderers.Length +
                      ", кандидатов бюджета (наш расчёт)=" + _cityCandidates.Count +
                      ", component entries=" + _budgetEntriesCount +
                      ", ship roots=" + _shipRoots.Length);
            Append("NOTE;renderers;all=" + _allRenderers.Length + ";ships=" + _shipRenderers.Length +
                   ";shipRoots=" + _shipRoots.Length + ";ourCityCandidates=" + _cityCandidates.Count +
                   ";componentEntries=" + _budgetEntriesCount);
        }

        private static bool IsShipRenderer(Renderer r)
        {
            return r != null && _shipSet.Contains(r);
        }

        /// <summary>Кандидаты и их базовые режимы — из приватного списка самого CityShadowBudget.</summary>
        private static void CollectCityBudgetFromComponent()
        {
            if (_budget == null) return;
            var f = typeof(CityShadowBudget).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) return;
            var list = f.GetValue(_budget) as IList;
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry == null) continue;
                var et = entry.GetType();
                var rf = et.GetField("renderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var bf = et.GetField("baseMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var r = rf != null ? rf.GetValue(entry) as Renderer : null;
                if (r == null) continue;
                _cityCandidates.Add(r);
                _cityBaseModes.Add(bf != null ? (ShadowCastingMode)bf.GetValue(entry) : ShadowCastingMode.On);
            }
        }

        /// <summary>A/B кораблей: только runtime renderer.enabled, префабы не трогаются.</summary>
        private static void SetShipsVisible(bool visible)
        {
            if (visible)
            {
                for (int i = 0; i < _shipsDisabled.Count; i++)
                    if (_shipsDisabled[i] != null) _shipsDisabled[i].enabled = true;
                _shipsDisabled.Clear();
                return;
            }

            if (_shipsDisabled.Count > 0) return;
            for (int i = 0; i < _shipRenderers.Length; i++)
            {
                var r = _shipRenderers[i];
                if (r == null || !r.enabled) continue;
                r.enabled = false;
                _shipsDisabled.Add(r);
            }
        }

        /// <summary>A/B городского бюджета: включение/выключение игрового компонента.</summary>
        private static void SetBudgetEnabled(bool on)
        {
            _budgetWantedOn = on;
            if (_budget == null) _budget = UnityEngine.Object.FindAnyObjectByType<CityShadowBudget>();
            if (_budget == null) return;
            if (on && !_budget.enabled) _budget.enabled = true;
            if (!on && _budget.enabled) _budget.enabled = false;
            _budgetEntriesCount = CountPrivate(_budget, "_entries");
        }

        // ------------------------------------------------------------------- точки

        private static void BeginCase(int index)
        {
            _caseIndex = index;
            if (_caseIndex >= _cases.Count) { BeginRebase(); return; }

            var c = _cases[_caseIndex];
            SetShipsVisible(c.shipsVisible);
            SetBudgetEnabled(c.budgetOn);
            ApplyPose(c);
            ForcePhaseSilent(c.hour);

            _dc.Clear(); _spc.Clear(); _tris.Clear(); _sc.Clear();
            _hybridDC.Clear(); _hybridInst.Clear(); _srpDC.Clear(); _ms.Clear();
            _censusDone = false;
            _settleUntilFrame = Time.frameCount + SettleFrames;
            _nextSampleFrame = _settleUntilFrame;
            _caseWindowFrame = Time.frameCount;
            _caseWindowStart = Now;
            Debug.Log("[FinalValidation] Точка " + c.id + " (ships=" + c.shipsVisible + ", budget=" + c.budgetOn +
                      ", entries=" + _budgetEntriesCount + ")");
        }

        private static void ForcePhaseSilent(float hour)
        {
            if (_dayNight == null) return;
            var type = _dayNight.GetType();
            var setTime = type.GetMethod("SetTimeOfDay", BindingFlags.Instance | BindingFlags.Public);
            var refresh = type.GetMethod("ForceRefresh", BindingFlags.Instance | BindingFlags.Public);
            if (setTime != null) setTime.Invoke(_dayNight, new object[] { hour });
            if (refresh != null) refresh.Invoke(_dayNight, null);
        }

        private static void ApplyPose(BenchCase c)
        {
            if (_cam == null) return;
            Vector3 shift = _worldRoot != null ? _worldRoot.position : Vector3.zero;
            _targetPos = c.local + AuthoredWorldOffset + shift;
            _targetRot = Quaternion.Euler(c.pitch, c.yaw, 0f);
            _cam.transform.SetPositionAndRotation(_targetPos, _targetRot);
        }

        private static void StepPoint()
        {
            var c = _cases[_caseIndex];
            ApplyPose(c);

            if (Time.frameCount < _settleUntilFrame) return;

            if (!_censusDone)
            {
                RunCensus();
                if (_state != State.Point) return;
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
                Debug.LogWarning("[FinalValidation] UnityStats.drawCalls=0 (screenRes=" + UnityStats.screenRes +
                                 "): Game View не рендерит, замер недостоверен.");
                Append("NOTE;warning=drawCalls_0_at_first_sample;screenRes=" + UnityStats.screenRes);
            }

            if (_dc.Count < SampleCount) { _nextSampleFrame = Time.frameCount + SampleGapFrames; return; }

            WriteCaseLine(c);
            BeginCase(_caseIndex + 1);
        }

        private static void RunCensus()
        {
            _censusInFrustum = 0;
            _censusDoorsInFrustum = 0;
            _censusEnabledTotal = 0;
            _censusCity = 0;
            _censusShadowsOff = 0;

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
                if (!IsShipRenderer(r)) _censusCity++;
            }

            for (int i = 0; i < _cityCandidates.Count; i++)
            {
                var r = _cityCandidates[i];
                if (r != null && r.shadowCastingMode == ShadowCastingMode.Off) _censusShadowsOff++;
            }

            if (_censusCity == 0)
            {
                Debug.LogError("[FinalValidation] В кадре 0 городских рендереров: поза/область неверны, замер остановлен.");
                Append("NOTE;error=census_zero;campos=" + _targetPos.ToString("F1"));
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
            sb.Append(";ships=").Append(c.shipsVisible ? "on" : "off");
            sb.Append(";budget=").Append(c.budgetOn ? "on" : "off");
            sb.Append(";budgetEntries=").Append(_budgetEntriesCount);
            sb.Append(";shadowsOff=").Append(_censusShadowsOff);
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
            sb.Append(";cityInFrustum=").Append(_censusCity);
            sb.Append(";doorsInFrustum=").Append(_censusDoorsInFrustum);
            sb.Append(";rawDc=").Append(string.Join("|", _dc.ConvertAll(v => v.ToString()).ToArray()));
            sb.Append(";rawShadowCasters=").Append(string.Join("|", _sc.ConvertAll(v => v.ToString()).ToArray()));
            Append(sb.ToString());
            AppendCsv(c, dc, spc, tris, sc, hybridDC, hybridInst, srpDC, ms, fps);

            Debug.Log("[FinalValidation] " + c.id + ": dc=" + dc + " spc=" + spc + " tris=" + tris +
                      " shadowCasters=" + sc + " hybridDC=" + hybridDC + " shadowsOff=" + _censusShadowsOff +
                      " entries=" + _budgetEntriesCount);
        }

        private static void AppendCsv(BenchCase c, int dc, int spc, int tris, int sc, int hybridDC, int hybridInst,
                                      int srpDC, double ms, double fps)
        {
            bool writeHeader = !File.Exists(_outCsvPath);
            var sb = new StringBuilder();
            if (writeHeader)
                sb.AppendLine("run,date,case,ships,budget,budgetEntries,shadowsOff,dc,spc,tris,shadowCasters,hybridDC,hybridInstances,srpBatcherDC,ms,fps,enabledRenderers,inFrustum,cityInFrustum,doorsInFrustum,worldpos");
            sb.Append(_runTag).Append(',')
              .Append(DateTag).Append(',')
              .Append(c.id).Append(',')
              .Append(c.shipsVisible ? "on" : "off").Append(',')
              .Append(c.budgetOn ? "on" : "off").Append(',')
              .Append(_budgetEntriesCount).Append(',')
              .Append(_censusShadowsOff).Append(',')
              .Append(dc).Append(',').Append(spc).Append(',').Append(tris).Append(',').Append(sc).Append(',')
              .Append(hybridDC).Append(',').Append(hybridInst).Append(',').Append(srpDC).Append(',')
              .Append(ms.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
              .Append(fps.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
              .Append(_censusEnabledTotal).Append(',').Append(_censusInFrustum).Append(',')
              .Append(_censusCity).Append(',').Append(_censusDoorsInFrustum).Append(',')
              .Append('"').Append(_targetPos.ToString("F2")).Append('"');
            sb.AppendLine();
            try { File.AppendAllText(_outCsvPath, sb.ToString(), Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[FinalValidation] CSV: " + e.Message); }
        }

        // ============================================================ F8 / F9 (реальный ввод)

        private static void BeginRebase()
        {
            // FO-тест выполняется в состоянии реальной игры: корабли видимы, бюджет включён.
            SetShipsVisible(true);
            SetBudgetEnabled(true);
            ApplyPose(_cases[0]);

            _rebaseSlice = FindByTypeName("GlobalMotionControlledRebaseSlice") as MonoBehaviour;
            if (_rebaseSlice == null)
            {
                Append("REBASE;status=slice_not_found");
                Debug.LogError("[FinalValidation] GlobalMotionControlledRebaseSlice не найден — F8/F9 не проверены.");
                _state = State.Cleanup;
                _stateSince = Now;
                return;
            }

            _rebasePhase = 0;
            _state = State.Rebase;
            _stateSince = Now;
            Debug.Log("[FinalValidation] F8/F9: срез найден, начинаю фазу 0");
        }

        private static int DoorControllerCount()
        {
            return UnityEngine.Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None).Length;
        }

        private static int DoorRendererCount()
        {
            if (_allRenderers == null) return -1;
            int n = 0;
            for (int i = 0; i < _allRenderers.Length; i++)
                if (_allRenderers[i] != null && _allRenderers[i].gameObject.layer == DoorLayer) n++;
            return n;
        }

        private static string ShipRootsDump()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _shipRoots.Length && i < 8; i++)
            {
                var go = _shipRoots[i];
                if (go == null) continue;
                Vector3 rel = go.transform.position - (_worldRoot != null ? _worldRoot.position : Vector3.zero);
                if (i > 0) sb.Append('|');
                sb.Append(go.name).Append('@').Append(rel.ToString("F2"));
            }
            return sb.ToString();
        }

        private static void PressKey(Key key)
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                Append("NOTE;error=no_keyboard_device");
                Debug.LogError("[FinalValidation] Keyboard.current == null — реальный ввод невозможен.");
                return;
            }
            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            _keyReleaseFrame = Time.frameCount + 2;
            _pressAttempts++;
            _rebaseLines.Add("input=QueueStateEvent(" + key + ");frame=" + Time.frameCount + ";attempt=" + _pressAttempts);
            Append("REBASE_INPUT;key=" + key + ";frame=" + Time.frameCount + ";attempt=" + _pressAttempts);
        }

        private static void TickKeyRelease()
        {
            if (_keyReleaseFrame < 0) return;
            if (Time.frameCount < _keyReleaseFrame) return;
            var kb = Keyboard.current;
            if (kb != null) InputSystem.QueueStateEvent(kb, new KeyboardState());
            _keyReleaseFrame = -1;
        }

        private static ulong FrameGenOf()
        {
            if (_rebaseSlice == null) return 0ul;
            var p = _rebaseSlice.GetType().GetProperty("FrameGeneration", BindingFlags.Instance | BindingFlags.Public);
            return p != null ? Convert.ToUInt64(p.GetValue(_rebaseSlice), CultureInfo.InvariantCulture) : 0ul;
        }

        private static bool TryTeleportPlayerPastThreshold(string tag, out Vector3 newPos, out float predicted)
        {
            newPos = Vector3.zero;
            predicted = 0f;

            if (_playerRoot == null)
            {
                var type = _rebaseSlice.GetType();
                var m = type.GetMethod("TryResolveLocalPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
                object[] args = { null, null };
                if (m != null)
                {
                    bool ok = (bool)m.Invoke(_rebaseSlice, args);
                    if (ok) _playerRoot = args[0] as Transform;
                    else Append("REBASE;step=" + tag + ";player_resolve_failed=" + (args[1] as string));
                }
            }
            if (_playerRoot == null) return false;

            var frameProp = _rebaseSlice.GetType().GetProperty("CurrentFrame", BindingFlags.Instance | BindingFlags.Public);
            if (frameProp == null) return false;
            var frame = (LocalCoordinateFrame)frameProp.GetValue(_rebaseSlice);

            float threshold = ReadPrivateFloat(_rebaseSlice, "_threshold");
            float quantum = ReadPrivateFloat(_rebaseSlice, "_quantum");
            if (threshold <= 0f) threshold = 256f;
            if (quantum <= 0f) quantum = 256f;

            Vector3 basePos = _playerRoot.position;
            float[] offsets = { 420f, 520f, 700f, 900f, -420f, -520f, 1300f, -1300f };
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 cand = basePos + new Vector3(offsets[i], 0f, 0f);
                OriginRebasePlan plan;
                if (!OriginRebasePlan.TryCreate(frame, cand, threshold, quantum, out plan)) continue;

                newPos = cand;
                predicted = plan.LocalTranslation.magnitude;
                Append("REBASE;step=" + tag + ";playerFrom=" + basePos.ToString("F2") +
                       ";playerTo=" + cand.ToString("F2") + ";offset=" + offsets[i].ToString("F1", CultureInfo.InvariantCulture) +
                       ";threshold=" + threshold.ToString("F0", CultureInfo.InvariantCulture) +
                       ";quantum=" + quantum.ToString("F0", CultureInfo.InvariantCulture) +
                       ";predictedTranslation=" + plan.LocalTranslation.ToString("F2"));
                return true;
            }

            Append("REBASE;step=" + tag + ";status=no_plan_for_any_offset;playerPos=" + basePos.ToString("F2") +
                   ";threshold=" + threshold.ToString("F0", CultureInfo.InvariantCulture));
            return false;
        }

        private static void StepRebase()
        {
            var type = _rebaseSlice != null ? _rebaseSlice.GetType() : null;
            if (type == null) { _state = State.Cleanup; _stateSince = Now; return; }

            switch (_rebasePhase)
            {
                case 0: // baseline + F8
                    {
                        Vector3 newPos; float predicted;
                        if (!TryTeleportPlayerPastThreshold("F8", out newPos, out predicted))
                        {
                            Append("REBASE;phase=F8;status=skipped_no_plan");
                            _rebasePhase = 3;
                            _rebaseStepFrame = Time.frameCount;
                            return;
                        }

                        _frameGenBefore = FrameGenOf();
                        _cumulativeBefore = GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset;
                        _rootBefore = _worldRoot != null ? _worldRoot.position : Vector3.zero;
                        _playerBefore = _playerRoot.position;
                        _predictedShiftMagnitude = predicted;

                        _playerRoot.position = newPos;
                        Physics.SyncTransforms();

                        Append("REBASE;phase=F8_before;frameGen=" + _frameGenBefore +
                               ";cumulative=" + _cumulativeBefore.ToString("F2") +
                               ";worldRoot=" + _rootBefore.ToString("F2") +
                               ";player=" + _playerRoot.position.ToString("F2") +
                               ";doors=" + DoorControllerCount() + ";doorRenderers=" + DoorRendererCount() +
                               ";budgetEntries=" + CountPrivate(_budget, "_entries") +
                               ";ships=" + ShipRootsDump() +
                               ";errorsSoFar=" + _errorCount);

                        PressKey(Key.F8);
                        _rebasePhase = 1;
                        _rebaseStepFrame = Time.frameCount;
                        _rebasePhaseName = "F8";
                        break;
                    }

                case 1: // ожидание F8
                    {
                        bool settled = FrameGenOf() != _frameGenBefore ||
                                       GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset != _cumulativeBefore ||
                                       (_worldRoot != null && Vector3.Distance(_worldRoot.position, _rootBefore) > 0.01f);

                        if (!settled && Time.frameCount - _rebaseStepFrame < RebaseTimeoutFrames) return;

                        if (!settled)
                        {
                            if (_pressAttempts < 3)
                            {
                                Append("REBASE;phase=F8;status=no_transaction_yet;retry=" + (_pressAttempts + 1));
                                PressKey(Key.F8);
                                _rebaseStepFrame = Time.frameCount;
                                return;
                            }
                            Append("REBASE;phase=F8;status=FAILED_no_transaction;attempts=" + _pressAttempts +
                                   ";frames=" + (Time.frameCount - _rebaseStepFrame) + ";errorsSoFar=" + _errorCount);
                            _rebasePhase = 3;
                            _rebaseStepFrame = Time.frameCount;
                            return;
                        }

                        Vector3 rootAfter = _worldRoot != null ? _worldRoot.position : Vector3.zero;
                        Append("REBASE;phase=F8_after;frameGen=" + FrameGenOf() +
                               ";cumulative=" + GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset.ToString("F2") +
                               ";worldRoot=" + rootAfter.ToString("F2") +
                               ";worldRootShift_m=" + Vector3.Distance(rootAfter, _rootBefore).ToString("F3", CultureInfo.InvariantCulture) +
                               ";predictedShift_m=" + _predictedShiftMagnitude.ToString("F3", CultureInfo.InvariantCulture) +
                               ";player=" + _playerRoot.position.ToString("F2") +
                               ";doors=" + DoorControllerCount() + ";doorRenderers=" + DoorRendererCount() +
                               ";budgetEntries=" + CountPrivate(_budget, "_entries") +
                               ";ships=" + ShipRootsDump() +
                               ";frames=" + (Time.frameCount - _rebaseStepFrame) + ";errorsSoFar=" + _errorCount);

                        _rebasePhase = 2;
                        _rebaseStepFrame = Time.frameCount;
                        _rebasePhaseName = "F9";
                        break;
                    }

                case 2: // подготовка и F9
                    {
                        if (Time.frameCount - _rebaseStepFrame < 30) return;

                        Vector3 newPos; float predicted;
                        if (!TryTeleportPlayerPastThreshold("F9", out newPos, out predicted))
                        {
                            Append("REBASE;phase=F9;status=skipped_no_plan");
                            _rebasePhase = 3;
                            _rebaseStepFrame = Time.frameCount;
                            return;
                        }

                        _frameGenBefore = FrameGenOf();
                        _cumulativeBefore = GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset;
                        _rootBefore = _worldRoot != null ? _worldRoot.position : Vector3.zero;
                        _predictedShiftMagnitude = predicted;

                        _playerRoot.position = newPos;
                        Physics.SyncTransforms();

                        Append("REBASE;phase=F9_before;frameGen=" + _frameGenBefore +
                               ";cumulative=" + _cumulativeBefore.ToString("F2") +
                               ";worldRoot=" + _rootBefore.ToString("F2") +
                               ";player=" + _playerRoot.position.ToString("F2") +
                               ";doors=" + DoorControllerCount() + ";doorRenderers=" + DoorRendererCount() +
                               ";budgetEntries=" + CountPrivate(_budget, "_entries") +
                               ";ships=" + ShipRootsDump() +
                               ";errorsSoFar=" + _errorCount);

                        PressKey(Key.F9);
                        _rebasePhase = 4;
                        _rebaseStepFrame = Time.frameCount;
                        break;
                    }

                case 4: // ожидание F9
                    {
                        bool settled = FrameGenOf() != _frameGenBefore ||
                                       GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset != _cumulativeBefore;
                        if (!settled && Time.frameCount - _rebaseStepFrame < RebaseTimeoutFrames) return;

                        if (!settled && _pressAttempts < 6)
                        {
                            Append("REBASE;phase=F9;status=no_transaction_yet;retry=" + (_pressAttempts + 1));
                            PressKey(Key.F9);
                            _rebaseStepFrame = Time.frameCount;
                            return;
                        }

                        Vector3 rootAfter = _worldRoot != null ? _worldRoot.position : Vector3.zero;
                        Append("REBASE;phase=F9_after;" + (settled ? "status=settled" : "status=FAILED_no_transaction") +
                               ";frameGen=" + FrameGenOf() +
                               ";cumulative=" + GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset.ToString("F2") +
                               ";worldRoot=" + rootAfter.ToString("F2") +
                               ";worldRootShift_m=" + Vector3.Distance(rootAfter, _rootBefore).ToString("F3", CultureInfo.InvariantCulture) +
                               ";predictedShift_m=" + _predictedShiftMagnitude.ToString("F3", CultureInfo.InvariantCulture) +
                               ";player=" + _playerRoot.position.ToString("F2") +
                               ";doors=" + DoorControllerCount() + ";doorRenderers=" + DoorRendererCount() +
                               ";budgetEntries=" + CountPrivate(_budget, "_entries") +
                               ";ships=" + ShipRootsDump() +
                               ";frames=" + (Time.frameCount - _rebaseStepFrame) + ";errorsSoFar=" + _errorCount);

                        _rebasePhase = 3;
                        _rebaseStepFrame = Time.frameCount;
                        break;
                    }

                case 3: // итог
                    {
                        Append("REBASE;phase=summary;pressAttempts=" + _pressAttempts +
                               ";errors=" + _errorCount + ";exceptions=" + _exceptionCount +
                               ";baselineErrors=" + _baselineErrorCount + ";shaderErrors=" + _shaderErrorCount +
                               ";hybridEvidence=see_DC_cases");
                        for (int i = 0; i < _rebaseLines.Count && i < 24; i++) Append("REBASE_TRACE;" + _rebaseLines[i]);
                        _state = State.Cleanup;
                        _stateSince = Now;
                        break;
                    }
            }
        }

        // ------------------------------------------------------------------ cleanup

        private static void StepCleanup()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                RestoreRuntime();
                _stateSince = Now;
                EditorApplication.ExitPlaymode();
                return;
            }

            Append("NOTE;post_exit;sceneDirtyAtStart=" + _dirtyAtStart +
                   ";sceneDirtyNow=" + SceneManager.GetActiveScene().isDirty);
            Finish("OK", false);
        }

        /// <summary>Возвращает всё, что бенч менял в runtime (сцена не сохраняется).</summary>
        private static void RestoreRuntime()
        {
            try { SetShipsVisible(true); } catch (Exception) { }

            for (int i = 0; i < _cityCandidates.Count; i++)
            {
                var r = _cityCandidates[i];
                if (r == null) continue;
                r.shadowCastingMode = _cityBaseModes[i];
            }

            for (int i = 0; i < _rigBehavioursDisabled.Count; i++)
                if (_rigBehavioursDisabled[i] != null) _rigBehavioursDisabled[i].enabled = true;
            _rigBehavioursDisabled.Clear();

            if (_dayNight != null) _dayNight.enabled = _dayNightWasEnabled;

            if (_cam != null && _camTransformBefore != null)
            {
                _camTransformBefore.SetPositionAndRotation(_camPosBefore, _camRotBefore);
                _cam.farClipPlane = _camFarBefore;
                _cam.fieldOfView = _camFovBefore;
                _cam.nearClipPlane = _camNearBefore;
            }

            if (_playerRoot != null) _playerRoot.position = _playerBefore;
        }

        // ------------------------------------------------------------------ финиш

        private static void Finish(string result, bool keepPlaying)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(SessionKey, false);
            UnhookLog();

            Append("FINAL_ERRORS;errors=" + _errorCount +
                   ";exceptions=" + _exceptionCount +
                   ";baselineErrors=" + _baselineErrorCount +
                   ";shaderErrors=" + _shaderErrorCount +
                   ";warnings=" + _warnCount +
                   ";budgetLogs=" + _budgetLogCount);
            for (int i = 0; i < _errorLines.Count && i < 30; i++)
                Append("ERROR_LINE;" + _errorLines[i].Replace("\n", " | "));

            Append("BENCH_DONE;result=" + result +
                   ";casesCompleted=" + _caseIndex + "/" + _cases.Count +
                   ";sceneSaved=no;push=no;hold=" + (keepPlaying ? "yes" : "no"));

            Debug.Log("[FinalValidation] Завершено: " + result + ". Файл: " + _outRawPath);

            if (keepPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _state = State.Off;
                return;
            }

            _state = State.Off;
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        }

        private static void Append(string line)
        {
            if (string.IsNullOrEmpty(_outRawPath)) return;
            try { File.AppendAllText(_outRawPath, line + Environment.NewLine, Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[FinalValidation] Не удалось записать строку: " + e.Message); }
        }

        // ------------------------------------------------------------- консоль

        private static void HookLog()
        {
            if (_logHooked) return;
            Application.logMessageReceived += OnLogMessage;
            _logHooked = true;
        }

        private static void UnhookLog()
        {
            if (!_logHooked) return;
            Application.logMessageReceived -= OnLogMessage;
            _logHooked = false;
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (condition == null) return;

            if (condition.Contains("[CityShadowBudget]"))
            {
                _budgetLogCount++;
                if (_budgetLogLines.Count < 12) _budgetLogLines.Add(condition);
            }

            if (condition.Contains("[T-FO06CY]"))
            {
                if (_rebaseLines.Count < 60) _rebaseLines.Add(condition);
            }

            if (type == LogType.Warning)
            {
                _warnCount++;
                return;
            }

            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;

            if (type == LogType.Exception) _exceptionCount++;
            else _errorCount++;

            if (condition.Contains("PadStateSync")) _baselineErrorCount++;
            if (condition.IndexOf("shader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                condition.Contains("Hidden/InternalErrorShader")) _shaderErrorCount++;

            if (_errorLines.Count < 40) _errorLines.Add(type + ":" + condition);
        }

        // ------------------------------------------------------------- утилиты

        private static int CountPrivate(object target, Type type, string field)
        {
            if (target == null || type == null) return -1;
            var f = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) return -1;
            var v = f.GetValue(target) as ICollection;
            return v != null ? v.Count : -1;
        }

        private static int CountPrivate(object target, string field)
        {
            return target == null ? -1 : CountPrivate(target, target.GetType(), field);
        }

        private static float ReadPrivateFloat(object target, string field)
        {
            if (target == null) return -1f;
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null || f.FieldType != typeof(float)) return -1f;
            return (float)f.GetValue(target);
        }

        private static string DescribeCameras(object target)
        {
            if (target == null) return "none";
            var f = target.GetType().GetField("_cameras", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) return "none";
            var list = f.GetValue(target) as IList;
            if (list == null) return "none";
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count && i < 8; i++)
            {
                var cam = list[i] as Camera;
                if (i > 0) sb.Append('|');
                sb.Append(cam != null ? cam.name : "null");
            }
            return sb.Length > 0 ? sb.ToString() : "none";
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
                Debug.LogError("[FinalValidation] Слишком много доменных перезагрузок подряд — прогон отменён.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _outRawPath = Path.Combine(projectRoot, DataDir, "Q_final_validation_" + DateTag + "_raw.txt");
            _outCsvPath = Path.Combine(projectRoot, DataDir, "Q_final_validation_" + DateTag + ".csv");
            _state = State.WaitPlayMode;
            _stateSince = Now;
            _hostRequested = false;
            _menuHidden = false;

            Append("NOTE;domain_reload=true;restart=" + reloads);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[FinalValidation] Доменная перезагрузка (#" + reloads + ") — прогон продолжен из SessionState.");
        }
    }

    [InitializeOnLoad]
    internal static class FinalValidationBenchBootstrap
    {
        static FinalValidationBenchBootstrap()
        {
            FinalValidationBench.ResumeIfRunning();
        }
    }
}
