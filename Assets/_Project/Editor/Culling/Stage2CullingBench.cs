// Project C — T-CULL01 / Stage 2 (H3): рантайм-скамейка draw calls.
// Воспроизводит методику Stage 0 (08_DATA/K_stage0_report.md §1):
//   Play Mode → Host, прогрев 1500 кадров, фаза Midday forced, камера рига far 30000 / fov 60,
//   40 кадров успокоения на точку, 3 замера с шагом 8 кадров, медиана, метрики UnityStats.
// Точки — авторские (FO-локальные) координаты Stage 0 + живая дельта WorldRoot_0_0.position,
// поэтому сдвиг Floating Origin поглощается и мировые Vector3 между кадрами не кэшируются.
//
// Что делает: сам входит в Play Mode, стартует Host (тем же путём, что кнопка меню),
// глушит SpringArmCamera, форсирует Midday, снимает P1 / P1-повтор / P2 / P3 / ночной контроль,
// пишет CASE-строки в docs/.../08_DATA/L_stage2_2026-10-31_raw.txt и выходит из Play Mode.
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

namespace ProjectC.EditorTools
{
    /// <summary>Рантайм-замер DC на эталонных точках Stage 0 (T-CULL01 / H3).</summary>
    public static class Stage2CullingBench
    {
        private const string RunMenu = "Project C/Culling/Stage 2 Bench/Run";
        private const string AbortMenu = "Project C/Culling/Stage 2 Bench/Abort";
        private const string RelOutPath =
            "docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/L_stage2_2026-10-31_raw.txt";

        /// <summary>Ключ в SessionState: прогон переживает доменную перезагрузку при входе в Play Mode.</summary>
        public const string SessionKey = "ProjectC.Stage2Bench.Running";

        private const float BenchFar = 30000f;
        private const float BenchFov = 60f;
        private const float DoorCullDistance = 150f;
        private const float ShadowOnDistance = 60f;

        // Авторские точки Stage 0 заданы относительно центра порта: авторская мировая позиция =
        // local + AuthoredWorldOffset (например P1: -342.90 → 39593.1 по X). В Play Mode поверх этого
        // действует сдвиг Floating Origin = WorldRoot_0_0.position, поэтому мировая точка считается
        // каждый тик заново: world = local + AuthoredWorldOffset + WorldRoot_0_0.position.
        private static readonly Vector3 AuthoredWorldOffset = new Vector3(39936.0f, 2560.03f, 39936.0f);

        private const int WarmupFrames = 1500;
        private const int SettleFrames = 40;
        private const int SampleGapFrames = 8;
        private const int SampleCount = 3;

        private const double HostTimeoutSeconds = 90.0;
        private const double RigTimeoutSeconds = 240.0;
        private const double WarmupTimeoutSeconds = 900.0;

        // ------------------------------------------------------------- точки Stage 0

        private class BenchCase
        {
            public string id;
            public string name;
            public Vector3 local;
            public float yaw;
            public float pitch;
            public float hour;      // 12 = Midday, 23 = Night
            public string phase;
            public string doors;    // on / off (off = контроль без дверей, не запускается)
            public string shadows;
        }

        private static List<BenchCase> BuildCases()
        {
            var list = new List<BenchCase>
            {
                new BenchCase { id = "P1", name = "tsentr_ploshhadi", local = new Vector3(-342.90f, -50.43f, -102.20f), yaw = 337.5f, pitch = 0f, hour = 12f, phase = "Midday", doors = "on", shadows = "on" },
                new BenchCase { id = "P1_repeat", name = "tsentr_ploshhadi_repeat", local = new Vector3(-342.90f, -50.43f, -102.20f), yaw = 337.5f, pitch = 0f, hour = 12f, phase = "Midday", doors = "on", shadows = "on" },
                new BenchCase { id = "P2", name = "uzkaya_ulitsa", local = new Vector3(-382.90f, -58.23f, -126.20f), yaw = 225f, pitch = 0f, hour = 12f, phase = "Midday", doors = "on", shadows = "on" },
                new BenchCase { id = "P3", name = "krysha", local = new Vector3(-416.00f, -27.94f, -36.00f), yaw = 132f, pitch = -18f, hour = 12f, phase = "Midday", doors = "on", shadows = "on" },
                new BenchCase { id = "P1", name = "tsentr_ploshhadi_night", local = new Vector3(-342.90f, -50.43f, -102.20f), yaw = 337.5f, pitch = 0f, hour = 23f, phase = "Night", doors = "on", shadows = "on" },
            };
            return list;
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
        private static readonly List<int> _frames = new List<int>();

        private static Camera _cam;
        private static Transform _worldRoot;
        private static Renderer[] _doorRenderers;
        private static Vector3 _targetPos;
        private static Quaternion _targetRot;
        private static int _settleUntilFrame;
        private static int _nextSampleFrame;
        private static bool _censusDone;
        private static int _censusInFrustum;
        private static int _censusKept;
        private static int _censusShadowOn;
        private static int _censusEnabled;
        private static int _censusGroups;
        private static double _stateSince;
        private static double _caseWindowStart;
        private static int _caseWindowFrame;
        private static int _warmStartFrame;
        private static bool _hostRequested;
        private static bool _menuHidden;
        private static string _outPath;
        private static readonly StringBuilder _notes = new StringBuilder();

        private static double Now { get { return EditorApplication.timeSinceStartup; } }

        private static readonly Plane[] _planes = new Plane[6];

        // --------------------------------------------------------------------- меню

        [MenuItem(RunMenu)]
        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Stage2Bench] Уже в Play Mode / вход выполняется. Сначала Abort.");
                return;
            }

