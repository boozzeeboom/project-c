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
            ChartTrackRecorder.Instance?.SaveJournal(); // метки сейвим сразу (их мало)
            return mark.id;
        }

        public bool RemoveMark(int id)
        {
            for (int i = 0; i < _marks.Count; i++)
            {
                if (_marks[i].id != id) continue;
                _marks.RemoveAt(i);
                if (SelectedId == id) SelectedId = -1;
                ChartTrackRecorder.Instance?.SaveJournal();
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

        /// <summary>Восстановить из файла (зовёт рекордер при загрузке).</summary>
        public void RestoreFromDtos(System.Collections.Generic.List<ChartMarkDto> dtos, int nextId)
        {
            _marks.Clear();
            SelectedId = -1;
            if (dtos != null)
            {
                foreach (var d in dtos)
                {
                    if (d == null) continue;
                    _marks.Add(new ChartMark
                    {
                        id = d.id,
                        worldPos = new Vector3(d.x, d.y, d.z),
                        type = (ChartMarkType)Mathf.Clamp(d.type, 0, 3),
                        text = string.IsNullOrEmpty(d.text) ? DefaultName(ChartMarkType.Note) : d.text,
                        utcSeconds = d.utc,
                        source = (ChartTrackSource)Mathf.Clamp(d.source, 0, 2),
                    });
                }
            }
            _nextId = Mathf.Max(nextId, MaxId() + 1);
        }

        /// <summary>Выгрузить в DTO для сейва.</summary>
        public void ExportToDtos(System.Collections.Generic.List<ChartMarkDto> outDtos)
        {
            outDtos.Clear();
            foreach (var m in _marks)
            {
                outDtos.Add(new ChartMarkDto
                {
                    id = m.id,
                    x = m.worldPos.x, y = m.worldPos.y, z = m.worldPos.z,
                    type = (int)m.type,
                    text = m.text,
                    utc = m.utcSeconds,
                    source = (int)m.source,
                });
            }
        }

        public int ExportNextId() => _nextId;

        private int MaxId()
        {
            int max = 0;
            foreach (var m in _marks) if (m.id > max) max = m.id;
            return max;
        }

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
