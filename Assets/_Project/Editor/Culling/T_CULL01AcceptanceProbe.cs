// T-CULL01 / финальная приёмка — проба свет-инвариантов и дверей на принятой базе Stage 8.
//
// Зачем: Stage 8 bench (FinalValidationBench Stage 8) даёт DC/двери/HLOD/F8/F9, но не читает
// диагностику светового пула и ALIC. Финальная приёмка требует структурных свет-инвариантов
// (FocusCamera/FocusSource, intensityZeroActive днём, activeOUT вечером, HeldForFade/GateGraceHeld,
// параметры fade) без повторного запуска световых бенчей T-LIGHT16…21.
//
// Что делает: обычный вход в игру (ProjectCMainMenuUiHost.OnMenuAction("host") — та же кнопка
// «ОДИНОЧНАЯ ИГРА»), поза P1 (площадь), камера ThirdPersonCamera_0 с far 30 000 / fov 60 / near 0.1,
// 12 ч → сэмпл, 18 ч → сэмпл, затем выход из Play Mode.
//
// Что НЕ делает: не сохраняет сцену, не пишет в префабы/FBX/материалы/URP/ProjectSettings, не меняет
// свет-код и не вызывает Apply* конфиги. Владение: Light.enabled — пул, Light.intensity — ALIC;
// проба только читает. Меню: Project C → Culling → T-CULL01 Acceptance → Probe.
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ProjectC.Core;
using ProjectC.Ship;
using ProjectC.World;

namespace ProjectC.EditorTools
{
    /// <summary>T-CULL01: read-only проба свет-инвариантов и дистанционной политики дверей.</summary>
    public static class T_CULL01AcceptanceProbe
    {
        private const string RunMenu = "Project C/Culling/T-CULL01 Acceptance/Probe (light + doors)";
        private const string AbortMenu = "Project C/Culling/T-CULL01 Acceptance/Abort";
        private const string SessionKey = "ProjectC.TCull01AcceptanceProbe.Running";

        private const string DataDir = "Temp/T-CULL01-final";

        private const int WarmupFrames = 300;
        private const int SettleFrames = 120;
        private const int SampleGapFrames = 8;
        private const int SampleCount = 3;

        private const int DoorLayer = 10;
        private const float DoorNearDistance = 130f;
        private const float DoorGateDistance = 150f;
        private const float DoorBandEnd = 165f;

        private const float BenchFar = 30000f;
        private const float BenchFov = 60f;
        private const float P1Yaw = 337.5f;

        private static readonly Vector3 AuthoredWorldOffset = new Vector3(39936.0f, 2560.03f, 39936.0f);
        private static readonly Vector3 P1Local = new Vector3(-342.90f, -50.43f, -102.20f);

        private enum State { Off, WaitPlay, WaitHost, Warmup, SampleDay12, SampleEvening18, Done }

        private static State _state = State.Off;
        private static string _outPath;
        private static readonly StringBuilder _lines = new StringBuilder();

        private static int _stateFrame;
        private static int _nextSampleFrame;
        private static bool _hostRequested;

        private static Camera _cam;
        private static PrimumLightPool _pool;
        private static AdditionalLightIntensityController _alic;
        private static CityShadowBudget _budget;
        private static VisibilityDirector _director;
        private static PrimumHlodManifest _manifest;
        private static Renderer[] _renderers;
        private static MonoBehaviour _dayNight;
        private static bool _dayNightWasEnabled;

        private static readonly System.Collections.Generic.List<int> _dc = new System.Collections.Generic.List<int>();
        private static readonly System.Collections.Generic.List<int> _spc = new System.Collections.Generic.List<int>();
        private static readonly System.Collections.Generic.List<int> _tris = new System.Collections.Generic.List<int>();
        private static readonly System.Collections.Generic.List<int> _hybridDc = new System.Collections.Generic.List<int>();
        private static readonly System.Collections.Generic.List<int> _hybridInst = new System.Collections.Generic.List<int>();
        private static readonly System.Collections.Generic.List<int> _srpDc = new System.Collections.Generic.List<int>();