            int doorRenderers = CountDoorRenderers();
            if (doorRenderers == 0)
            {
                Debug.LogError("[Stage2Bench] На слое PRIMUM_DOORS (10) нет рендереров: " +
                               "замер бессмыслен. Выполните Project C → Culling → Door Layer → Apply.");
                return;
            }

            _outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RelOutPath));
            string dir = Path.GetDirectoryName(_outPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(_outPath)) File.Delete(_outPath);

            _state = State.WaitPlayMode;
            _caseIndex = 0;
            _hostRequested = false;
            _menuHidden = false;
            _cam = null;
            _worldRoot = null;
            _doorRenderers = null;
            _notes.Length = 0;
            _stateSince = Now;

            SessionState.SetBool(SessionKey, true);
            SessionState.SetInt(SessionKey + ".reloads", 0);

            Append("NOTE;bench=Stage2CullingBench;unity=" + Application.unityVersion +
                   ";date=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Append("NOTE;method=PlayMode→Host;warmup=" + WarmupFrames + "fr;settle=" + SettleFrames +
                   "fr;samples=" + SampleCount + "x gap " + SampleGapFrames + "fr;metric=UnityStats median;far=" +
                   BenchFar + ";fov=" + BenchFov + ";doorLayerCullDistance=" + DoorCullDistance +
                   ";doorShadowOff=" + ShadowOnDistance + ";doorRenderersOnLayer=" + doorRenderers);
            Append("NOTE;camera=game rig (ThirdPersonCamera*);SpringArmCamera отключён;AtmosphereCamera вне замера;Camera.main не используется");

            Debug.Log("[Stage2Bench] Старт: " + doorRenderers + " дверных рендереров на слое 10, " +
                      _cases.Count + " точек. Файл: " + _outPath);

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(AbortMenu)]
        public static void Abort()
        {
            Finish("ABORTED_BY_USER");
        }

        private static int CountDoorRenderers()
        {
            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].gameObject.layer == 10) n++;
            return n;
        }

        // ------------------------------------------------------------------- тик

        private static void Tick()
        {
            try
            {
                Step();
            }
            catch (Exception e)
            {
                Debug.LogError("[Stage2Bench] Исключение: " + e);
                Finish("EXCEPTION");
            }
        }

        private static void Step()
        {
            switch (_state)
            {
                case State.WaitPlayMode:
                    if (!EditorApplication.isPlaying) return;
                    if (Now - _stateSince < 2.0) return;   // дать отработать RuntimeInitializeOnLoadMethod
                    _state = State.WaitHost;
                    _stateSince = Now;
                    break;

                case State.WaitHost:
                    if (!TryStartHost())
                    {
                        if (Now - _stateSince > HostTimeoutSeconds) Finish("HOST_TIMEOUT");
                        return;
                    }
                    _state = State.WaitRig;
                    _stateSince = Now;
                    break;

                case State.WaitRig:
                    if (!TryAcquireRig())
                    {
                        if (Now - _stateSince > RigTimeoutSeconds) Finish("RIG_TIMEOUT");
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
                        if (Time.frameCount - _warmStartFrame > 0 && (Time.frameCount - _warmStartFrame) % 500 == 0)
                            Debug.Log("[Stage2Bench] прогрев: " + (Time.frameCount - _warmStartFrame) + "/" + WarmupFrames + " кадров");
                        if (Now - _stateSince > WarmupTimeoutSeconds) Finish("WARMUP_TIMEOUT");
                        return;
                    }
                    CacheDoorRenderers();
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
            // Риг уже есть (например, после доменной перезагрузки посреди прогона) — Host не перезапускаем.
            if (TryAcquireRig())
            {
                _hostRequested = true;
                _menuHidden = true;
                return true;
            }

            var menu = FindByTypeName("MainMenuWindow");
            var nmc = FindByTypeName("NetworkManagerController");
            if (nmc == null) return false;

            // Игрок/риг нужен обязательно: без Host NGO не спавнит ни NetworkPlayer, ни камеру.
            if (!_hostRequested)
            {
                _hostRequested = true;
                if (menu != null)
                {
                    // Тот же путь, что клик по «Одиночная игра»: учитывает GlobalMotionPilotRuntime.
                    var onClick = menu.GetType().GetMethod("OnHostClicked", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (onClick != null)
                    {
                        onClick.Invoke(menu, null);
                        Debug.Log("[Stage2Bench] Host запрошен через MainMenuWindow.OnHostClicked()");
                    }
                    else
                    {
                        var start = nmc.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
                        if (start != null) start.Invoke(nmc, null);
                        Debug.Log("[Stage2Bench] Host запрошен напрямую через NetworkManagerController.StartHost()");
                    }
                }
                else
                {
                    var start = nmc.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
                    if (start != null) start.Invoke(nmc, null);
                    Debug.Log("[Stage2Bench] MainMenuWindow не найден: StartHost() напрямую");
                }
            }

            // Меню перекрывает Game View и добавляет свои draw calls — прячем.
            if (!_menuHidden && menu != null)
            {
                var hide = menu.GetType().GetMethod("Hide", BindingFlags.Instance | BindingFlags.Public);
                if (hide != null)
                {
                    hide.Invoke(menu, null);
                    _menuHidden = true;
                }
            }
            else if (menu == null)
            {
                _menuHidden = true;
            }

            return _menuHidden;
        }

        // ------------------------------------------------------------------- камера

        private static bool TryAcquireRig()
        {
            var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Camera best = null;
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null || c.cameraType != CameraType.Game) continue;
                if (c.targetTexture != null) continue;
                if (!IsUnderThirdPersonRig(c.transform)) continue;
                best = c;
                break;
            }
            if (best == null) return false;

            _cam = best;
            return true;
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
            // Глушим всё, что переписывает transform камеры в LateUpdate.
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

            Debug.Log("[Stage2Bench] Риг: " + _cam.name + " (far " + _cam.farClipPlane + ", fov " + _cam.fieldOfView +
                      "), отключено компонентов-двигателей камеры: " + disabled +
                      ", WorldRoot_0_0=" + (_worldRoot != null ? _worldRoot.position.ToString("F2") : "нет"));
        }

        private static Transform FindWorldRoot()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == "WorldRoot_0_0") return roots[i].transform;
            }
            return null;
        }

        /// <summary>Midday/Night forced: тем же способом, что Stage 0 — публичный SetTimeOfDay + отключение компонента.</summary>
        private static void ForcePhase(float hour)
        {
            var dnc = FindByTypeName("DayNightController");
            if (dnc == null)
            {
                Debug.LogWarning("[Stage2Bench] DayNightController не найден — фаза суток не форсирована");
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
            if (mb != null) mb.enabled = false;   // стоп серверному poll'у _serverTimeOfDay

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
                   ";phaseName=" + phaseName + ";sun=" + sunState + ";dnc=disabled_runtime_only");
            Debug.Log("[Stage2Bench] Фаза форсирована: " + hour + "h → " + phaseName + ", " + sunState);
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
            {
                if (all[i] != null && all[i].GetType().Name == typeName) return all[i];
            }
            return null;
        }

        // ------------------------------------------------------------------- точки

        private static void CacheDoorRenderers()
        {
            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var doors = new List<Renderer>(6000);
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r == null || r.gameObject.layer != 10) continue;
                doors.Add(r);
            }
            _doorRenderers = doors.ToArray();
            Debug.Log("[Stage2Bench] Дверных рендереров в сцене: " + _doorRenderers.Length);
        }

        private static void BeginCase(int index)
        {
            _caseIndex = index;
            if (_caseIndex >= _cases.Count)
            {
                Finish("OK");
                return;
            }

            var c = _cases[_caseIndex];
            ApplyPose(c);
            ForcePhase(c.hour);

            _dc.Clear(); _spc.Clear(); _tris.Clear(); _sc.Clear(); _frames.Clear();
            _censusDone = false;
            _settleUntilFrame = Time.frameCount + SettleFrames;
            _nextSampleFrame = _settleUntilFrame;
            _caseWindowFrame = Time.frameCount;
            _caseWindowStart = Now;
            Debug.Log("[Stage2Bench] Точка " + c.id + " (" + c.name + ") — " + c.phase);
        }

        private static void ApplyPose(BenchCase c)
        {
            Vector3 shift = _worldRoot != null ? _worldRoot.position : Vector3.zero;
            _targetPos = c.local + AuthoredWorldOffset + shift;
            _targetRot = Quaternion.Euler(c.pitch, c.yaw, 0f);

            if (_cam == null) return;
            // Поза пере-утверждается каждый тик: двигатели камеры выключены, но это страховка от чужих LateUpdate.
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
                if (_state == State.Off) return;   // самопроверка позы остановила прогон
                _censusDone = true;
                _nextSampleFrame = Time.frameCount;
            }

            if (Time.frameCount < _nextSampleFrame) return;

            _dc.Add(UnityStats.drawCalls);
            _spc.Add(UnityStats.setPassCalls);
            _tris.Add(UnityStats.triangles);
            _sc.Add(UnityStats.shadowCasters);
            _frames.Add(Time.frameCount);

            if (_dc.Count == 1 && UnityStats.drawCalls == 0)
            {
                Debug.LogWarning("[Stage2Bench] UnityStats.drawCalls=0 (screenRes=" + UnityStats.screenRes +
                                 "): Game View не рендерит, замер недостоверен. Держите окно Unity " +
                                 "развёрнутым и Game View видимым.");
                Append("NOTE;warning=drawCalls_0_at_first_sample;screenRes=" + UnityStats.screenRes);
            }

            if (_dc.Count < SampleCount)
            {
                _nextSampleFrame = Time.frameCount + SampleGapFrames;
                return;
            }

            WriteCaseLine(c);
            BeginCase(_caseIndex + 1);
        }

        private static void RunCensus()
        {
            _censusInFrustum = 0;
            _censusKept = 0;
            _censusShadowOn = 0;
            _censusEnabled = 0;
            _censusGroups = 0;

            if (_cam == null || _doorRenderers == null) return;

            GeometryUtility.CalculateFrustumPlanes(_cam, _planes);
            Vector3 camPos = _cam.transform.position;

            for (int i = 0; i < _doorRenderers.Length; i++)
            {
                var r = _doorRenderers[i];
                if (r == null) continue;
                if (r.enabled) _censusEnabled++;
                if (!GeometryUtility.TestPlanesAABB(_planes, r.bounds)) continue;
                _censusInFrustum++;
                float d = Vector3.Distance(camPos, r.bounds.center);
                if (d <= DoorCullDistance) _censusKept++;
                if (d <= ShadowOnDistance && r.shadowCastingMode != ShadowCastingMode.Off) _censusShadowOn++;
            }

            // Самопроверка позы: во всех эталонных точках двери в кадре есть (P1 ≈ 3 165, P2 ≈ 91, P3 ≈ 284).
            // Ноль означает, что камера стоит не там — замер в таком виде бессмыслен.
            if (_censusInFrustum == 0)
            {
                Debug.LogError("[Stage2Bench] В кадре 0 дверных рендереров: поза камеры неверна, замер остановлен.");
                Append("NOTE;error=census_zero;campos=" + Fmt(_targetPos.x) + "," + Fmt(_targetPos.y) + "," + Fmt(_targetPos.z) +
                       ";worldrootshift=" + Fmt(_worldRoot != null ? _worldRoot.position.x : 0f) + "," +
                       Fmt(_worldRoot != null ? _worldRoot.position.y : 0f) + "," +
                       Fmt(_worldRoot != null ? _worldRoot.position.z : 0f));
                Finish("CENSUS_ZERO");
            }
        }

        private static void WriteCaseLine(BenchCase c)
        {
            int dc = Median(_dc);
            int spc = Median(_spc);
            int tris = Median(_tris);
            int sc = Median(_sc);
            int frame = Median(_frames);
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
            string pos = Fmt(c.local.x) + "," + Fmt(c.local.y) + "," + Fmt(c.local.z);
            string world = Fmt(_targetPos.x) + "," + Fmt(_targetPos.y) + "," + Fmt(_targetPos.z);
            double keptPct = _censusInFrustum > 0 ? 100.0 * _censusKept / _censusInFrustum : 0.0;

            var sb = new StringBuilder();
            sb.Append("CASE;id=").Append(c.id);
            sb.Append(";name=").Append(c.name);
            sb.Append(";dayPhase=").Append(c.phase);
            sb.Append(";doors=").Append(c.doors).Append(";shadows=").Append(c.shadows);
            sb.Append(";far=").Append(BenchFar.ToString("F0", CultureInfo.InvariantCulture));
            sb.Append(";fov=").Append(BenchFov.ToString("F0", CultureInfo.InvariantCulture));
            sb.Append(";pos=").Append(pos);
            sb.Append(";worldpos=").Append(world);
            sb.Append(";worldrootshift=").Append(Fmt(shift.x) + "," + Fmt(shift.y) + "," + Fmt(shift.z));
            sb.Append(";yaw=").Append(Fmt(c.yaw)).Append(";pitch=").Append(Fmt(c.pitch));
            sb.Append(";layerCull10=").Append(Fmt(lcd));
            sb.Append(";dc=").Append(dc);
            sb.Append(";spc=").Append(spc);
            sb.Append(";tris=").Append(tris);
            sb.Append(";shadowCasters=").Append(sc);
            sb.Append(";fps=").Append(fps.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(";frame=").Append(frame);
            sb.Append(";doorsInFrustum=").Append(_censusInFrustum);
            sb.Append(";doorsKept=").Append(_censusKept);
            sb.Append(";kept_pct=").Append(keptPct.ToString("F1", CultureInfo.InvariantCulture));
            sb.Append(";doorsShadowOn=").Append(_censusShadowOn);
            sb.Append(";doorsEnabled=").Append(_censusEnabled);
            sb.Append(";rawDc=").Append(string.Join("|", _dc.ConvertAll(v => v.ToString()).ToArray()));
            sb.Append(";rawSpc=").Append(string.Join("|", _spc.ConvertAll(v => v.ToString()).ToArray()));
            sb.Append(";rawShadow=").Append(string.Join("|", _sc.ConvertAll(v => v.ToString()).ToArray()));
            sb.Append(";sampleFrames=").Append(string.Join("|", _frames.ConvertAll(v => v.ToString()).ToArray()));
            Append(sb.ToString());

            Debug.Log("[Stage2Bench] " + c.id + " " + c.phase + ": dc=" + dc + " spc=" + spc + " tris=" + tris +
                      " shadowCasters=" + sc + " doorsInFrustum=" + _censusInFrustum + " kept=" + _censusKept);
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

        // ------------------------------------------------------------------ финиш

        private static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(SessionKey, false);

            if (_state != State.Off)
            {
                Append("BENCH_DONE;result=" + result + ";casesCompleted=" + _caseIndex + "/" + _cases.Count +
                       ";sceneSaved=no;push=no");
                if (_notes.Length > 0) Append("NOTE;bench_notes=" + _notes);
            }

            _state = State.Off;
            Debug.Log("[Stage2Bench] Завершено: " + result + ". Файл: " + _outPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode && result != "OK")
            {
                EditorApplication.ExitPlaymode();
            }
            else if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.ExitPlaymode();
            }
        }

        private static void Append(string line)
        {
            if (string.IsNullOrEmpty(_outPath)) return;
            try
            {
                File.AppendAllText(_outPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Stage2Bench] Не удалось записать строку: " + e.Message);
            }
        }

        /// <summary>
        /// Вход в Play Mode вызывает доменную перезагрузку: статические поля сбрасываются,
        /// подписка на EditorApplication.update теряется. Прогон переживает это через SessionState.
        /// </summary>
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
                Debug.LogError("[Stage2Bench] Слишком много доменных перезагрузок подряд — прогон отменён.");
                return;
            }

            _outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RelOutPath));
            _state = State.WaitPlayMode;
            _stateSince = Now;
            _hostRequested = false;
            _menuHidden = false;
            _cam = null;
            _worldRoot = null;
            _doorRenderers = null;
            _caseIndex = 0;

            Append("NOTE;domain_reload=true;restart=" + reloads);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[Stage2Bench] Доменная перезагрузка (#" + reloads + ") — прогон продолжен из SessionState.");
        }
    }

    /// <summary>Точка входа InitializeOnLoad: восстанавливает прогон после доменной перезагрузки.</summary>
    [InitializeOnLoad]
    internal static class Stage2CullingBenchBootstrap
    {
        static Stage2CullingBenchBootstrap()
        {
            Stage2CullingBench.ResumeIfRunning();
        }
    }
}
