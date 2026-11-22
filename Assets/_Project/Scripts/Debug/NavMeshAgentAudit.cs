using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using ProjectC.Ship;

namespace ProjectC.Diagnostics
{
    /// <summary>
    /// T-PERF05/C — разовая диагностика: сколько живых NavMeshAgent стоит вне навмеша.
    ///
    /// Зачем. В консоли замера D 671 сообщение `Failed to create agent because it is not close
    /// enough to the NavMesh` — нативное, без стека, из кода не глушится. Гипотеза: это крю на
    /// палубах (NavMeshAgent.enabled = true в префабах капитанов, инстанс на палубу в море).
    /// Гипотезу нельзя проверить по профайлеру — нужен подсчёт живых объектов в рантайме.
    ///
    /// Что делает. Само-устанавливается при входе в Play Mode (скрытый DontDestroyOnLoad-объект),
    /// делает два снимка — на ~8 с и ~20 с после старта — и пишет на каждый РОВНО ОДНУ запись
    /// в консоль: всего агентов / включённых / на навмеше / ВНЕ навмеша, разбивка вне-навмеша
    /// по именам (префабам), в иерархии ли корабля, есть ли рядом навмеш (NavMesh.SamplePosition)
    /// и примеры координат.
    ///
    /// Как читать. Строка `off-mesh=0` — гипотеза неверна, ищем дальше. Строка
    /// `off-mesh by name: <X>_Captain=1 …` — подтверждение: правится префаб капитанов.
    /// После решения компонент можно удалить (файл самодостаточный, ничего не держит).
    /// </summary>
    public sealed class NavMeshAgentAudit : MonoBehaviour
    {
        /// <summary>Выключатель без правки кода (по умолчанию включён).</summary>
        public static bool Enabled = true;

        private const string HostName = "[PerfNavAudit]";
        private const float FirstSnapshotAt = 8f;
        private const float SecondSnapshotAt = 20f;
        private const int TopNames = 12;
        private const int SamplePositions = 5;
        private const float NavProbeRadius = 60f;
        private const string ProxyPrefix = "NpcDeckNavProxy_";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain reload может быть выключен — не тащим «выключено» из прошлого сеанса.
            Enabled = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Enabled) return;
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            if (FindAnyObjectByType<NavMeshAgentAudit>() != null) return;

            var host = new GameObject(HostName);
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            host.AddComponent<NavMeshAgentAudit>();
        }

        private void Start()
        {
            if (!Enabled)
            {
                Destroy(gameObject);
                return;
            }
            StartCoroutine(RunSnapshots());
        }

        private IEnumerator RunSnapshots()
        {
            yield return new WaitForSecondsRealtime(FirstSnapshotAt);
            TakeSnapshot("8s");

            yield return new WaitForSecondsRealtime(SecondSnapshotAt - FirstSnapshotAt);
            TakeSnapshot("20s");
        }

        // ------------------------------------------------------------------ снимок

        private static void TakeSnapshot(string tag)
        {
            NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int total = agents.Length;
            int enabledCount = 0;
            int onMesh = 0;
            int offMesh = 0;
            int offMeshDriving = 0;
            int pausedUpdatePos = 0;
            int offMeshOnShip = 0;
            int offMeshNavNearby = 0;
            int offMeshOnInactiveObject = 0;
            float offMeshMaxNavDist = 0f;

            var offMeshByName = new Dictionary<string, int>(32);
            var allByName = new Dictionary<string, int>(64);
            var samples = new List<string>(SamplePositions);

            for (int i = 0; i < total; i++)
            {
                NavMeshAgent agent = agents[i];
                if (agent == null) continue;

                // Считаем ВСЕ живые компоненты, включая выключенные: инкремент идёт до проверки enabled.
                // Поэтому бакет ниже подписан `all agents by name`, а не `enabled` — иначе выключенные
                // агенты (например капитаны после варианта B) читаются как включённые.
                string bucket = BucketName(agent);
                Increment(allByName, bucket);

                if (!agent.enabled) continue;
                enabledCount++;

                if (!agent.updatePosition) pausedUpdatePos++;

                bool on = agent.isOnNavMesh;
                if (on)
                {
                    onMesh++;
                    continue;
                }

                offMesh++;
                Increment(offMeshByName, bucket);
                if (agent.updatePosition) offMeshDriving++;
                if (!agent.gameObject.activeInHierarchy) offMeshOnInactiveObject++;
                if (agent.GetComponentInParent<ShipDeckNav>(true) != null) offMeshOnShip++;

                Vector3 p = agent.transform.position;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, NavProbeRadius, NavMesh.AllAreas))
                {
                    offMeshNavNearby++;
                    if (hit.distance > offMeshMaxNavDist) offMeshMaxNavDist = hit.distance;
                }

                if (samples.Count < SamplePositions)
                    samples.Add($"{bucket}@({p.x:F0},{p.y:F0},{p.z:F0})");
            }

            var sb = new StringBuilder(768);
            sb.Append("[PerfNavAudit] t=").Append(tag)
              .Append(" frame=").Append(Time.frameCount)
              .Append(" | agents=").Append(total)
              .Append(" enabled=").Append(enabledCount)
              .Append(" onMesh=").Append(onMesh)
              .Append(" OFF-MESH=").Append(offMesh)
              .Append(" (driving=").Append(offMeshDriving)
              .Append(", inactiveGo=").Append(offMeshOnInactiveObject)
              .Append(", updatePositionOff=").Append(pausedUpdatePos).Append(')');

            sb.Append('\n').Append("   off-mesh: onShip=").Append(offMeshOnShip)
              .Append(" world=").Append(offMesh - offMeshOnShip)
              .Append(" | navmeshWithin").Append(NavProbeRadius.ToString("F0")).Append('m')
              .Append('=').Append(offMeshNavNearby)
              .Append(" maxDist=").Append(offMeshMaxNavDist.ToString("F1"));

            AppendTop(sb, "   off-mesh by name: ", offMeshByName);
            AppendTop(sb, "   all agents by name: ", allByName);

            if (samples.Count > 0)
                sb.Append('\n').Append("   off-mesh samples: ").Append(string.Join(", ", samples));

            Debug.Log(sb.ToString());
        }

        private static void AppendTop(StringBuilder sb, string header, Dictionary<string, int> counts)
        {
            if (counts.Count == 0) return;

            var list = new List<KeyValuePair<string, int>>(counts);
            list.Sort((a, b) =>
            {
                int byCount = b.Value.CompareTo(a.Value);
                return byCount != 0 ? byCount : string.CompareOrdinal(a.Key, b.Key);
            });

            sb.Append('\n').Append(header);
            int limit = Mathf.Min(TopNames, list.Count);
            for (int i = 0; i < limit; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(list[i].Key).Append('=').Append(list[i].Value);
            }
            if (list.Count > limit)
                sb.Append(", …(+").Append(list.Count - limit).Append(" names)");
        }

        /// <summary>Прокси палубной навигации создаются per-NPC — сваливаем их в один бакет.</summary>
        private static string BucketName(NavMeshAgent agent)
        {
            string n = agent.gameObject.name;
            if (n.StartsWith(ProxyPrefix, System.StringComparison.Ordinal))
                return ProxyPrefix + "*";
            return n;
        }

        private static void Increment(Dictionary<string, int> map, string key)
        {
            map.TryGetValue(key, out int count);
            map[key] = count + 1;
        }
    }
}
