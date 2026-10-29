// Cloudy Navigator — движение окна (шаг 5 плана; образец: .Aura/Mods/v4-2/src/motion.js).
//
// Один общий слой на всё окно: облако собирается ПЕРЕД появлением блока, рассеивается ПОСЛЕ его ухода,
// смена содержимого — старые подписи уносит тот же порыв. Своего планировщика и таймеров нет: кадр приходит
// из CloudyNavigatorWindow.Update(dt), значения берутся из накопленного времени (как в моде — update(dt)).
//
// Механика — не копия, а вызов уже проверенных классов меню:
//   ProjectCMainMenuSmokeField  — пуфы gather/disperse (своя запечённая текстура, свой пул);
//   ProjectCMainMenuTextBlower  — снос подписей по буквам (ghost-копия, возвращает host на место).
// Пул ограничен 48, как POOL_MAX мода: на открытие/закрытие уходит 8 + 8 + 6 пуфов, потолок не достигается.
//
// Длительности (DUR мода, секунды): gather .26, lead .12, reveal .22, disperse .32, swapOut .22, swapIn .24,
// swapLead .06, edge .30, fade .15. Кривая — CSS ease-out (ProjectCMainMenuPalette.EaseOut).
// prefers-reduced-motion → без пуфов, простой fade .15 с; флаг хранится в PlayerPrefs с префиксом окна.
using System;
using System.Collections.Generic;
using ProjectCMainMenu;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Тайминги мода (<c>src/motion.js</c> DUR), секунды.</summary>
    public static class CnMotionTiming
    {
        public const float Gather = 0.26f;
        public const float Lead = 0.12f;
        public const float Reveal = 0.22f;
        public const float Disperse = 0.32f;
        public const float SwapOut = 0.22f;
        public const float SwapIn = 0.24f;
        public const float SwapLead = 0.06f;
        public const float Edge = 0.30f;
        public const float Fade = 0.15f;

        /// <summary>POOL_MAX мода: столько пуфов максимум живут одновременно.</summary>
        public const int PoolMax = 48;
    }

    /// <summary>Облачные переходы окна. Только визуал: состояние игры слой не читает и не меняет.</summary>
    public sealed class CnMotion
    {
        private const string ReducedMotionKey = "ProjectC.CloudyNavigator.ReducedMotion";
        private const float GustDecay = 2.2f;   // mod: gust.value -= dt * 2.2

        private sealed class Reveal
        {
            public VisualElement El;
            public float T;
            public float Delay;
            public float Duration;
            public float From;
            public float To;
            public float Drift;
            public Action OnDone;
        }

        private readonly VisualElement _layer;
        private readonly ProjectCMainMenuSmokeField _smoke;
        private readonly ProjectCMainMenuTextBlower _blower;
        private readonly List<Reveal> _reveals = new List<Reveal>(8);

        private float _gust;
        private int _gustDir = 1;

        public CnMotion()
        {
            Reduced = PlayerPrefs.GetInt(ReducedMotionKey, 0) == 1;

            _layer = new VisualElement { name = "cn-motion", pickingMode = PickingMode.Ignore };
            _layer.AddToClassList("cn-motion");
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0f;
            _layer.style.top = 0f;
            _layer.style.right = 0f;
            _layer.style.bottom = 0f;

            // оба класса сами вставляют свой слой в host
            _smoke = new ProjectCMainMenuSmokeField(_layer, ProjectCMainMenuSmokeField.DefaultSeed,
                CnMotionTiming.PoolMax);
            _blower = new ProjectCMainMenuTextBlower(_layer, ProjectCMainMenuTextBlower.DefaultSeed,
                CnMotionTiming.PoolMax);
        }

        /// <summary>Слой движения: кладётся последним ребёнком корня, чтобы пуфы шли поверх панелей.</summary>
        public VisualElement Layer => _layer;

        /// <summary>Уменьшенное движение: пуфы не создаются, остаётся fade.</summary>
        public bool Reduced { get; private set; }

        /// <summary>Есть ли незавершённые проявления (по этому флагу хост решает, когда прятать корень).</summary>
        public bool Busy => _reveals.Count > 0;

        public int PuffCount => _smoke.Count();

        public float Gust => _gust;

        public void SetReduced(bool reduced)
        {
            Reduced = reduced;
            PlayerPrefs.SetInt(ReducedMotionKey, reduced ? 1 : 0);
        }

        /// <summary>Общий порыв: значение не убывает от нового толчка и гаснет само (~0.45 с).</summary>
        public float Blow(float strength, int dir = 0)
        {
            _gust = Mathf.Max(_gust, Mathf.Clamp01(strength));
            if (dir != 0) _gustDir = dir < 0 ? -1 : 1;
            return _gust;
        }

        /// <summary>Появление: облако собирается к прямоугольнику, затем блок проявляется по opacity.</summary>
        public void Open(VisualElement target, int count = 8, float strength = 1f, float delay = 0f)
        {
            if (target == null) return;

            target.style.opacity = 0f;

            Rect rect = target.worldBound;
            if (!Reduced && rect.width > 1f && rect.height > 1f)
            {
                _smoke.Gather(rect, count, strength, 1f, CnMotionTiming.Gather * 1000f, 0f, _gustDir);
                Blow(0.55f);
            }

            Animate(target, 0f, 1f, Reduced ? 0f : CnMotionTiming.Lead + delay,
                Reduced ? CnMotionTiming.Fade : CnMotionTiming.Reveal, 0f, null);
        }

        /// <summary>
        /// Уход: вызывать, пока блок ещё виден — прямоугольник берётся с живой раскладки.
        /// <paramref name="onDone"/> срабатывает в кадре, когда fade закончился.
        /// </summary>
        public void Close(VisualElement target, int count = 8, float strength = 1f, Action onDone = null)
        {
            if (target == null)
            {
                onDone?.Invoke();
                return;
            }

            Rect rect = target.worldBound;
            if (!Reduced && rect.width > 1f && rect.height > 1f)
            {
                Blow(0.8f);
                _smoke.Disperse(rect, count, strength, 1f, CnMotionTiming.Disperse * 1000f, 0f, _gustDir);
            }

            float from = target.resolvedStyle.opacity;
            Animate(target, float.IsNaN(from) ? 1f : from, 0f, 0f,
                Reduced ? CnMotionTiming.Fade : CnMotionTiming.Disperse * 0.5f, 0f, onDone);
        }

        /// <summary>
        /// Смена содержимого (вкладка, фокус дока): старые подписи уносит порыв, новые проявляются против ветра.
        /// <paramref name="render"/> перестраивает содержимое — вызывается до замера.
        /// </summary>
        public void Swap(VisualElement target, Action render, int count = 4, float strength = 0.8f)
        {
            render?.Invoke();
            if (target == null) return;

            Rect rect = target.worldBound;
            if (!Reduced && rect.width > 1f && rect.height > 1f)
            {
                Blow(0.5f);
                _smoke.Disperse(rect, count, strength, 0.7f, CnMotionTiming.SwapOut * 1000f, 0f, _gustDir);
            }

            target.style.opacity = 0f;
            Animate(target, 0f, 1f, Reduced ? 0f : CnMotionTiming.SwapLead,
                Reduced ? CnMotionTiming.Fade : CnMotionTiming.SwapIn, Reduced ? 0f : 14f, null);
        }

        /// <summary>Дымная кромка (подъём дока): короткая полоса дыма по верхнему краю блока.</summary>
        public void Edge(VisualElement target, int count = 4)
        {
            if (Reduced || target == null) return;

            Rect rect = target.worldBound;
            if (rect.width <= 1f || rect.height <= 1f) return;

            var band = new Rect(rect.x, rect.y, rect.width, Mathf.Min(24f, rect.height * 0.25f));
            Blow(0.45f);
            _smoke.Gather(band, count, 0.7f, 0.45f, CnMotionTiming.Edge * 1000f, 0f, _gustDir);
        }

        /// <summary>Растворение подписи по буквам: снос идёт тем же порывом, что и облако.</summary>
        public int Dissolve(VisualElement label, int dir = 0, float stagger = 20f, float duration = 680f)
        {
            if (Reduced || label == null) return 0;

            int wind = dir == 0 ? _gustDir : dir;
            Blow(0.6f, wind);
            return _blower.Blow(label, wind, stagger, duration, 1f, 2, false, 0f, 1f,
                (rect, _) => _smoke.Disperse(rect, 1, 0.7f, 1f, 300f, 0f, wind));
        }

        /// <summary>Кадр: порыв, проявления, пуфы и снос подписей. dt уже зажат хостом.</summary>
        public void Tick(float dt)
        {
            float d = Mathf.Clamp(dt, 0f, 0.1f);
            _gust = Mathf.Max(0f, _gust - d * GustDecay);

            for (int i = _reveals.Count - 1; i >= 0; i--)
            {
                Reveal reveal = _reveals[i];
                if (reveal.El == null)
                {
                    _reveals.RemoveAt(i);
                    continue;
                }

                reveal.T += d;
                float local = reveal.T - reveal.Delay;
                if (local < 0f) continue;

                float k = Mathf.Clamp01(local / reveal.Duration);
                float e = ProjectCMainMenuPalette.EaseOut(k);
                reveal.El.style.opacity = Mathf.Lerp(reveal.From, reveal.To, e);

                if (reveal.Drift > 0f)
                {
                    float dx = -_gustDir * reveal.Drift * (1f - e);
                    reveal.El.style.translate = new StyleTranslate(
                        new Translate(Length.Pixels(dx), Length.Pixels(0f)));
                }

                if (k < 1f) continue;

                reveal.El.style.opacity = reveal.To;
                if (reveal.Drift > 0f)
                {
                    reveal.El.style.translate = new StyleTranslate(
                        new Translate(Length.Pixels(0f), Length.Pixels(0f)));
                }

                _reveals.RemoveAt(i);
                reveal.OnDone?.Invoke();
            }

            _smoke.Tick();
            _blower.Tick();
        }

        /// <summary>Довести все переходы до конца немедленно (скрытие окна, Dispose).</summary>
        public void FinishAll()
        {
            for (int i = _reveals.Count - 1; i >= 0; i--)
            {
                Reveal reveal = _reveals[i];
                if (reveal.El != null)
                {
                    reveal.El.style.opacity = reveal.To;
                    if (reveal.Drift > 0f)
                    {
                        reveal.El.style.translate = new StyleTranslate(
                            new Translate(Length.Pixels(0f), Length.Pixels(0f)));
                    }
                }

                reveal.OnDone?.Invoke();
            }

            _reveals.Clear();
        }

        /// <summary>Снять всё движение и вернуть подписи на место.</summary>
        public void Reset()
        {
            _reveals.Clear();
            _gust = 0f;
            _smoke.Clear();
            _blower.Clear();
        }

        private void Animate(VisualElement el, float from, float to, float delay, float duration, float drift,
            Action onDone)
        {
            for (int i = _reveals.Count - 1; i >= 0; i--)
            {
                if (_reveals[i].El == el) _reveals.RemoveAt(i);
            }

            _reveals.Add(new Reveal
            {
                El = el,
                T = 0f,
                Delay = Mathf.Max(0f, delay),
                Duration = Mathf.Max(0.01f, duration),
                From = from,
                To = to,
                Drift = drift,
                OnDone = onDone,
            });
        }
    }
}
