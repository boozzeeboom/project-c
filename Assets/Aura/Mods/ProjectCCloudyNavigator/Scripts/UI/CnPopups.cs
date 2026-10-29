// Cloudy Navigator — менеджер полноразмерных попапов окна.
//
// Один слой CnPopup и один набор панелей: у панели своё содержимое и свои вкладки, а оболочка отвечает только за
// затемнение, шапку и закрытие. Секции мода подключены целиком: задания, багаж, корабль, навыки (боевые и
// социальные), календарь и отношения.
//
// Попап — визуальный слой: он не владеет данными и не вызывает игровых методов. Действия панели уходят в CnActions,
// их результат показывается строкой «СЕЙЧАС» главного окна. Выбор корабля, вкладка корабля, фокус и сортировка
// багажа — состояние вида (CnViewState), а не игры.
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
        private readonly CnSkillsPanel _skills;
        private readonly CnCalendarPanel _calendar;
        private readonly CnRelPanel _rel;
        private readonly CnShipPanel _ship;
        private readonly CnBagPanel _bag;

        private readonly CnViewState _view;

        private Section _section = Section.None;

        /// <summary>Отчёт о результате действия — ставит главное окно после своей постройки.</summary>
        public Action<CnActionResult> OnResult { get; set; }

        public CnPopups(CnActions actions, CnViewState view)
        {
            _view = view != null ? view : new CnViewState();

            _quests = new CnQuestsPanel(actions, Report);
            _skills = new CnSkillsPanel();
            _calendar = new CnCalendarPanel();
            _rel = new CnRelPanel();
            _ship = new CnShipPanel(actions, _view, Report, OpenBag);
            _bag = new CnBagPanel(actions, _view, Report);
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

        /// <summary>Граф навыков. social = true — социальный раздел, иначе боевой.</summary>
        public void OpenSkills(bool social)
        {
            _section = social ? Section.Social : Section.Skills;
            _skills.Open(social);
            _popup.Show("НАВЫКИ", social ? "социальные" : "боевые", _skills.Build(), ResetSection);
        }

        /// <summary>Окно корабля: index — корабль, который надо показать первым.</summary>
        public void OpenShip(int index)
        {
            _view.selectedShip = index < 0 ? 0 : index;
            _section = Section.Ship;
            _ship.Open();
            _popup.Show("КОРАБЛЬ", "телеметрия и модули", _ship.Build(), ResetSection);
        }

        /// <summary>Окно багажа: focusShip −1 — багаж игрока, иначе трюм этого корабля.</summary>
        public void OpenBag(int focusShip)
        {
            _view.bagFocus = focusShip;
            _section = Section.Bag;
            _bag.Open();
            _popup.Show("БАГАЖ", "багаж игрока и трюм", _bag.Build(), ResetSection);
        }

        /// <summary>Календарь года: сетка месяцев и сроки контрактов.</summary>
        public void OpenCalendar()
        {
            _section = Section.Calendar;
            _calendar.Open();
            _popup.Show("КАЛЕНДАРЬ", "год игры", _calendar.Build(), ResetSection);
        }

        /// <summary>Отношения: репутация фракций и отношение людей.</summary>
        public void OpenRel()
        {
            _section = Section.Rel;
            _rel.Open();
            _popup.Show("ОТНОШЕНИЯ", "репутация и люди", _rel.Build(), ResetSection);
        }

        public void Refresh(CnDataFeed feed, float dt)
        {
            if (!_popup.IsOpen) return;

            if (_section == Section.Quests) _quests.Refresh(feed);
            else if (_section == Section.Skills || _section == Section.Social) _skills.Refresh(feed);
            else if (_section == Section.Calendar) _calendar.Refresh(feed);
            else if (_section == Section.Rel) _rel.Refresh(feed);
            else if (_section == Section.Ship) _ship.Refresh(feed);
            else if (_section == Section.Bag) _bag.Refresh(feed);
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
