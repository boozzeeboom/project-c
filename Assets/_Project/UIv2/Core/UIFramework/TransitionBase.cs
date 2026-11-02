using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Core.UIFramework
{
    public abstract class TransitionBase : ITransition
    {
        public abstract Task PlayInAsync(VisualElement target, CancellationToken ct = default);
        public abstract Task PlayOutAsync(VisualElement target, CancellationToken ct = default);
    }

    public class FadeTransition : TransitionBase
    {
        public float durationMs = 200f;

        public override async Task PlayInAsync(VisualElement target, CancellationToken ct = default)
        {
            if (target == null) return;
            target.style.opacity = 0;
            target.style.display = DisplayStyle.Flex;
            // Simple fade via inline style; USS transitions preferred in production
            float t = 0f;
            while (t < 1f && !ct.IsCancellationRequested)
            {
                t += Time.deltaTime * (1000f / durationMs);
                target.style.opacity = Mathf.Clamp01(t);
                await Task.Yield();
            }
            target.style.opacity = 1;
        }

        public override async Task PlayOutAsync(VisualElement target, CancellationToken ct = default)
        {
            if (target == null) return;
            float t = 1f;
            while (t > 0f && !ct.IsCancellationRequested)
            {
                t -= Time.deltaTime * (1000f / durationMs);
                target.style.opacity = Mathf.Clamp01(t);
                await Task.Yield();
            }
            target.style.opacity = 0;
            target.style.display = DisplayStyle.None;
        }
    }
}
