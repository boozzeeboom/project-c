// Project C — T-CULL01 / Stage 3a (H2): рантайм-скамейка корабля HeavyII.
// Методика — та же, что в Stage 2 (Stage2CullingBench): Play Mode → Host, прогрев 1500 кадров,
// Midday forced, камера рига (SpringArmCamera отключён), UnityStats, медиана из 3 замеров.
//
// Что меряется:
//   1) стабильность корабля на платформе (позиция/скорость Rigidbody за 120 кадров);
//   2) DC/SetPass/tris/Slots, приписанные КОРАБЛЮ, на трёх дистанциях — A/B по выключению
//      MeshRenderer'ов корабля (A: корабль виден, B: корабль выключен, shipDC = A − B);
//   3) Frame Debugger на LOD0-точке: число событий кадра и события со склейкой HeavyII_VIS_*;
//   4) цены интерактива: DoorController.Open/Close (сдвиг створки), 9 дверей/250 коллайдеров/
//      11 ModuleSlot/PilotSeat/CargoVisual/ContrailVFX;
//   5) F8/F9 — тот же путь транзакции Floating Origin (RequestControlledRebase / …WithRollback):
//      сдвиг WorldRoot_0_0, позиция корабля до/после, ошибки в консоли.
//
// Запуск (важно): замер НЕ входит в Play Mode сам. Раньше он это делал и попадал в пустой мир —
// открытая на тот момент BootstrapScene без загруженного мира и без персонажа. Порядок такой:
//   1) Project C → Culling → Stage 3 Ship Bench → Run — взводит замер и пишет шапку файла;
//   2) обычный старт игры: Play → «Одиночная игра» (Bootstrap сам загрузит мир);
//   3) замер подхватывает сессию на EnteredPlayMode и дальше идёт сам.
// Обратный порядок тоже работает: сначала старт игры, потом Run — замер подключится к идущей сессии.
//
// Чего НЕ делает: не сохраняет сцену, не правит ассеты и код, не трогает коллайдеры и материалы.
// Всё, что меняется (поза камеры, enabled рендереров, enabled DayNightController), живёт только в Play Mode.
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
    public static class Stage3ShipBench
    {
        private const string RunMenu = "Project C/Culling/Stage 3 Ship Bench/Run";
        private const string ProbeMenu = "Project C/Culling/Stage 3 Ship Bench/Probe Only (rebase)";
        private const string AbortMenu = "Project C/Culling/Stage 3 Ship Bench/Abort";

        private const string RelOutPath =
            "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/N_stage3c_heavy_runtime_2026-10-31.txt";

        public const string SessionKey = "ProjectC.Stage3ShipBench.Running";

        private const string ShipRootName = "Ship_havyII_base";
        private const string MergedPrefix = "HeavyII_VIS_";

        private const float BenchFar = 30000f;
        private const float BenchFov = 60f;

        private const int WarmupFrames = 1500;
        private const int SettleFrames = 40;
        private const int SampleGapFrames = 8;
        private const int SampleCount = 3;
        private const int OffSettleFrames = 12;

        private const double HostTimeoutSeconds = 300.0;
        private const double RigTimeoutSeconds = 240.0;
        private const double WarmupTimeoutSeconds = 900.0;

        private sealed class DistanceCase
        {
            public string id;
            public float distance;
            public bool repeat;
        }

        // Stage 3c: набор точек из брифа — 80/120/200/300/400/1200, повтор на 200 м (±5 %).
        private static readonly List<DistanceCase> Cases = new List<DistanceCase>
        {
            new DistanceCase { id = "d80",  distance = 80f },
            new DistanceCase { id = "d120", distance = 120f },
            new DistanceCase { id = "d200", distance = 200f },
            new DistanceCase { id = "d200_repeat", distance = 200f, repeat = true },
            new DistanceCase { id = "d300", distance = 300f },
            new DistanceCase { id = "d400", distance = 400f },
            new DistanceCase { id = "d1200", distance = 1200f },
        };

        private enum State { Off, WaitPlayMode, WaitHost, WaitRig, EarlyProbe, Warmup, Case, Stability, Doors, Rebase, Done }

        private static State _state = State.Off;
        private static int _caseIndex;
        private static double _stateSince;
        private static string _outPath;

        private static Camera _cam;
        private static Transform _worldRoot;
        private static Transform _ship;
        private static LODGroup _lodGroup;
        private static MeshRenderer[] _shipRenderers;
        private static readonly List<bool> _shipEnabledBefore = new List<bool>();

        private static readonly List<int> _dcA = new List<int>();
        private static readonly List<int> _spcA = new List<int>();
        private static readonly List<int> _trisA = new List<int>();
        private static readonly List<int> _scA = new List<int>();
        private static readonly List<int> _dcB = new List<int>();
        private static readonly List<int> _spcB = new List<int>();
        private static readonly List<int> _trisB = new List<int>();
        private static readonly List<int> _scB = new List<int>();

        private static int _settleUntilFrame, _nextSampleFrame;
        private static int _fdPhase;   // 0 idle, 1 enabled/waiting, 2 done
        private static int _fdFrame;
        private static Type _fdType;
        private static bool _censusDone, _shipDisabled, _fdDone;
        private static int _caseFrameStart;
        private static double _caseTimeStart;
        private static int _warmStartFrame;
        private static bool _hostRequested, _menuHidden;
        private static string _hostDiag = "";
        private static int _errorCount;
        private static readonly List<string> _firstErrors = new List<string>();
        private static readonly Plane[] _planes = new Plane[6];

        // этапы после дистанционных точек
        private static int _probeFrame;
        private static int _probeStartFrame;
        private static int _stepFrame;
        private static Vector3 _stabPos0, _stabVel0;
        private static DoorController _testDoor;
        private static Transform _testDoorPanel;
        private static Vector3 _doorPanelLocal0;
        private static Quaternion _doorPanelRot0;
        private static Vector3 _doorPanelCenter0;
        private static float _doorOpenProgress;
        private static bool _doorOpened;
        private static MonoBehaviour _rebaseSlice;
        private static Vector3 _rebaseRootBefore, _rebaseShipBefore;
        private static int _rebasePhase;
        private static bool _rebaseSawStart;

        private static double Now { get { return EditorApplication.timeSinceStartup; } }

        // ============================================================ меню

        private static bool _probeOnly;

        [MenuItem(ProbeMenu)]
        public static void ProbeOnly()
        {
            SessionState.SetBool("ProjectC.Stage3Bench.ProbeOnly", true);
            Run();
        }

        [MenuItem(RunMenu)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
            {
                Debug.LogError("[Stage3Bench] Идёт вход в Play Mode — дождись загрузки мира и нажми Run ещё раз.");
                return;
            }

            _probeOnly = SessionState.GetBool("ProjectC.Stage3Bench.ProbeOnly", false);
            SessionState.SetBool("ProjectC.Stage3Bench.ProbeOnly", false);

            _outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RelOutPath));
            string dir = Path.GetDirectoryName(_outPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(_outPath)) File.Delete(_outPath);

            _state = State.WaitPlayMode;
            _caseIndex = 0;
            _hostRequested = false;
            _menuHidden = false;
            _cam = null; _worldRoot = null; _ship = null; _lodGroup = null; _shipRenderers = null;
            _errorCount = 0;
            _firstErrors.Clear();
            _stateSince = Now;

            SessionState.SetBool(SessionKey, true);
            SessionState.SetInt(SessionKey + ".reloads", 0);

            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;

            Append("NOTE;bench=Stage3ShipBench;unity=" + Application.unityVersion +
                   ";date=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Append("NOTE;method=PlayMode→Host;warmup=" + WarmupFrames + "fr;settle=" + SettleFrames +
                   "fr;samples=" + SampleCount + "x gap " + SampleGapFrames +
                   "fr;metric=UnityStats median;far=" + BenchFar + ";fov=" + BenchFov +
                   ";shipDC=A(ship on)-B(all ship MeshRenderer.enabled=false)");
            Append("NOTE;lodThresholds=raw 300/900@FOV60 lodBias1 (Stage 3c, без изменений с Stage 3a); эффективная граница = raw × lodBias (ViewDistanceApplier: Near 0.7 / default 1.0 / Far+Ultra 1.3); порог LOD0 ближе ~281.6 м задать нельзя — P2 закрывается бюджетом теней");

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;

            if (EditorApplication.isPlaying)
                Debug.Log("[Stage3Bench] Замер подключён к идущей сессии Play Mode. Файл: " + _outPath);
            else
                Debug.Log("[Stage3Bench] Замер взведён. Нажми Play и стартуй игру как обычно (Bootstrap → «Одиночная игра») — замер начнётся сам. Файл: " + _outPath);
        }

        /// <summary>
        /// Замер не входит в Play Mode сам: вход в Play на открытой сцене без мира давал пустой мир
        /// без персонажа. Сессию подхватываем по событию, а если она уже идёт — сразу из Run().
        /// </summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(SessionKey, false)) return;

            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                _state = State.WaitPlayMode;
                _stateSince = Now;
                _hostRequested = false;
                _menuHidden = false;
                _hostDiag = "";
                Append("NOTE;playmode=attached_by_event;scene=" +
                       UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                Debug.Log("[Stage3Bench] Play Mode начался — замер подхватил сессию.");
            }
            else if (change == PlayModeStateChange.ExitingPlayMode && _state != State.Off)
            {
                Append("NOTE;playmode=exiting;state=" + _state);
            }
        }

        [MenuItem(AbortMenu)]
        public static void Abort()
        {
            Finish("ABORTED_BY_USER");
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _errorCount++;
            if (_firstErrors.Count < 8) _firstErrors.Add(type + ": " + condition);
        }

        // ============================================================ тик

        private static void Tick()
        {
            try { Step(); }
            catch (Exception e)
            {
                Debug.LogError("[Stage3Bench] Исключение: " + e);
                Finish("EXCEPTION");
            }
        }

        private static void Step()
        {
            // Play Mode закончился (пользователь остановил игру) — тикер не должен продолжать работу
            // в edit mode: объекты уже уничтожены и обращение к ним даёт MissingReferenceException.
            if (_state != State.Off && _state != State.WaitPlayMode && !EditorApplication.isPlaying)
            {
                Append("NOTE;playmode_ended=true;state=" + _state);
                Finish("PLAYMODE_ENDED");
                return;
            }

            switch (_state)
            {
                case State.WaitPlayMode:
                    if (!EditorApplication.isPlaying) return;
                    if (Now - _stateSince < 2.0) return;
                    _state = State.WaitHost; _stateSince = Now;
                    break;

                case State.WaitHost:
                    if (!TryStartHost())
                    {
                        if (Now - _stateSince > HostTimeoutSeconds)
                        {
                            Append("NOTE;host_timeout_diag=" + _hostDiag);
                            Finish("HOST_TIMEOUT");
                        }
                        return;
                    }
                    _state = State.WaitRig; _stateSince = Now;
                    break;

                case State.WaitRig:
                    if (!TryAcquireRig() || !TryAcquireShip())
                    {
                        if (Now - _stateSince > RigTimeoutSeconds) Finish("RIG_TIMEOUT");
                        return;
                    }
                    PrepareBench();
                    _warmStartFrame = Time.frameCount;
                    _state = State.EarlyProbe; _stateSince = Now;
                    _probeFrame = 0;
                    break;

                case State.EarlyProbe:
                    StepEarlyProbe();
                    break;

                case State.Warmup:
                    if (Time.frameCount - _warmStartFrame < WarmupFrames)
                    {
                        if (Now - _stateSince > WarmupTimeoutSeconds) Finish("WARMUP_TIMEOUT");
                        return;
                    }
                    CacheShipRenderers();
                    _state = State.Stability;
                    _stepFrame = Time.frameCount;
                    _stabPos0 = _ship.position;
                    var rb = _ship.GetComponent<Rigidbody>();
                    _stabVel0 = rb != null ? rb.linearVelocity : Vector3.zero;
                    break;

                case State.Stability:
                    if (Time.frameCount - _stepFrame < 120) return;
                    FinishStability();
                    _state = State.Case;
                    BeginCase(0);
                    break;

                case State.Case:
                    StepCase();
                    break;

                case State.Doors:
                    StepDoors();
                    break;

                case State.Rebase:
                    StepRebase();
                    break;
            }
        }

        // ============================================================ Play Mode → Host

        private static bool TryStartHost()
        {
            if (TryAcquireRig() && TryAcquireShip()) { _hostRequested = true; _menuHidden = true; return true; }

            var menu = FindByTypeName("MainMenuWindow");
            var nmc = FindByTypeName("NetworkManagerController");
            if (nmc == null)
            {
                _hostDiag = "нет NetworkManagerController — мир/бутстрап ещё не загрузился";
                return false;
            }
            if (menu == null) _hostDiag = "нет MainMenuWindow — Host стартуем напрямую через NMC";

            if (!_hostRequested)
            {
                _hostRequested = true;
                var onClick = menu != null ? menu.GetType().GetMethod("OnHostClicked", BindingFlags.Instance | BindingFlags.NonPublic) : null;
                if (onClick != null)
                {
                    onClick.Invoke(menu, null);
                    Debug.Log("[Stage3Bench] Host запрошен через MainMenuWindow.OnHostClicked()");
                }
                else
                {
                    var start = nmc.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
                    if (start != null) start.Invoke(nmc, null);
                    Debug.Log("[Stage3Bench] Host запрошен через NetworkManagerController.StartHost()");
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

        private static bool TryAcquireShip()
        {
            var groups = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
            for (int i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                if (g == null) continue;
                var root = g.transform;
                while (root.parent != null && !root.name.Equals(ShipRootName, StringComparison.Ordinal)) root = root.parent;
                if (!root.name.Equals(ShipRootName, StringComparison.Ordinal)) continue;
                _lodGroup = g;
                _ship = root;
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

            Debug.Log("[Stage3Bench] Риг: " + _cam.name + " (far " + _cam.farClipPlane + ", fov " + _cam.fieldOfView +
                      "), отключено компонентов камеры: " + disabled +
                      ", ship=" + _ship.name + " pos=" + _ship.position.ToString("F2") +
                      ", WorldRoot_0_0=" + (_worldRoot != null ? _worldRoot.position.ToString("F2") : "нет"));
            Append("NOTE;camera=" + _cam.name + ";far=" + BenchFar + ";fov=" + BenchFov +
                   ";lodBias=" + Fmt(QualitySettings.lodBias) + ";quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                   ";shipWorldPos=" + Vec(_ship.position));
            AppendLodInfo();
            AppendShadowBudgetInfo();
        }

        /// <summary>
        /// Состояние бюджета теней (Stage 3c): есть ли компонент, какие параметры и сколько частей
        /// он ведёт. Классификация берётся из того же ShipShadowBudget.CollectCandidates, что и в рантайме.
        /// </summary>
        private static void AppendShadowBudgetInfo()
        {
            var budget = _ship != null ? _ship.GetComponent<ShipShadowBudget>() : null;
            if (budget == null)
            {
                Append("SHADOWBUDGET;present=no");
                Debug.LogWarning("[Stage3Bench] На корабле нет ShipShadowBudget — P2 закрывается только LOD-порогами.");
                return;
            }

            var candidates = new List<MeshRenderer>(512);
            ShipShadowBudget.CollectCandidates(_ship.gameObject, _lodGroup, budget.SmallPartMaxAxis, candidates);

            var byBucket = new int[9];
            int slots = 0, casterSlots = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                var r = candidates[i];
                if (r == null) continue;
                int s = r.sharedMaterials.Length;
                slots += s;
                if (r.shadowCastingMode != ShadowCastingMode.Off) casterSlots += s;
                Vector3 sz = r.bounds.size;
                float axis = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
                byBucket[Mathf.Clamp(Mathf.CeilToInt(axis), 0, 8)] += s;
            }

            Append("SHADOWBUDGET;present=yes;enabled=" + budget.BudgetEnabled +
                   ";on_m=" + Fmt(budget.ShadowOnDistance) + ";off_m=" + Fmt(budget.ShadowOffDistance) +
                   ";maxAxis_m=" + Fmt(budget.SmallPartMaxAxis) +
                   ";candidates=" + candidates.Count + ";candidateSlots=" + slots +
                   ";casterSlotsNow=" + casterSlots +
                   ";trackedRuntime=" + budget.TrackedRendererCount +
                   ";bucketSlots_ceilm=" + string.Join("|", Array.ConvertAll(byBucket, v => v.ToString())));
        }

        /// <summary>Сырые пороги LODGroup и дистанции перехода при FOV 60 без lodBias (граница выбора = дистанция × lodBias).</summary>
        private static void AppendLodInfo()
        {
            if (_lodGroup == null) return;
            var lods = _lodGroup.GetLODs();
            var ls = _lodGroup.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
            float worldSize = _lodGroup.size * maxScale;
            double tan = Math.Tan(BenchFov * 0.5f * Mathf.Deg2Rad);
            var sb = new StringBuilder();
            sb.Append("LODINFO;bias=").Append(Fmt(QualitySettings.lodBias));
            sb.Append(";size=").Append(Fmt(_lodGroup.size)).Append(";worldSize=").Append(Fmt(worldSize));
            for (int i = 0; i < lods.Length; i++)
            {
                sb.Append(";LOD").Append(i).Append("_h=").Append(lods[i].screenRelativeTransitionHeight.ToString("F6", CultureInfo.InvariantCulture));
                sb.Append("_r=").Append(lods[i].renderers.Length);
                if (lods[i].screenRelativeTransitionHeight > 0f)
                    sb.Append("_distFov60=").Append((worldSize / (2.0 * tan * lods[i].screenRelativeTransitionHeight)).ToString("F1", CultureInfo.InvariantCulture));
                else
                    sb.Append("_distFov60=none");
            }
            Append(sb.ToString());
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
                Debug.LogWarning("[Stage3Bench] DayNightController не найден — фаза суток не форсирована");
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

            var sunField = type.GetField("sunLight", BindingFlags.Instance | BindingFlags.Public);
            var sun = sunField != null ? sunField.GetValue(dnc) as Light : null;
            Append("NOTE;phase_forced=" + hour.ToString("F1", CultureInfo.InvariantCulture) +
                   ";sun=" + (sun == null ? "none" : sun.name + "(enabled=" + sun.enabled + ",shadows=" + sun.shadows +
                              ",intensity=" + Fmt(sun.intensity) + ")"));
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

        // ============================================================ ранний зонд падения

        private static void StepEarlyProbe()
        {
            if (_probeFrame == 0)
            {
                _probeStartFrame = Time.frameCount;
                var rb0 = _ship.GetComponent<Rigidbody>();
                Append("PROBE;t=0;y=" + Fmt(_ship.position.y) + ";vy=" + Fmt(rb0 != null ? rb0.linearVelocity.y : 0f) +
                       ";mass=" + Fmt(rb0 != null ? rb0.mass : 0f) + ";kinematic=" + (rb0 != null && rb0.isKinematic) +
                       ";engineRunning=" + ShipField("_engineRunning") + ";frozenNoPilot=" + ShipField("_frozenByNoPilot"));
            }

            int elapsed = Time.frameCount - _probeStartFrame;
            if (elapsed < _probeFrame) return;   // ещё не пришёл следующий шаг
            {
                var rb = _ship.GetComponent<Rigidbody>();
                Append("PROBE;frame=" + elapsed + ";y=" + Fmt(_ship.position.y) +
                       ";vy=" + Fmt(rb != null ? rb.linearVelocity.y : 0f) +
                       ";speed=" + Fmt(rb != null ? rb.linearVelocity.magnitude : 0f) +
                       ";kinematic=" + (rb != null && rb.isKinematic) +
                       ";engineRunning=" + ShipField("_engineRunning") +
                       ";frozenNoPilot=" + ShipField("_frozenByNoPilot") +
                       ";contacts=" + GroundContacts() +
                       ";groundDist_m=" + Fmt(GroundDistance()) +
                       ";worldRoot=" + (_worldRoot != null ? Vec(_worldRoot.position) : "n/a"));
            }
            _probeFrame = elapsed + 10;

            if (elapsed >= 600)
            {
                if (_probeOnly)
                {
                    Append("BENCH_DONE;result=PROBE_ONLY;probeFrames=" + elapsed + ";sceneSaved=no;push=no");
                    Finish("PROBE_ONLY");
                    return;
                }
                // Корабль без пилота и с выключенным двигателем падает сам (ShipController.ApplyAntiGravity: return).
                // Для замеров LOD/DC/дверей фиксируем его runtime-только: isKinematic = true, velocity = 0.
                // Сцена не сохраняется; после выхода из Play Mode состояние вернётся с диска.
                var rbHold = _ship.GetComponent<Rigidbody>();
                if (rbHold != null)
                {
                    rbHold.linearVelocity = Vector3.zero;
                    rbHold.angularVelocity = Vector3.zero;
                    rbHold.isKinematic = true;
                    Append("NOTE;ship_hold=kinematic_at_frame=" + elapsed +
                           ";y=" + Fmt(_ship.position.y) + ";reason=ship_falls_without_pilot_or_engine");
                }
                _state = State.Warmup;
                _warmStartFrame = Time.frameCount;
                Debug.Log("[Stage3Bench] Ранний зонд завершён, дальше прогрев.");
            }
        }

        private static string ShipField(string name)
        {
            var sc = _ship != null ? _ship.GetComponent("ShipController") : null;
            if (sc == null) return "n/a";
            var f = sc.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f != null ? Convert.ToString(f.GetValue(sc), CultureInfo.InvariantCulture) : "n/a";
        }

        private static string ShipFlag(string propName)
        {
            var sc = _ship != null ? _ship.GetComponent("ShipController") : null;
            if (sc == null) return "n/a";
            var p = sc.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
            if (p != null) return Convert.ToString(p.GetValue(sc), CultureInfo.InvariantCulture);
            return "n/a";
        }

        /// <summary>Расстояние по вертикали от нижней точки корабля до ближайшего чужого коллайдера.</summary>
        private static float GroundDistance()
        {
            Bounds b = ShipBounds();
            Vector3 start = new Vector3(b.center.x, b.min.y + 0.5f, b.center.z);
            float best = -1f;
            var hits = Physics.RaycastAll(start, Vector3.down, 200000f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.transform.IsChildOf(_ship)) continue;
                if (best < 0f || hits[i].distance < best) best = hits[i].distance;
            }
            return best;
        }

        private static int GroundContacts()
        {
            Bounds b = ShipBounds();
            Vector3 center = new Vector3(b.center.x, b.min.y + 0.5f, b.center.z);
            Vector3 half = new Vector3(Mathf.Max(b.extents.x * 0.9f, 1f), 1.5f, Mathf.Max(b.extents.z * 0.9f, 1f));
            var hits = Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            int n = 0;
            for (int i = 0; i < hits.Length; i++)
                if (hits[i] != null && !hits[i].transform.IsChildOf(_ship)) n++;
            return n;
        }

        // ============================================================ стабильность

        private static void CacheShipRenderers()
        {
            _shipRenderers = _ship.GetComponentsInChildren<MeshRenderer>(true);
            _shipEnabledBefore.Clear();
            int merged = 0;
            foreach (var r in _shipRenderers) if (r.name.StartsWith(MergedPrefix, StringComparison.Ordinal)) merged++;
            Append("NOTE;ship_meshrenderers=" + _shipRenderers.Length + ";merged_renderers=" + merged +
                   ";doorControllers=" + _ship.GetComponentsInChildren<DoorController>(true).Length +
                   ";boxColliders=" + _ship.GetComponentsInChildren<BoxCollider>(true).Length +
                   ";moduleSlots=" + _ship.GetComponentsInChildren<ModuleSlot>(true).Length);
        }

        private static void FinishStability()
        {
            var rb = _ship.GetComponent<Rigidbody>();
            Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
            float drift = Vector3.Distance(_ship.position, _stabPos0);
            var ls = _ship.lossyScale;
            Append("CASE;id=stability;frames=120;drift_m=" + Fmt(drift) + ";vel_now=" + Vec(vel) +
                   ";vel0=" + Vec(_stabVel0) + ";y=" + Fmt(_ship.position.y) + ";lossyScale=" + Vec(ls) +
                   ";lodGroupSize=" + Fmt(_lodGroup != null ? _lodGroup.size : 0f) +
                   ";lodLevels=" + (_lodGroup != null ? _lodGroup.lodCount : 0));
            Debug.Log("[Stage3Bench] Стабильность: drift=" + Fmt(drift) + " м за 120 кадров, vel=" + Vec(vel));
        }

        // ============================================================ дистанционные точки

        private static void BeginCase(int index)
        {
            _caseIndex = index;
            if (_caseIndex >= Cases.Count)
            {
                _state = State.Doors;
                _stepFrame = Time.frameCount;
                BeginDoors();
                return;
            }
            var c = Cases[_caseIndex];
            _dcA.Clear(); _spcA.Clear(); _trisA.Clear(); _scA.Clear();
            _dcB.Clear(); _spcB.Clear(); _trisB.Clear(); _scB.Clear();
            _censusDone = false; _shipDisabled = false; _fdDone = true;   // Frame Debugger снимается отдельно через MCP (встроенный reflection-путь в 6000.5 отдаёт count=0)
            _settleUntilFrame = Time.frameCount + SettleFrames;
            _nextSampleFrame = _settleUntilFrame;
            _caseFrameStart = Time.frameCount;
            _caseTimeStart = Now;
            ApplyPose(c);
            Debug.Log("[Stage3Bench] Точка " + c.id + " (дистанция " + c.distance + " м от центра корабля)");
        }

        private static void ApplyPose(DistanceCase c)
        {
            if (_cam == null || _ship == null) return;
            Bounds b = ShipBounds();
            Vector3 dir = new Vector3(1f, 0.35f, 1f).normalized;
            Vector3 pos = b.center + dir * c.distance;
            _cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(b.center - pos, Vector3.up));
        }

        private static Bounds ShipBounds()
        {
            if (_shipRenderers == null) _shipRenderers = _ship.GetComponentsInChildren<MeshRenderer>(true);
            Bounds b = default; bool has = false;
            for (int i = 0; i < _shipRenderers.Length; i++)
            {
                var r = _shipRenderers[i];
                if (r == null || !r.enabled) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            if (!has) b = new Bounds(_ship.position, Vector3.one * 100f);
            return b;
        }

        private static void StepCase()
        {
            if (_cam == null || _ship == null || _lodGroup == null)
            {
                Append("NOTE;error=session_lost;case=" + _caseIndex);
                Finish("SESSION_LOST");
                return;
            }

            var c = Cases[_caseIndex];
            ApplyPose(c);

            if (Time.frameCount < _settleUntilFrame) return;

            if (!_censusDone)
            {
                RunCensus(c);
                _censusDone = true;
                _nextSampleFrame = Time.frameCount;
            }

            if (!_fdDone && !_shipDisabled && _fdPhase == 0) BeginFdCapture(c);
            if (StepFdCapture(c)) return;

            if (Time.frameCount < _nextSampleFrame) return;

            if (!_shipDisabled)
            {
                if (!_fdDone && _dcA.Count >= SampleCount) BeginFdCapture(c);

                _dcA.Add(UnityStats.drawCalls); _spcA.Add(UnityStats.setPassCalls);
                _trisA.Add(UnityStats.triangles); _scA.Add(UnityStats.shadowCasters);
                if (_dcA.Count == 1 && UnityStats.drawCalls == 0)
                    Append("NOTE;warning=drawCalls_0_at_first_sample;screenRes=" + UnityStats.screenRes);

                if (_dcA.Count >= SampleCount)
                {
                    SetShipRenderers(false);
                    _shipDisabled = true;
                    _settleUntilFrame = Time.frameCount + OffSettleFrames;
                    _nextSampleFrame = _settleUntilFrame;
                    return;
                }
                _nextSampleFrame = Time.frameCount + SampleGapFrames;
                return;
            }

            _dcB.Add(UnityStats.drawCalls); _spcB.Add(UnityStats.setPassCalls);
            _trisB.Add(UnityStats.triangles); _scB.Add(UnityStats.shadowCasters);
            if (_dcB.Count < SampleCount)
            {
                _nextSampleFrame = Time.frameCount + SampleGapFrames;
                return;
            }

            SetShipRenderers(true);
            _shipDisabled = false;
            WriteCaseLine(c);
            BeginCase(_caseIndex + 1);
        }

        private static void SetShipRenderers(bool on)
        {
            if (_shipRenderers == null) return;
            if (on)
            {
                for (int i = 0; i < _shipRenderers.Length; i++)
                {
                    var r = _shipRenderers[i];
                    if (r == null) continue;
                    r.enabled = i < _shipEnabledBefore.Count ? _shipEnabledBefore[i] : r.enabled;
                }
                _shipEnabledBefore.Clear();
            }
            else
            {
                _shipEnabledBefore.Clear();
                for (int i = 0; i < _shipRenderers.Length; i++)
                {
                    var r = _shipRenderers[i];
                    if (r == null) { _shipEnabledBefore.Add(false); continue; }
                    _shipEnabledBefore.Add(r.enabled);
                    r.enabled = false;
                }
            }
        }

        private static void RunCensus(DistanceCase c)
        {
            if (_cam == null) return;
            GeometryUtility.CalculateFrustumPlanes(_cam, _planes);
            Vector3 camPos = _cam.transform.position;
            int visible = 0, enabledCount = 0, slotsVisible = 0, inFrustum = 0, shadowVisible = 0;
            int lod0 = 0, lod1 = 0, lod2 = 0, dynCount = 0;
            var lods = _lodGroup != null ? _lodGroup.GetLODs() : new LOD[0];
            var lodIndex = new Dictionary<Renderer, int>();
            for (int i = 0; i < lods.Length; i++)
                foreach (var r in lods[i].renderers) if (r != null && !lodIndex.ContainsKey(r)) lodIndex[r] = i;

            for (int i = 0; i < _shipRenderers.Length; i++)
            {
                var r = _shipRenderers[i];
                if (r == null) continue;
                if (r.enabled) enabledCount++;
                bool frustum = GeometryUtility.TestPlanesAABB(_planes, r.bounds);
                if (frustum) inFrustum++;
                if (r.isVisible)
                {
                    visible++;
                    slotsVisible += r.sharedMaterials.Length;
                    if (r.shadowCastingMode != ShadowCastingMode.Off) shadowVisible++;
                    int li;
                    if (lodIndex.TryGetValue(r, out li))
                    {
                        if (li == 0) lod0++; else if (li == 1) lod1++; else lod2++;
                    }
                    else dynCount++;
                }
            }

            Append("CENSUS;id=" + c.id + ";enabled=" + enabledCount + ";inFrustum=" + inFrustum +
                   ";isVisible=" + visible + ";slotsVisible=" + slotsVisible + ";shadowVisible=" + shadowVisible +
                   ";vis_LOD0=" + lod0 + ";vis_LOD1=" + lod1 + ";vis_LOD2=" + lod2 + ";vis_dynamic=" + dynCount +
                   ";camPos=" + Vec(camPos) + ";camDist=" + Fmt(Vector3.Distance(camPos, ShipBounds().center)));

            if (inFrustum == 0)
            {
                Append("NOTE;error=census_zero;id=" + c.id + ";camPos=" + Vec(camPos));
                Debug.LogError("[Stage3Bench] В кадре 0 рендереров корабля — поза камеры неверна.");
                Finish("CENSUS_ZERO");
            }
        }

        private static void WriteCaseLine(DistanceCase c)
        {
            int dcA = Median(_dcA), dcB = Median(_dcB);
            int spcA = Median(_spcA), spcB = Median(_spcB);
            int trisA = Median(_trisA), trisB = Median(_trisB);
            int scA = Median(_scA), scB = Median(_scB);

            var sb = new StringBuilder();
            sb.Append("CASE;id=").Append(c.id);
            sb.Append(";distance_m=").Append(Fmt(c.distance));
            sb.Append(";lodBias=").Append(Fmt(QualitySettings.lodBias));
            sb.Append(";dc_all=").Append(dcA);
            sb.Append(";dc_withoutShip=").Append(dcB);
            sb.Append(";ship_dc=").Append(dcA - dcB);
            sb.Append(";spc_all=").Append(spcA).Append(";ship_spc=").Append(spcA - spcB);
            sb.Append(";tris_all=").Append(trisA).Append(";ship_tris=").Append(trisA - trisB);
            sb.Append(";shadowCasters_all=").Append(scA).Append(";ship_shadowCasters=").Append(scA - scB);
            sb.Append(";rawDc=").Append(string.Join("|", _dcA.ConvertAll(v => v.ToString()).ToArray()));
            sb.Append(";rawDcOff=").Append(string.Join("|", _dcB.ConvertAll(v => v.ToString()).ToArray()));
            Append(sb.ToString());

            double seconds = Math.Max(0.0001, Now - _caseTimeStart);
            int frames = Math.Max(1, Time.frameCount - _caseFrameStart);
            Debug.Log("[Stage3Bench] " + c.id + ": ship_dc=" + (dcA - dcB) + " (all " + dcA + ", без корабля " + dcB +
                      "), ship_spc=" + (spcA - spcB) + ", ship_tris=" + (trisA - trisB) +
                      ", fps≈" + Fmt((float)(frames / seconds)));
        }

        private static void BeginFdCapture(DistanceCase c)
        {
            var fdu = typeof(UnityEditor.Editor).Assembly
                .GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility");
            if (fdu == null) { Append("FD;id=" + c.id + ";status=api_not_found"); _fdDone = true; return; }

            _fdType = fdu;
            try
            {
                var limitProp = fdu.GetProperty("limit", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (limitProp != null && limitProp.CanWrite) limitProp.SetValue(null, 100000);
                var setEnabled = fdu.GetMethod("SetEnabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                setEnabled.Invoke(null, new object[] { true, 0 });
                _fdPhase = 1;
                _fdFrame = 0;
            }
            catch (Exception e)
            {
                Append("FD;id=" + c.id + ";status=exception;" + e.GetType().Name + ":" + e.Message);
                _fdDone = true;
            }
        }

        /// <summary>Не блокирующий шаг Frame Debugger: кадр должен успеть отрисоваться.</summary>
        private static bool StepFdCapture(DistanceCase c)
        {
            if (_fdPhase != 1) return false;
            _fdFrame++;

            int count = 0;
            try
            {
                var countProp = _fdType.GetProperty("count", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (countProp != null) count = (int)countProp.GetValue(null);
            }
            catch { }

            if ((_fdFrame < 6 || count <= 0) && _fdFrame < 120) return true;   // ждём кадр

            try
            {
                var limitProp = _fdType.GetProperty("limit", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                int limit = limitProp != null ? (int)limitProp.GetValue(null) : -1;
                var nameMethod = _fdType.GetMethod("GetFrameEventInfoName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                int merged = 0, shipLike = 0, clear = 0;
                var firstMerged = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    string n = null;
                    try { n = nameMethod.Invoke(null, new object[] { i }) as string; } catch { }
                    if (string.IsNullOrEmpty(n)) continue;
                    if (n.StartsWith("Clear", StringComparison.OrdinalIgnoreCase)) clear++;
                    if (n.IndexOf(MergedPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        merged++;
                        if (firstMerged.Count < 6) firstMerged.Add(n);
                    }
                    if (n.IndexOf("HeavyII", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Ship_havyII", StringComparison.OrdinalIgnoreCase) >= 0) shipLike++;
                }

                Append("FD;id=" + c.id + ";events=" + count + ";limit=" + limit +
                       ";truncated=" + (limit > 0 && count >= limit) +
                       ";clearEvents=" + clear + ";events_merged=" + merged + ";events_heavy=" + shipLike +
                       ";first_merged=" + string.Join(" ‖ ", firstMerged.ToArray()));
                Debug.Log("[Stage3Bench] Frame Debugger: events=" + count + " merged=" + merged +
                          " limit=" + limit + " (кадров ожидания " + _fdFrame + ")");

                var setEnabled = _fdType.GetMethod("SetEnabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                setEnabled.Invoke(null, new object[] { false, 0 });
            }
            catch (Exception e)
            {
                Append("FD;id=" + c.id + ";status=exception;" + e.GetType().Name + ":" + e.Message);
                try
                {
                    var off = _fdType.GetMethod("SetEnabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    off.Invoke(null, new object[] { false, 0 });
                }
                catch { }
            }

            _fdPhase = 2;
            _fdDone = true;
            return true;
        }


        // ============================================================ двери / интерактив

        private static void BeginDoors()
        {
            var doorList = _ship.GetComponentsInChildren<DoorController>(true);
            int active = 0;
            DoorController picked = null;
            for (int i = 0; i < doorList.Length; i++)
            {
                if (!doorList[i].isActiveAndEnabled) continue;
                active++;
                if (picked == null) picked = doorList[i];
                if (doorList[i].name.IndexOf("Mess", StringComparison.OrdinalIgnoreCase) >= 0) { picked = doorList[i]; break; }
            }
            _testDoor = picked;
            _doorOpened = false;

            if (_testDoor != null)
            {
                _testDoorPanel = _testDoor.transform;
                _doorPanelLocal0 = _testDoorPanel.localPosition;
                _doorPanelRot0 = _testDoorPanel.localRotation;
                _doorPanelCenter0 = DoorLocalCenter(_testDoor.transform);
            }
            Append("DOORS;count=" + doorList.Length + ";active=" + active +
                   ";testDoor=" + (_testDoor != null ? _testDoor.name : "none") +
                   ";testDoorActive=" + (_testDoor != null && _testDoor.isActiveAndEnabled) +
                   ";colliders=" + _ship.GetComponentsInChildren<BoxCollider>(true).Length +
                   ";moduleSlots=" + _ship.GetComponentsInChildren<ModuleSlot>(true).Length +
                   ";pilotSeat=" + (_ship.GetComponentInChildren<PilotSeatController>(true) != null) +
                   ";cargoVisual=" + (_ship.GetComponentInChildren<ProjectC.Ship.Cargo.ShipCargoVisual>(true) != null) +
                   ";contrailVfx=" + (_ship.GetComponentInChildren<ShipContrailVfx>(true) != null) +
                   ";engineThrusterVisual=" + _ship.GetComponentsInChildren<ProjectC.Ship.Engine.EngineThrusterVisual>(true).Length +
                   ";contrailUseShipBounds=" + (_ship.GetComponentInChildren<ShipContrailVfx>(true) != null &&
                                               _ship.GetComponentInChildren<ShipContrailVfx>(true).UseShipBounds));

            if (_testDoor != null) _testDoor.Open();
        }

        /// <summary>Центр поддерева двери в ЛОКАЛЬНОМ пространстве корабля — падение корабля не искажает замер.</summary>
        private static Vector3 DoorLocalCenter(Transform t)
        {
            Bounds world = DoorBounds(t);
            return _ship.InverseTransformPoint(world.center);
        }

        private static void StepDoors()
        {
            if (_testDoor == null)
            {
                _state = State.Rebase; _rebasePhase = 0; _stepFrame = Time.frameCount;
                return;
            }

            int elapsed = Time.frameCount - _stepFrame;

            if (!_doorOpened)
            {
                if (elapsed < 90) return;
                _doorOpened = true;
                Vector3 localNow = _ship.InverseTransformPoint(DoorBounds(_testDoor.transform).center);
                Append("DOORS;test=open;isOpen=" + _testDoor.IsOpen + ";progress=" + Fmt(_testDoor.OpenProgress) +
                       ";animating=" + _testDoor.IsAnimating +
                       ";localMove_m=" + Fmt(Vector3.Distance(localNow, _doorPanelCenter0)) +
                       ";selfRotated_deg=" + Fmt(Quaternion.Angle(_testDoorPanel.localRotation, _doorPanelRot0)));
                _testDoor.Close();
                _stepFrame = Time.frameCount;
                return;
            }

            if (elapsed >= 120 && _testDoor.OpenProgress <= 0.01f)
            {
                Vector3 localBack = _ship.InverseTransformPoint(DoorBounds(_testDoor.transform).center);
                Append("DOORS;test=close;progress=" + Fmt(_testDoor.OpenProgress) +
                       ";localBack_m=" + Fmt(Vector3.Distance(localBack, _doorPanelCenter0)));
                _state = State.Rebase;
                _rebasePhase = 0;
                _stepFrame = Time.frameCount;
            }
        }

        private static Bounds DoorBounds(Transform t)
        {
            Bounds b = new Bounds(t.position, Vector3.zero);
            bool has = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // ============================================================ F8 / F9

        private static void StepRebase()
        {
            if (_rebaseSlice == null) _rebaseSlice = FindByTypeName("GlobalMotionControlledRebaseSlice") as MonoBehaviour;
            if (_rebaseSlice == null)
            {
                Append("REBASE;status=slice_not_found");
                _state = State.Done;
                Finish("OK");
                return;
            }

            var type = _rebaseSlice.GetType();
            var activeField = type.GetField("_transactionActive", BindingFlags.Instance | BindingFlags.NonPublic);
            bool active = activeField != null && (bool)activeField.GetValue(_rebaseSlice);

            switch (_rebasePhase)
            {
                case 0:
                    _rebaseRootBefore = _worldRoot != null ? _worldRoot.position : Vector3.zero;
                    _rebaseShipBefore = _ship.position;
                    Append("REBASE;phase=before;worldRoot=" + Vec(_rebaseRootBefore) + ";ship=" + Vec(_rebaseShipBefore) +
                           ";transactionActive=" + active + ";autoRebaseCooldownNote=35s");
                    type.GetMethod("RequestControlledRebase", Type.EmptyTypes).Invoke(_rebaseSlice, null);
                    _rebasePhase = 1; _stepFrame = Time.frameCount; _rebaseSawStart = false;
                    Debug.Log("[Stage3Bench] F8-путь: RequestControlledRebase() (success)");
                    break;

                case 1:
                    if (active) _rebaseSawStart = true;
                    if ((!active && _rebaseSawStart) || Time.frameCount - _stepFrame > 600)
                    {
                        Vector3 rootAfter = _worldRoot != null ? _worldRoot.position : Vector3.zero;
                        Vector3 shipAfter = _ship.position;
                        float shipShift = Vector3.Distance(shipAfter, _rebaseShipBefore);
                        float rootShift = Vector3.Distance(rootAfter, _rebaseRootBefore);
                        Append("REBASE;phase=success;transactionsSeen=" + _rebaseSawStart +
                               ";worldRootShift_m=" + Fmt(rootShift) + ";shipShift_m=" + Fmt(shipShift) +
                               ";worldRootAfter=" + Vec(rootAfter) + ";shipAfter=" + Vec(shipAfter) +
                               ";frames=" + (Time.frameCount - _stepFrame) + ";errorsSoFar=" + _errorCount);
                        _rebasePhase = 2; _stepFrame = Time.frameCount; _rebaseShipBefore = shipAfter;
                        _rebaseRootBefore = rootAfter;
                        type.GetMethod("RequestControlledRebaseWithRollback", Type.EmptyTypes).Invoke(_rebaseSlice, null);
                        Debug.Log("[Stage3Bench] F9-путь: RequestControlledRebaseWithRollback()");
                    }
                    break;

                case 2:
                    if (active) _rebaseSawStart = true;
                    if ((!active && _rebaseSawStart) || Time.frameCount - _stepFrame > 600)
                    {
                        Vector3 rootAfter = _worldRoot != null ? _worldRoot.position : Vector3.zero;
                        Vector3 shipAfter = _ship.position;
                        Append("REBASE;phase=rollback;worldRootShift_m=" + Fmt(Vector3.Distance(rootAfter, _rebaseRootBefore)) +
                               ";shipShift_m=" + Fmt(Vector3.Distance(shipAfter, _rebaseShipBefore)) +
                               ";worldRootAfter=" + Vec(rootAfter) + ";shipAfter=" + Vec(shipAfter) +
                               ";frames=" + (Time.frameCount - _stepFrame) + ";errorsSoFar=" + _errorCount);
                        _rebasePhase = 3;
                        _state = State.Done;
                        Finish("OK");
                    }
                    break;
            }
        }

        // ============================================================ финиш

        private static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SessionState.SetBool(SessionKey, false);
            Application.logMessageReceived -= OnLog;

            // вернуть рендереры корабля, если прогон оборвался между A и B
            if (_shipRenderers != null && _shipDisabled) SetShipRenderers(true);

            Append("BENCH_DONE;result=" + result + ";casesCompleted=" + _caseIndex + "/" + Cases.Count +
                   ";errors=" + _errorCount + ";sceneSaved=no;push=no");
            if (_firstErrors.Count > 0) Append("NOTE;first_errors=" + string.Join(" ‖ ", _firstErrors.ToArray()));

            _state = State.Off;
            Debug.Log("[Stage3Bench] Завершено: " + result + ". Файл: " + _outPath);

            if (EditorApplication.isPlaying)
                Debug.Log("[Stage3Bench] Play Mode оставлен запущенным (останавливает пользователь); сцена не сохраняется.");
        }

        private static int Median(List<int> values)
        {
            if (values == null || values.Count == 0) return 0;
            var copy = new List<int>(values);
            copy.Sort();
            return copy[copy.Count / 2];
        }

        private static string Fmt(float v)
        {
            return v.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string Vec(Vector3 v)
        {
            return Fmt(v.x) + "," + Fmt(v.y) + "," + Fmt(v.z);
        }

        private static void Append(string line)
        {
            if (string.IsNullOrEmpty(_outPath)) return;
            try { File.AppendAllText(_outPath, line + Environment.NewLine, Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[Stage3Bench] Не удалось записать строку: " + e.Message); }
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
                Debug.LogError("[Stage3Bench] Слишком много доменных перезагрузок подряд — прогон отменён.");
                return;
            }

            _outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RelOutPath));
            _state = State.WaitPlayMode;
            _stateSince = Now;
            _hostRequested = false;
            _menuHidden = false;
            _cam = null; _worldRoot = null; _ship = null; _lodGroup = null; _shipRenderers = null;
            _caseIndex = 0;
            Append("NOTE;domain_reload=true;restart=" + reloads);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[Stage3Bench] Доменная перезагрузка (#" + reloads + ") — прогон продолжен из SessionState.");
        }
    }

    [InitializeOnLoad]
    internal static class Stage3ShipBenchBootstrap
    {
        static Stage3ShipBenchBootstrap()
        {
            Stage3ShipBench.ResumeIfRunning();
        }
    }
}
