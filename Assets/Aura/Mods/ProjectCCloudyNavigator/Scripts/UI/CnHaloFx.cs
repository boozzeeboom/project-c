// Cloudy Navigator — кадровые keyframes ореолов (06 §8, перенос анимаций мода).
//
// В USS нет @keyframes, а анимации мода — это scale + opacity по кадрам (rwpulse, calpulse, calhl, calhl2,
// won, bghit-hl, chflash). Поэтому импульс считается здесь: один статический список активных записей и
// Tick(dt) из CloudyNavigatorWindow.Update — того же кадра, что ведёт CnMotion. Своих таймеров нет:
// планировщик панели и setTimeout-цепочки не используются, время копится из dt окна.
//
// Импульс живёт на элементе-ореоле: рамка создаётся один раз по числам мода и погашена (display:none),
// импульс её показывает, разгоняет по ease-out и снова гасит. Если элемент ушёл из дерева (пересборка
// панели), запись снимается сама — мёртвых ссылок список не держит.
//
// Кривая — CSS ease-out (ProjectCMainMenuPalette.EaseOut), та же, что у движения окна: в моде easing
// всех keyframes из §8 записан как ease-out.
using System.Collections.Generic;
using ProjectCMainMenu;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Кадровый драйвер импульсов ореолов: scale + opacity, ease-out, один повтор.</summary>
    public static class CnHaloFx
    {
        /// <summary>Класс ореола, который умеет импульс: он гашен по умолчанию и его показывает Pulse.</summary>
        public const string PulseClass = "cn-halo--pulse";

        private sealed class Impulse
        {
            public VisualElement El;
            public float T;
            public float Duration;
            public float ScaleTo;
            public float AlphaFrom;
        }

        private static readonly List<Impulse> Items = new List<Impulse>(8);

        /// <summary>Сколько импульсов считается прямо сейчас — для пробы окна.</summary>
        public static int Count => Items.Count;

        /// <summary>
        /// Импульс ореола-ребёнка <paramref name="host"/> с классом <see cref="PulseClass"/>.
        /// false — у хоста такого ореола нет (панель ещё не собрана нужным видом).
        /// </summary>
        public static bool Pulse(VisualElement host, float duration, float scaleTo, float alphaFrom)
        {
            VisualElement halo = Find(host);
            if (halo == null) return false;

            for (int i = Items.Count - 1; i >= 0; i--)
                if (Items[i].El == halo) Items.RemoveAt(i);

            halo.style.display = DisplayStyle.Flex;
            halo.style.opacity = alphaFrom;
            halo.style.scale = new Scale(new Vector2(1f, 1f));

            Items.Add(new Impulse
            {
                El = halo,
                T = 0f,
                Duration = Mathf.Max(0.05f, duration),
                ScaleTo = scaleTo,
                AlphaFrom = alphaFrom,
            });

            return true;
        }

        /// <summary>Кадр: продвинуть все импульсы. dt уже зажат хостом окна.</summary>
        public static void Tick(float dt)
        {
            if (Items.Count == 0) return;

            float d = Mathf.Clamp(dt, 0f, 0.1f);

            for (int i = Items.Count - 1; i >= 0; i--)
            {
                Impulse impulse = Items[i];

                // Элемент выпал из дерева: панель пересобралась или закрылась — импульс больше некуда вести.
                // Проверка по родителю, а не по panel: у корня закрытой панели panel уже null, но его дети
                // ещё в дереве, и добить их импульс дешевле, чем терять кадр на возврат класса.
                if (impulse.El == null || impulse.El.hierarchy.parent == null)
                {
                    Items.RemoveAt(i);
                    continue;
                }

                impulse.T += d;
                float k = Mathf.Clamp01(impulse.T / impulse.Duration);
                float e = ProjectCMainMenuPalette.EaseOut(k);

                impulse.El.style.opacity = Mathf.Lerp(impulse.AlphaFrom, 0f, e);
                float s = Mathf.Lerp(1f, impulse.ScaleTo, e);
                impulse.El.style.scale = new Scale(new Vector2(s, s));

                if (k < 1f) continue;

                impulse.El.style.opacity = 0f;
                impulse.El.style.display = DisplayStyle.None;
                Items.RemoveAt(i);
            }
        }

        /// <summary>Снять все импульсы и погасить их ореолы (скрытие окна, Dispose).</summary>
        public static void Reset()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].El == null) continue;
                Items[i].El.style.opacity = 0f;
                Items[i].El.style.display = DisplayStyle.None;
            }

            Items.Clear();
        }

        /// <summary>Ореол-импульс — первый прямой ребёнок хоста с классом <see cref="PulseClass"/>.</summary>
        private static VisualElement Find(VisualElement host)
        {
            if (host == null) return null;

            for (int i = 0; i < host.childCount; i++)
                if (host[i].ClassListContains(PulseClass)) return host[i];

            return null;
        }
    }
}
