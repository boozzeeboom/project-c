// Project C — T-LIGHT16 (L1 светового трека): BASELINE-замер пула источников PRIMUM + ALIC.
//
// Что мерит (ничего не оптимизирует и не меняет поведение света):
//   1. Реальная игра (Play Mode → Host через ProjectCMainMenuUiHost.OnMenuAction("host")).
//   2. Точки P1 (площадь), STREET (улица), ADMIN (интерьер), FARM (кластер ферм) при
//      времени суток 12 / 18 / 20 ч.
//   3. Счётчики PrimumLightPool: registered, activeTotal, OUT/IN/FARM, кандидаты, cap
//      (binding + сколько отсеяно), дистанционные buckets, активные с intensity ≈ 0,
//      длительность Check и FindFocusCamera, источник фокуса, SetVersion.
//   4. Счётчики AdditionalLightIntensityController: tracked, applied, activeApplied,
//      zeroIntensity, ApplyCallsThisFrame, ApplyIntensity ms.
//   5. Профиллеры: PRIMUM/LightPool.Check, PRIMUM/LightPool.FindFocusCamera, ALIC.ApplyIntensity.
//   6. Покадровые UnityStats (DC / SetPass / tris / кадр) + Camera.visibleLights.
//   7. Разворот камеры на 180°: за сколько кадров SetVersion пула изменил набор.
//   8. F8/F9 реальным вводом: 0 ошибок, лампы на месте после ребейза.
//
// Что НЕ делает: не сохраняет сцену, не пишет в префабы/FBX/ProjectSettings/URP-ассет,
// не меняет Light.enabled / Light.intensity / focus / maxActive / activeRadius / LOD,
// не делает push. Все правки — runtime (поза камеры, far/fov/near, компоненты-двигатели
// камеры) и возвращаются в Cleanup.
//
// Меню: Project C → Lighting → T-LIGHT16 Baseline → Run / Abort.
//
// T-LIGHT17 (L2 светового трека) — тот же прогон в режиме A/B (меню Project C → Lighting →
// T-LIGHT17 L2 → Run): те же точки/часы и тот же камерный риг, но каждый кейс несёт конфиг
// пула (dayGate / split OUT-IN / радиус) и переключает его через PrimumLightPool.ApplyConfig
// в памяти. Сцена не сохраняется: A/B живёт только в рантайме.
//   ctrl140  — L1-поведение (gate off, split off, radius 140) — контроль;
//   gate140  — только day/night-гейт;
//   split140 — только резерв бюджета OUT/IN;
//   l2_100   — gate + split + радиус 100 м;
//   l2_70    — gate + split + радиус 70 м.
// В режиме L2 при смене часа успокоение длиннее (200 кадров): ALIC сглаживает intensity с
// transitionSpeed=4, и на 40 кадрах часть ламп ещё не дошла до нуля — гейт измерился бы ложно.
//
// T-LIGHT17b (L2b светового трека) — режим ACTIVE-SET A/B (меню Project C → Lighting →
// T-LIGHT17b ALIC → Run): тот же риг/точки/часы, но пул зафиксирован в принятом L2-конфиге
// (gate on, split on, radius 100), а ALIC переключается между двумя режимами в памяти
// через AdditionalLightIntensityController.ApplyActiveSetConfig:
//   alicAll    — поведение до T-LIGHT17b (запись всем 2 238 каждый кадр) — контроль;
//   alicActive — запись только активным/изменённым (активный набор пула).
// Сцена не сохраняется: оба режима живут только в рантайме.
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
using UnityEngine.SceneManagement;
using ProjectC.Core;
using ProjectC.World;
using ProjectC.World.FloatingOrigin;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools
{
    /// <summary>T-LIGHT16: baseline-инструментация светового пула PRIMUM; T-LIGHT17 — режим A/B конфига L2.</summary>
    public static class TLight16BaselineBench
    {
        private const string RunMenu = "Project C/Lighting/T-LIGHT16 Baseline/Run";
        private const string AbortMenu = "Project C/Lighting/T-LIGHT16 Baseline/Abort";
        private const string RunMenuL2 = "Project C/Lighting/T-LIGHT17 L2/Run";
        private const string AbortMenuL2 = "Project C/Lighting/T-LIGHT17 L2/Abort";
        private const string RunMenuL2B = "Project C/Lighting/T-LIGHT17b ALIC/Run";
        private const string AbortMenuL2B = "Project C/Lighting/T-LIGHT17b ALIC/Abort";

        private enum RunMode { Baseline, L2, L2B }

        /// <summary>Префикс логов/ошибок: в режимах L2/L2b не выдаём их за L1.</summary>
        private static string Tag =>
            _mode == RunMode.L2B ? "[T-LIGHT17b]" : _mode == RunMode.L2 ? "[T-LIGHT17]" : "[T-LIGHT16]";

        private const string CsvDir = "docs/world/lights";
        private const string DateTag = "2026-11-01";
        private const string RawDir = "Temp/T-LIGHT16";

        private const float BenchFar = 30000f;
        private const float BenchFov = 60f;

        private const int WarmupFrames = 1500;
        private const int SettleFrames = 40;
        /// <summary>L2: успокоение после смены часа (ALIC сглаживает intensity с transitionSpeed=4).</summary>
        private const int SettleFramesHourSwitch = 200;
        private const int SampleGapFrames = 8;
        private const int SampleCount = 3;
        private const int TurnTimeoutFrames = 240;

        private const double HostTimeoutSeconds = 120.0;
        private const double RigTimeoutSeconds = 300.0;
        private const double WarmupTimeoutSeconds = 900.0;

        private const string WorldRootName = "WorldRoot_0_0";
        private const string AdminNodeName = "PRIMUM_ADMIN_S0_ADM_P1_01H";
        private const int RebaseTimeoutFrames = 360;
        private const int MinExpectedLights = 2000;

        private static readonly Vector3 AuthoredWorldOffset = new Vector3(39936.0f, 2560.03f, 39936.0f);

        private enum State { Off, WaitPlayMode, WaitHost, WaitRig, Warmup, Point, Rebase, Cleanup }

        private class LightCase
        {
            public string id;
            public string point;               // P1 / STREET / ADMIN / FARM
            public string poseKind;            // local / admin / farm
            public Vector3 local;
            public float yaw;
            public float pitch;
            public float hour;
            public bool turn;                  // разворот 180° с замером задержки
            public string note = string.Empty;

            // T-LIGHT17: конфиг пула, который применяется перед кейсом (A/B).
            public string cfgTag = "ctrl140";
            public bool cfgDayGate;
            public bool cfgSplit;
            public int cfgOutMin = 40;
            public bool cfgUseL2;
            public float cfgRadius = 140f;

            // T-LIGHT17b: режим ALIC в кейсе (true = активный набор, false = контроль «всем»).
            public bool cfgAlicActiveSet = true;
        }

        // ------------------------------------------------------------------ состояние

        private static State _state = State.Off;
        private static RunMode _mode = RunMode.Baseline;
        private static float _lastHour = 12f;
        private static List<LightCase> _cases = new List<LightCase>();
        private static int _caseIndex;

        private static Camera _cam;
        private static Transform _worldRoot;
        private static PrimumLightPool _pool;
        private static AdditionalLightIntensityController _alic;
        private static MonoBehaviour _dayNight;
        private static bool _dayNightWasEnabled;

        private static readonly List<Light> _lightScratch = new List<Light>(4096);

        private static readonly List<int> _dc = new List<int>();
        private static readonly List<int> _spc = new List<int>();
        private static readonly List<int> _tris = new List<int>();
        private static readonly List<double> _ms = new List<double>();

        private static Unity.Profiling.ProfilerRecorder _checkRecorder;
        private static Unity.Profiling.ProfilerRecorder _focusRecorder;
        private static Unity.Profiling.ProfilerRecorder _alicRecorder;
        private static readonly List<double> _checkMsFrames = new List<double>();
        private static readonly List<double> _focusMsFrames = new List<double>();
        private static readonly List<double> _alicMsFrames = new List<double>();
        // T-LIGHT17b: покадровые счётчики записи ALIC (средние по окну кейса).
        private static readonly List<double> _alicWritesFrames = new List<double>();
        private static readonly List<double> _alicActiveWritesFrames = new List<double>();
        private static readonly List<double> _alicIteratedFrames = new List<double>();
        private static readonly List<double> _alicBaseSweepFrames = new List<double>();
        private static int _lastProfiledFrame = -1;

        // снимок census текущего кейса
        private static int _cActiveTotal;
        private static int _cActiveOut;
        private static int _cActiveIn;
        private static int _cActiveFarm;
        private static int _cActiveOther;
        private static int _cActiveAdmin;
        private static int _cAheadActive;
        private static int _cBehindActive;
        private static int _cInFrustumActive;
        private static int _cCandidate;
        private static int _cCapCut;
        private static bool _cCapSaturated;
        private static int _cCap;
        private static int _cBucket0, _cBucket1, _cBucket2, _cBucket3, _cBucket4;
        private static int _cIntensityZero;
        private static double _cPoolCheckMs;
        private static double _cPoolCheckMaxMs;
        private static double _cFocusSearchMs;
        private static int _cFocusSearches;
        private static int _cFocusCameraPool;
        private static string _cFocusName = "?";
        private static string _cFocusKind = "none";
        private static int _cRegistered;
        private static int _cVisibleLights = -1;
        private static int _cAlicTracked;
        private static int _cAlicApplied;
        private static int _cAlicActiveApplied;
        private static int _cAlicZero;
        private static int _cAlicCallsPerFrame;
        private static float _cAlicHour = -1f;
        private static bool _cAlicFlicker;
        private static int _cSetVersion;
        private static int _cCheckCount;
        // T-LIGHT17
        private static int _cDayGateDropped;
        private static int _cQuotaDeferred;
        private static int _cQuotaFilled;
        private static int _cOutMinUsed;
        // T-LIGHT17b
        private static bool _cAlicActiveSet;
        private static int _cAlicSetSize;
        private static int _cAlicWrites;
        private static int _cAlicActiveWrites;
        private static int _cAlicIterated;
        private static int _cAlicBaseSweep;
        private static int _cAlicResyncs;
        private static bool _visibleLightsNoteWritten;

        // реакция на разворот
        private static bool _turnStarted;
        private static int _turnFrame = -1;
        private static int _turnVersionBefore;
        private static int _turnReactionFrames = -1;
        private static bool _turnTimeout;

        private static Vector3 _targetPos;
        private static Quaternion _targetRot;
        private static int _settleUntilFrame;
        private static int _nextSampleFrame;
        private static bool _censusDone;
        private static double _stateSince;
        private static double _caseWindowStart;
        private static int _caseWindowFrame;
        private static int _warmStartFrame;
        private static bool _hostRequested;
        private static bool _menuHidden;
        private static string _outRawPath;
        private static string _outCsvPath;
        private static string _csvDirFull;
        private static string _runTag = string.Empty;

        // снимки для восстановления
        private static Vector3 _camPosBefore;
        private static Quaternion _camRotBefore;
        private static float _camFarBefore;
        private static float _camFovBefore;
        private static float _camNearBefore;
        private static readonly List<MonoBehaviour> _rigBehavioursDisabled = new List<MonoBehaviour>();

        // F8/F9
        private static int _rebasePhase;
        private static int _rebaseStepFrame;
        private static int _keyReleaseFrame = -1;
        private static int _pressAttempts;
        private static ulong _frameGenBefore;
        private static Vector3 _cumulativeBefore;
        private static Vector3 _rootBefore;
        private static Vector3 _playerBefore;
        private static float _predictedShift;
        private static MonoBehaviour _rebaseSlice;
        private static Transform _playerRoot;
        private static bool _f8Pass;
        private static bool _f9Pass;
        private static int _rollbackSeen;
        private static int _rollbackRestoreTrue;
        private static readonly List<string> _rebaseLines = new List<string>();

        // консоль
        private static readonly List<string> _errorLines = new List<string>();
        private static int _errorCount;
        private static int _exceptionCount;
        private static int _shaderErrorCount;
        private static int _warnCount;
        private static bool _logHooked;
        private static bool _dirtyAtStart;

        private static readonly Plane[] _planes = new Plane[6];
        private static readonly FrameTiming[] _timings = new FrameTiming[1];

        private static double Now { get { return EditorApplication.timeSinceStartup; } }

        // --------------------------------------------------------------------- меню

        [MenuItem(RunMenu)]
        public static void Run()
        {
            StartRun(RunMode.Baseline);
        }

        [MenuItem(RunMenuL2)]
        public static void RunL2()
        {
            StartRun(RunMode.L2);
        }

        [MenuItem(RunMenuL2B)]
        public static void RunL2B()
        {
            StartRun(RunMode.L2B);
        }

        [MenuItem(AbortMenu)]
        [MenuItem(AbortMenuL2)]
        [MenuItem(AbortMenuL2B)]
        public static void Abort()
        {
            Finish("ABORTED_BY_USER", false);
        }

        private static void StartRun(RunMode mode)
        {
            _mode = mode;

            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(Tag + " Уже в Play Mode / вход выполняется. Сначала Abort.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _csvDirFull = Path.Combine(projectRoot, CsvDir);
            if (!Directory.Exists(_csvDirFull)) Directory.CreateDirectory(_csvDirFull);
            string rawDir = Path.Combine(projectRoot, _mode == RunMode.L2 ? "Temp/T-LIGHT17" :
                                                    _mode == RunMode.L2B ? "Temp/T-LIGHT17B" : RawDir);
            if (!Directory.Exists(rawDir)) Directory.CreateDirectory(rawDir);

            _runTag = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
            _cases = BuildCases();
            string prefix = _mode == RunMode.L2 ? "T-LIGHT17" :
                            _mode == RunMode.L2B ? "T-LIGHT17B" : "T-LIGHT16_BASELINE";
            _outRawPath = Path.Combine(rawDir, prefix + "_" + DateTag + "_raw.txt");
            _outCsvPath = Path.Combine(_csvDirFull, prefix + "_" + DateTag + ".csv");
            if (File.Exists(_outRawPath)) File.Delete(_outRawPath);

            _dirtyAtStart = SceneManager.GetActiveScene().isDirty;
            _lastHour = 12f;

            _state = State.WaitPlayMode;
            _caseIndex = 0;
            _hostRequested = false;
            _menuHidden = false;
            _cam = null;
            _worldRoot = null;
            _pool = null;
            _alic = null;
            _errorCount = 0;
            _exceptionCount = 0;
            _shaderErrorCount = 0;
            _warnCount = 0;
            _errorLines.Clear();
            _rebaseLines.Clear();
            _rigBehavioursDisabled.Clear();
            _rebasePhase = 0;
            _pressAttempts = 0;
            _f8Pass = false;
            _f9Pass = false;
            _rollbackSeen = 0;
            _rollbackRestoreTrue = 0;
            _stateSince = Now;

            HookLog();

            Append("NOTE;bench=TLight16BaselineBench;mode=" + _mode + ";unity=" + Application.unityVersion +
                   ";date=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                   ";scene=" + SceneManager.GetActiveScene().path + ";sceneDirtyAtStart=" + _dirtyAtStart +
                   ";headNote=" + (_mode == RunMode.L2B ? "alic_active_set_ab"
                                 : _mode == RunMode.L2 ? "l2_day_gate_split_radius_ab"
                                 : "baseline_instrumentation_only"));
            Append("OWNERSHIP;" + PrimumLightPool.OwnershipLine);
            Append("NOTE;method=PlayMode→Host(ProjectCMainMenuUiHost.OnMenuAction(host));warmup=" + WarmupFrames +
                   "fr;settle=" + SettleFrames + "fr" +
                   (_mode == RunMode.Baseline ? string.Empty : "(hourSwitch=" + SettleFramesHourSwitch + "fr)") +
                   ";samples=" + SampleCount + "x gap " + SampleGapFrames +
                   "fr;metric=UnityStats median;far=" + BenchFar.ToString("F0", CultureInfo.InvariantCulture) +
                   ";fov=" + BenchFov.ToString("F0", CultureInfo.InvariantCulture) + ";near=0.1");
            Append(_mode == RunMode.L2B
                ? "NOTE;behaviour_changed=alic_active_set_only;curves_groups_overrides_flicker_unchanged;pool_policy_unchanged;scene_saved=no;push=no"
                : _mode == RunMode.L2
                ? "NOTE;behaviour_changed=pool_dayGate_split_radius_only;pool_writes_intensity=no;scene_saved=no;push=no"
                : "NOTE;behaviour_unchanged=pool.enabled_intensity_focus_maxActive_radius_untouched;scene_saved=no;push=no");
            Append("NOTE;pool_markers=" + PrimumLightPool.CheckMarkerName + "|" + PrimumLightPool.FocusMarkerName +
                   ";alic_marker=" + AdditionalLightIntensityController.ApplyMarkerName);

            Debug.Log(Tag + " Старт, кейсов: " + _cases.Count + ". Файл: " + _outRawPath);

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static List<LightCase> BuildCases()
        {
            if (_mode == RunMode.L2) return BuildCasesL2();
            if (_mode == RunMode.L2B) return BuildCasesL2B();

            // Координаты P1/STREET — те же, что в бенче Stage 8 (P1 2 229 DC при hour 12):
            // нужны для контрольного сравнения, что инструментация не изменила поведение.
            var p1 = new Vector3(-342.90f, -50.43f, -102.20f);
            var street = new Vector3(-382.90f, -58.23f, -126.20f);

            var list = new List<LightCase>
            {
                new LightCase { id = "P1_h12", point = "P1", poseKind = "local", local = p1, yaw = 337.5f, hour = 12f, note = "control_dc_vs_stage8_2229" },
                new LightCase { id = "P1_h18", point = "P1", poseKind = "local", local = p1, yaw = 337.5f, hour = 18f, note = "user_complaint_window_17_20" },
                new LightCase { id = "P1_h18_repeat", point = "P1", poseKind = "local", local = p1, yaw = 337.5f, hour = 18f, note = "repeat_control_pm1pct" },
                new LightCase { id = "P1_h20", point = "P1", poseKind = "local", local = p1, yaw = 337.5f, hour = 20f, note = "late_17_20" },
                new LightCase { id = "P1_h18_turn180", point = "P1", poseKind = "local", local = p1, yaw = 337.5f, hour = 18f, turn = true, note = "reaction_frames_after_180" },
                new LightCase { id = "STREET_h12", point = "STREET", poseKind = "local", local = street, yaw = 225f, hour = 12f },
                new LightCase { id = "STREET_h18", point = "STREET", poseKind = "local", local = street, yaw = 225f, hour = 18f },
                new LightCase { id = "STREET_h20", point = "STREET", poseKind = "local", local = street, yaw = 225f, hour = 20f },
                new LightCase { id = "ADMIN_h12", point = "ADMIN", poseKind = "admin", hour = 12f },
                new LightCase { id = "ADMIN_h18", point = "ADMIN", poseKind = "admin", hour = 18f },
                new LightCase { id = "FARM_h12", point = "FARM", poseKind = "farm", hour = 12f },
                new LightCase { id = "FARM_h18", point = "FARM", poseKind = "farm", hour = 18f },
                new LightCase { id = "FARM_h20", point = "FARM", poseKind = "farm", hour = 20f },
            };
            return list;
        }

        /// <summary>
        /// T-LIGHT17: A/B-кейсы L2. Один и тот же кейс повторяется в разных конфигах пула —
        /// так разница списывается на конкретный рычаг (gate / split / radius), а не на точку.
        /// </summary>
        private static List<LightCase> BuildCasesL2()
        {
            var p1 = new Vector3(-342.90f, -50.43f, -102.20f);
            var street = new Vector3(-382.90f, -58.23f, -126.20f);

            var list = new List<LightCase>();

            void Add(string point, string poseKind, Vector3 local, float yaw, float hour, string cfgTag,
                     bool gate, bool split, bool l2, float radius, bool turn = false, string note = "")
            {
                list.Add(new LightCase
                {
                    id = point + "_h" + hour.ToString("F0", CultureInfo.InvariantCulture) + "_" + cfgTag +
                         (turn ? "_turn180" : string.Empty),
                    point = point,
                    poseKind = poseKind,
                    local = local,
                    yaw = yaw,
                    hour = hour,
                    turn = turn,
                    note = note,
                    cfgTag = cfgTag,
                    cfgDayGate = gate,
                    cfgSplit = split,
                    cfgOutMin = 40,
                    cfgUseL2 = l2,
                    cfgRadius = radius,
                });
            }

            // P1 (площадь) — главная точка жалобы. Сначала 12 ч: после 1 500 кадров прогрева ALIC
            // уже выставил OUT-группе точный ноль, поэтому day/night-гейт измеряется честно.
            Add("P1", "local", p1, 337.5f, 12f, "ctrl140", false, false, false, 140f, false, "control_L1_behaviour");
            Add("P1", "local", p1, 337.5f, 12f, "gate140", true, false, true, 140f, false, "gate_only");
            Add("P1", "local", p1, 337.5f, 12f, "split140", false, true, true, 140f, false, "split_only");
            Add("P1", "local", p1, 337.5f, 12f, "l2_100", true, true, true, 100f, false, "l2_candidate");
            Add("P1", "local", p1, 337.5f, 12f, "l2_70", true, true, true, 70f, false, "l2_aggressive");
            Add("P1", "local", p1, 337.5f, 18f, "ctrl140", false, false, false, 140f, false, "evening_control");
            Add("P1", "local", p1, 337.5f, 18f, "l2_100", true, true, true, 100f);
            Add("P1", "local", p1, 337.5f, 18f, "l2_70", true, true, true, 70f, false, "evening_transition_17_21");
            Add("P1", "local", p1, 337.5f, 20f, "l2_100", true, true, true, 100f, false, "night_full_brightness");
            Add("P1", "local", p1, 337.5f, 18f, "l2_100", true, true, true, 100f, true, "turn_reaction");

            Add("STREET", "local", street, 225f, 12f, "ctrl140", false, false, false, 140f);
            Add("STREET", "local", street, 225f, 12f, "l2_100", true, true, true, 100f);
            Add("STREET", "local", street, 225f, 12f, "l2_70", true, true, true, 70f);
            Add("STREET", "local", street, 225f, 18f, "l2_100", true, true, true, 100f);

            Add("ADMIN", "admin", Vector3.zero, 0f, 12f, "ctrl140", false, false, false, 140f, false, "interior_takes_all_slots");
            Add("ADMIN", "admin", Vector3.zero, 0f, 12f, "split140", false, true, true, 140f, false, "out_reserve_effect");
            Add("ADMIN", "admin", Vector3.zero, 0f, 12f, "l2_100", true, true, true, 100f);
            Add("ADMIN", "admin", Vector3.zero, 0f, 18f, "l2_100", true, true, true, 100f);

            Add("FARM", "farm", Vector3.zero, 0f, 12f, "ctrl140", false, false, false, 140f, false, "farm_budget_no_quota");
            Add("FARM", "farm", Vector3.zero, 0f, 12f, "l2_100", true, true, true, 100f);
            Add("FARM", "farm", Vector3.zero, 0f, 12f, "l2_70", true, true, true, 70f, false, "radius_must_not_starve_farm");
            Add("FARM", "farm", Vector3.zero, 0f, 18f, "l2_100", true, true, true, 100f);

            return list;
        }

        /// <summary>
        /// T-LIGHT17b: A/B активного набора ALIC. Пул зафиксирован в принятом L2-конфиге
        /// (gate on, split on, outMin 40, радиус 100) — сравниваются только режимы ALIC,
        /// поэтому разница в alicMs списывается на ограничение записи, а не на политику пула.
        /// Кейсы группируются по часу: смена часа стоит 200 кадров успокоения, а не 40.
        /// </summary>
        private static List<LightCase> BuildCasesL2B()
        {
            var p1 = new Vector3(-342.90f, -50.43f, -102.20f);
            var street = new Vector3(-382.90f, -58.23f, -126.20f);

            var list = new List<LightCase>();

            void Add(string point, string poseKind, Vector3 local, float yaw, float hour,
                     bool alicActive, string note, string idTag = "")
            {
                string cfg = alicActive ? "alicActive" : "alicAll";
                list.Add(new LightCase
                {
                    id = point + "_h" + hour.ToString("F0", CultureInfo.InvariantCulture) + "_" + cfg + idTag,
                    point = point,
                    poseKind = poseKind,
                    local = local,
                    yaw = yaw,
                    hour = hour,
                    note = note,
                    cfgTag = cfg,
                    // Политика пула между кейсами не меняется — это принятый L2-конфиг.
                    cfgDayGate = true,
                    cfgSplit = true,
                    cfgOutMin = 40,
                    cfgUseL2 = true,
                    cfgRadius = 100f,
                    cfgAlicActiveSet = alicActive,
                });
            }

            float[] hours = { 12f, 18f, 20f };

            for (int h = 0; h < hours.Length; h++)
            {
                float hour = hours[h];
                Add("P1", "local", p1, 337.5f, hour, false, "control_all_lights_every_frame");
                Add("P1", "local", p1, 337.5f, hour, true, "active_set");
                Add("STREET", "local", street, 225f, hour, false, "control_all_lights_every_frame");
                Add("STREET", "local", street, 225f, hour, true, "active_set");
                Add("ADMIN", "admin", Vector3.zero, 0f, hour, false, "control_all_lights_every_frame");
                Add("ADMIN", "admin", Vector3.zero, 0f, hour, true, "active_set");
                Add("FARM", "farm", Vector3.zero, 0f, hour, false, "control_all_lights_every_frame");
                Add("FARM", "farm", Vector3.zero, 0f, hour, true, "active_set");
            }

            // Повтор ключевого кейса (площадь, 18 ч) — контроль стабильности ±5 %.
            Add("P1", "local", p1, 337.5f, 18f, false, "repeat_control", "_repeat");
            Add("P1", "local", p1, 337.5f, 18f, true, "repeat_active_set", "_repeat");

            return list;
        }

        // -------------------------------------------------------------------- тик

        private static void Tick()
        {
            try { Step(); }
            catch (Exception e)
            {
                Debug.LogError(Tag + " Исключение: " + e);
                Append("NOTE;exception=" + e.GetType().Name + ":" + (e.Message ?? string.Empty).Replace(";", ","));
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
                    if (!GateInGame()) return;
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

        // ------------------------------------------------------- Play Mode → Host/риг

        private static bool TryStartHost()
        {
            var pcmm = FindByTypeName("ProjectCMainMenuUiHost");
            if (pcmm != null)
            {
                if (!_hostRequested)
                {
                    var action = pcmm.GetType().GetMethod("OnMenuAction", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (action == null) return false;
                    _hostRequested = true;
                    action.Invoke(pcmm, new object[] { "host", "ОДИНОЧНАЯ ИГРА" });
                    Debug.Log(Tag + " Host запрошен через ProjectCMainMenuUiHost.OnMenuAction(\"host\")");
                    Append("NOTE;host_start=ProjectCMainMenuUiHost.OnMenuAction(host)");
                    return false;
                }
                _menuHidden = !PcmmShown(pcmm);
                return _menuHidden && TryAcquireRig();
            }

            if (TryAcquireRig()) { _hostRequested = true; _menuHidden = true; return true; }

            var menu = FindByTypeName("MainMenuWindow");
            var nmc = FindByTypeName("NetworkManagerController");
            if (nmc == null) return false;

            if (!_hostRequested)
            {
                _hostRequested = true;
                var start = nmc.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
                if (menu != null)
                {
                    var onClick = menu.GetType().GetMethod("OnHostClicked", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (onClick != null) onClick.Invoke(menu, null);
                    else if (start != null) start.Invoke(nmc, null);
                }
                else if (start != null) start.Invoke(nmc, null);
                Debug.Log(Tag + " Host запрошен через legacy-путь");
            }

            if (!_menuHidden && menu != null)
            {
                var hide = menu.GetType().GetMethod("Hide", BindingFlags.Instance | BindingFlags.Public);
                if (hide != null) { hide.Invoke(menu, null); _menuHidden = true; }
            }
            else if (menu == null) _menuHidden = true;

            return _menuHidden;
        }

        private static bool PcmmShown(Component pcmm)
        {
            if (pcmm == null) return false;
            var p = pcmm.GetType().GetProperty("IsShown", BindingFlags.Instance | BindingFlags.Public);
            return p != null && (bool)p.GetValue(pcmm);
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
            StartProfilers();

            Debug.Log(Tag + " Риг: " + _cam.name + " (far " + _cam.farClipPlane + ", fov " + _cam.fieldOfView +
                      "), отключено двигателей камеры: " + disabled +
                      ", WorldRoot_0_0=" + (_worldRoot != null ? _worldRoot.position.ToString("F2") : "нет"));
            Append("NOTE;camera;name=" + _cam.name + ";far=" + _cam.farClipPlane.ToString("F0", CultureInfo.InvariantCulture) +
                   ";fov=" + _cam.fieldOfView.ToString("F0", CultureInfo.InvariantCulture) +
                   ";rigBehavioursDisabled=" + disabled);
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
                Debug.LogWarning(Tag + " DayNightController не найден — фаза суток не форсирована");
                Append("NOTE;phase_forced=FAILED_no_DayNightController");
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
            Append("NOTE;phase_forced=" + hour.ToString("F1", CultureInfo.InvariantCulture) +
                   ";sun=" + (sun == null ? "none" : sun.name + "(intensity=" + sun.intensity.ToString("F2", CultureInfo.InvariantCulture) + ")"));
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

        /// <summary>
        /// Гейт «мы реально в игре»: риг ThirdPersonCamera, локальный игрок, меню не показано,
        /// пул нашёл ≥ 2 000 источников. Иначе замер не в игре (частый случай: остались в главном меню).
        /// </summary>
        private static bool GateInGame()
        {
            _pool = UnityEngine.Object.FindAnyObjectByType<PrimumLightPool>();
            _alic = UnityEngine.Object.FindAnyObjectByType<AdditionalLightIntensityController>();

            bool pcmmShown = PcmmShown(FindByTypeName("ProjectCMainMenuUiHost"));
            bool atmosphereCamera = false;
            var atm = FindByTypeName("ProjectCMainMenuAtmosphere");
            if (atm != null)
            {
                var p = atm.GetType().GetProperty("Camera", BindingFlags.Instance | BindingFlags.Public);
                var cam = p != null ? p.GetValue(atm) as Camera : null;
                atmosphereCamera = cam != null && cam.isActiveAndEnabled;
            }

            bool player = false;
            _rebaseSlice = FindByTypeName("GlobalMotionControlledRebaseSlice") as MonoBehaviour;
            if (_rebaseSlice != null)
            {
                var m = _rebaseSlice.GetType().GetMethod("TryResolveLocalPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
                if (m != null)
                {
                    object[] args = { null, null };
                    player = (bool)m.Invoke(_rebaseSlice, args) && args[0] is Transform;
                    if (player) _playerRoot = args[0] as Transform;
                }
            }

            int registered = _pool != null ? _pool.RegisteredCount : -1;

            Append("GATE;inGame;rig=" + (_cam != null ? _cam.name : "none") +
                   ";localPlayer=" + player + ";pcmmMenuShown=" + pcmmShown + ";pcmmAtmosphereCamera=" + atmosphereCamera +
                   ";pool=" + (_pool != null) + ";alic=" + (_alic != null) +
                   ";registered=" + registered + ";minExpected=" + MinExpectedLights +
                   ";cursorLock=" + Cursor.lockState);

            // T-LIGHT17b: зафиксировать режим ALIC рядом с числами (до первого кейса).
            if (_alic != null) Append("ALIC_CONFIG;at_gate;" + _alic.ConfigLine);

            bool ok = _cam != null && player && !pcmmShown && !atmosphereCamera && _pool != null && registered >= MinExpectedLights;
            if (!ok)
            {
                Debug.LogError(Tag + " GATE FAIL: замер не в игре (rig=" + (_cam != null) + ", player=" + player +
                               ", menu=" + pcmmShown + ", atmosphereCam=" + atmosphereCamera +
                               ", pool=" + (_pool != null) + ", registered=" + registered + ") — прогон остановлен.");
                Append("GATE;result=FAIL_not_in_game;poolRegistered=" + registered);
                Finish("GATE_FAIL_NOT_IN_GAME", false);
                return false;
            }

            Append("GATE;result=OK");
            return true;
        }

        // ------------------------------------------------------------------ точки

        private static void BeginCase(int index)
        {
            _caseIndex = index;
            if (_caseIndex >= _cases.Count)
            {
                BeginRebase();
                return;
            }

            var c = _cases[_caseIndex];
            _turnStarted = false;
            _turnFrame = -1;
            _turnReactionFrames = -1;
            _turnTimeout = false;
            _turnVersionBefore = 0;

            _checkMsFrames.Clear();
            _focusMsFrames.Clear();
            _alicMsFrames.Clear();
            _alicWritesFrames.Clear();
            _alicActiveWritesFrames.Clear();
            _alicIteratedFrames.Clear();
            _alicBaseSweepFrames.Clear();
            _lastProfiledFrame = -1;

            ForcePhaseSilent(c.hour);
            ApplyPose(c);

            // T-LIGHT17: A/B-конфиг пула — только поля компонента в памяти, сцена не пишется.
            if (_mode != RunMode.Baseline && _pool != null)
            {
                _pool.ApplyConfig(c.cfgDayGate, c.cfgSplit, c.cfgOutMin, c.cfgUseL2, c.cfgRadius);
                Append("CONFIG;case=" + c.id + ";" + _pool.ConfigLine);

                // T-LIGHT17b: режим ALIC переключается так же — только поле в памяти.
                if (_mode == RunMode.L2B && _alic != null)
                {
                    _alic.ApplyActiveSetConfig(c.cfgAlicActiveSet);
                    Append("CONFIG;case=" + c.id + ";" + _alic.ConfigLine);
                }
            }

            _dc.Clear(); _spc.Clear(); _tris.Clear(); _ms.Clear();
            _censusDone = false;

            int settle = SettleFrames;
            if (_mode != RunMode.Baseline)
            {
                // После смены часа ALIC ещё сглаживает intensity: на 40 кадрах гейт увидел бы
                // не погашенные лампы, а их переход — и «day activeZero=0» измерился бы ложно.
                if (!Mathf.Approximately(_lastHour, c.hour)) settle = SettleFramesHourSwitch;
                _lastHour = c.hour;
            }
            _settleUntilFrame = Time.frameCount + settle;
            _nextSampleFrame = _settleUntilFrame;
            _caseWindowFrame = Time.frameCount;
            _caseWindowStart = Now;

            Debug.Log(Tag + " Кейс " + c.id + " (point=" + c.point + ", hour=" + c.hour +
                      ", turn=" + c.turn + ")");
        }

        private static void ApplyPose(LightCase c)
        {
            if (_cam == null) return;

            Vector3 pos;
            Quaternion rot;
            if (c.poseKind == "admin")
            {
                if (!TryAdminPose(out pos, out rot)) { if (!TryLocalPose(c, out pos, out rot)) return; }
            }
            else if (c.poseKind == "farm")
            {
                if (!TryFarmPose(out pos, out rot)) { if (!TryLocalPose(c, out pos, out rot)) return; }
            }
            else if (!TryLocalPose(c, out pos, out rot)) return;

            if (c.turn && _turnStarted)
            {
                // разворот на 180° вокруг вертикали — тот же приём, что делает игрок камерой
                rot = Quaternion.Euler(c.pitch, c.yaw + 180f, 0f);
            }

            _targetPos = pos;
            _targetRot = rot;
            _cam.transform.SetPositionAndRotation(_targetPos, _targetRot);
        }

        private static bool TryLocalPose(LightCase c, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            Vector3 shift = _worldRoot != null ? _worldRoot.position : Vector3.zero;
            pos = c.local + AuthoredWorldOffset + shift;
            rot = Quaternion.Euler(c.pitch, c.yaw, 0f);
            return true;
        }

        /// <summary>Интерьер ADMIN: центр кластера интерьерных источников этого чанка (позиции живые, FO-safe).</summary>
        private static bool TryAdminPose(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            if (_worldRoot == null || _pool == null) return false;

            _pool.CopyRegisteredLights(_lightScratch);
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < _lightScratch.Count; i++)
            {
                var l = _lightScratch[i];
                if (l == null) continue;
                if (l.name.IndexOf("ADMIN", StringComparison.Ordinal) < 0) continue;
                sum += l.transform.position;
                n++;
            }
            if (n == 0)
            {
                var city = _worldRoot.Find("PRIMUM");
                var node = city != null ? city.Find(AdminNodeName) : null;
                if (node == null) return false;
                var rs = node.GetComponentsInChildren<Renderer>(false);
                if (rs.Length == 0) return false;
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                pos = b.center;
                rot = Quaternion.Euler(0f, 0f, 0f);
                return true;
            }

            pos = sum / n;
            rot = Quaternion.Euler(0f, 0f, 0f);
            return true;
        }

        /// <summary>Кластер ферм: самый плотный куст PRIMUM_FARMLIGHT_* (радиус 20 м) и камера в 25 м от него.</summary>
        private static bool TryFarmPose(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            if (_pool == null) return false;

            _pool.CopyRegisteredLights(_lightScratch);
            var farms = new List<Light>(256);
            for (int i = 0; i < _lightScratch.Count; i++)
            {
                var l = _lightScratch[i];
                if (l == null) continue;
                if (l.name.IndexOf("PRIMUM_FARMLIGHT", StringComparison.Ordinal) < 0) continue;
                farms.Add(l);
            }
            if (farms.Count == 0) return false;

            const float clusterRadius = 20f;
            float bestSqr = clusterRadius * clusterRadius;
            int bestIndex = -1;
            int bestCount = -1;
            for (int i = 0; i < farms.Count; i++)
            {
                Vector3 pi = farms[i].transform.position;
                int count = 0;
                for (int j = 0; j < farms.Count; j++)
                {
                    if (i == j) continue;
                    if ((farms[j].transform.position - pi).sqrMagnitude <= bestSqr) count++;
                }
                if (count > bestCount) { bestCount = count; bestIndex = i; }
            }
            if (bestIndex < 0) return false;

            Vector3 centerOfCluster = farms[bestIndex].transform.position;
            Vector3 sum = centerOfCluster;
            int n = 1;
            for (int j = 0; j < farms.Count; j++)
            {
                if (j == bestIndex) continue;
                if ((farms[j].transform.position - centerOfCluster).sqrMagnitude > bestSqr) continue;
                sum += farms[j].transform.position;
                n++;
            }
            Vector3 center = sum / n;

            pos = center + new Vector3(0f, 10f, -25f);
            rot = Quaternion.LookRotation((center - pos).normalized, Vector3.up);
            Append("NOTE;farm_cluster;lights=" + farms.Count + ";cluster=" + n + ";center=" + center.ToString("F1"));
            return true;
        }

        private static void StepPoint()
        {
            var c = _cases[_caseIndex];
            ApplyPose(c);
            SampleProfilers();

            // Кадр, в котором камера уже развёрнута на 180° — от него считается задержка реакции.
            if (c.turn && _turnStarted && _turnFrame < 0) _turnFrame = Time.frameCount;

            if (!_censusDone)
            {
                if (Time.frameCount < _settleUntilFrame) return;
                RunCensus();
                if (_state != State.Point) return;

                if (c.turn && !_turnStarted)
                {
                    _turnStarted = true;
                    _turnFrame = -1;
                    _turnVersionBefore = _pool != null ? _pool.SetVersion : 0;
                    Append("TURN;case=" + c.id + ";phase=before;frame=" + Time.frameCount +
                           ";setVersion=" + _turnVersionBefore + ";active=" + _cActiveTotal +
                           ";ahead=" + _cAheadActive + ";behind=" + _cBehindActive);
                    return;
                }

                _censusDone = true;
                _nextSampleFrame = Time.frameCount;
            }

            // реакция на разворот: ждём смены набора пула
            if (c.turn && _turnFrame >= 0 && _turnReactionFrames < 0 && !_turnTimeout)
            {
                int version = _pool != null ? _pool.SetVersion : 0;
                if (version != _turnVersionBefore)
                {
                    _turnReactionFrames = Time.frameCount - _turnFrame;
                    Append("TURN;case=" + c.id + ";phase=reacted;frames=" + _turnReactionFrames +
                           ";setVersion=" + version + ";poolCheckMs=" +
                           _pool.LastCheckMs.ToString("F3", CultureInfo.InvariantCulture));
                }
                else if (Time.frameCount - _turnFrame > TurnTimeoutFrames)
                {
                    _turnTimeout = true;
                    Append("TURN;case=" + c.id + ";phase=timeout;frames=" + (Time.frameCount - _turnFrame));
                }
                else return;
            }

            if (Time.frameCount < _nextSampleFrame) return;

            _dc.Add(UnityStats.drawCalls);
            _spc.Add(UnityStats.setPassCalls);
            _tris.Add(UnityStats.triangles);
            _ms.Add((double)Time.deltaTime * 1000.0);

            if (_dc.Count == 1 && UnityStats.drawCalls == 0)
            {
                Append("NOTE;warning=drawCalls_0_at_first_sample;screenRes=" + UnityStats.screenRes);
            }

            if (_dc.Count < SampleCount) { _nextSampleFrame = Time.frameCount + SampleGapFrames; return; }

            WriteCaseLine(c);
            BeginCase(_caseIndex + 1);
        }

        /// <summary>Независимый census: активные лампы впереди/позади камеры, в фрустуме, по группам и дистанциям.</summary>
        private static void RunCensus()
        {
            _cAheadActive = 0;
            _cBehindActive = 0;
            _cInFrustumActive = 0;
            _cActiveAdmin = 0;

            GeometryUtility.CalculateFrustumPlanes(_cam, _planes);
            Vector3 camPos = _cam.transform.position;
            Vector3 fwd = _cam.transform.forward;

            _pool.CopyRegisteredLights(_lightScratch);
            int enabledTotal = 0;
            for (int i = 0; i < _lightScratch.Count; i++)
            {
                var l = _lightScratch[i];
                if (l == null || !l.enabled) continue;
                enabledTotal++;
                Vector3 p = l.transform.position;
                Vector3 delta = p - camPos;
                if (Vector3.Dot(fwd, delta) >= 0f) _cAheadActive++;
                else _cBehindActive++;
                if (GeometryUtility.TestPlanesAABB(_planes, new Bounds(p, Vector3.one))) _cInFrustumActive++;
                if (l.name.IndexOf("ADMIN", StringComparison.Ordinal) >= 0) _cActiveAdmin++;
            }

            _cRegistered = _pool.RegisteredCount;
            _cActiveTotal = enabledTotal;
            _cActiveOut = _pool.ActiveOut;
            _cActiveIn = _pool.ActiveIn;
            _cActiveFarm = _pool.ActiveFarm;
            _cActiveOther = _pool.ActiveOther;
            _cCandidate = _pool.CandidateCount;
            _cCapCut = _pool.CapCutCount;
            _cCapSaturated = _pool.CapSaturated;
            _cCap = _pool.CapValue;
            _cBucket0 = _pool.Bucket0To70;
            _cBucket1 = _pool.Bucket70To100;
            _cBucket2 = _pool.Bucket100To140;
            _cBucket3 = _pool.Bucket140To154;
            _cBucket4 = _pool.BucketOver154;
            _cIntensityZero = _pool.ActiveIntensityZero;
            _cPoolCheckMs = _pool.LastCheckMs;
            _cPoolCheckMaxMs = _pool.MaxCheckMs;
            _cFocusSearchMs = _pool.LastFocusSearchMs;
            _cFocusSearches = _pool.FocusSearchCount;
            _cFocusCameraPool = _pool.FocusCameraPool;
            _cFocusName = _pool.FocusSourceName;
            _cFocusKind = _pool.FocusSourceKind;
            _cSetVersion = _pool.SetVersion;
            _cCheckCount = _pool.CheckCount;
            _cDayGateDropped = _pool.DayGateDropped;
            _cQuotaDeferred = _pool.KindQuotaDeferred;
            _cQuotaFilled = _pool.KindQuotaFilled;
            _cOutMinUsed = _pool.OutdoorMinSlotsUsed;

            // В Unity 6 VisibleLight выдаёт только CullingResults внутри render-pass, у Camera
            // такого API нет — пишем -1 и отмечаем в raw, чтобы не выдавать пустое значение за 0.
            _cVisibleLights = -1;
            if (!_visibleLightsNoteWritten)
            {
                _visibleLightsNoteWritten = true;
                Append("NOTE;visibleLights=unavailable_Camera_api_in_Unity6;substitute=pool_active_and_census");
            }

            if (_alic != null)
            {
                _cAlicTracked = _alic.TrackedLightCount;
                _cAlicApplied = _alic.AppliedLastCall;
                _cAlicActiveApplied = _alic.ActiveAppliedLastCall;
                _cAlicZero = _alic.ZeroIntensityTracked;
                _cAlicCallsPerFrame = _alic.ApplyCallsThisFrame;
                _cAlicHour = _alic.CurrentTimeOfDay;
                _cAlicFlicker = _alic.FlickerAnyEnabled;
                _cAlicActiveSet = _alic.ActiveSetEnabled;
                _cAlicSetSize = _alic.ActiveSetSize;
                _cAlicWrites = _alic.WritesThisFrame;
                _cAlicActiveWrites = _alic.ActiveWritesThisFrame;
                _cAlicIterated = _alic.IteratedThisFrame;
                _cAlicBaseSweep = _alic.BaseSweepThisFrame ? 1 : 0;
                _cAlicResyncs = _alic.ActiveSetResyncs;
            }
            else
            {
                _cAlicTracked = -1;
                _cAlicApplied = -1;
                _cAlicActiveApplied = -1;
                _cAlicZero = -1;
                _cAlicCallsPerFrame = -1;
                _cAlicHour = -1f;
                _cAlicActiveSet = false;
                _cAlicSetSize = -1;
                _cAlicWrites = -1;
                _cAlicActiveWrites = -1;
                _cAlicIterated = -1;
                _cAlicBaseSweep = -1;
                _cAlicResyncs = -1;
            }

            if (enabledTotal == 0)
            {
                Debug.LogError(Tag + " 0 активных источников на точке — поза/пул неверны, замер остановлен.");
                Append("NOTE;error=census_zero_active;campos=" + _targetPos.ToString("F1"));
                Finish("CENSUS_ZERO_ACTIVE", false);
            }
        }

        private static void WriteCaseLine(LightCase c)
        {
            int dc = Median(_dc);
            int spc = Median(_spc);
            int tris = Median(_tris);
            double ms = MedianD(_ms);
            double seconds = Math.Max(0.0001, Now - _caseWindowStart);
            int framesInWindow = Math.Max(1, Time.frameCount - _caseWindowFrame);
            double fps = framesInWindow / seconds;
            double gpuMs = ReadGpuMs();
            double alicMs = Avg(_alicMsFrames);
            double checkMs = Avg(_checkMsFrames);
            double focusMs = Avg(_focusMsFrames);
            // T-LIGHT17b
            double alicWrites = Avg(_alicWritesFrames);
            double alicActiveWrites = Avg(_alicActiveWritesFrames);
            double alicIterated = Avg(_alicIteratedFrames);
            double alicBaseSweepFrames = Sum(_alicBaseSweepFrames);

            var sb = new StringBuilder();
            sb.Append("CASE;id=").Append(c.id);
            sb.Append(";point=").Append(c.point);
            sb.Append(";hour=").Append(c.hour.ToString("F1", CultureInfo.InvariantCulture));
            sb.Append(";worldpos=").Append(_targetPos.ToString("F2"));
            sb.Append(";registered=").Append(_cRegistered);
            sb.Append(";activeTotal=").Append(_cActiveTotal);
            sb.Append(";activeOUT=").Append(_cActiveOut);
            sb.Append(";activeIN=").Append(_cActiveIn);
            sb.Append(";activeFarm=").Append(_cActiveFarm);
            sb.Append(";activeOther=").Append(_cActiveOther);
            sb.Append(";activeADMIN=").Append(_cActiveAdmin);
            sb.Append(";activeAhead=").Append(_cAheadActive).Append(";activeBehind=").Append(_cBehindActive);
            sb.Append(";activeInFrustum=").Append(_cInFrustumActive);
            sb.Append(";intensityZeroActive=").Append(_cIntensityZero);
            sb.Append(";candidates=").Append(_cCandidate).Append(";cap=").Append(_cCap)
              .Append(";capCut=").Append(_cCapCut).Append(";capSaturated=").Append(_cCapSaturated);
            sb.Append(";b0_70=").Append(_cBucket0).Append(";b70_100=").Append(_cBucket1)
              .Append(";b100_140=").Append(_cBucket2).Append(";b140_154=").Append(_cBucket3)
              .Append(";b154plus=").Append(_cBucket4);
            sb.Append(";poolCheckMs=").Append(_cPoolCheckMs.ToString("F3", CultureInfo.InvariantCulture));
            sb.Append(";poolCheckMaxMs=").Append(_cPoolCheckMaxMs.ToString("F3", CultureInfo.InvariantCulture));
            sb.Append(";checkMarkerMsAvg=").Append(checkMs.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(";focusSearchMs=").Append(_cFocusSearchMs.ToString("F3", CultureInfo.InvariantCulture));
            sb.Append(";focusMarkerMsAvg=").Append(focusMs.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(";focusSearches=").Append(_cFocusSearches);
            sb.Append(";focusCameraPool=").Append(_cFocusCameraPool);
            sb.Append(";focusSource=").Append(_cFocusKind + ":" + _cFocusName);
            sb.Append(";alicMs=").Append(alicMs.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(";alicLastMs=").Append((_alic != null ? _alic.LastApplyMs : -1).ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(";alicCallsPerFrame=").Append(_cAlicCallsPerFrame);
            sb.Append(";alicTracked=").Append(_cAlicTracked);
            sb.Append(";alicApplied=").Append(_cAlicApplied);
            sb.Append(";alicActiveApplied=").Append(_cAlicActiveApplied);
            sb.Append(";alicIntensityZero=").Append(_cAlicZero);
            sb.Append(";alicHour=").Append(_cAlicHour.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(";alicFlicker=").Append(_cAlicFlicker);
            if (_mode == RunMode.L2B)
            {
                sb.Append(";alicActiveSet=").Append(_cAlicActiveSet)
                  .Append(";alicSetSize=").Append(_cAlicSetSize)
                  .Append(";alicWritesAvg=").Append(alicWrites.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(";alicActiveWritesAvg=").Append(alicActiveWrites.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(";alicIteratedAvg=").Append(alicIterated.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(";alicBaseSweepFrames=").Append((int)alicBaseSweepFrames)
                  .Append(";alicResyncs=").Append(_cAlicResyncs);
            }
            sb.Append(";visibleLights=").Append(_cVisibleLights);
            sb.Append(";dc=").Append(dc).Append(";spc=").Append(spc).Append(";tris=").Append(tris);
            sb.Append(";gpuMs=").Append(gpuMs.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(";ms=").Append(ms.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(";fps=").Append(fps.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(";setVersion=").Append(_cSetVersion).Append(";checks=").Append(_cCheckCount);
            sb.Append(";turn=").Append(c.turn);
            sb.Append(";turnReactionFrames=").Append(_turnReactionFrames);
            sb.Append(";turnTimeout=").Append(_turnTimeout);
            if (_mode == RunMode.L2)
            {
                sb.Append(";cfg=").Append(c.cfgTag)
                  .Append(";cfgRadius=").Append(c.cfgRadius.ToString("F0", CultureInfo.InvariantCulture))
                  .Append(";dayGateDropped=").Append(_cDayGateDropped)
                  .Append(";quotaDeferred=").Append(_cQuotaDeferred)
                  .Append(";quotaFilled=").Append(_cQuotaFilled)
                  .Append(";outMinUsed=").Append(_cOutMinUsed);
            }
            sb.Append(";note=").Append(c.note);
            Append(sb.ToString());
            AppendCsv(c, dc, spc, tris, gpuMs, ms, fps, checkMs, alicMs, alicWrites, alicActiveWrites, alicIterated, alicBaseSweepFrames);

            Debug.Log(Tag + " " + c.id + ": dc=" + dc + " active=" + _cActiveTotal +
                      " (OUT " + _cActiveOut + "/IN " + _cActiveIn + "/FARM " + _cActiveFarm + ")" +
                      " cand=" + _cCandidate + " capCut=" + _cCapCut + " zero=" + _cIntensityZero +
                      " checkMs=" + _cPoolCheckMs.ToString("F3", CultureInfo.InvariantCulture) +
                      " alicMs=" + alicMs.ToString("F4", CultureInfo.InvariantCulture) +
                      " focus=" + _cFocusKind + ":" + _cFocusName);
        }

        private static double ReadGpuMs()
        {
            try
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
                {
                    double g = _timings[0].gpuFrameTime;
                    if (g > 0.0) return g;
                }
            }
            catch (Exception) { }
            return -1.0;
        }

        private static void AppendCsv(LightCase c, int dc, int spc, int tris, double gpuMs, double ms, double fps,
                                      double checkMs, double alicMs, double alicWrites, double alicActiveWrites,
                                      double alicIterated, double alicBaseSweepFrames)
        {
            bool writeHeader = !File.Exists(_outCsvPath);
            var sb = new StringBuilder();
            if (writeHeader)
            {
                sb.AppendLine("run,date,case,point,hour,worldpos,registered,activeTotal,activeOUT,activeIN,activeFarm," +
                              "activeADMIN,activeAhead,activeBehind,activeInFrustum,intensityZeroActive,candidates,cap,capCut," +
                              "capSaturated,b0_70,b70_100,b100_140,b140_154,b154plus,poolCheckMs,poolCheckMaxMs,checkMarkerMsAvg," +
                              "focusSearchMs,focusSource,alicMs,alicLastMs,alicCallsPerFrame,alicTracked,alicApplied," +
                              "alicActiveApplied,alicIntensityZero,alicHour,visibleLights,dc,spc,tris,gpuMs,ms,fps," +
                              "setVersion,turnReactionFrames,turnTimeout" +
                              (_mode == RunMode.L2B
                                  ? ",alicActiveSet,alicSetSize,alicWritesAvg,alicActiveWritesAvg,alicIteratedAvg,alicBaseSweepFrames"
                                  : string.Empty) +
                              (_mode == RunMode.L2
                                  ? ",cfgTag,cfgRadius,cfgDayGate,cfgSplit,cfgOutMin,dayGateDropped,quotaDeferred,quotaFilled,outMinUsed"
                                  : string.Empty) +
                              ",note");
            }
            sb.Append(_runTag).Append(',')
              .Append(DateTag).Append(',')
              .Append(c.id).Append(',')
              .Append(c.point).Append(',')
              .Append(c.hour.ToString("F1", CultureInfo.InvariantCulture)).Append(',')
              .Append('"').Append(_targetPos.ToString("F2")).Append('"').Append(',')
              .Append(_cRegistered).Append(',')
              .Append(_cActiveTotal).Append(',').Append(_cActiveOut).Append(',').Append(_cActiveIn).Append(',')
              .Append(_cActiveFarm).Append(',').Append(_cActiveAdmin).Append(',')
              .Append(_cAheadActive).Append(',').Append(_cBehindActive).Append(',').Append(_cInFrustumActive).Append(',')
              .Append(_cIntensityZero).Append(',')
              .Append(_cCandidate).Append(',').Append(_cCap).Append(',').Append(_cCapCut).Append(',')
              .Append(_cCapSaturated ? 1 : 0).Append(',')
              .Append(_cBucket0).Append(',').Append(_cBucket1).Append(',').Append(_cBucket2).Append(',')
              .Append(_cBucket3).Append(',').Append(_cBucket4).Append(',')
              .Append(_cPoolCheckMs.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
              .Append(_cPoolCheckMaxMs.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
              .Append(checkMs.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
              .Append(_cFocusSearchMs.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
              .Append(_cFocusKind).Append(':').Append(_cFocusName).Append(',')
              .Append(alicMs.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
              .Append((_alic != null ? _alic.LastApplyMs : -1).ToString("F4", CultureInfo.InvariantCulture)).Append(',')
              .Append(_cAlicCallsPerFrame).Append(',').Append(_cAlicTracked).Append(',').Append(_cAlicApplied).Append(',')
              .Append(_cAlicActiveApplied).Append(',').Append(_cAlicZero).Append(',')
              .Append(_cAlicHour.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
              .Append(_cVisibleLights).Append(',')
              .Append(dc).Append(',').Append(spc).Append(',').Append(tris).Append(',')
              .Append(gpuMs.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
              .Append(ms.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
              .Append(fps.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
              .Append(_cSetVersion).Append(',')
              .Append(_turnReactionFrames).Append(',').Append(_turnTimeout ? 1 : 0);
            if (_mode == RunMode.L2B)
            {
                sb.Append(',').Append(_cAlicActiveSet ? 1 : 0)
                  .Append(',').Append(_cAlicSetSize)
                  .Append(',').Append(alicWrites.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(',').Append(alicActiveWrites.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(',').Append(alicIterated.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(',').Append((int)alicBaseSweepFrames);
            }
            if (_mode == RunMode.L2)
            {
                sb.Append(',').Append(c.cfgTag)
                  .Append(',').Append(c.cfgRadius.ToString("F0", CultureInfo.InvariantCulture))
                  .Append(',').Append(c.cfgDayGate ? 1 : 0)
                  .Append(',').Append(c.cfgSplit ? 1 : 0)
                  .Append(',').Append(c.cfgOutMin)
                  .Append(',').Append(_cDayGateDropped)
                  .Append(',').Append(_cQuotaDeferred)
                  .Append(',').Append(_cQuotaFilled)
                  .Append(',').Append(_cOutMinUsed);
            }
            sb.Append(',').Append('"').Append(c.note).Append('"');
            sb.AppendLine();
            try { File.AppendAllText(_outCsvPath, sb.ToString(), Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning(Tag + " CSV: " + e.Message); }
        }

        // ------------------------------------------------------------ профиллеры

        private static void StartProfilers()
        {
            _checkRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, PrimumLightPool.CheckMarkerName);
            _focusRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, PrimumLightPool.FocusMarkerName);
            _alicRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, AdditionalLightIntensityController.ApplyMarkerName);
            Append("NOTE;recorders;check=" + _checkRecorder.Valid + ";focus=" + _focusRecorder.Valid + ";alic=" + _alicRecorder.Valid);
            if (!_checkRecorder.Valid)
                Debug.LogWarning(Tag + " ProfilerRecorder(" + PrimumLightPool.CheckMarkerName + ") не валиден — используем Stopwatch-счётчики пула.");
        }

        private static void SampleProfilers()
        {
            if (Time.frameCount == _lastProfiledFrame) return;
            _lastProfiledFrame = Time.frameCount;
            if (_checkRecorder.Valid) _checkMsFrames.Add(_checkRecorder.LastValue / 1e6);
            if (_focusRecorder.Valid) _focusMsFrames.Add(_focusRecorder.LastValue / 1e6);
            if (_alicRecorder.Valid) _alicMsFrames.Add(_alicRecorder.LastValue / 1e6);

            // T-LIGHT17b: сколько записей intensity и просмотров делает ALIC за кадр.
            if (_alic != null)
            {
                _alicWritesFrames.Add(_alic.WritesThisFrame);
                _alicActiveWritesFrames.Add(_alic.ActiveWritesThisFrame);
                _alicIteratedFrames.Add(_alic.IteratedThisFrame);
                _alicBaseSweepFrames.Add(_alic.BaseSweepThisFrame ? 1.0 : 0.0);
            }
        }

        // ============================================================ F8 / F9

        private static void BeginRebase()
        {
            ApplyPose(_cases[0]);
            ForcePhaseSilent(12f);

            _rebaseSlice = FindByTypeName("GlobalMotionControlledRebaseSlice") as MonoBehaviour;
            if (_rebaseSlice == null)
            {
                Append("REBASE;status=slice_not_found");
                Debug.LogError(Tag + " GlobalMotionControlledRebaseSlice не найден — F8/F9 не проверены.");
                _state = State.Cleanup;
                _stateSince = Now;
                return;
            }

            _rebasePhase = 0;
            _state = State.Rebase;
            _stateSince = Now;
            Debug.Log(Tag + " F8/F9: срез найден, начинаю фазу 0");
        }

        private static void PressKey(Key key)
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                Append("NOTE;error=no_keyboard_device");
                Debug.LogError(Tag + " Keyboard.current == null — реальный ввод невозможен.");
                return;
            }
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType != null) EditorWindow.FocusWindowIfItsOpen(gameViewType);
            var focusedWindow = EditorWindow.focusedWindow;
            bool focused = focusedWindow != null && gameViewType != null && gameViewType.IsInstanceOfType(focusedWindow);
            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            _keyReleaseFrame = Time.frameCount + 2;
            _pressAttempts++;
            _rebaseLines.Add("input=QueueStateEvent(" + key + ");frame=" + Time.frameCount +
                             ";attempt=" + _pressAttempts + ";gameViewFocused=" + focused);
            Append("REBASE_INPUT;key=" + key + ";frame=" + Time.frameCount + ";attempt=" + _pressAttempts +
                   ";gameViewFocused=" + focused);
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

        private static float ReadPrivateFloat(object target, string field)
        {
            if (target == null) return 0f;
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            return f != null && f.FieldType == typeof(float) ? (float)f.GetValue(target) : 0f;
        }

        private static bool TryTeleportPlayerPastThreshold(string tag, out Vector3 newPos, out float predicted)
        {
            newPos = Vector3.zero;
            predicted = 0f;

            if (_playerRoot == null)
            {
                var m = _rebaseSlice.GetType().GetMethod("TryResolveLocalPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
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
                Append("REBASE;step=" + tag + ";offset=" + offsets[i].ToString("F1", CultureInfo.InvariantCulture) +
                       ";threshold=" + threshold.ToString("F0", CultureInfo.InvariantCulture) +
                       ";predictedTranslation=" + predicted.ToString("F2", CultureInfo.InvariantCulture));
                return true;
            }

            Append("REBASE;step=" + tag + ";status=no_plan_for_any_offset;playerPos=" + basePos.ToString("F2"));
            return false;
        }

        private static string LightStateBrief()
        {
            if (_pool == null) return "pool=none";
            return "poolRegistered=" + _pool.RegisteredCount + ";poolActive=" + _pool.ActiveCount +
                   ";poolOut=" + _pool.ActiveOut + ";poolIn=" + _pool.ActiveIn +
                   ";poolFarm=" + _pool.ActiveFarm + ";poolZero=" + _pool.ActiveIntensityZero +
                   ";poolGateDropped=" + _pool.DayGateDropped + ";poolRadius=" + _pool.ActiveRadius +
                   ";poolQuotaDeferred=" + _pool.KindQuotaDeferred + ";poolQuotaFilled=" + _pool.KindQuotaFilled +
                   ";focus=" + _pool.FocusSourceKind + ":" + _pool.FocusSourceName;
        }

        private static void StepRebase()
        {
            var type = _rebaseSlice != null ? _rebaseSlice.GetType() : null;
            if (type == null) { _state = State.Cleanup; _stateSince = Now; return; }

            switch (_rebasePhase)
            {
                case 0: // F8
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
                        _predictedShift = predicted;

                        Append("REBASE;phase=F8_before;frameGen=" + _frameGenBefore +
                               ";worldRoot=" + _rootBefore.ToString("F2") + ";" + LightStateBrief() +
                               ";errorsSoFar=" + _errorCount);

                        _playerRoot.position = newPos;
                        Physics.SyncTransforms();
                        PressKey(Key.F8);
                        _rebasePhase = 1;
                        _rebaseStepFrame = Time.frameCount;
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
                            _f8Pass = false;
                            _rebasePhase = 3;
                            _rebaseStepFrame = Time.frameCount;
                            return;
                        }

                        Vector3 rootAfter = _worldRoot != null ? _worldRoot.position : Vector3.zero;
                        float shift = Vector3.Distance(rootAfter, _rootBefore);
                        _f8Pass = shift > 1f;
                        Append("REBASE;phase=F8_after;f8Pass=" + _f8Pass +
                               ";worldRootShift_m=" + shift.ToString("F3", CultureInfo.InvariantCulture) +
                               ";predictedShift_m=" + _predictedShift.ToString("F3", CultureInfo.InvariantCulture) +
                               ";" + LightStateBrief() +
                               ";frames=" + (Time.frameCount - _rebaseStepFrame) + ";errorsSoFar=" + _errorCount);

                        _rebasePhase = 2;
                        _rebaseStepFrame = Time.frameCount;
                        break;
                    }

                case 2: // F9
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
                        _predictedShift = predicted;
                        _rollbackSeen = 0;
                        _rollbackRestoreTrue = 0;

                        Append("REBASE;phase=F9_before;frameGen=" + _frameGenBefore +
                               ";worldRoot=" + _rootBefore.ToString("F2") + ";" + LightStateBrief() +
                               ";errorsSoFar=" + _errorCount);

                        _playerRoot.position = newPos;
                        Physics.SyncTransforms();
                        PressKey(Key.F9);
                        _rebasePhase = 4;
                        _rebaseStepFrame = Time.frameCount;
                        break;
                    }

                case 4: // ожидание F9 (откат транзакции = успех)
                    {
                        bool stateChanged = FrameGenOf() != _frameGenBefore ||
                                            GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset != _cumulativeBefore;
                        bool settled = stateChanged || _rollbackSeen > 0;

                        if (!settled && Time.frameCount - _rebaseStepFrame < RebaseTimeoutFrames) return;

                        if (!settled && _pressAttempts < 3)
                        {
                            Append("REBASE;phase=F9;status=no_transaction_yet;retry=" + (_pressAttempts + 1));
                            PressKey(Key.F9);
                            _rebaseStepFrame = Time.frameCount;
                            return;
                        }

                        Vector3 rootAfter = _worldRoot != null ? _worldRoot.position : Vector3.zero;
                        float rootShift = Vector3.Distance(rootAfter, _rootBefore);
                        _f9Pass = _rollbackRestoreTrue > 0 && !stateChanged && rootShift < 0.01f;

                        string status = stateChanged && _rollbackSeen == 0
                            ? "FAILED_changed_state_without_rollback"
                            : _f9Pass ? "PASS_rollback_restore"
                            : _rollbackSeen > 0 ? "rollback_without_restore_true" : "FAILED_no_transaction";

                        Append("REBASE;phase=F9_after;status=" + status + ";f9Pass=" + _f9Pass +
                               ";rollbackLogs=" + _rollbackSeen + ";restoreTrue=" + _rollbackRestoreTrue +
                               ";worldRootShift_m=" + rootShift.ToString("F3", CultureInfo.InvariantCulture) +
                               ";predictedShift_m=" + _predictedShift.ToString("F3", CultureInfo.InvariantCulture) +
                               ";" + LightStateBrief() +
                               ";frames=" + (Time.frameCount - _rebaseStepFrame) + ";errorsSoFar=" + _errorCount);

                        _rebasePhase = 3;
                        _rebaseStepFrame = Time.frameCount;
                        break;
                    }

                case 3: // итог
                    {
                        Append("REBASE;phase=summary;f8Pass=" + _f8Pass + ";f9Pass=" + _f9Pass +
                               ";pressAttempts=" + _pressAttempts + ";rollbackLogs=" + _rollbackSeen +
                               ";restoreTrue=" + _rollbackRestoreTrue +
                               ";errors=" + _errorCount + ";exceptions=" + _exceptionCount +
                               ";shaderErrors=" + _shaderErrorCount + ";" + LightStateBrief());
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

        private static void RestoreRuntime()
        {
            for (int i = 0; i < _rigBehavioursDisabled.Count; i++)
                if (_rigBehavioursDisabled[i] != null) _rigBehavioursDisabled[i].enabled = true;
            _rigBehavioursDisabled.Clear();

            if (_dayNight != null) _dayNight.enabled = _dayNightWasEnabled;

            if (_cam != null)
            {
                _cam.farClipPlane = _camFarBefore;
                _cam.fieldOfView = _camFovBefore;
                _cam.nearClipPlane = _camNearBefore;
                _cam.transform.SetPositionAndRotation(_camPosBefore, _camRotBefore);
            }

            if (_playerRoot != null) _playerRoot.position = _playerBefore;
        }

        private static void Finish(string result, bool keepPlaying)
        {
            EditorApplication.update -= Tick;
            UnhookLog();
            if (_checkRecorder.Valid) _checkRecorder.Dispose();
            if (_focusRecorder.Valid) _focusRecorder.Dispose();
            if (_alicRecorder.Valid) _alicRecorder.Dispose();

            Append("FINAL_ERRORS;errors=" + _errorCount + ";exceptions=" + _exceptionCount +
                   ";shaderErrors=" + _shaderErrorCount + ";warnings=" + _warnCount);
            for (int i = 0; i < _errorLines.Count && i < 30; i++)
                Append("ERROR_LINE;" + _errorLines[i].Replace("\n", " | "));

            Append("BENCH_DONE;result=" + result +
                   ";casesCompleted=" + _caseIndex + "/" + _cases.Count +
                   ";f8Pass=" + _f8Pass + ";f9Pass=" + _f9Pass +
                   ";csv=" + (_outCsvPath ?? string.Empty) +
                   ";sceneSaved=no;push=no;hold=" + (keepPlaying ? "yes" : "no"));

            Debug.Log(Tag + " Завершено: " + result + ". Файл: " + _outRawPath);

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
            catch (Exception e) { Debug.LogWarning(Tag + " Не удалось записать строку: " + e.Message); }
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

            if (condition.Contains("[T-FO06CY]"))
            {
                if (condition.Contains("rolled back"))
                {
                    _rollbackSeen++;
                    if (condition.Contains("restore=True")) _rollbackRestoreTrue++;
                }
                if (_rebaseLines.Count < 60) _rebaseLines.Add(condition);
            }

            if (type == LogType.Warning)
            {
                _warnCount++;
                return;
            }

            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;

            // Шум старта/остановки игры не относится к свету: фиксируем, но не валим гейт.
            if (condition.Contains("[PadStateSync]")) return;

            _errorCount++;
            if (type == LogType.Exception) _exceptionCount++;
            if (condition.IndexOf("shader", StringComparison.OrdinalIgnoreCase) >= 0) _shaderErrorCount++;
            if (_errorLines.Count < 40) _errorLines.Add(type + ": " + condition);
        }

        // ------------------------------------------------------------- утилиты

        private static int Median(List<int> v)
        {
            if (v == null || v.Count == 0) return 0;
            var copy = new List<int>(v);
            copy.Sort();
            return copy[copy.Count / 2];
        }

        private static double MedianD(List<double> v)
        {
            if (v == null || v.Count == 0) return 0.0;
            var copy = new List<double>(v);
            copy.Sort();
            return copy[copy.Count / 2];
        }

        private static double Avg(List<double> v)
        {
            if (v == null || v.Count == 0) return 0.0;
            double s = 0.0;
            for (int i = 0; i < v.Count; i++) s += v[i];
            return s / v.Count;
        }

        private static double Sum(List<double> v)
        {
            if (v == null || v.Count == 0) return 0.0;
            double s = 0.0;
            for (int i = 0; i < v.Count; i++) s += v[i];
            return s;
        }
    }
}
