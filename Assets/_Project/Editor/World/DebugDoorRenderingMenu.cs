using System.Text;
using UnityEngine;
using UnityEditor;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Быстрая диагностика «двери видны в редакторе, но не в игре».
    /// Запуск: в PLAY — меню ProjectC → World → Debug → Двери: диагностика рендеринга.
    /// Пишет в консоль: активные камеры и их маски, попадает ли дверь во фрустум,
    /// состояние рендереров тестовых дверей и используемый URP-ассет.
    /// </summary>
    public static class DebugDoorRenderingMenu
    {
        [MenuItem("ProjectC/World/Debug/Двери: диагностика рендеринга (Play)")]
        public static void Run()
        {
            var sb = new StringBuilder();
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[DoorDebug] Запустите Play Mode и повторите меню.");
                return;
            }

            // 1. Камеры
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine("КАМЕР: " + cams.Length);
            foreach (var cam in cams)
            {
                if (cam == null || !cam.isActiveAndEnabled) continue;
                bool l8 = (cam.cullingMask & (1 << 8)) != 0;
                sb.AppendLine($"  [{(cam == Camera.main ? "MAIN" : "   ")}] {cam.name} scene={cam.gameObject.scene.name} " +
                              $"mask=0x{cam.cullingMask:X8} слойPRIMUM8={(l8 ? "ДА" : "НЕТ")} far={cam.farClipPlane} " +
                              $"pos={cam.transform.position.ToString("F1")}");
            }

            // 2. URP / качество
            var rp = QualitySettings.renderPipeline;
            sb.AppendLine("RenderPipeline (качество): " + (rp != null ? rp.name + " (" + rp.GetType().Name + ")" : "builtin"));

            // 3. Тестовые двери
            var root = GameObject.Find("PRIMUM/PRIMUM_HOUSES_S9_TRD-N_P1_28H/DOORS_PREFAB_TEST");
            if (root == null)
            {
                sb.AppendLine("УЗЕЛ DOORS_PREFAB_TEST НЕ НАЙДЕН в сцене!");
                Debug.Log("[DoorDebug]\n" + sb.ToString());
                return;
            }
            sb.AppendLine("ТЕСТОВЫХ ДВЕРЕЙ: " + root.transform.childCount);

            int n = 0;
            foreach (Transform door in root.transform)
            {
                if (n++ >= 5) break;
                sb.AppendLine("-- " + door.name + " active=" + door.gameObject.activeSelf +
                              " pos=" + door.position.ToString("F1"));
                foreach (var r in door.GetComponentsInChildren<Renderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    sb.AppendLine($"     {r.name}: enabled={r.enabled} forceOff={r.forceRenderingOff} " +
                                  $"isVisible={r.isVisible} bounds={r.bounds.size.ToString("F2")} " +
                                  $"layer={LayerMask.LayerToName(r.gameObject.layer)} " +
                                  $"renderingLayerMask=0x{(uint)r.renderingLayerMask:X} " +
                                  $"mat={(r.sharedMaterial != null ? r.sharedMaterial.name + "/" + (r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "?") : "NULL")} " +
                                  $"mesh={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount + "v" : "нет")}");

                    // 4. Во фрустуме ли у основной камеры?
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                        bool inFrustum = GeometryUtility.TestPlanesAABB(planes, r.bounds);
                        float dist = Vector3.Distance(cam.transform.position, r.bounds.center);
                        sb.AppendLine($"     фрустум MainCamera: {(inFrustum ? "ДА" : "НЕТ")} | дистанция {dist:F1} м");
                    }
                }
            }

            // 5. Все рендереры слоя PRIMUM(8): сколько реально видно в этом кадре
            int cityVisible = 0, cityTotal = 0;
            foreach (var rr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (rr.gameObject.layer != 8) continue;
                cityTotal++;
                if (rr.isVisible) cityVisible++;
            }
            sb.AppendLine($"рендереры слоя PRIMUM(8): {cityTotal}, isVisible={cityVisible} (включая тестовые двери)");

            Debug.Log("[DoorDebug]\n" + sb.ToString());
        }
    }
}
