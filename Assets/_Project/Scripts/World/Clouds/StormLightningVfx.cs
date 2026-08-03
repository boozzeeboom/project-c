// StormLightningVfx.cs — Phase 2.4
// Subscribes to StormController.OnLightningTriggered and plays VFX Graph lightning bolt.
// Null-guard: VisualEffect may be absent in test scenes.

using UnityEngine;
using UnityEngine.VFX;
using ProjectC.Core;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Подписывается на StormController.OnLightningTriggered и запускает
    /// VFX Graph молнию (LightningBolt.vfx) с параметрами позиции шторма.
    /// </summary>
    public class StormLightningVfx : MonoBehaviour
    {
        [Header("VFX")]
        [Tooltip("VisualEffect компонент с LightningBolt.vfx.")]
        public VisualEffect Vfx;

        [Header("Lightning")]
        [Tooltip("Высота верхней точки молнии над центром шторма.")]
        public float BoltTopOffset = 300f;
        [Tooltip("Высота нижней точки молнии под центром шторма.")]
        public float BoltBottomOffset = 50f;
        [Range(0.1f, 2f)] public float BoltDuration = 0.3f;

        // VFX property IDs
        private static readonly int StartPosId = Shader.PropertyToID("StartPos");
        private static readonly int EndPosId   = Shader.PropertyToID("EndPos");
        private static readonly int SeedId     = Shader.PropertyToID("Seed");

        private void Start()
        {
            if (Vfx == null)
                Vfx = GetComponent<VisualEffect>();

            if (Vfx != null)
                Vfx.Stop();
        }

        private void OnEnable()
        {
            StormController.OnLightningTriggered += HandleLightning;
        }

        private void OnDisable()
        {
            StormController.OnLightningTriggered -= HandleLightning;
        }

        private void HandleLightning(StormController storm)
        {
            if (Vfx == null) return;

            Vector3 pos = storm.transform.position;
            Vfx.SetVector3(StartPosId, pos + Vector3.up * BoltTopOffset);
            Vfx.SetVector3(EndPosId, pos - Vector3.up * BoltBottomOffset);
            Vfx.SetFloat(SeedId, Random.value);
            Vfx.Play();

            // Auto-stop after duration
            StartCoroutine(StopAfterDelay());
        }

        private System.Collections.IEnumerator StopAfterDelay()
        {
            yield return new WaitForSeconds(BoltDuration);
            if (Vfx != null) Vfx.Stop();
        }
    }
}
