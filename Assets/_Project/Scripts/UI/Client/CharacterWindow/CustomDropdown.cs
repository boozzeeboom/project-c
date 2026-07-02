// =====================================================================================
// CustomDropdown.cs — кастомный выпадающий список для UI Toolkit (T-CARGO-UI-01-5)
// =====================================================================================
// Документация:
//   • docs/UI/CUSTOM_DROPDOWN_DESIGN.md
//
// Проблема: DropdownField в Unity 6 runtime использует GenericDropdownMenu
// (AbstractGenericMenu, не VisualElement) — popup-список не стилизуется USS.
//
// Решение: полноценный VisualElement-компонент с программатик-попапом
// на панели rootVisualElement. Всё стилизуется USS.
//
// Классы USS:
//   .custom-dropdown          — корневой контейнер
//   .custom-dropdown__button  — кликабельная кнопка (текст + стрелка)
//   .custom-dropdown__text    — текст выбранного
//   .custom-dropdown__arrow   — стрелка ▼
//   .custom-dropdown__popup   — popup-контейнер (overlay)
//   .custom-dropdown__item    — элемент в popup-списке
//   .custom-dropdown__item.selected — выбранный элемент
//   .custom-dropdown__item:hover    — ховер
// =====================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UI.Client
{
    /// <summary>
    /// VisualElement-based кастомный дропдаун. Полностью стилизуется USS.
    /// Popup рендерится на panel.visualTree (overlay), чтобы не обрезался overflow: hidden
    /// родительских контейнеров.
    /// </summary>
    public class CustomDropdown : VisualElement
    {
        // ===== События =====
        public event Action<int> OnSelectionChanged;

        // ===== Поля =====
        private readonly Label _buttonText;
        private readonly Label _buttonArrow;
        private readonly VisualElement _button;
        private VisualElement _popupContainer;

        private readonly List<string> _choices = new List<string>();
        private int _selectedIndex = -1;
        private bool _popupOpen;

        // ===== Public API =====

        public int SelectedIndex => _selectedIndex;

        public string SelectedText => _selectedIndex >= 0 && _selectedIndex < _choices.Count
            ? _choices[_selectedIndex]
            : string.Empty;

        public CustomDropdown()
        {
            // Root
            AddToClassList("custom-dropdown");

            // Button (clickable row: text + arrow)
            _button = new VisualElement();
            _button.AddToClassList("custom-dropdown__button");
            _button.RegisterCallback<PointerDownEvent>(OnButtonPointerDown);
            Add(_button);

            _buttonText = new Label("—");
            _buttonText.AddToClassList("custom-dropdown__text");
            _button.Add(_buttonText);

            _buttonArrow = new Label("▼");
            _buttonArrow.AddToClassList("custom-dropdown__arrow");
            _button.Add(_buttonArrow);

            // Popup создаётся при открытии, уничтожается при закрытии
            _popupOpen = false;
            _popupContainer = null;
        }

        /// <summary>Установить список choices и выбрать индекс по умолчанию.</summary>
        public void SetChoices(List<string> choices, int defaultIndex = -1)
        {
            _choices.Clear();
            if (choices != null)
                _choices.AddRange(choices);

            if (defaultIndex >= 0 && defaultIndex < _choices.Count)
                _selectedIndex = defaultIndex;
            else if (_choices.Count > 0)
                _selectedIndex = 0;
            else
                _selectedIndex = -1;

            UpdateButtonText();
        }

        /// <summary>Выбрать item по индексу. Вызывает OnSelectionChanged.</summary>
        public void SetSelectedIndex(int index, bool fireEvent = false)
        {
            if (index < 0 || index >= _choices.Count) return;
            _selectedIndex = index;
            UpdateButtonText();
            if (fireEvent)
                OnSelectionChanged?.Invoke(_selectedIndex);
        }

        // ===== Popup management =====

        private void OnButtonPointerDown(PointerDownEvent evt)
        {
            if (_choices.Count == 0) return;
            if (_popupOpen)
                ClosePopup();
            else
                ShowPopup();
        }

        private void ShowPopup()
        {
            if (_popupOpen) return;
            var panel = this.panel;
            if (panel == null) return;

            ClosePopup(); // clean up any stale popup

            // Ищем main-container (без overflow:hidden) для размещения popup'а
            var mainContainer = FindMainContainer();
            if (mainContainer == null) mainContainer = panel.visualTree;

            // Позиция кнопки относительно mainContainer
            var worldPos = _button.LocalToWorld(Vector2.zero);
            var localPos = mainContainer.WorldToLocal(worldPos);
            float btnHeight = _button.layout.height > 0 ? _button.layout.height : 24f;

            // Popup overlay
            _popupContainer = new VisualElement();
            _popupContainer.AddToClassList("custom-dropdown__popup");

            // Позиционируем под кнопкой в local-координатах mainContainer
            float btnWidth = _button.layout.width > 10f ? _button.layout.width : 200f;
            _popupContainer.style.left = localPos.x;
            _popupContainer.style.top = localPos.y + btnHeight;
            _popupContainer.style.minWidth = btnWidth;

            // Items
            for (int i = 0; i < _choices.Count; i++)
            {
                int captureIndex = i; // capture for closure
                var item = new Label(_choices[i]);
                item.AddToClassList("custom-dropdown__item");
                if (i == _selectedIndex)
                    item.AddToClassList("selected");

                item.RegisterCallback<PointerDownEvent>(evt =>
                {
                    SetSelectedIndex(captureIndex, fireEvent: true);
                    ClosePopup();
                    evt.StopPropagation();
                });

                item.RegisterCallback<PointerEnterEvent>(evt =>
                {
                    item.AddToClassList("hovered");
                });
                item.RegisterCallback<PointerLeaveEvent>(evt =>
                {
                    item.RemoveFromClassList("hovered");
                });

                _popupContainer.Add(item);
            }

            // Добавляем на mainContainer (без overflow:hidden)
            mainContainer.Add(_popupContainer);
            _popupOpen = true;

            // Закрытие при клике на root панели
            var rootForClose = mainContainer;
            RegisterGlobalPointerDown(rootForClose);
        }

        private void RegisterGlobalPointerDown(VisualElement rootElement)
        {
            if (rootElement == null) return;

            // Вешаем временный callback на root контейнера
            rootElement.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
        }

        private void UnregisterGlobalPointerDown()
        {
            // Ищем main-container для отписки
            var mc = FindMainContainer();
            if (mc == null) return;
            try
            {
                mc.UnregisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
            }
            catch { /* suppressed */ }
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (!_popupOpen) return;

            // Если клик внутри popup'а (target — дочерний элемент) — не закрываем
            var target = evt.target as VisualElement;
            if (target != null && _popupContainer != null)
            {
                if (_popupContainer.Contains(target))
                    return;
            }

            // Если клик внутри кнопки — не закрываем
            if (target != null && _button.Contains(target))
                return;

            ClosePopup();
        }

        private void ClosePopup()
        {
            if (!_popupOpen) return;
            UnregisterGlobalPointerDown();

            if (_popupContainer != null && _popupContainer.parent != null)
                _popupContainer.parent.Remove(_popupContainer);

            _popupContainer = null;
            _popupOpen = false;
        }

        // ===== Helpers =====

        /// <summary>Ручной поиск main-container по parent chain (GetFirstAncestorWhere не существует в Unity 6).</summary>
        private VisualElement FindMainContainer()
        {
            var el = parent;
            while (el != null)
            {
                if (el.name == "main-container")
                    return el;
                el = el.parent;
            }
            return null;
        }

        private void UpdateButtonText()
        {
            _buttonText.text = _selectedIndex >= 0 && _selectedIndex < _choices.Count
                ? _choices[_selectedIndex]
                : "—";
        }

        // ===== Lifecycle =====

        /// <summary>Вызвать при скрытии/уничтожении окна.</summary>
        public void Cleanup()
        {
            ClosePopup();
        }
    }
}
