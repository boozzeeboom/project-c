using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.AI;
using ProjectC.Combat;
using ProjectC.Core;
using ProjectC.Core.ShipPosition;
using ProjectC.Player;
using ProjectC.UI;
using ProjectC.UI.MainMenu;
using ProjectC.World;
using ProjectC.World.Clouds;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.Admin
{
    /// <summary>
    /// T-ADM-02: единый фасад админ-панели (D5 — максимально полный).
    /// Только вызывает существующие public методы; новой игровой логики нет
    /// (кроме читов — они в AdminMoveCheats).
    /// Обычный MonoBehaviour (НЕ NetworkBehaviour): динамический спавн сетевых
    /// объектов сломан в проекте (NetworkPrefabsList), поэтому серверные вызовы —
    /// через существующие серверные синглтоны и только в host/single (см. L1_DESIGN §6).
    /// Мировых Vector3 между кадрами не хранит (FO-безопасно).
    /// </summary>
    public class AdminFacade : MonoBehaviour
    {
        public static AdminFacade Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Ленивое создание (образец: HUDManager.EnsureExists). BootstrapScene не трогаем.</summary>
        public static AdminFacade EnsureExists()
        {
            if (Instance != null) return Instance;
            var found = FindAnyObjectByType<AdminFacade>();
            if (found != null) return found;
            var go = new GameObject("AdminFacade");
            DontDestroyOnLoad(go);
            return go.AddComponent<AdminFacade>();
        }

        // ==================== Игрок ====================

        /// <summary>Локальный игрок (owner). Null если не заспавнен.</summary>
        public NetworkPlayer LocalPlayer()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
            {
                var np = nm.LocalClient.PlayerObject.GetComponent<NetworkPlayer>();
                if (np != null) return np;
            }
            var all = FindObjectsByType<NetworkPlayer>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].IsOwner) return all[i];
            return null;
        }

        public AdminMoveCheats EnsureMoveCheats()
        {
            var p = LocalPlayer();
            if (p == null) { Debug.LogWarning("[AdminFacade] No local player for MoveCheats"); return null; }
            var cheats = p.GetComponent<AdminMoveCheats>();
            if (cheats == null) cheats = p.gameObject.AddComponent<AdminMoveCheats>();
            return cheats;
        }

        public void SetGod(bool enabled)
        {
            var cheats = EnsureMoveCheats();
            if (cheats != null) cheats.SetGod(enabled);
        }

        public void SetNoclip(bool enabled)
        {
            var cheats = EnsureMoveCheats();
            if (cheats != null) cheats.SetNoclip(enabled);
        }

        public void SetSpeedMult(float mult)
        {
            var cheats = EnsureMoveCheats();
            if (cheats != null) cheats.SetSpeedMult(mult);
            else
            {
                // Запасной путь без компонента: напрямую через PlayerController.
                var p = LocalPlayer();
                var pc = p != null ? p.GetComponent<PlayerController>() : null;
                if (pc != null) pc.SpeedMultiplier = Mathf.Max(0.1f, mult);
            }
        }

        /// <summary>Клиентский телепорт себя (CC-safe, тот же приём что PlayerPositionServer.TeleportPlayer).</summary>
        public bool TeleportLocalPlayer(Vector3 position)
        {
            var p = LocalPlayer();
            if (p == null) { Debug.LogWarning("[AdminFacade] TeleportLocalPlayer: no local player"); return false; }
            var controller = p.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            p.transform.position = position;
            if (controller != null) controller.enabled = true;
            Physics.SyncTransforms();
            Debug.Log($"[AdminFacade] Teleported local player to {position}");
            return true;
        }

        /// <summary>Телепорт по clientId: на сервере — через PlayerPositionServer, иначе fallback на себя.</summary>
        public bool AdminTeleportPlayer(ulong clientId, Vector3 position)
        {
            var server = PlayerPositionServer.Instance;
            if (server != null) return server.AdminTeleportPlayer(clientId, position);
            var p = LocalPlayer();
            if (p != null && p.OwnerClientId == clientId) return TeleportLocalPlayer(position);
            Debug.LogWarning("[AdminFacade] AdminTeleportPlayer: no server and not own id");
            return false;
        }

        // ==================== Камера/полёт ====================

        public void ToggleFly()
        {
            var cam = FindAnyObjectByType<WorldCamera>();
            if (cam != null) cam.ToggleFlyMode();
            else Debug.LogWarning("[AdminFacade] WorldCamera not found (её нет в рантайм-сценах — полёт игрока через Noclip)");
        }

        public void TeleportToPeak(int index)
        {
            var cam = FindAnyObjectByType<WorldCamera>();
            if (cam != null) cam.TeleportToPeak(index);
            else Debug.LogWarning("[AdminFacade] WorldCamera not found — используй TeleportPlayerToPeak");
        }

        /// <summary>Пики напрямую из WorldGenerator (WorldCamera в рантайме нет).</summary>
        public List<WorldGenerator.PeakInfo> GetPeaks()
        {
            var gen = FindAnyObjectByType<WorldGenerator>();
            if (gen == null) return new List<WorldGenerator.PeakInfo>();
            return gen.GetAllPeaks();
        }

        /// <summary>Телепорт ИГРОКА к пику (над вершиной +50м, CC-safe). Возвращает имя пика или null.</summary>
        public string TeleportPlayerToPeak(int index)
        {
            var peaks = GetPeaks();
            if (index < 0 || index >= peaks.Count)
            {
                Debug.LogWarning($"[AdminFacade] TeleportPlayerToPeak: bad index {index} (всего {peaks.Count})");
                return null;
            }
            var peak = peaks[index];
            Vector3 pos = peak.position + Vector3.up * (peak.height * 0.5f + 50f);
            if (!TeleportLocalPlayer(pos)) return null;
            Debug.Log($"[AdminFacade] Player teleported to peak '{peak.name}' at {pos}");
            return peak.name;
        }

        // ==================== Респавн ====================

        public List<Vector3> GetRespawnPositions()
        {
            var result = new List<Vector3>();
            var rm = FindAnyObjectByType<RespawnManager>();
            if (rm == null) return result;
            for (int i = 0; i < rm.Count; i++) result.Add(rm.GetEffectivePosition(i));
            return result;
        }

        // ==================== HUD ====================

        /// <summary>PerfHUD: найти или создать (в сценах его нет — см. шапку класса).</summary>
        public ProjectCPerfHUD EnsurePerfHud()
        {
            var hud = FindAnyObjectByType<ProjectCPerfHUD>();
            if (hud != null) return hud;
            var go = new GameObject("ProjectCPerfHUD (Admin)");
            DontDestroyOnLoad(go);
            Debug.Log("[AdminFacade] ProjectCPerfHUD auto-created (в сценах отсутствует)");
            return go.AddComponent<ProjectCPerfHUD>();
        }

        public void SetPerfHud(bool visible)
        {
            EnsurePerfHud().SetVisible(visible);
        }

        public void SetSceneHud(bool visible)
        {
            // T-ADM-08 заменит на SceneDebugHUD.SetVisible; пока — активностью объекта.
            var hud = FindAnyObjectByType<SceneDebugHUD>();
            if (hud != null) hud.gameObject.SetActive(visible);
            else Debug.LogWarning("[AdminFacade] SceneDebugHUD not found");
        }

        public void SetDayNightOverlay(bool visible)
        {
            var c = FindAnyObjectByType<DayNightController>();
            if (c == null) { Debug.LogWarning("[AdminFacade] DayNightController not found"); return; }
            c.showDebugOverlay = visible;
            if (c.profile != null) c.profile.showDebugInfo = visible;
        }

        public string GetNgoSummary()
        {
            var m = NgoMetricsCollector.Instance;
            if (m == null)
            {
                // В сценах коллектора нет — создаём (чистый счётчик, безопасно).
                var go = new GameObject("NgoMetricsCollector (Admin)");
                DontDestroyOnLoad(go);
                m = go.AddComponent<NgoMetricsCollector>();
            }
            return m.GetSummary();
        }

        // ==================== Сдвиг мира / стриминг ====================

        public void RequestRebase(bool withRollback)
        {
            var slice = FindAnyObjectByType<GlobalMotionControlledRebaseSlice>();
            if (slice != null) slice.RequestControlledRebase(withRollback);
            else Debug.LogWarning("[AdminFacade] GlobalMotionControlledRebaseSlice not found");
        }

        public void LoadChunksHere()
        {
            var p = LocalPlayer();
            var sm = WorldStreamingManager.Instance;
            if (sm == null) { Debug.LogWarning("[AdminFacade] WorldStreamingManager not found"); return; }
            Vector3 pos = p != null ? p.transform.position
                : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            sm.LoadChunksAroundPlayer(pos);
        }

        // ==================== Логи (D4) ====================

        /// <summary>Мастер-mute: шина + прямое выставление известных флагов (сами поля не удаляем).</summary>
        public void SetLogsMuted(bool muted)
        {
            AdminLogBus.SetMuted(muted);

            var spawners = FindObjectsByType<NpcSpawner>();
            for (int i = 0; i < spawners.Length; i++)
                if (spawners[i] != null) spawners[i].SetDebugLogs(!muted);

            if (PlayerPositionServer.Instance != null)
                PlayerPositionServer.Instance.SetDebugMode(!muted);
            if (ShipPositionServer.Instance != null)
                ShipPositionServer.Instance.SetDebugMode(!muted);

            var trackers = FindObjectsByType<PlayerRespawnTracker>();
            for (int i = 0; i < trackers.Length; i++)
                if (trackers[i] != null) trackers[i].SetDebugLog(!muted);

            var targets = FindObjectsByType<PlayerTarget>();
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) targets[i].SetDebugLog(!muted);

            var buffers = FindObjectsByType<LocalDensityBuffer>();
            for (int i = 0; i < buffers.Length; i++)
                if (buffers[i] != null) buffers[i].SetVerboseLogging(!muted);

            var dayNight = FindAnyObjectByType<DayNightController>();
            if (dayNight != null)
            {
                dayNight.logInitialization = !muted;
                dayNight.logVolumeBlend = !muted;
                dayNight.logWarnings = !muted;
            }
            var constellation = FindAnyObjectByType<ConstellationController>();
            if (constellation != null) constellation.showDebugGizmos = !muted;
        }

        // ==================== Сейвы (1-в-1 из PersistenceDebugTools) ====================

        public string WipeAllSaves() => PersistenceDebugTools.DeleteAllSaves();
        public string WipePositions() => PersistenceDebugTools.DeleteCharacterPositionSaves();
        public string WipeInventory() => PersistenceDebugTools.DeleteCharacterInventory();
        public string WipeProgression() => PersistenceDebugTools.DeleteCharacterProgression();
        public string WipeCustomisation() => PersistenceDebugTools.DeleteCharacterCustomisation();
        public string WipeQuests() => PersistenceDebugTools.DeleteQuestSaves();
        public string WipeSkillBindings() => PersistenceDebugTools.DeleteSkillBindingSaves();
        public string WipeKeyInstances() => PersistenceDebugTools.DeleteKeyInstanceSaves();
        public string WipeWorldTime() => PersistenceDebugTools.DeleteWorldTimeSaves();
        public string WipeTrade() => PersistenceDebugTools.DeleteTradeSaves();
    }
}
