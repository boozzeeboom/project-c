// Cloudy Navigator — менеджер полноразмерных попапов окна.
//
// Один слой CnPopup и один набор панелей: у панели своё содержимое и свои вкладки, а оболочка отвечает только за
// затемнение, шапку и закрытие. Пока панелей меньше, чем секций мода, — открываются реализованные; остальные
// подключаются по мере переноса (корабль, багаж, навыки, календарь, связи).
//
// Попап — визуальный слой: он не владеет данными и не вызывает игровых методов. Действия панели уходят в CnActions,
// их результат показывается строкой «СЕЙЧАС» главного окна.
using System;
using ProjectCCloudyNavigator.Actions;
using ProjectCCloudyNavigator.Data;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Слой попапов окна и переключение секций.</summary>
    public sealed class CnPopups
    {
        /// <summary>Секция попапа. None — попап закрыт.</summary>
        public enum Section
        {
            None = 0,
            Quests = 1,
            Ship = 2,
            Bag = 3,
            Skills = 4,
            Social = 5,
            Calendar = 6,
            Rel = 7
        }

        private readonly CnPopup _popup = new CnPopup();
        private readonly CnQuestsPanel _quests;

        private Section _section = Section.None;

        /// <summary>Отчёт о результате действия — ставит главное окно после своей постройки.</summary>
        public Action<CnActionResult> OnResult { get; set; }

        public CnPopups(CnActions actions)
        {
            _quests = new CnQuestsPanel(actions, Report);
        }

        public VisualElement Root => _popup.Root;
        public bool IsOpen => _popup.IsOpen;
        public Section Current => _section;

        public void Mount(VisualElement parent)
        {
            if (parent != null) parent.Add(_popup.Root);
        }

        /// <summary>Окно «ЗАДАНИЯ». focusId — id линии («q:questId» / «c:contractId»), contracts — сразу вкладка контрактов.</summary>
        public void OpenQuests(string focusId, bool contracts)
        {
            _section = Section.Quests;
            _quests.Focus(focusId);
            _popup.Show("ЗАДАНИЯ", contracts ? "контракты" : "квесты", _quests.Build(), ResetSection);
        }

        public void Refresh(CnDataFeed feed, float dt)
        {
            if (!_popup.IsOpen) return;

            if (_section == Section.Quests) _quests.Refresh(feed);
        }

        public void Close() => _popup.Close();

        public void Dispose()
        {
            _section = Section.None;
            _popup.Dispose();
        }

        private void ResetSection() => _section = Section.None;

        private void Report(CnActionResult result)
        {
            OnResult?.Invoke(result);
        }
    }
}