        private static double Now { get { return EditorApplication.timeSinceStartup; } }

        [MenuItem(RunMenu)]
        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[T-CULL01/Acceptance] Уже в Play Mode / вход выполняется. Сначала Abort.");
                return;
            }

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dir = Path.Combine(root, DataDir);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _outPath = Path.Combine(dir, "acceptance_probe_" +
                System.DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + ".txt");
            if (File.Exists(_outPath)) File.Delete(_outPath);

            _lines.Length = 0;
            _state = State.WaitPlay;
            _stateFrame = 0;
            _hostRequested = false;
            _cam = null;
            _pool = null;
            _alic = null;
            _budget = null;
            _director = null;
            _manifest = null;
            _renderers = null;
            _dayNight = null;

            Append("NOTE;probe=T_CULL01AcceptanceProbe;unity=" + Application.unityVersion +
                   ";date=" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                   ";scene=" + SceneManager.GetActiveScene().path +
                   ";sceneDirtyAtStart=" + SceneManager.GetActiveScene().isDirty);
            Append("NOTE;method=main_menu→host(OnMenuAction);pose=P1;camera=ThirdPersonCamera_0;far=30000;fov=60");
            Append("NOTE;readonly=yes;pool/intensity_write=no;scene_save=no;push=no");

            SessionState.SetBool(SessionKey, true);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[T-CULL01/Acceptance] Старт пробы. Файл: " + _outPath);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(AbortMenu)]
        public static void Abort()
        {
            Finish("ABORTED_BY_USER");
        }

        private static void Tick()
        {
            try { Step(); }
            catch (System.Exception e)
            {
                Debug.LogError("[T-CULL01/Acceptance] Исключение пробы: " + e);
                Finish("EXCEPTION");
            }
        }

        private static void Step()
        {
            switch (_state)
            {
                case State.WaitPlay:
                    if (EditorApplication.isPlaying) { _state = State.WaitHost; _stateFrame = Time.frameCount; }
                    break;

                case State.WaitHost:
                    if (!TryEnterGame()) return;
                    PlaceCameraAndWarmup();
                    _state = State.Warmup;
                    _stateFrame = Time.frameCount;
                    break;

                case State.Warmup:
                    if (Time.frameCount - _stateFrame < WarmupFrames) return;
                    ResetSamples();
                    ForceHour(12f);
                    _nextSampleFrame = Time.frameCount + SettleFrames;
                    _state = State.SampleDay12;
                    break;

                case State.SampleDay12:
                    if (!Collect()) return;
                    WritePhase("day12");
                    ResetSamples();
                    ForceHour(18f);
                    _nextSampleFrame = Time.frameCount + SettleFrames;
                    _state = State.SampleEvening18;
                    break;

                case State.SampleEvening18:
                    if (!Collect()) return;
                    WritePhase("evening18");
                    Finish("OK");
                    break;
            }
        }

        // ---------------------------------------------------------------- вход в игру

