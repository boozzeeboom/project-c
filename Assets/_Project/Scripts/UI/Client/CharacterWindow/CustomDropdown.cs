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

        // ===== Статический трекинг открытых попапов =====
        private static readonly HashSet<CustomDropdown> _openDropdowns = new HashSet<CustomDropdown>();
        private static readonly List<CustomDropdown> _toRemove = new List<CustomDropdown>();

        /// <summary>Закрыть все открытые попапы всех CustomDropdown. Вызывать при скрытии окна.</summary>
        public static void CloseAllPopups()
        {
            if (_openDropdowns.Count == 0) return;
            // Snapshot: ClosePopup() modifies _openDropdowns — нельзя итерировать и модифицировать одновременно
            _toRemove.Clear();
            foreach (var dd in _openDropdowns)
                _toRemove.Add(dd);
            _openDropdowns.Clear();
            foreach (var dd in _toRemove)
            {
                try { dd?.ClosePopup(); } catch { /* suppressed */ }
            }
        }

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
            Debug.Log($"[CustomDropdown] SetChoices: count={_choices.Count}, defaultIndex={defaultIndex}");

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
            evt.StopPropagation();
            evt.StopImmediatePropagation();
            if (_choices.Count == 0) return;
            if (_popupOpen)
                ClosePopup();
            else
                this.schedule.Execute(() => ShowPopup());
        }

        private void ShowPopup()
        {
            if (_popupOpen) return;
            var panel = this.panel;
            if (panel == null) return;

            // Force layout update before reading worldBound
            _button.MarkDirtyRepaint();

            ClosePopup(); // clean up any stale popup

            // Всегда добавляем в корень панели (поверх всего)
            var root = panel.visualTree;

            // Позиция и размер кнопки: worldBound надёжнее resolvedStyle (может быть 0 до layout)
            var btnRect = _button.worldBound;
            float btnWidth = btnRect.width > 10f ? btnRect.width : 200f;
            float btnHeight = btnRect.height > 0f ? btnRect.height : 24f;
            // Переводим world-координаты в координаты root
            var rootWorld = root.worldBound;
            float popupLeft = btnRect.x - rootWorld.x;
            float btnTop = btnRect.y - rootWorld.y;

            // Адаптивное направление: если снизу мало места — открываем вверх
            float screenHeight = root.resolvedStyle.height > 0 ? root.resolvedStyle.height : 1080f;
            float spaceBelow = screenHeight - (btnTop + btnHeight);
            float spaceAbove = btnTop;
            float popupHeight = Mathf.Min(_choices.Count * 30f + 8f, 220f); // ~30px per item + padding
            float popupTop;
            if (spaceBelow >= popupHeight || spaceBelow >= spaceAbove)
                popupTop = btnTop + btnHeight; // вниз
            else
                popupTop = btnTop - popupHeight; // вверх

            // Popup overlay — USS class + inline fallback styles
            _popupContainer = new VisualElement();
            _popupContainer.AddToClassList("custom-dropdown__popup");
            _popupContainer.style.position = Position.Absolute;
            _popupContainer.pickingMode = PickingMode.Position;
            _popupContainer.style.backgroundColor = new Color(0.1f, 0.12f, 0.18f, 0.97f);
            _popupContainer.style.borderTopWidth = _popupContainer.style.borderBottomWidth =
                _popupContainer.style.borderLeftWidth = _popupContainer.style.borderRightWidth = 1f;
            _popupContainer.style.borderTopColor = _popupContainer.style.borderBottomColor =
                _popupContainer.style.borderLeftColor = _popupContainer.style.borderRightColor =
                    new Color(0.31f, 0.39f, 0.55f);
            _popupContainer.style.borderTopLeftRadius = _popupContainer.style.borderTopRightRadius =
                _popupContainer.style.borderBottomLeftRadius = _popupContainer.style.borderBottomRightRadius = 4f;
            _popupContainer.style.paddingTop = _popupContainer.style.paddingBottom = 4f;
            _popupContainer.style.paddingLeft = _popupContainer.style.paddingRight = 4f;
            _popupContainer.style.maxHeight = popupHeight;
            _popupContainer.style.flexDirection = FlexDirection.Column;

            // Позиционируем под кнопкой
            _popupContainer.style.left = popupLeft;
            _popupContainer.style.top = popupTop;
            _popupContainer.style.width = btnWidth;

            // ScrollView для скролла (overflow: auto не работает в UI Toolkit)
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1f;
            scrollView.style.maxHeight = popupHeight - 10f; // минус padding попапа
            scrollView.mode = ScrollViewMode.Vertical;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _popupContainer.Add(scrollView);

            // Items — кладём в ScrollView
            for (int i = 0; i < _choices.Count; i++)
            {
                int captureIndex = i; // capture for closure
                var item = new Label(_choices[i]);
                item.AddToClassList("custom-dropdown__item");
                item.style.color = new Color(0.78f, 0.82f, 0.88f);
                item.style.paddingTop = item.style.paddingBottom = 6f;
                item.style.paddingLeft = item.style.paddingRight = 12f;
                item.style.fontSize = 13f;
                item.style.flexShrink = 0f;
                if (i == _selectedIndex)
                {
                    item.AddToClassList("selected");
                    item.style.color = new Color(0.55f, 0.78f, 0.98f);
                    item.style.backgroundColor = new Color(0.24f, 0.39f, 0.59f, 0.5f);
                    item.style.unityFontStyleAndWeight = FontStyle.Bold;
                }

                item.RegisterCallback<PointerDownEvent>(evt =>
                {
                    SetSelectedIndex(captureIndex, fireEvent: true);
                    ClosePopup();
                    evt.StopPropagation();
                });

                var hoverBg = new Color(0.27f, 0.43f, 0.63f, 0.6f);
                var normalBg = i == _selectedIndex
                    ? new Color(0.24f, 0.39f, 0.59f, 0.5f)
                    : new Color(0, 0, 0, 0);

                item.RegisterCallback<PointerEnterEvent>(evt =>
                {
                    item.AddToClassList("hovered");
                    item.style.backgroundColor = hoverBg;
                });
                item.RegisterCallback<PointerLeaveEvent>(evt =>
                {
                    item.RemoveFromClassList("hovered");
                    item.style.backgroundColor = normalBg;
                });

                scrollView.Add(item);
            }

            // Добавляем в корень панели и выносим на передний план
            root.Add(_popupContainer);
            _popupContainer.BringToFront();
            _popupOpen = true;
            _openDropdowns.Add(this);
            Debug.Log($"[CustomDropdown] ShowPopup: items={_choices.Count}, worldBound=({btnRect.x:F0},{btnRect.y:F0},{btnRect.width:F0}x{btnRect.height:F0}), popupPos=({popupLeft:F0},{popupTop:F0}), openCount={_openDropdowns.Count}");

            // Закрытие при клике вне попапа
            root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (!_popupOpen) return;

            // Если клик внутри popup'а — не закрываем
            var target = evt.target as VisualElement;
            if (target != null && _popupContainer != null && _popupContainer.Contains(target))
                return;

            // Если клик внутри кнопки — не закрываем
            if (target != null && _button.Contains(target))
                return;

            ClosePopup();
        }

        private void ClosePopup()
        {
            if (!_popupOpen) return;

            var panel = this.panel;
            if (panel != null)
            {
                try { panel.visualTree.UnregisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown); }
                catch { /* suppressed */ }
            }

            if (_popupContainer != null && _popupContainer.parent != null)
                _popupContainer.parent.Remove(_popupContainer);

            _popupContainer = null;
            _popupOpen = false;
            _openDropdowns.Remove(this);
            Debug.Log($"[CustomDropdown] ClosePopup: remaining open={_openDropdowns.Count}");
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
