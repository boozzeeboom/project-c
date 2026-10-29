// Cloudy Navigator — шина наведения окна (порт src/main.js:463-499 мода).
//
// В моде наведение — это не только карточка. setHover({line|slot|stat|item}) складывает ТЕКУЩИЙ субъект, а applyHover()
// раздаёт подсветку: линия подсвечивает связанные слоты/статы/репутацию и день дедлайна в календаре, слот — линии,
// которые на него влияют, характеристика — свои линии, предмет — линии, где он нужен. Соседи при этом глушатся:
// lines.js ставит #rows.hlmode и .ln:not(.hl) { opacity: .28 }.
//
// Здесь ровно этот шаг без таймеров и без собственных данных: субъект один, подписчиков двое (лента и станция),
// пересчёт делает окно по реальным моделям линий. Новых связей шина не выдумывает — только те, что есть в данных.
using System;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Что сейчас под курсором.</summary>
    public enum CnHoverKind : byte
    {
        None = 0,
        Line = 1,
        Slot = 2,
        Stat = 3,
        Item = 4
    }

    /// <summary>Субъект наведения: вид, ключ и (для слота) индекс экипировки.</summary>
    public struct CnHoverSubject
    {
        public CnHoverKind kind;
        public string id;   // line: «q:…»/«c:…» · stat: str/dex/int/credits · item: displayName
        public int slot;    // 0…12 для Slot, иначе −1

        public static readonly CnHoverSubject None = new CnHoverSubject { kind = CnHoverKind.None, slot = -1 };
    }

    /// <summary>Одна точка правды о наведении: кто под курсором и кому об этом сказать.</summary>
    public sealed class CnHover
    {
        private CnHoverSubject _current;

        /// <summary>Смена субъекта. Окно пересчитывает подсветку и раздаёт её видам.</summary>
        public event Action<CnHoverSubject> Changed;

        public CnHoverSubject Current => _current;

        public void SetLine(string lineId) => Set(new CnHoverSubject { kind = CnHoverKind.Line, id = lineId, slot = -1 });

        public void SetSlot(int index) => Set(new CnHoverSubject { kind = CnHoverKind.Slot, id = null, slot = index });

        public void SetStat(string key) => Set(new CnHoverSubject { kind = CnHoverKind.Stat, id = key, slot = -1 });

        public void SetItem(string itemName) => Set(new CnHoverSubject { kind = CnHoverKind.Item, id = itemName, slot = -1 });

        public void Clear() => Set(CnHoverSubject.None);

        private void Set(CnHoverSubject subject)
        {
            if (subject.kind == _current.kind && subject.slot == _current.slot && subject.id == _current.id) return;
            _current = subject;
            Changed?.Invoke(_current);
        }

        public void Dispose() => Changed = null;
    }
}