        /// <summary>
        /// Вход «как игрок»: кнопка «ОДИНОЧНАЯ ИГРА» нового меню, затем ждём скрытия меню и риг
        /// ThirdPersonCamera с живым локальным игроком. Если меню всё ещё на экране — не начинаем замер.
        /// </summary>
        private static bool TryEnterGame()
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
                    Append("NOTE;host_start=ProjectCMainMenuUiHost.OnMenuAction(host)");
                    return false;
                }
                bool shown = PcmmShown(pcmm);
                if (shown) return false;
                return TryAcquireRig() != null;
            }

            if (TryAcquireRig() != null) { _hostRequested = true; return true; }
            return false;
        }

        private static bool PcmmShown(Component pcmm)
        {
            if (pcmm == null) return false;
            var p = pcmm.GetType().GetProperty("IsShown", BindingFlags.Instance | BindingFlags.Public);
            return p != null && (bool)p.GetValue(pcmm);
        }

        private static Camera TryAcquireRig()
        {
            if (_cam != null) return _cam;
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null || c.cameraType != CameraType.Game) continue;
                if (c.targetTexture != null) continue;
                if (!IsUnderThirdPersonRig(c.transform)) continue;
                _cam = c;
                return _cam;
            }
            return null;
        }

        private static bool IsUnderThirdPersonRig(Transform t)
        {
            var node = t;
            for (int depth = 0; depth < 6 && node != null; depth++)
            {
                if (node.name.StartsWith("ThirdPersonCamera", System.StringComparison.OrdinalIgnoreCase)) return true;
                node = node.parent;
            }
            return false;
        }

        private static void PlaceCameraAndWarmup()
        {
            _pool = Object.FindAnyObjectByType<PrimumLightPool>();
            _alic = Object.FindAnyObjectByType<AdditionalLightIntensityController>();
            _budget = Object.FindAnyObjectByType<CityShadowBudget>();
            _director = Object.FindAnyObjectByType<VisibilityDirector>();
            _manifest = Resources.Load<PrimumHlodManifest>("HLOD/PRIMUM_HLOD");
            _renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

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

            var worldRoot = FindWorldRoot();
            Vector3 shift = worldRoot != null ? worldRoot.position : Vector3.zero;
            Vector3 pos = P1Local + AuthoredWorldOffset + shift;
            _cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, P1Yaw, 0f));

            ForceHour(12f);

            Append("NOTE;camera=" + _cam.name + ";far=" + _cam.farClipPlane.ToString("F0", CultureInfo.InvariantCulture) +
                   ";fov=" + _cam.fieldOfView.ToString("F0", CultureInfo.InvariantCulture) +
                   ";camPos=" + pos.ToString("F1") + ";rigBehavioursDisabled=" + disabled);
            Append("NOTE;worldRoot=" + (worldRoot != null ? worldRoot.name + "@" + worldRoot.position.ToString("F1") : "none"));

            var player = FindLocalPlayer();
            Append("NOTE;localPlayer=" + (player != null ? player.name + "@" + player.transform.position.ToString("F1") : "none"));

            if (_manifest != null && worldRoot != null)
            {
                Vector3 cityCenter = worldRoot.position + _manifest.cityCenter;
                Append("NOTE;cityCenter=" + cityCenter.ToString("F1") +
                       ";cameraToCityM=" + Vector3.Distance(pos, cityCenter).ToString("F1", CultureInfo.InvariantCulture));
            }

            Debug.Log("[T-CULL01/Acceptance] В игре: rig=" + (_cam != null ? _cam.name : "none") +
                      ", player=" + (player != null) + ", pool=" + (_pool != null) +
                      ", alic=" + (_alic != null) + ", budget=" + (_budget != null) + ", director=" + (_director != null));
        }

        private static Transform FindWorldRoot()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i] != null && roots[i].name == "WorldRoot_0_0") return roots[i].transform;
            return null;
        }

        private static GameObject FindLocalPlayer()
        {
            var nmc = FindByTypeName("NetworkManagerController");
            if (nmc != null)
            {
                var t = nmc.GetType();
                var p = t.GetProperty("LocalClient", BindingFlags.Instance | BindingFlags.Public);
                object client = p != null ? p.GetValue(nmc) : null;
                if (client != null)
                {
                    var po = client.GetType().GetProperty("PlayerObject", BindingFlags.Instance | BindingFlags.Public);
                    var go = po != null ? po.GetValue(client) as GameObject : null;
                    if (go != null) return go;
                }
            }
            var net = FindByTypeName("NetworkPlayer");
            return net != null ? net.gameObject : null;
        }

        private static void ForceHour(float hour)
        {
            var dnc = FindByTypeName("DayNightController");
            if (dnc == null) { Append("NOTE;daynight=none;hour=" + hour.ToString("F1", CultureInfo.InvariantCulture)); return; }

            var type = dnc.GetType();
            var blend = type.GetField("_blendProgress", BindingFlags.Instance | BindingFlags.NonPublic);
            var blendTarget = type.GetField("_targetBlendProgress", BindingFlags.Instance | BindingFlags.NonPublic);
            if (blend != null && blend.FieldType == typeof(float)) blend.SetValue(dnc, 1f);
            if (blendTarget != null && blendTarget.FieldType == typeof(float)) blendTarget.SetValue(dnc, 1f);

            var setTime = type.GetMethod("SetTimeOfDay", BindingFlags.Instance | BindingFlags.Public);
            var refresh = type.GetMethod("ForceRefresh", BindingFlags.Instance | BindingFlags.Public);
            if (setTime != null) setTime.Invoke(dnc, new object[] { hour });
            if (refresh != null) refresh.Invoke(dnc, null);

            _dayNight = dnc as MonoBehaviour;
            _dayNightWasEnabled = _dayNight != null && _dayNight.enabled;
            if (_dayNight != null) _dayNight.enabled = false;
        }

        private static Component FindByTypeName(string typeName)
        {
            var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].GetType().Name == typeName) return all[i];
            return null;
        }

        // ---------------------------------------------------------------- сэмплы

        private static void ResetSamples()
        {
            _dc.Clear(); _spc.Clear(); _tris.Clear();
            _hybridDc.Clear(); _hybridInst.Clear(); _srpDc.Clear();
            _nextSampleFrame = Time.frameCount + SettleFrames;
            _stateFrame = Time.frameCount;
        }

        private static bool Collect()
        {
            if (Time.frameCount < _nextSampleFrame) return false;
            _dc.Add(UnityStats.drawCalls);
            _spc.Add(UnityStats.setPassCalls);
            _tris.Add(UnityStats.triangles);
            _hybridDc.Add(UnityStats.hybridBatcherDrawCalls);
            _hybridInst.Add(UnityStats.hybridBatcherInstances);
            _srpDc.Add(UnityStats.srpBatcherDrawCalls);
            _nextSampleFrame = Time.frameCount + SampleGapFrames;
            return _dc.Count >= SampleCount;
        }

        private static void WritePhase(string phase)
        {
            int dc = Median(_dc), spc = Median(_spc), tris = Median(_tris);
            int hybridDc = Median(_hybridDc), hybridInst = Median(_hybridInst), srpDc = Median(_srpDc);

            Append("");
            Append("PHASE;" + phase + ";frame=" + Time.frameCount);

            // ---- свет: только чтение публичной диагностики пула/ALIC
            if (_pool != null)
            {
                Append("LIGHT;" + phase +
                       ";focusCamera=" + _pool.FocusCameraName +
                       ";focusSource=" + _pool.FocusSourceKind +
                       ";active=" + _pool.ActiveCount +
                       ";activeOut=" + _pool.ActiveOut +
                       ";activeIn=" + _pool.ActiveIn +
                       ";activeFarm=" + _pool.ActiveFarm +
                       ";intensityZeroActive=" + _pool.ActiveIntensityZero +
                       ";heldForFade=" + _pool.HeldForFade +
                       ";gateGraceHeld=" + _pool.GateGraceHeld +
                       ";capSaturated=" + (_pool.CapSaturated ? 1 : 0) +
                       ";capCut=" + _pool.CapCutCount +
                       ";registered=" + _pool.RegisteredCount +
                       ";lastCheckMs=" + _pool.LastCheckMs.ToString("F3", CultureInfo.InvariantCulture));
                Append("CONFIG_POOL;" + phase + ";" + _pool.ConfigLine);
                Append("CONFIG_CAMERA;" + phase + ";" + _pool.CameraConfigLine);
                Append("CONFIG_CONE;" + phase + ";" + _pool.ConeConfigLine);
            }
            else Append("LIGHT;" + phase + ";pool=none");

            if (_alic != null)
            {
                Append("ALIC;" + phase +
                       ";fadeEnabled=" + _alic.FadeEnabled +
                       ";fadeInSec=" + _alic.FadeInSeconds.ToString("F2", CultureInfo.InvariantCulture) +
                       ";fadeOutSec=" + _alic.FadeOutSeconds.ToString("F2", CultureInfo.InvariantCulture) +
                       ";fadeActive=" + _alic.FadeActiveCount +
                       ";fadeInCount=" + _alic.FadeInCount +
                       ";fadeOutCount=" + _alic.FadeOutCount +
                       ";writesThisFrame=" + _alic.WritesThisFrame +
                       ";zeroIntensityTracked=" + _alic.ZeroIntensityTracked);
                Append("CONFIG_FADE;" + phase + ";" + _alic.FadeConfigLine);
            }
            else Append("ALIC;" + phase + ";alic=none");

            // ---- двери: census по слою 10 от позиции камеры
            Append(CensusDoors(phase));

            // ---- рисование
            Append("DRAWING;" + phase + ";dc=" + dc + ";setPass=" + spc + ";tris=" + tris +
                   ";hybridDC=" + hybridDc + ";hybridInstances=" + hybridInst + ";srpBatcherDC=" + srpDc);

            // ---- бюджеты/директор
            if (_budget != null)
            {
                var bt = _budget.GetType();
                int entries = CountPrivate(_budget, bt, "_entries");
                Append("BUDGET;" + phase + ";enabled=" + _budget.enabled +
                       ";entries=" + entries +
                       ";onM=" + ReadFloatField(_budget, bt, "shadowOnDistance") +
                       ";offM=" + ReadFloatField(_budget, bt, "shadowOffDistance"));
            }
            else Append("BUDGET;" + phase + ";budget=none");

            if (_director != null)
            {
                Append("DIRECTOR;" + phase + ";groups=" + _director.GroupCount +
                       ";directed=" + _director.DirectedMemberCount +
                       ";cameras=" + _director.CameraCount +
                       ";policy=" + _director.PolicyEnabled +
                       ";hlodBlocks=" + _director.HlodBlockCount +
                       ";impostor=" + _director.HlodImpostorPresent +
                       ";cityLevel=" + _director.CityLevel +
                       ";levels=" + _director.HlodLevelCounts +
                       ";doorCull=" + _director.DoorCullEnabled +
                       ";doorDistM=" + _director.DoorCullDistance.ToString("F0", CultureInfo.InvariantCulture) +
                       ";doorCullTotal=" + _director.DoorCullTotal +
                       ";doorBlockBound=" + _director.DoorCullBlockBound +
                       ";doorHiddenByDist=" + _director.DoorCullHiddenByDistance +
                       ";doorsHiddenHlod=" + _director.HlodDoorsHidden);
            }
            else Append("DIRECTOR;" + phase + ";director=none");

            Debug.Log("[T-CULL01/Acceptance] " + phase + ": dc=" + dc + ", hybridDC=" + hybridDc +
                      ", focus=" + (_pool != null ? _pool.FocusCameraName + "/" + _pool.FocusSourceKind : "none") +
                      ", zeroActive=" + (_pool != null ? _pool.ActiveIntensityZero : -1) +
                      ", activeOut=" + (_pool != null ? _pool.ActiveOut : -1) +
                      ", held=" + (_pool != null ? _pool.HeldForFade : -1) +
                      ", grace=" + (_pool != null ? _pool.GateGraceHeld : -1));
        }

        private static string CensusDoors(string phase)
        {
            if (_cam == null || _renderers == null) return "DOORS;" + phase + ";census=none";

            Vector3 camPos = _cam.transform.position;
            float nearSqr = DoorNearDistance * DoorNearDistance;
            float gateSqr = DoorGateDistance * DoorGateDistance;
            float bandSqr = DoorBandEnd * DoorBandEnd;

            int total = 0, forcedOffTotal = 0, enabledTotal = 0;
            int nearDrawn = 0, nearForcedOff = 0;
            int beyondDrawn = 0, beyondForcedOff = 0;
            int bandDrawn = 0, bandForcedOff = 0;
            int midDrawn = 0, midForcedOff = 0;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null || r.gameObject.layer != DoorLayer) continue;
                total++;
                if (r.forceRenderingOff) forcedOffTotal++;
                if (!r.enabled) continue;
                enabledTotal++;

                bool drawn = !r.forceRenderingOff;
                float d = (r.bounds.center - camPos).sqrMagnitude;

                if (d < nearSqr) { if (drawn) nearDrawn++; else nearForcedOff++; }
                else if (d > gateSqr) { if (drawn) beyondDrawn++; else beyondForcedOff++; }
                else { if (drawn) bandDrawn++; else bandForcedOff++; }
                if (d >= nearSqr && d <= bandSqr) { if (drawn) midDrawn++; else midForcedOff++; }
            }

            int controllers = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None).Length;
            int controllersWithCollider = 0;
            var all = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].GetComponent<Collider>() != null) controllersWithCollider++;

            return "DOORS;" + phase +
                   ";layer10=" + total +
                   ";enabled=" + enabledTotal +
                   ";forcedOffTotal=" + forcedOffTotal +
                   // ближняя зона (<130 м): двери обязаны быть видны
                   ";near130_drawn=" + nearDrawn +
                   ";near130_forcedOff=" + nearForcedOff +
                   // гистерезисная полоса 130–165 м — смешанное состояние по замыслу
                   ";band130_165_drawn=" + bandDrawn +
                   ";band130_165_forcedOff=" + bandForcedOff +
                   ";mid130_165_drawn=" + midDrawn +
                   ";mid130_165_forcedOff=" + midForcedOff +
                   // за 150 м — рисоваться не должно
                   ";beyond150_drawn=" + beyondDrawn +
                   ";beyond150_forcedOff=" + beyondForcedOff +
                   ";doorControllers=" + controllers +
                   ";doorControllersWithCollider=" + controllersWithCollider;
        }

        // ---------------------------------------------------------------- утилиты

        private static int CountPrivate(object target, System.Type type, string field)
        {
            if (target == null || type == null) return -1;
            var f = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) return -1;
            object v = f.GetValue(target);
            var list = v as System.Collections.ICollection;
            if (list != null) return list.Count;
            if (v is System.Array arr) return arr.Length;
            return -1;
        }

        private static string ReadFloatField(object target, System.Type type, string field)
        {
            var f = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) return "n/a";
            object v = f.GetValue(target);
            return v is float fl ? fl.ToString("F0", CultureInfo.InvariantCulture) : "n/a";
        }

        private static int Median(System.Collections.Generic.List<int> values)
        {
            if (values == null || values.Count == 0) return 0;
            var copy = new System.Collections.Generic.List<int>(values);
            copy.Sort();
            return copy[copy.Count / 2];
        }

        private static void Append(string line)
        {
            _lines.AppendLine(line);
            if (!string.IsNullOrEmpty(_outPath))
            {
                try { File.AppendAllText(_outPath, line + System.Environment.NewLine); } catch (System.Exception) { }
            }
        }

        private static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            Append("");
            Append("PROBE_DONE;result=" + result +
                   ";sceneSaved=no;push=no;readonly=yes" +
                   ";sceneDirtyNow=" + SceneManager.GetActiveScene().isDirty);
            SessionState.SetBool(SessionKey, false);
            _state = State.Done;
            Debug.Log("[T-CULL01/Acceptance] Завершено: " + result + ". Файл: " + _outPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        }
    }
}
