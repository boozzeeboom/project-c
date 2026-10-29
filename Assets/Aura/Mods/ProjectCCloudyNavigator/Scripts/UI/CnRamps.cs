// Cloudy Navigator — запечённые рампы ореолов (D25 в 04 §6, спецификация в 06 §2-§3).
//
// В USS нет ни box-shadow, ни градиентов: единственный способ получить мягкую тень или свечение — положить в
// backgroundImage готовую Texture2D. В моде это делают дети-элементы .halo-sh / .halo-rad с CSS-градиентом на
// полосе; здесь та же геометрия запекается в текстуру, а толщина полосы держится 9-slice-бордюром, поэтому
// рампа не растягивается вместе с элементом.
//
// Альфа монотонна от a на кромке хозяина к 0 на внешней кромке (06 §9.2 — это и есть линейная аппроксимация
// прежнего размытия). Гамма-коррекция обязательна: проект в Linear (04 §2), поэтому альфа проходит через
// CnStyle.Alpha с подложкой-вуалью окна — иначе мягкие тени читаются как ровные серые плашки.
//
// Кэш общий и бессрочный: тени строятся по литеральным числам мода, набор конечен, повторная сборка одной и той же
// записи возвращает ту же текстуру. HideFlags.DontSave — рампы процедурные, в ассет они не попадают.
using System.Collections.Generic;
using UnityEngine;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Процедурные текстуры ореолов: тень (9-slice) и круглое свечение.</summary>
    public static class CnRamps
    {
        /// <summary>Середина текстуры тени: столько пикселей остаётся на растягивание, полосы не мылятся.</summary>
        private const int ShadowCore = 8;

        /// <summary>Размер текстуры свечения. 64 хватает: она растягивается на элемент до 40 px без потери мягкости.</summary>
        private const int GlowSize = 64;

        /// <summary>До этой доли радиуса свечение держит полную альфу, дальше линейно гаснет.</summary>
        private const float GlowInner = 0.5f;

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Тень хоста: полосы толщиной 26/16/36 px по краям (как .halo-sh в 06 §3). Возвращает текстуру под
        /// 9-slice с бордюрами (left, top, right, bottom) — их обязан выставить вызывающий (CnStyle.HaloShadow).
        /// </summary>
        public static Texture2D Shadow(int left, int top, int right, int bottom, Color tint, float alpha)
        {
            int l = Mathf.Max(1, left);
            int t = Mathf.Max(1, top);
            int r = Mathf.Max(1, right);
            int b = Mathf.Max(1, bottom);

            Color corrected = CnStyle.Alpha(tint, alpha, CnStyle.Veil);
            string key = string.Concat("sh", l, ":", t, ":", r, ":", b, ":", Key(corrected));

            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            int w = l + ShadowCore + r;
            int h = t + ShadowCore + b;
            var pixels = new Color32[w * h];
            Color32 ink = corrected;
            ink.a = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Кромка ореола: 0 на внешней границе, 1 у кромки хозяина.
                    bool inX = x < l || x >= w - r;
                    bool inY = y < t || y >= h - b;

                    float fx = 1f;
                    if (x < l) fx = x / (float)l;
                    else if (x >= w - r) fx = (w - 1 - x) / (float)r;

                    float fy = 1f;
                    if (y < t) fy = y / (float)t;
                    else if (y >= h - b) fy = (h - 1 - y) / (float)b;

                    // Угол берёт МИНИМУМ двух полос: иначе на внешнем углу нижняя полоса давала бы полную
                    // альфу при нулевой боковой, и тень заканчивалась бы жёстким L-образным углом.
                    // В центре обе полосы единицы — значит там альфа 0, а не полная.
                    float f = 0f;
                    if (inX && inY) f = Mathf.Min(fx, fy);
                    else if (inX) f = fx;
                    else if (inY) f = fy;

                    ink.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(f * corrected.a) * 255f);
                    pixels[y * w + x] = ink;
                }
            }

            return Store(key, w, h, pixels);
        }

        /// <summary>
        /// Круглое свечение (как .halo-rad, 06 §2/§7): полная альфа до половины радиуса, дальше линейный спад к 0.
        /// Бордюров нет — текстура растягивается целиком, поэтому для кружка её берут квадратной.
        /// </summary>
        public static Texture2D Glow(Color tint, float alpha)
        {
            Color corrected = CnStyle.Alpha(tint, alpha, CnStyle.Veil);
            string key = "gl:" + Key(corrected);

            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            const int size = GlowSize;
            var pixels = new Color32[size * size];
            Color32 ink = corrected;
            ink.a = 0;
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    // 1 на кромке хозяина (d = GlowInner), 0 на внешней кромке (d = 1), за ней — прозрачно.
                    float f = d <= GlowInner ? 1f : Mathf.Clamp01((1f - d) / (1f - GlowInner));
                    ink.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(f * corrected.a) * 255f);
                    pixels[y * size + x] = ink;
                }
            }

            return Store(key, size, size, pixels);
        }

        /// <summary>Сколько текстур запечено — для пробника окна.</summary>
        public static int Count => Cache.Count;

        private static Texture2D Store(string key, int w, int h, Color32[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                name = "cn-halo",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            Cache[key] = tex;
            return tex;
        }

        /// <summary>Ключ кэша по цвету и альфе с точностью до байта: рампа строится из целых значений.</summary>
        private static string Key(Color c)
        {
            var c32 = (Color32)c;
            return string.Concat(c32.r, ".", c32.g, ".", c32.b, ".", c32.a);
        }
    }
}
