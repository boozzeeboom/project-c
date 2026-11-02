using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Core.Animation
{
    /// <summary>
    /// Simple tween helper for UI Toolkit (no external dependencies).
    /// Use coroutines for fade/slide/scale animations.
    /// For complex animations (halos, particles, 3D) use Rive or VFX Graph.
    /// </summary>
    public static class UIAnimator
    {
        public static IEnumerator FadeIn(VisualElement target, float duration = 0.2f, Action onComplete = null)
        {
            if (target == null) yield break;
            target.style.opacity = 0;
            target.style.display = DisplayStyle.Flex;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                target.style.opacity = Mathf.Clamp01(t);
                yield return null;
            }
            target.style.opacity = 1;
            onComplete?.Invoke();
        }

        public static IEnumerator FadeOut(VisualElement target, float duration = 0.2f, Action onComplete = null)
        {
            if (target == null) yield break;
            float t = 1f;
            while (t > 0f)
            {
                t -= Time.unscaledDeltaTime / duration;
                target.style.opacity = Mathf.Clamp01(t);
                yield return null;
            }
            target.style.opacity = 0;
            target.style.display = DisplayStyle.None;
            onComplete?.Invoke();
        }

        public static IEnumerator SlideIn(VisualElement target, Vector2 from, float duration = 0.25f, Action onComplete = null)
        {
            if (target == null) yield break;
            target.style.translate = new Translate(from.x, from.y);
            target.style.display = DisplayStyle.Flex;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
                target.style.translate = new Translate(
                    Mathf.Lerp(from.x, 0, eased),
                    Mathf.Lerp(from.y, 0, eased));
                yield return null;
            }
            target.style.translate = new Translate(0, 0);
            onComplete?.Invoke();
        }

        public static IEnumerator SlideOut(VisualElement target, Vector2 to, float duration = 0.25f, Action onComplete = null)
        {
            if (target == null) yield break;
            float t = 0f;
            Vector2 start = new Vector2(target.resolvedStyle.translate.x, target.resolvedStyle.translate.y);
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float eased = t * t; // ease-in quad
                target.style.translate = new Translate(
                    Mathf.Lerp(start.x, to.x, eased),
                    Mathf.Lerp(start.y, to.y, eased));
                yield return null;
            }
            target.style.translate = new Translate(to.x, to.y);
            target.style.display = DisplayStyle.None;
            onComplete?.Invoke();
        }

        public static IEnumerator ScaleIn(VisualElement target, float duration = 0.2f, Action onComplete = null)
        {
            if (target == null) yield break;
            target.style.scale = new Scale(new Vector2(0.8f, 0.8f));
            target.style.opacity = 0;
            target.style.display = DisplayStyle.Flex;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float s = Mathf.Lerp(0.8f, 1f, eased);
                target.style.scale = new Scale(new Vector2(s, s));
                target.style.opacity = Mathf.Clamp01(t);
                yield return null;
            }
            target.style.scale = new Scale(new Vector2(1f, 1f));
            target.style.opacity = 1;
            onComplete?.Invoke();
        }

        public static IEnumerator Pulse(VisualElement target, float scale = 1.05f, float duration = 0.3f)
        {
            if (target == null) yield break;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / (duration / 2f);
                float s = Mathf.Lerp(1f, scale, Mathf.Sin(t * Mathf.PI));
                target.style.scale = new Scale(new Vector2(s, s));
                yield return null;
            }
            target.style.scale = new Scale(new Vector2(1f, 1f));
        }
    }
}
