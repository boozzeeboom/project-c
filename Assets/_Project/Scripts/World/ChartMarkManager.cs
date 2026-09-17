using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World
{
    /// <summary>Тип ручной отметки на карте.</summary>
    public enum ChartMarkType
    {
        Landmark,     // ориентир (кольцо)
        Danger,       // опасность (треугольник)
        Note,         // заметка (квадрат)
        Destination,  // цель (двойное кольцо)
    }

    /// <summary>Ручная отметка: где + что + подпись рукой игрока.</summary>
    [Serializable]
    public struct ChartMark
    {
        public int id;
        public Vector3 worldPos;
        public ChartMarkType type;
        public string text;
        public double utcSeconds;
        public ChartTrackSource source; // Self сейчас, чужие — позже
    }

    /// <summary>
    /// Ручные метки карты (слой 2). v1: сессионные, per-player, источник Self.
    /// Выбор метки (SelectedId) — якорь для будущего пеленга в HUD.
    ///
    /// 🟡 Floating Origin: метки — мировые Vector3, сдвигаются вместе с миром
    /// через ApplyRebaseTranslation (вызывается из слайса рядом с треками).
    /// </summary>
    [DisallowMultipleComponent]
    public class ChartMarkManager : MonoBehaviour
    {
        public static ChartMarkManager Instance { get; private set; }

        /// <summary>Выбранная метка (клик по ней), -1 — нет выбора.</summary>
        public int SelectedId { get; private set; } = -1;

        private readonly List<ChartMark> _marks = new List<ChartMark>();
        private int _nextId = 1;

        public IReadOnlyList<ChartMark> Marks => _marks;
        public int Count => _marks.Count;

        public static void EnsureExists()
        {
            if (Instance != null || !Application.isPlaying) return;
            var go = new GameObject("ChartMarkManager");
            DontDestroyOnLoad(go);
            go.AddComponent<ChartMarkManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null && Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Поставить метку. Возвращает id (и сразу выбирает её).</summary>
        public int AddMark(Vector3 worldPos, ChartMarkType type, string text)
        {
            var mark = new ChartMark
            {
                id = _nextId++,
                worldPos = worldPos,
                type = type,
                text = string.IsNullOrWhiteSpace(text) ? DefaultName(type) : text.Trim(),
                utcSeconds = ChartTrackRecorder.NowUtcSeconds(),
                source = ChartTrackSource.Self,
            };
            _marks.Add(mark);
            SelectedId = mark.id;
            return mark.id;
        }

        public bool RemoveMark(int id)
        {
            for (int i = 0; i < _marks.Count; i++)
            {
                if (_marks[i].id != id) continue;
                _marks.RemoveAt(i);
                if (SelectedId == id) SelectedId = -1;
                return true;
            }
            return false;
        }

        public bool TryGetMark(int id, out ChartMark mark)
        {
            foreach (var m in _marks)
            {
                if (m.id != id) continue;
                mark = m;
                return true;
            }
            mark = default;
            return false;
        }

        public void Select(int id) => SelectedId = id;
        public void Deselect() => SelectedId = -1;

        public static string DefaultName(ChartMarkType type)
        {
            switch (type)
            {
                case ChartMarkType.Danger: return "Опасно";
                case ChartMarkType.Note: return "Заметка";
                case ChartMarkType.Destination: return "Цель";
                default: return "Ориентир";
            }
        }

        public static string TypeName(ChartMarkType type)
        {
            switch (type)
            {
                case ChartMarkType.Danger: return "опасность";
                case ChartMarkType.Note: return "заметка";
                case ChartMarkType.Destination: return "цель";
                default: return "ориентир";
            }
        }

        /// <summary>Сдвинуть метки вместе с миром (см. шапку).</summary>
        public int ApplyRebaseTranslation(Vector3 translation)
        {
            for (int i = 0; i < _marks.Count; i++)
            {
                var m = _marks[i];
                m.worldPos += translation;
                _marks[i] = m;
            }
            return _marks.Count;
        }

        public void Clear()
        {
            _marks.Clear();
            SelectedId = -1;
        }
    }
}
