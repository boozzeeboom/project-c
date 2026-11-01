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

        // T-CULL01 / Stage 6 (H6): тот же прогон (Host → риг → прогрев → точки → F8/F9), но A/B — по
        // VisibilityDirector (CityShadowBudget везде включён), плюс PILOT, culling ms и статистика
        // директора. Меню: Project C → Culling → Stage 6 Director → Run.
        private const string Stage6RunMenu = "Project C/Culling/Stage 6 Director/Run";
        private const string Stage6AbortMenu = "Project C/Culling/Stage 6 Director/Abort";
        private const string ProfileKey = SessionKey + ".stage6";
        private const string PilotChunkName = "PRIMUM_HOUSES_S9_TRD-S2_P1_45H";
        private const float PilotCamDistance = 58f;
        private const float PilotCamHeight = 24f;
        private static bool _stage6;

        // T-CULL01 / Stage 7 (H7): та же методика, A/B по VisibilityDirector.HlodEnabled; камера на 0.5/3/5/10/20/40/80 км
        // от центра города (манифест HLOD), атрибуция города = DC − DC(city_off), ночь, после F8/F9 —
        // повтор дальних точек. Меню: Project C → Culling → Stage 7 HLOD → Run Bench.
        private const string Stage7RunMenu = "Project C/Culling/Stage 7 HLOD/Run Bench";
        private const string Stage7AbortMenu = "Project C/Culling/Stage 7 HLOD/Abort Bench";
        private const string Profile7Key = SessionKey + ".stage7";
        private const float FarBench120 = 120000f;
        private static bool _stage7;

        // T-CULL01 / Stage 8 (H8): та же методика и тот же кодовый путь, что у Stage 7 (_stage7 = true),
        // но A/B — по дистанционной политике дверей VisibilityDirector.DoorCullEnabled, а в кадре
        // считаются двери нарисованные / погашенные / нарисованные дальше 150 м.
        // Меню: Project C → Culling → Stage 8 Doors → Run Bench.
        private const string Stage8RunMenu = "Project C/Culling/Stage 8 Doors/Run Bench";
        private const string Stage8AbortMenu = "Project C/Culling/Stage 8 Doors/Abort Bench";
        private const string Profile8Key = SessionKey + ".stage8";
        private const float DoorGateDistance = 150f;
        private static bool _stage8;
        private static bool _postPhase;
        private static bool Profiled { get { return _stage6 || _stage7; } }

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
            public bool directorOn = true;
            public bool pilot;
            // Stage 7
            public bool hlodOn = true;
            public bool cityOff;
            public float distance;
            public float azimuth = 200f;
            public float far;
            public bool shot;
            public bool post;
            public bool fogOff;
            public float elevation = 2f;
            // Stage 8
            public bool doorCullOn = true;
        }

        private static BenchCase Dist(string id, float d, bool hlod, bool cityOff = false, float hour = 12f, bool shot = false, bool post = false, bool doorCull = true)
        {
            return new BenchCase
            {
                id = id, distance = d, hour = hour, shipsVisible = false, budgetOn = true, directorOn = true,
                hlodOn = hlod, cityOff = cityOff, far = d >= 30000f ? FarBench120 : 0f, shot = shot, post = post,
                doorCullOn = doorCull
            };
        }

        private static List<BenchCase> BuildStage8Cases(Vector3 p1)
        {
            var list = new List<BenchCase>
            {
                // L0 в реальной игре: политика on / off (A/B того же кадра).
                new BenchCase { id = "P1_doors_on", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true, shot = true },
                new BenchCase { id = "P1_doors_on_repeat", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true },
                new BenchCase { id = "P1_doors_off", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true, doorCullOn = false, shot = true },
                new BenchCase { id = "P1_real_doors_on", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = true, budgetOn = true },
            };
            // Лестница дистанций: L0 (0.5 км) и границы L1/L2 — проверить, что Stage 7 не сломан.
            list.Add(Dist("D0.5k_doors_on", 500f, true));
            list.Add(Dist("D0.5k_doors_off", 500f, true, doorCull: false));
            list.Add(Dist("D3k_doors_on", 3000f, true));
            list.Add(Dist("D5k_doors_on", 5000f, true, shot: true));
            list.Add(Dist("D5k_doors_off", 5000f, true, doorCull: false));
            list.Add(Dist("D10k_doors_on", 10000f, true));
            list.Add(Dist("D10k_doors_on_repeat", 10000f, true));
            list.Add(Dist("D20k_doors_on", 20000f, true));
            // Атрибуция города (DC − DC(city_off)) для гейтов L1 46–48 / L2 2 — как в Stage 7.
            list.Add(Dist("D0.5k_city_off", 500f, true, cityOff: true));
            list.Add(Dist("D10k_city_off", 10000f, true, cityOff: true));
            list.Add(Dist("D20k_city_off", 20000f, true, cityOff: true));
            list.Add(Dist("N0.5k_doors_on", 500f, true, hour: 0f, shot: true));
            list.Add(Dist("N5k_doors_on", 5000f, true, hour: 0f, shot: true));
            // После F8/F9: двери едут с миром, политика не залипает на старой позиции.
            list.Add(new BenchCase { id = "P1_doors_on_postFO", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true, post = true });
            list.Add(Dist("D10k_doors_on_postFO", 10000f, true, post: true));
            return list;
        }

        private static List<BenchCase> BuildStage7Cases(Vector3 p1)
        {
            var list = new List<BenchCase>
            {
                new BenchCase { id = "P1_hlod_on", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true, shot = true },
                new BenchCase { id = "P1_hlod_on_repeat", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true },
                new BenchCase { id = "P1_hlod_off", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true, hlodOn = false },
                new BenchCase { id = "P1_real_hlod_on", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = true, budgetOn = true },
            };
            float[] ds = { 500f, 3000f, 5000f, 10000f, 20000f, 40000f, 80000f };
            string[] tags = { "D0.5k", "D3k", "D5k", "D10k", "D20k", "D40k", "D80k" };
            for (int i = 0; i < ds.Length; i++)
            {
                bool shotOn = ds[i] >= 3000f;
                bool shotOff = ds[i] >= 3000f && ds[i] <= 20000f;
                list.Add(Dist(tags[i] + "_hlod_on", ds[i], true, shot: shotOn));
                list.Add(Dist(tags[i] + "_hlod_off", ds[i], false, shot: shotOff));
                list.Add(Dist(tags[i] + "_city_off", ds[i], true, cityOff: true));
            }
            list.Add(Dist("D10k_hlod_on_repeat", 10000f, true));
            list.Add(Dist("D20k_hlod_on_repeat", 20000f, true));
            list.Add(Dist("N5k_hlod_on", 5000f, true, hour: 0f, shot: true));
            list.Add(Dist("N10k_hlod_on", 10000f, true, hour: 0f, shot: true));
            list.Add(Dist("N10k_hlod_off", 10000f, false, hour: 0f, shot: true));
            list.Add(Dist("N20k_hlod_on", 20000f, true, hour: 0f, shot: true));
            list.Add(Dist("N20k_hlod_off", 20000f, false, hour: 0f, shot: true));
            // Диагностика силуэта: туман сцены выключен (только runtime, возвращается), камера 8° — иначе
            // с 3+ км город скрыт туманом и рельефом и L0/L1/L2 нельзя сравнить глазом.
            float[] vd = { 5000f, 10000f, 20000f, 40000f };
            string[] vt = { "V5k", "V10k", "V20k", "V40k" };
            for (int i = 0; i < vd.Length; i++)
            {
                var on = Dist(vt[i] + "_hlod_on", vd[i], true, shot: true); on.fogOff = true; on.elevation = 8f; list.Add(on);
                var off = Dist(vt[i] + "_hlod_off", vd[i], false, shot: true); off.fogOff = true; off.elevation = 8f; list.Add(off);
            }
            var n10 = Dist("VN10k_hlod_on", 10000f, true, hour: 0f, shot: true); n10.fogOff = true; n10.elevation = 8f; list.Add(n10);
            var n10o = Dist("VN10k_hlod_off", 10000f, false, hour: 0f, shot: true); n10o.fogOff = true; n10o.elevation = 8f; list.Add(n10o);
            var n20 = Dist("VN20k_hlod_on", 20000f, true, hour: 0f, shot: true); n20.fogOff = true; n20.elevation = 8f; list.Add(n20);
            var n20o = Dist("VN20k_hlod_off", 20000f, false, hour: 0f, shot: true); n20o.fogOff = true; n20o.elevation = 8f; list.Add(n20o);
            // После F8/F9: те же точки — HLOD/импостор едут с миром, DC тот же.
            list.Add(new BenchCase { id = "P1_hlod_on_postFO", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true, post = true });
            list.Add(Dist("D10k_hlod_on_postFO", 10000f, true, post: true, shot: true));
            list.Add(Dist("D20k_hlod_on_postFO", 20000f, true, post: true, shot: true));
            return list;
        }

        private static List<BenchCase> BuildCases()
        {
            var p1 = new Vector3(-342.90f, -50.43f, -102.20f);
            var c2 = new Vector3(-382.90f, -58.23f, -126.20f);
            var c3 = new Vector3(-416.00f, -27.94f, -36.00f);
            if (_stage8) return BuildStage8Cases(p1);
            if (_stage7) return BuildStage7Cases(p1);
            if (_stage6)
            {
                return new List<BenchCase>
                {
                    new BenchCase { id = "P1_dir_on", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true, directorOn = true },
                    new BenchCase { id = "P1_dir_on_repeat", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true, directorOn = true },
                    new BenchCase { id = "P1_dir_off", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = false, budgetOn = true, directorOn = false },
                    new BenchCase { id = "P1_real_dir_on", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = true, budgetOn = true, directorOn = true },
                    new BenchCase { id = "P1_real_dir_off", local = p1, yaw = 337.5f, hour = 12f, shipsVisible = true, budgetOn = true, directorOn = false },
                    new BenchCase { id = "C2_dir_on", local = c2, yaw = 225f, hour = 12f, shipsVisible = false, budgetOn = true, directorOn = true },
                    new BenchCase { id = "C2_dir_off", local = c2, yaw = 225f, hour = 12f, shipsVisible = false, budgetOn = true, directorOn = false },
                    new BenchCase { id = "C3_dir_on", local = c3, yaw = 132f, pitch = -18f, hour = 12f, shipsVisible = false, budgetOn = true, directorOn = true },
                    new BenchCase { id = "C3_dir_off", local = c3, yaw = 132f, pitch = -18f, hour = 12f, shipsVisible = false, budgetOn = true, directorOn = false },
                    new BenchCase { id = "PILOT_dir_off", local = Vector3.zero, hour = 12f, shipsVisible = false, budgetOn = true, directorOn = false, pilot = true },
                    new BenchCase { id = "PILOT_dir_on", local = Vector3.zero, hour = 12f, shipsVisible = false, budgetOn = true, directorOn = true, pilot = true },
                    new BenchCase { id = "PILOT_dir_on_repeat", local = Vector3.zero, hour = 12f, shipsVisible = false, budgetOn = true, directorOn = true, pilot = true },
                    new BenchCase { id = "PILOT_dir_off_repeat", local = Vector3.zero, hour = 12f, shipsVisible = false, budgetOn = true, directorOn = false, pilot = true },
                };
            }
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
        private static List<BenchCase> _cases = BuildCases();
        private static int _caseIndex;

        private static readonly List<int> _dc = new List<int>();
        private static readonly List<int> _spc = new List<int>();
        private static readonly List<int> _tris = new List<int>();
        private static readonly List<int> _sc = new List<int>();
        private static readonly List<int> _hybridDC = new List<int>();
        private static readonly List<int> _hybridInst = new List<int>();
        private static readonly List<int> _srpDC = new List<int>();
        private static readonly List<double> _ms = new List<double>();

        // Stage 6: culling ms (ProfilerRecorder) — покадрово в окне замера точки.
        private static Unity.Profiling.ProfilerRecorder _cullRecorder;
        private static Unity.Profiling.ProfilerRecorder _dirRecorder;
        private static readonly List<double> _cullMsFrames = new List<double>();
        private static readonly List<double> _dirMsFrames = new List<double>();
        private static int _lastProfiledFrame = -1;
        private static readonly List<string> _directorLogLines = new List<string>();

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
        private static int _censusDoorsTotal;
        private static int _censusDoorsForcedOffTotal;
        private static int _censusDoorsForcedOffInFrustum;
        private static int _censusDoorsDrawnInFrustum;
        private static int _censusDoorsDrawnBeyond;
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

        // F9 — это откат транзакции: успех = `rolled back ...;restore=True`, а не смена frameGen.
        // Счётчики запоминаются на момент нажатия, поэтому отфильтрованы от прежних авто-rebase.
        private static int _rollbackSeen;
        private static int _rollbackRestoreTrue;
        private static int _rebaseCompleted;
        private static bool _f9Pass;

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
            _stage6 = false;
            _stage7 = false;
            _stage8 = false;
            StartRun();
        }

        [MenuItem(Stage6RunMenu)]
        public static void RunStage6()
        {
            _stage6 = true;
            _stage7 = false;
            _stage8 = false;
            StartRun();
        }

        [MenuItem(Stage7RunMenu)]
        public static void RunStage7()
        {
            _stage6 = false;
            _stage7 = true;
            _stage8 = false;
            StartRun();
        }

        [MenuItem(Stage8RunMenu)]
        public static void RunStage8()
        {
            _stage6 = false;
            _stage7 = true;   // тот же кодовый путь (HLOD + гейт меню + F8/F9)
            _stage8 = true;
            StartRun();
        }

        [MenuItem(Stage8AbortMenu)]
        public static void AbortStage8()
        {
            Finish("ABORTED_BY_USER", false);
        }

        [MenuItem(Stage7AbortMenu)]
        public static void AbortStage7()
        {
            Finish("ABORTED_BY_USER", false);
        }

        [MenuItem(Stage6AbortMenu)]
        public static void AbortStage6()
        {
            Finish("ABORTED_BY_USER", false);
        }

        private static string FilePrefix { get { return _stage8 ? "V_stage8_" : _stage7 ? "U_stage7_" : _stage6 ? "S_stage6_" : "R_stage4c_"; } }

        private static void StartRun()
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
            _cases = BuildCases();
            _postPhase = false;
            _cityOffDisabled.Clear();
            _director = null;
            _directorFinalWritten = false;
            _outRawPath = Path.Combine(dataDir, FilePrefix + DateTag + "_raw.txt");
            _outCsvPath = Path.Combine(dataDir, FilePrefix + DateTag + ".csv");
            _directorLogLines.Clear();
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
            SessionState.SetBool(ProfileKey, _stage6);
            SessionState.SetBool(Profile7Key, _stage7);
            SessionState.SetBool(Profile8Key, _stage8);
            SessionState.SetInt(SessionKey + ".reloads", 0);

            Append("NOTE;bench=FinalValidationBench(" + (_stage8 ? "stage8_doors" : _stage7 ? "stage7_hlod" : _stage6 ? "stage6_director" : "stage4c") + ");unity=" + Application.unityVersion +
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
                    if (Profiled) AppendStage6Gate();
                    if (_state == State.Off) return; // гейт «в игре» не пройден — прогон уже завершён
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
            // Новое главное меню (ProjectCMainMenu): тот же путь, что нажатие «ОДИНОЧНАЯ ИГРА» —
            // OnMenuAction("host") → StartHost() → _awaitingStart; меню и его AtmosphereCamera уходят сами,
            // когда локальный игрок заспавнен. Раньше бенч звал legacy MainMenuWindow.OnHostClicked():
            // игра стартовала, но новое меню оставалось на экране (его _awaitingStart не взводился).
            var pcmm = FindByTypeName("ProjectCMainMenuUiHost");
            if (pcmm != null)
            {
                if (!_hostRequested)
                {
                    var action = pcmm.GetType().GetMethod("OnMenuAction", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (action == null) return false;
                    _hostRequested = true;
                    action.Invoke(pcmm, new object[] { "host", "ОДИНОЧНАЯ ИГРА" });
                    Debug.Log("[FinalValidation] Host запрошен через ProjectCMainMenuUiHost.OnMenuAction(\"host\") (как кнопка «ОДИНОЧНАЯ ИГРА»)");
                    Append("NOTE;host_start=ProjectCMainMenuUiHost.OnMenuAction(host)");
                    return false;
                }
                // Ждём, пока меню само уйдёт (игрок заспавнен) и появится риг камеры игрока.
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

        private static bool PcmmShown(Component pcmm)
        {
            if (pcmm == null) return false;
            var p = pcmm.GetType().GetProperty("IsShown", BindingFlags.Instance | BindingFlags.Public);
            return p != null && (bool)p.GetValue(pcmm);
        }

        /// <summary>Состояние нового меню: (показано, камера атмосферы включена). Нет меню — (false,false).</summary>
        private static void ReadPcmmState(out bool shown, out bool atmosphereCamera)
        {
            var pcmm = FindByTypeName("ProjectCMainMenuUiHost");
            shown = PcmmShown(pcmm);
            atmosphereCamera = false;
            var atm = FindByTypeName("ProjectCMainMenuAtmosphere");
            if (atm == null) return;
            var p = atm.GetType().GetProperty("Camera", BindingFlags.Instance | BindingFlags.Public);
            var cam = p != null ? p.GetValue(atm) as Camera : null;
            atmosphereCamera = cam != null && cam.isActiveAndEnabled;
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
            if (Profiled) StartProfilers();

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

            // Паритет кандидатов (Stage 4c): независимый скан в тех же правилах, но БЕЗ Off-фильтра.
            // Внимание: скан идёт после прогрева, когда бюджет уже погасил большинство теней,
            // поэтому режим Off сам по себе ничего не говорит о «авторском» Off. Единственный
            // корректный счётчик — принадлежность списку компонента; он считается ниже.
            int liveAll = 0;
            for (int i = 0; i < _allRenderers.Length; i++)
            {
                var r = _allRenderers[i];
                if (r == null || !r.enabled) continue;
                if (IsShipRenderer(r)) continue;
                if (r.gameObject.layer == DoorLayer) continue;
                liveAll++;
            }

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

            // Паритет: liveScan = entries + off_not_in_entries + on_not_in_entries.
            var entrySet = new HashSet<Renderer>(_cityCandidates);
            int offNotInEntries = 0, onNotInEntries = 0;
            for (int i = 0; i < _allRenderers.Length; i++)
            {
                var r = _allRenderers[i];
                if (r == null || !r.enabled) continue;
                if (IsShipRenderer(r)) continue;
                if (r.gameObject.layer == DoorLayer) continue;
                if (entrySet.Contains(r)) continue;
                if (r.shadowCastingMode == ShadowCastingMode.Off) offNotInEntries++;
                else onNotInEntries++;
            }

            Debug.Log("[FinalValidation] Рендереров всего=" + _allRenderers.Length +
                      ", корабельных=" + _shipRenderers.Length +
                      ", кандидатов бюджета (наш расчёт)=" + _cityCandidates.Count +
                      ", component entries=" + _budgetEntriesCount +
                      ", liveScan=" + liveAll + "(offNotInEntries=" + offNotInEntries +
                      ", onNotInEntries=" + onNotInEntries + ")" +
                      ", ship roots=" + _shipRoots.Length);
            Append("NOTE;renderers;all=" + _allRenderers.Length + ";ships=" + _shipRenderers.Length +
                   ";shipRoots=" + _shipRoots.Length + ";ourCityCandidates=" + _cityCandidates.Count +
                   ";componentEntries=" + _budgetEntriesCount +
                   ";liveScanSameRules=" + liveAll + ";offNotInEntries=" + offNotInEntries +
                   ";onNotInEntries=" + onNotInEntries +
                   ";parityNote=scan_after_warmup_budget_already_cut");
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

        // ------------------------------------------------------------ Stage 6 (H6)

        private static VisibilityDirector _director;

        private static VisibilityDirector FindDirector()
        {
            if (_director == null) _director = UnityEngine.Object.FindAnyObjectByType<VisibilityDirector>(FindObjectsInactive.Include);
            return _director;
        }

        /// <summary>A/B директора: выключение компонента возвращает forceRenderingOff = false всем (OnDisable).</summary>
        private static void SetDirectorEnabled(bool on)
        {
            var d = FindDirector();
            if (d == null) return;
            if (d.enabled != on) d.enabled = on;
        }

        private static string DirectorBrief()
        {
            var d = FindDirector();
            if (d == null) return "director=absent";
            string s = "dirEnabled=" + d.enabled + ";dirGroups=" + d.GroupCount + ";dirMembers=" + d.DirectedMemberCount +
                       ";dirCulled=" + d.CulledCount + ";dirCameras=" + d.CameraCount +
                       ";dirRebaseResets=" + d.RebaseResets + ";dirRollbackResets=" + d.RollbackResets +
                       ";dirTeleportResets=" + d.TeleportResets;
            if (_stage7)
            {
                var root = d.HlodRoot;
                string rootState = root == null
                    ? "none"
                    : (root.parent != null ? root.parent.name : "no_parent") + "@" + root.localPosition.ToString("F2") +
                      "|worldDelta=" + (_worldRoot != null ? (root.position - _worldRoot.position).magnitude.ToString("F3", CultureInfo.InvariantCulture) : "-");
                s += ";hlod=" + d.HlodEnabled + ";hlodBlocks=" + d.HlodBlockCount + ";impostor=" + d.HlodImpostorPresent +
                     ";cityLevel=" + d.CityLevel + ";levels=" + d.HlodLevelCounts.Replace(',', '/') +
                     ";hlodHidden=" + d.HlodHiddenCount + ";hlodDoorsHidden=" + d.HlodDoorsHidden + "/" + d.HlodDoorCount +
                     ";doorCull=" + d.DoorCullEnabled + ";doorTotal=" + d.DoorCullTotal + ";doorHiddenDist=" + d.DoorCullHiddenByDistance +
                     ";night=" + d.NightFactor.ToString("F2", CultureInfo.InvariantCulture) +
                     ";fog=" + RenderSettings.fog +
                     ";hlodRoot=" + rootState;
            }
            return s;
        }

        // ------------------------------------------------------------ Stage 7 (H7)

        private static readonly List<Renderer> _cityOffDisabled = new List<Renderer>();
        private static PrimumHlodManifest _hlodManifest;
        private static bool _fogSaved;
        private static bool _fogOriginal;

        /// <summary>Диагностические кадры: RenderSettings.fog выключается только на время точки и возвращается.</summary>
        private static void SetFogOff(bool off)
        {
            if (off)
            {
                if (!_fogSaved) { _fogOriginal = RenderSettings.fog; _fogSaved = true; }
                RenderSettings.fog = false;
                return;
            }
            if (_fogSaved) { RenderSettings.fog = _fogOriginal; _fogSaved = false; }
        }

        private static void SetHlod(bool on)
        {
            var d = FindDirector();
            if (d != null) d.HlodEnabled = on;
        }

        /// <summary>A/B дистанционной политики дверей (Stage 8): тот же директор, только флаг политики.</summary>
        private static void SetDoorCull(bool on)
        {
            var d = FindDirector();
            if (d != null) d.DoorCullEnabled = on;
        }

        /// <summary>Атрибуция города: runtime renderer.enabled у PRIMUM / PRIMUM_DOORS / PRIMUM_HLOD (сцена не пишется).</summary>
        private static void SetCityVisible(bool visible)
        {
            if (visible)
            {
                for (int i = 0; i < _cityOffDisabled.Count; i++)
                    if (_cityOffDisabled[i] != null) _cityOffDisabled[i].enabled = true;
                _cityOffDisabled.Clear();
                return;
            }
            if (_cityOffDisabled.Count > 0 || _worldRoot == null) return;
            string[] roots = { "PRIMUM", "PRIMUM_DOORS", VisibilityDirector.HlodRootName };
            for (int k = 0; k < roots.Length; k++)
            {
                var t = _worldRoot.Find(roots[k]);
                if (t == null) continue;
                var rs = t.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rs.Length; i++)
                {
                    if (rs[i] == null || !rs[i].enabled) continue;
                    rs[i].enabled = false;
                    _cityOffDisabled.Add(rs[i]);
                }
            }
        }

        /// <summary>Камера на дистанции d от центра города (манифест HLOD), азимут az, высота 60 м + 2°, взгляд на центр.</summary>
        private static bool TryCityPose(BenchCase c, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            if (_worldRoot == null) return false;
            if (_hlodManifest == null) _hlodManifest = Resources.Load<PrimumHlodManifest>(PrimumHlodManifest.ResourcePath);
            if (_hlodManifest == null) return false;
            Vector3 center = _worldRoot.position + _hlodManifest.cityCenter;
            float az = c.azimuth * Mathf.Deg2Rad;
            Vector3 h = new Vector3(Mathf.Sin(az), 0f, Mathf.Cos(az));
            pos = center + h * c.distance + Vector3.up * (60f + c.distance * Mathf.Tan(c.elevation * Mathf.Deg2Rad));
            rot = Quaternion.LookRotation((center - pos).normalized, Vector3.up);
            return true;
        }

        private static void RequestShot(BenchCase c)
        {
            if (!_stage7 || !c.shot) return;
            string dir = Path.GetDirectoryName(_outRawPath);
            string path = Path.Combine(dir, FilePrefix + "shot_" + c.id + ".png");
            try
            {
                ScreenCapture.CaptureScreenshot(path);
                Append("SHOT;id=" + c.id + ";path=" + path.Replace('\\', '/'));
            }
            catch (Exception e) { Append("SHOT;id=" + c.id + ";error=" + e.Message); }
        }

        private static int FirstPostIndex()
        {
            for (int i = 0; i < _cases.Count; i++) if (_cases[i].post) return i;
            return -1;
        }

        private static bool _directorFinalWritten;

        /// <summary>Итог директора — пишется, пока Play Mode ещё идёт (после выхода компонент уничтожен).</summary>
        private static void AppendDirectorFinal()
        {
            if (_directorFinalWritten) return;
            var d = FindDirector();
            if (d == null) return;
            _directorFinalWritten = true;
            Append("DIRECTOR_FINAL;" + DirectorBrief() +
                   ";groups=" + d.DescribeGroups() + ";cameras=" + d.CameraReport + ";providers=" + d.ProviderReport +
                   ";ownershipConflicts=" + d.OwnershipConflicts +
                   ";tickAvgMs=" + d.AvgTickMs.ToString("F4", CultureInfo.InvariantCulture) +
                   ";tickMaxMs=" + d.MaxTickMs.ToString("F4", CultureInfo.InvariantCulture) + ";ticks=" + d.TickCount);
        }

        /// <summary>PILOT: камера в 24 м над и 58 м перед центром чанка S9_TRD-S2 (как Stage4GrBench), позиции живые.</summary>
        private static bool TryPilotPose(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            if (_worldRoot == null) return false;
            var city = _worldRoot.Find("PRIMUM");
            var chunk = city != null ? city.Find(PilotChunkName) : null;
            if (chunk == null) return false;

            var rs = chunk.GetComponentsInChildren<Renderer>(false);
            bool has = false;
            Bounds b = default(Bounds);
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null || rs[i].gameObject.layer == DoorLayer) continue;
                if (!has) { b = rs[i].bounds; has = true; }
                else b.Encapsulate(rs[i].bounds);
            }
            if (!has) return false;

            Vector3 target = b.center;
            pos = target + new Vector3(0f, PilotCamHeight, -PilotCamDistance);
            rot = Quaternion.LookRotation((target - pos).normalized, Vector3.up);
            return true;
        }

        private static void StartProfilers()
        {
            string[] cullNames = { "CullScriptable", "Culling", "SceneCulling" };
            for (int i = 0; i < cullNames.Length && !_cullRecorder.Valid; i++)
            {
                var r = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, cullNames[i]);
                if (r.Valid) { _cullRecorder = r; Append("NOTE;cullRecorder=" + cullNames[i]); }
                else r.Dispose();
            }
            if (!_cullRecorder.Valid) Append("NOTE;cullRecorder=invalid");

            _dirRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, VisibilityDirector.ProfilerMarkerName);
            Append("NOTE;directorRecorder=" + VisibilityDirector.ProfilerMarkerName + ";valid=" + _dirRecorder.Valid);
        }

        /// <summary>Покадрово: движковый culling (CullScriptable) и тик директора, мс (LastValue = прошлый кадр).</summary>
        private static void SampleProfilers()
        {
            if (!Profiled || Time.frameCount == _lastProfiledFrame) return;
            _lastProfiledFrame = Time.frameCount;
            if (_cullRecorder.Valid) _cullMsFrames.Add(_cullRecorder.LastValue / 1e6);
            if (_dirRecorder.Valid) _dirMsFrames.Add(_dirRecorder.LastValue / 1e6);
        }

        /// <summary>
        /// Гейт «мы в игре, а не в главном меню»: риг ThirdPersonCamera, локальный игрок, живая сессия и
        /// скрытое MainMenuWindow. Если меню всё ещё видно — скрыть и записать это в raw.
        /// </summary>
        private static void AppendStage6Gate()
        {
            bool menuVisible = false;
            var menu = FindByTypeName("MainMenuWindow");
            if (menu != null)
            {
                var f = menu.GetType().GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
                var root = f != null ? f.GetValue(menu) as UnityEngine.UIElements.VisualElement : null;
                menuVisible = root != null && root.style.display.value != UnityEngine.UIElements.DisplayStyle.None;
                if (menuVisible)
                {
                    var hide = menu.GetType().GetMethod("Hide", BindingFlags.Instance | BindingFlags.Public);
                    if (hide != null) hide.Invoke(menu, null);
                }
            }

            string liveSession = "unknown";
            var types = typeof(VisibilityDirector).Assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i].Name != "UIManager") continue;
                var p = types[i].GetProperty("HasLiveGameplaySession", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null) liveSession = Convert.ToString(p.GetValue(null), CultureInfo.InvariantCulture);
                break;
            }

            bool player = false;
            var slice = FindByTypeName("GlobalMotionControlledRebaseSlice");
            if (slice != null)
            {
                var m = slice.GetType().GetMethod("TryResolveLocalPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
                if (m != null)
                {
                    object[] args = { null, null };
                    player = (bool)m.Invoke(slice, args) && args[0] is Transform;
                }
            }

            bool pcmmShown, atmCam;
            ReadPcmmState(out pcmmShown, out atmCam);

            Append("GATE;inGame;rig=" + (_cam != null ? _cam.name : "none") + ";localPlayer=" + player +
                   ";liveSession=" + liveSession + ";mainMenuVisibleAtWarmupEnd=" + menuVisible +
                   (menuVisible ? ";action=menu_hidden_again" : "") +
                   ";pcmmMenuShown=" + pcmmShown + ";pcmmAtmosphereCamera=" + atmCam +
                   ";cursorLock=" + UnityEngine.Cursor.lockState + ";" + DirectorBrief());
            if (_stage7 && (pcmmShown || atmCam || !player))
            {
                Debug.LogError("[FinalValidation] GATE FAIL: новое главное меню ещё на экране (shown=" + pcmmShown +
                               ", atmosphereCamera=" + atmCam + ", player=" + player + ") — замер не в игре, остановлен.");
                Append("GATE;result=FAIL_menu_or_no_player");
                Finish("GATE_FAIL_MENU", false);
                return;
            }
            var d = FindDirector();
            if (d != null)
                Append("DIRECTOR;groups=" + d.DescribeGroups() + ";cameras=" + d.CameraReport + ";providers=" + d.ProviderReport +
                       ";ownershipConflicts=" + d.OwnershipConflicts + ";policy=" + d.PolicyEnabled +
                       ";minPx=" + d.MinScreenPixels.ToString("F1", CultureInfo.InvariantCulture));
            Debug.Log("[FinalValidation] GATE: rig=" + (_cam != null ? _cam.name : "none") + ", player=" + player +
                      ", liveSession=" + liveSession + ", menuVisible=" + menuVisible);
        }

        private static double Avg(List<double> v)
        {
            if (v == null || v.Count == 0) return 0.0;
            double s = 0.0;
            for (int i = 0; i < v.Count; i++) s += v[i];
            return s / v.Count;
        }

        private static double Max(List<double> v)
        {
            double m = 0.0;
            if (v == null) return m;
            for (int i = 0; i < v.Count; i++) if (v[i] > m) m = v[i];
            return m;
        }

        // ------------------------------------------------------------------- точки

        private static void BeginCase(int index)
        {
            _caseIndex = index;
            if (_caseIndex >= _cases.Count)
            {
                if (_postPhase) { _state = State.Cleanup; _stateSince = Now; }
                else BeginRebase();
                return;
            }

            var c = _cases[_caseIndex];
            if (_stage7 && c.post && !_postPhase) { BeginRebase(); return; }
            SetShipsVisible(c.shipsVisible);
            SetBudgetEnabled(c.budgetOn);
            if (_stage6) SetDirectorEnabled(c.directorOn);
            if (_stage7)
            {
                SetDirectorEnabled(true);
                SetHlod(c.hlodOn);
                SetCityVisible(true);
                if (c.cityOff) SetCityVisible(false);
                if (_cam != null) _cam.farClipPlane = c.far > 0f ? c.far : BenchFar;
                if (_stage8) SetDoorCull(c.doorCullOn);
            }
            _cullMsFrames.Clear();
            _dirMsFrames.Clear();
            _lastProfiledFrame = -1;
            ApplyPose(c);
            ForcePhaseSilent(c.hour);
            if (_stage7)
            {
                SetFogOff(false);           // сначала вернуть фазовое состояние тумана
                if (c.fogOff) SetFogOff(true);
            }

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
            if (c.distance > 0f)
            {
                Vector3 cityPos; Quaternion cityRot;
                if (TryCityPose(c, out cityPos, out cityRot))
                {
                    _targetPos = cityPos;
                    _targetRot = cityRot;
                    _cam.transform.SetPositionAndRotation(_targetPos, _targetRot);
                    return;
                }
            }
            if (c.pilot)
            {
                Vector3 pilotPos; Quaternion pilotRot;
                if (TryPilotPose(out pilotPos, out pilotRot))
                {
                    _targetPos = pilotPos;
                    _targetRot = pilotRot;
                    _cam.transform.SetPositionAndRotation(_targetPos, _targetRot);
                    return;
                }
            }
            Vector3 shift = _worldRoot != null ? _worldRoot.position : Vector3.zero;
            _targetPos = c.local + AuthoredWorldOffset + shift;
            _targetRot = Quaternion.Euler(c.pitch, c.yaw, 0f);
            _cam.transform.SetPositionAndRotation(_targetPos, _targetRot);
        }

        private static void StepPoint()
        {
            var c = _cases[_caseIndex];
            ApplyPose(c);
            SampleProfilers();

            if (Time.frameCount < _settleUntilFrame) return;

            if (!_censusDone)
            {
                RunCensus();
                if (_state != State.Point) return;
                _censusDone = true;
                _nextSampleFrame = Time.frameCount;
                RequestShot(c);
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
            _censusDoorsTotal = 0;
            _censusDoorsForcedOffTotal = 0;
            _censusDoorsForcedOffInFrustum = 0;
            _censusDoorsDrawnInFrustum = 0;
            _censusDoorsDrawnBeyond = 0;
            _censusEnabledTotal = 0;
            _censusCity = 0;
            _censusShadowsOff = 0;

            if (_cam == null || _allRenderers == null) return;

            GeometryUtility.CalculateFrustumPlanes(_cam, _planes);
            float doorGateSqr = DoorGateDistance * DoorGateDistance;

            for (int i = 0; i < _allRenderers.Length; i++)
            {
                var r = _allRenderers[i];
                if (r == null || !r.enabled) continue;
                _censusEnabledTotal++;

                bool isDoor = r.gameObject.layer == DoorLayer;
                if (isDoor)
                {
                    _censusDoorsTotal++;
                    if (r.forceRenderingOff) _censusDoorsForcedOffTotal++;
                }

                if (!GeometryUtility.TestPlanesAABB(_planes, r.bounds)) continue;
                _censusInFrustum++;

                if (isDoor)
                {
                    _censusDoorsInFrustum++;
                    // forceRenderingOff снимает инстанс и с GRD, и с обычного пути — «нарисовано» только без него.
                    if (r.forceRenderingOff) _censusDoorsForcedOffInFrustum++;
                    else
                    {
                        _censusDoorsDrawnInFrustum++;
                        if ((r.bounds.center - _targetPos).sqrMagnitude > doorGateSqr) _censusDoorsDrawnBeyond++;
                    }
                }

                if (!IsShipRenderer(r)) _censusCity++;
            }

            for (int i = 0; i < _cityCandidates.Count; i++)
            {
                var r = _cityCandidates[i];
                if (r != null && r.shadowCastingMode == ShadowCastingMode.Off) _censusShadowsOff++;
            }

            if (_censusCity == 0 && !_stage7)
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
            sb.Append(";doorsTotal=").Append(_censusDoorsTotal).Append(";doorsForcedOff=").Append(_censusDoorsForcedOffTotal)
              .Append(";doorsDrawn=").Append(_censusDoorsDrawnInFrustum)
              .Append(";doorsDrawnBeyond150=").Append(_censusDoorsDrawnBeyond)
              .Append(";doorsForcedOffInFrustum=").Append(_censusDoorsForcedOffInFrustum);
            sb.Append(";rawDc=").Append(string.Join("|", _dc.ConvertAll(v => v.ToString()).ToArray()));
            sb.Append(";rawShadowCasters=").Append(string.Join("|", _sc.ConvertAll(v => v.ToString()).ToArray()));
            if (_stage6 || _stage7)
            {
                sb.Append(";director=").Append(c.directorOn ? "on" : "off");
                if (_stage7)
                    sb.Append(";hlodCase=").Append(c.hlodOn ? "on" : "off").Append(";cityOff=").Append(c.cityOff)
                      .Append(";distance_m=").Append(c.distance.ToString("F0", CultureInfo.InvariantCulture))
                      .Append(";far=").Append(_cam != null ? _cam.farClipPlane.ToString("F0", CultureInfo.InvariantCulture) : "-")
                      .Append(";hour=").Append(c.hour.ToString("F1", CultureInfo.InvariantCulture));
                sb.Append(';').Append(DirectorBrief());
                sb.Append(";cullMsAvg=").Append(Avg(_cullMsFrames).ToString("F3", CultureInfo.InvariantCulture));
                sb.Append(";cullMsMax=").Append(Max(_cullMsFrames).ToString("F3", CultureInfo.InvariantCulture));
                sb.Append(";dirMsFrameAvg=").Append(Avg(_dirMsFrames).ToString("F4", CultureInfo.InvariantCulture));
                sb.Append(";dirMsFrameMax=").Append(Max(_dirMsFrames).ToString("F4", CultureInfo.InvariantCulture));
                sb.Append(";profiledFrames=").Append(_cullMsFrames.Count);
            }
            Append(sb.ToString());
            AppendCsv(c, dc, spc, tris, sc, hybridDC, hybridInst, srpDC, ms, fps);

            Debug.Log("[FinalValidation] " + c.id + ": dc=" + dc + " spc=" + spc + " tris=" + tris +
                      " shadowCasters=" + sc + " hybridDC=" + hybridDC + " shadowsOff=" + _censusShadowsOff +
                      " entries=" + _budgetEntriesCount + (Profiled ? " " + DirectorBrief() + " cullMs=" +
                      Avg(_cullMsFrames).ToString("F3", CultureInfo.InvariantCulture) : ""));
        }

        private static void AppendCsv(BenchCase c, int dc, int spc, int tris, int sc, int hybridDC, int hybridInst,
                                      int srpDC, double ms, double fps)
        {
            bool writeHeader = !File.Exists(_outCsvPath);
            var sb = new StringBuilder();
            if (writeHeader)
                sb.AppendLine("run,date,case,ships,budget,budgetEntries,shadowsOff,dc,spc,tris,shadowCasters,hybridDC,hybridInstances,srpBatcherDC,ms,fps,enabledRenderers,inFrustum,cityInFrustum,doorsInFrustum,worldpos" +
                              (_stage6 ? ",director,dirGroups,dirMembers,dirCulled,dirCameras,cullMsAvg,cullMsMax,dirMsFrameAvg,dirMsFrameMax,dirTickMaxMs" : "") +
                              (_stage7 ? ",hlod,cityOff,distance_m,far,hour,cityLevel,levels,hlodHidden,dirCulled,night,cullMsAvg,dirMsFrameAvg,dirTickMaxMs" : "") +
                              (_stage8 ? ",doorCull,doorsTotal,doorsDrawn,doorsDrawnBeyond150,doorsForcedOff,doorHiddenDist" : ""));
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
            if (_stage6)
            {
                var d = FindDirector();
                sb.Append(',').Append(c.directorOn ? "on" : "off")
                  .Append(',').Append(d != null ? d.GroupCount : -1)
                  .Append(',').Append(d != null ? d.DirectedMemberCount : -1)
                  .Append(',').Append(d != null ? d.CulledCount : -1)
                  .Append(',').Append(d != null ? d.CameraCount : -1)
                  .Append(',').Append(Avg(_cullMsFrames).ToString("F3", CultureInfo.InvariantCulture))
                  .Append(',').Append(Max(_cullMsFrames).ToString("F3", CultureInfo.InvariantCulture))
                  .Append(',').Append(Avg(_dirMsFrames).ToString("F4", CultureInfo.InvariantCulture))
                  .Append(',').Append(Max(_dirMsFrames).ToString("F4", CultureInfo.InvariantCulture))
                  .Append(',').Append(d != null ? d.MaxTickMs.ToString("F4", CultureInfo.InvariantCulture) : "-1");
            }
            if (_stage7)
            {
                var d = FindDirector();
                sb.Append(',').Append(c.hlodOn ? "on" : "off")
                  .Append(',').Append(c.cityOff ? 1 : 0)
                  .Append(',').Append(c.distance.ToString("F0", CultureInfo.InvariantCulture))
                  .Append(',').Append(_cam != null ? _cam.farClipPlane.ToString("F0", CultureInfo.InvariantCulture) : "-1")
                  .Append(',').Append(c.hour.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(',').Append(d != null ? d.CityLevel : -1)
                  .Append(',').Append(d != null ? d.HlodLevelCounts.Replace(',', '/') : "-")
                  .Append(',').Append(d != null ? d.HlodHiddenCount : -1)
                  .Append(',').Append(d != null ? d.CulledCount : -1)
                  .Append(',').Append(d != null ? d.NightFactor.ToString("F2", CultureInfo.InvariantCulture) : "-1")
                  .Append(',').Append(Avg(_cullMsFrames).ToString("F3", CultureInfo.InvariantCulture))
                  .Append(',').Append(Avg(_dirMsFrames).ToString("F4", CultureInfo.InvariantCulture))
                  .Append(',').Append(d != null ? d.MaxTickMs.ToString("F4", CultureInfo.InvariantCulture) : "-1");
            }
            if (_stage8)
            {
                var d = FindDirector();
                sb.Append(',').Append(c.doorCullOn ? "on" : "off")
                  .Append(',').Append(_censusDoorsTotal)
                  .Append(',').Append(_censusDoorsDrawnInFrustum)
                  .Append(',').Append(_censusDoorsDrawnBeyond)
                  .Append(',').Append(_censusDoorsForcedOffTotal)
                  .Append(',').Append(d != null ? d.DoorCullHiddenByDistance : -1);
            }
            sb.AppendLine();
            try { File.AppendAllText(_outCsvPath, sb.ToString(), Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[FinalValidation] CSV: " + e.Message); }
        }

        // ============================================================ F8 / F9 (реальный ввод)

        private static void BeginRebase()
        {
            // FO-тест выполняется в состоянии реальной игры: корабли видимы, бюджет включён, политика дверей — on.
            SetShipsVisible(true);
            SetBudgetEnabled(true);
            if (_stage8) SetDoorCull(true);
            SetDirectorEnabled(true);
            if (_stage7)
            {
                SetFogOff(false);
                SetCityVisible(true);
                SetHlod(true);
                if (_cam != null) _cam.farClipPlane = BenchFar;
                ForcePhaseSilent(12f);
            }
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
            // Input System в редакторе отбрасывает клавиатуру без фокуса Game View (Stage 6, прогон 3:
            // F9 без единого [T-FO06CY]-лога) — перед нажатием фокусируем Game View.
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType != null) EditorWindow.FocusWindowIfItsOpen(gameViewType);
            var focusedWindow = EditorWindow.focusedWindow;
            bool focused = focusedWindow != null && gameViewType != null && gameViewType.IsInstanceOfType(focusedWindow);
            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            _keyReleaseFrame = Time.frameCount + 2;
            _pressAttempts++;
            _rebaseLines.Add("input=QueueStateEvent(" + key + ");frame=" + Time.frameCount + ";attempt=" + _pressAttempts + ";gameViewFocused=" + focused);
            Append("REBASE_INPUT;key=" + key + ";frame=" + Time.frameCount + ";attempt=" + _pressAttempts + ";gameViewFocused=" + focused);
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
                               ";budgetEntries=" + CountPrivate(_budget, "_entries") + ";" + DirectorBrief() +
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
                               ";budgetEntries=" + CountPrivate(_budget, "_entries") + ";" + DirectorBrief() +
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
                        _rollbackSeen = 0;
                        _rollbackRestoreTrue = 0;
                        _rebaseCompleted = 0;
                        _f9Pass = false;

                        _playerRoot.position = newPos;
                        Physics.SyncTransforms();

                        Append("REBASE;phase=F9_before;frameGen=" + _frameGenBefore +
                               ";cumulative=" + _cumulativeBefore.ToString("F2") +
                               ";worldRoot=" + _rootBefore.ToString("F2") +
                               ";player=" + _playerRoot.position.ToString("F2") +
                               ";doors=" + DoorControllerCount() + ";doorRenderers=" + DoorRendererCount() +
                               ";budgetEntries=" + CountPrivate(_budget, "_entries") + ";" + DirectorBrief() +
                               ";ships=" + ShipRootsDump() +
                               ";errorsSoFar=" + _errorCount);

                        PressKey(Key.F9);
                        _rebasePhase = 4;
                        _rebaseStepFrame = Time.frameCount;
                        break;
                    }

                case 4: // ожидание F9 (откат транзакции — это и есть успех)
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

                        // Успех F9: транзакция прошла и откатилась (restore=True), состояние вернулось
                        // к исходному: кумулятив/frameGen без изменений, WorldRoot на месте.
                        _f9Pass = _rollbackRestoreTrue > 0 && !stateChanged && rootShift < 0.01f;

                        string status = stateChanged && _rollbackSeen == 0
                            ? "FAILED_changed_state_without_rollback"
                            : _f9Pass
                                ? "PASS_rollback_restore"
                                : _rollbackSeen > 0
                                    ? "rollback_without_restore_true"
                                    : "FAILED_no_transaction";

                        Append("REBASE;phase=F9_after;status=" + status +
                               ";f9Pass=" + _f9Pass +
                               ";rollbackLogs=" + _rollbackSeen +
                               ";restoreTrue=" + _rollbackRestoreTrue +
                               ";frameGen=" + FrameGenOf() +
                               ";cumulative=" + GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset.ToString("F2") +
                               ";worldRoot=" + rootAfter.ToString("F2") +
                               ";worldRootShift_m=" + rootShift.ToString("F3", CultureInfo.InvariantCulture) +
                               ";predictedShift_m=" + _predictedShiftMagnitude.ToString("F3", CultureInfo.InvariantCulture) +
                               ";player=" + _playerRoot.position.ToString("F2") +
                               ";doors=" + DoorControllerCount() + ";doorRenderers=" + DoorRendererCount() +
                               ";budgetEntries=" + CountPrivate(_budget, "_entries") + ";" + DirectorBrief() +
                               ";ships=" + ShipRootsDump() +
                               ";frames=" + (Time.frameCount - _rebaseStepFrame) + ";errorsSoFar=" + _errorCount);

                        _rebasePhase = 3;
                        _rebaseStepFrame = Time.frameCount;
                        break;
                    }

                case 3: // итог
                    {
                        Append("REBASE;phase=summary;pressAttempts=" + _pressAttempts +
                               ";f9Pass=" + _f9Pass +
                               ";rollbackLogs=" + _rollbackSeen +
                               ";restoreTrue=" + _rollbackRestoreTrue +
                               ";completedLogs=" + _rebaseCompleted +
                               ";errors=" + _errorCount + ";exceptions=" + _exceptionCount +
                               ";baselineErrors=" + _baselineErrorCount + ";shaderErrors=" + _shaderErrorCount +
                               ";hybridEvidence=see_DC_cases");
                        for (int i = 0; i < _rebaseLines.Count && i < 24; i++) Append("REBASE_TRACE;" + _rebaseLines[i]);
                        int post = _stage7 && !_postPhase ? FirstPostIndex() : -1;
                        if (post >= 0)
                        {
                            _postPhase = true;
                            Append("NOTE;post_rebase_cases;from=" + post + ";" + DirectorBrief());
                            _state = State.Point;
                            BeginCase(post);
                            break;
                        }
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
                if (Profiled) AppendDirectorFinal();
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
            try { SetDirectorEnabled(true); } catch (Exception) { }
            try { SetShipsVisible(true); } catch (Exception) { }
            try { SetCityVisible(true); } catch (Exception) { }
            try { SetHlod(true); } catch (Exception) { }
            try { SetFogOff(false); } catch (Exception) { }

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
            if (_cullRecorder.Valid) _cullRecorder.Dispose();
            if (_dirRecorder.Valid) _dirRecorder.Dispose();

            if (Profiled)
            {
                AppendDirectorFinal();
                for (int i = 0; i < _directorLogLines.Count && i < 8; i++)
                    Append("DIRECTOR_LOG;" + i + ";" + _directorLogLines[i]);
            }

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

            if (condition.Contains("[VisibilityDirector]") && _directorLogLines.Count < 12)
                _directorLogLines.Add(condition);

            if (condition.Contains("[T-FO06CY]"))
            {
                if (condition.Contains("rolled back"))
                {
                    _rollbackSeen++;
                    if (condition.Contains("restore=True")) _rollbackRestoreTrue++;
                }
                if (condition.Contains("completed")) _rebaseCompleted++;
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
            Vector3 rig = _cam != null ? _cam.transform.position : Vector3.zero;
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count && i < 8; i++)
            {
                var cam = list[i] as Camera;
                if (i > 0) sb.Append('|');
                if (cam == null) { sb.Append("null"); continue; }
                sb.Append(cam.name).Append('@').Append(Vector3.Distance(cam.transform.position, rig).ToString("F0"));
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
            _stage6 = SessionState.GetBool(ProfileKey, false);
            _stage7 = SessionState.GetBool(Profile7Key, false);
            _stage8 = SessionState.GetBool(Profile8Key, false);
            _postPhase = false;
            _cases = BuildCases();
            _outRawPath = Path.Combine(projectRoot, DataDir, FilePrefix + DateTag + "_raw.txt");
            _outCsvPath = Path.Combine(projectRoot, DataDir, FilePrefix + DateTag + ".csv");
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

    /// <summary>
    /// Stage 4c: диагностика паритета кандидатов CityShadowBudget (только чтение, ничего не оптимизирует).
    /// Отвечает на три вопроса брифа: (1) какие рендереры компонент не взял и почему,
    /// (2) какие камеры он реально держит в union и какая из них удерживает тени включёнными,
    /// (3) совпадает ли порог/дистанция с полигоном Stage 4b. Сцену не сохраняет, правок нет.
    /// Меню: Project C → Culling → Stage 4c → Parity Probe.
    /// </summary>
    internal static class Stage4cParityProbe
    {
        private const string RunMenu = "Project C/Culling/Stage 4c/Parity Probe";
        private const string DataDir = "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA";

        private const float BenchFar = 30000f;
        private const float BenchFov = 60f;
        private const int WarmupFrames = 400;
        private const int SettleFrames = 150;
        private const int DoorLayer = 10;
        private const string WorldRootName = "WorldRoot_0_0";

        private static readonly Vector3 AuthoredWorldOffset = new Vector3(39936.0f, 2560.03f, 39936.0f);
        private static readonly Vector3 P1Local = new Vector3(-342.90f, -50.43f, -102.20f);

        private enum PState { Off, WaitPlay, WaitHost, Warmup, AtSpawn, AtP1, Done }

        private static PState _state = PState.Off;
        private static Camera _cam;
        private static Transform _worldRoot;
        private static CityShadowBudget _budget;
        private static bool _hostRequested;
        private static int _warmStartFrame;
        private static int _phaseFrame;
        private static double _stateSince;
        private static double _lastHeartbeat;
        private static string _outPath;
        private static readonly HashSet<Renderer> _shipSet = new HashSet<Renderer>();

        private static double Now { get { return EditorApplication.timeSinceStartup; } }
        private static readonly Plane[] _planes = new Plane[6];

        [MenuItem(RunMenu)]
        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Stage4cProbe] Уже в Play Mode. Сначала остановить.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dataDir = Path.Combine(projectRoot, DataDir);
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

            _outPath = Path.Combine(dataDir, "T_stage4c_probe_" +
                                   DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".txt");

            _state = PState.WaitPlay;
            _cam = null;
            _worldRoot = null;
            _budget = null;
            _hostRequested = false;
            _stateSince = Now;
            _lastHeartbeat = Now;

            Append("PROBE;unity=" + Application.unityVersion + ";scene=" +
                   SceneManager.GetActiveScene().path + ";dirtyAtStart=" + SceneManager.GetActiveScene().isDirty);
            Append("PROBE;method=PlayMode_Host→warmup" + WarmupFrames + "fr→dump_at_spawn→pose_P1→wait" +
                   SettleFrames + "fr→dump_at_P1;read_only_no_scene_save");
            Debug.Log("[Stage4cProbe] Старт. Файл: " + _outPath);

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try { Step(); }
            catch (Exception e)
            {
                Append("PROBE_ERROR;" + e.GetType().Name + ":" + (e.Message ?? "").Replace(";", ","));
                Debug.LogError("[Stage4cProbe] Исключение: " + e);
                Shutdown("EXCEPTION");
            }
        }

        private static void Step()
        {
            if (Now - _lastHeartbeat > 5.0)
            {
                _lastHeartbeat = Now;
                Append("HEARTBEAT;state=" + _state + ";frame=" + Time.frameCount +
                       ";playing=" + EditorApplication.isPlaying +
                       ";hostRequested=" + _hostRequested +
                       ";rig=" + (_cam != null ? _cam.name : "none") +
                       ";menu=" + (FindMenu() != null) + ";nmc=" + (FindByTypeName("NetworkManagerController") != null));
            }

            switch (_state)
            {
                case PState.WaitPlay:
                    if (!EditorApplication.isPlaying) return;
                    if (Now - _stateSince < 2.0) return;
                    _state = PState.WaitHost;
                    _stateSince = Now;
                    break;

                case PState.WaitHost:
                    if (TryStartHost())
                    {
                        _state = PState.Warmup;
                        _stateSince = Now;
                        return;
                    }
                    if (Now - _stateSince > 180.0) { Append("PROBE;fail=host_or_rig_timeout"); Shutdown("HOST_TIMEOUT"); }
                    break;

                case PState.Warmup:
                    if (Time.frameCount - _warmStartFrame < WarmupFrames) return;
                    DumpSnapshot("spawn", null);
                    ApplyP1Pose();
                    _phaseFrame = Time.frameCount;
                    _state = PState.AtP1;
                    break;

                case PState.AtP1:
                    if (Time.frameCount - _phaseFrame < SettleFrames) return;
                    DumpSnapshot("P1", null);
                    DumpVerdict();
                    _state = PState.Done;
                    break;

                case PState.Done:
                    Shutdown("OK");
                    break;
            }
        }

        // ------------------------------------------------------------------ вход в игру

        private static bool TryStartHost()
        {
            if (_cam == null) _cam = AcquireRig();
            if (_cam != null) return WarmupStart();

            if (_hostRequested) return false;

            var nmc = FindByTypeName("NetworkManagerController");
            var menu = FindMenu();
            if (nmc == null) return false;

            _hostRequested = true;
            if (menu != null)
            {
                var onClick = menu.GetType().GetMethod("OnHostClicked", BindingFlags.Instance | BindingFlags.NonPublic);
                if (onClick != null)
                {
                    onClick.Invoke(menu, null);
                    Append("PROBE;host_requested=MainMenuWindow.OnHostClicked");
                }
                else
                {
                    var start = nmc.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
                    if (start != null) start.Invoke(nmc, null);
                    Append("PROBE;host_requested=NetworkManagerController.StartHost");
                }
            }
            else
            {
                var start = nmc.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
                if (start != null) start.Invoke(nmc, null);
                Append("PROBE;host_requested=StartHost_no_menu");
            }
            return false;
        }

        private static bool WarmupStart()
        {
            if (_warmStartFrame != 0) return true;

            var behaviours = _cam.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null || !b.enabled) continue;
                string n = b.GetType().Name;
                if (n == "SpringArmCamera" || n == "ThirdPersonCameraDefault_EmptyTEST") b.enabled = false;
            }

            _cam.farClipPlane = BenchFar;
            _cam.fieldOfView = BenchFov;
            _cam.nearClipPlane = 0.1f;

            _worldRoot = FindWorldRoot();
            _budget = UnityEngine.Object.FindAnyObjectByType<CityShadowBudget>();
            _warmStartFrame = Time.frameCount;
            Append("PROBE;rig=" + _cam.name + ";far=" + _cam.farClipPlane + ";fov=" + _cam.fieldOfView +
                   ";budgetFound=" + (_budget != null) + ";worldRoot=" + (_worldRoot != null ? "yes" : "no"));
            return true;
        }

        private static Camera AcquireRig()
        {
            var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null || c.cameraType != CameraType.Game) continue;
                if (c.targetTexture != null) continue;
                if (!IsUnderThirdPersonRig(c.transform)) continue;
                return c;
            }
            return null;
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

        private static Transform FindWorldRoot()
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i] != null && roots[i].name == WorldRootName) return roots[i].transform;
            return null;
        }

        private static Component FindByTypeName(string typeName)
        {
            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].GetType().Name == typeName) return all[i];
            return null;
        }

        private static Component FindMenu()
        {
            return FindByTypeName("MainMenuWindow");
        }

        private static void ApplyP1Pose()
        {
            if (_cam == null) return;
            Vector3 shift = _worldRoot != null ? _worldRoot.position : Vector3.zero;
            Vector3 pos = P1Local + AuthoredWorldOffset + shift;
            _cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 337.5f, 0f));
            Append("PROBE;pose=P1;campos=" + pos.ToString("F2"));
        }

        // ------------------------------------------------------------- снимок / диагностика

        private static void DumpSnapshot(string tag, object _)
        {
            if (_budget == null) _budget = UnityEngine.Object.FindAnyObjectByType<CityShadowBudget>();
            if (_cam == null) return;

            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var entries = ReadEntries();
            var entrySet = new HashSet<Renderer>();
            for (int i = 0; i < entries.Count; i++) if (entries[i] != null) entrySet.Add(entries[i]);

            var cams = ReadComponentCameras();
            Vector3 camPos = _cam.transform.position;

            Append("SNAP;tag=" + tag + ";frame=" + Time.frameCount + ";campos=" + camPos.ToString("F2") +
                   ";allRenderers=" + all.Length + ";entries=" + entries.Count +
                   ";componentCameras=" + cams.Count);

            // 1. Камеры компонента.
            for (int i = 0; i < cams.Count; i++)
            {
                var c = cams[i];
                if (c == null) { Append("CAM;tag=" + tag + ";idx=" + i + ";null=True"); continue; }
                Append("CAM;tag=" + tag + ";idx=" + i + ";name=" + c.name +
                       ";scene=" + c.gameObject.scene.name +
                       ";pos=" + c.transform.position.ToString("F2") +
                       ";distToRig=" + Vector3.Distance(c.transform.position, camPos).ToString("F1") +
                       ";far=" + c.farClipPlane + ";near=" + c.nearClipPlane +
                       ";depth=" + c.depth + ";mask=" + c.cullingMask +
                       ";isRig=" + (c == _cam));
            }

            // 2. Комплект кандидатов: компонент vs независимый скан (без Off-фильтра).
            CollectShips(all);
            int live = 0, liveOn = 0, authoredOff = 0, missingOther = 0;
            var missingOffNames = new List<string>();
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r == null || !r.enabled) continue;
                if (r.gameObject.layer == DoorLayer) continue;
                if (_shipSet.Contains(r)) continue;
                live++;
                if (r.shadowCastingMode != ShadowCastingMode.Off) { liveOn++; continue; }
                if (entrySet.Contains(r)) continue;
                authoredOff++;
                if (missingOffNames.Count < 12) missingOffNames.Add(r.name + "@" + r.gameObject.layer);
            }
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r == null) continue;
                if (r.enabled) continue;
                if (entrySet.Contains(r)) missingOther++; // в списке есть, а уже выключен
            }
            Append("SET;tag=" + tag + ";liveEnabled=" + live + ";liveOn=" + liveOn +
                   ";authoredOff_notInEntries=" + authoredOff +
                   ";entriesDisabledButListed=" + missingOther +
                   ";sampleAuthoredOff=" + string.Join("|", missingOffNames.ToArray()));

            // 3. Бакеты дистанции/состояния.
            int off = 0, onNearRig = 0, onFarRig = 0;
            var argminFar = new Dictionary<string, int>();
            int farInFrustum = 0;
            GeometryUtility.CalculateFrustumPlanes(_cam, _planes);

            for (int i = 0; i < entries.Count; i++)
            {
                var r = entries[i];
                if (r == null) continue;
                bool isOff = r.shadowCastingMode == ShadowCastingMode.Off;
                if (isOff) { off++; continue; }

                float dRig = Vector3.Distance(r.bounds.center, camPos);
                if (dRig <= 200f) { onNearRig++; continue; }
                onFarRig++;

                string argmin = "none";
                float best = float.MaxValue;
                for (int c = 0; c < cams.Count; c++)
                {
                    var cc = cams[c];
                    if (cc == null) continue;
                    float d = Vector3.Distance(r.bounds.center, cc.transform.position);
                    if (d < best) { best = d; argmin = cc.name; }
                }
                if (!argminFar.ContainsKey(argmin)) argminFar[argmin] = 0;
                argminFar[argmin] = argminFar[argmin] + 1;
                if (GeometryUtility.TestPlanesAABB(_planes, r.bounds)) farInFrustum++;
            }

            var sb = new StringBuilder();
            foreach (var kv in argminFar) sb.Append(kv.Key).Append('=').Append(kv.Value).Append('|');
            Append("BUCKET;tag=" + tag + ";entries=" + entries.Count + ";modeOff=" + off +
                   ";on_dRig_le200=" + onNearRig + ";on_dRig_gt200=" + onFarRig +
                   ";on_dRig_gt200_inFrustum=" + farInFrustum +
                   ";argminCam=" + (sb.Length > 0 ? sb.ToString() : "none"));

            // 4. Гистограмма дистанции до рига для On-записей.
            int[] bands = new int[6];
            for (int i = 0; i < entries.Count; i++)
            {
                var r = entries[i];
                if (r == null || r.shadowCastingMode == ShadowCastingMode.Off) continue;
                float d = Vector3.Distance(r.bounds.center, camPos);
                int b = d < 150f ? 0 : d < 180f ? 1 : d < 200f ? 2 : d < 220f ? 3 : d < 260f ? 4 : 5;
                bands[b]++;
            }
            Append("DIST;tag=" + tag + ";lt150=" + bands[0] + ";150_180=" + bands[1] + ";180_200=" + bands[2] +
                   ";200_220=" + bands[3] + ";220_260=" + bands[4] + ";ge260=" + bands[5]);
        }

        private static void DumpVerdict()
        {
            Append("VERDICT;see=BUCKET_on_dRig_gt200_and_argminCam;" +
                   "expect_component_cut_near_polygon_921_of_1301;polygon_sb200_dc=2423");
        }

        private static void CollectShips(Renderer[] all)
        {
            _shipSet.Clear();
            var scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go == null) continue;
                if (go.name == WorldRootName) continue;
                if (go.GetComponentInChildren<ShipRootReference>(true) == null) continue;
                var rs = go.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rs.Length; i++) if (rs[i] != null) _shipSet.Add(rs[i]);
            }
        }

        private static List<Renderer> ReadEntries()
        {
            var result = new List<Renderer>();
            if (_budget == null) return result;
            var f = typeof(CityShadowBudget).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) return result;
            var list = f.GetValue(_budget) as IList;
            if (list == null) return result;
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry == null) continue;
                var rf = entry.GetType().GetField("renderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var r = rf != null ? rf.GetValue(entry) as Renderer : null;
                if (r != null) result.Add(r);
            }
            return result;
        }

        private static List<Camera> ReadComponentCameras()
        {
            var result = new List<Camera>();
            if (_budget == null) return result;
            var f = typeof(CityShadowBudget).GetField("_cameras", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) return result;
            var list = f.GetValue(_budget) as IList;
            if (list == null) return result;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i] as Camera;
                if (c != null) result.Add(c);
            }
            return result;
        }

        private static void Shutdown(string result)
        {
            EditorApplication.update -= Tick;
            Append("PROBE_DONE;result=" + result + ";sceneSaved=no;push=no;dirtyNow=" +
                   SceneManager.GetActiveScene().isDirty);
            _state = PState.Off;
            Debug.Log("[Stage4cProbe] Завершено: " + result + ". Файл: " + _outPath);
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        }

        private static void Append(string line)
        {
            if (string.IsNullOrEmpty(_outPath)) return;
            try { File.AppendAllText(_outPath, line + Environment.NewLine, Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[Stage4cProbe] Не удалось записать: " + e.Message); }
        }
    }
}
