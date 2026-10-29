// Cloudy Navigator — блок навыков на станции (мод v4-2/4.3: панель быстрого доступа home.js).
//
// Шесть слотов в порядке реального биндинга (SkillInputSlot.Primary, .Secondary, .Slot1…Slot4) и бирка
// «СОЦИАЛЬНЫЕ НАВЫКИ» — вход в полноэкранный граф. Пустой слот подписан своей клавишей, а не выдуманным навыком:
// имя, тип и перезарядка приходят только из SkillInputService и SkillNodeConfig.
//
// Блок ничего не вызывает в игре: клик по слоту открывает боевой граф, бирка — социальный. Изучение навыка остаётся
// в легаси-окне персонажа (P): клиентского запроса изучения в проекте нет, он идёт серверным RPC
// SkillsServer.RequestLearnSkillRpc, и вторую точку вызова окно не заводит.
using System;
using System.Text;
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Шесть слотов быстрого доступа и вход в графы навыков.</summary>
    public sealed class CnSkillsView
    {
        private const int SlotCount = 6;

        // Сетка мода (src/skills.css .sk-grid): 3 × 36 + 2 × 6 = 120 ширины, 2 × 36 + 6 = 78 высоты.
        // Зазора в UI Toolkit нет, поэтому он задан полями плиток, а последние в ряду — классом.
        private const int GridColumns = 3;
        private const int GridRows = 2;

        /// <summary>Столько символов подписи слота ещё читается в плитке 36 px; длиннее («Ctrl+ЛКМ») — только в подсказке.</summary>
        private const int MaxCodeChars = 4;

        private readonly VisualElement _root;
        private readonly VisualElement[] _tiles = new VisualElement[SlotCount];
        private readonly Label[] _codes = new Label[SlotCount];
        private readonly Label[] _names = new Label[SlotCount];
        private readonly Label _count;
        private readonly CnTip _tip;

        // Последнее прочитанное состояние слотов — из него собирается карточка наведения (skills.js tooltip).
        private readonly CnSkillSlotRow[] _slots = new CnSkillSlotRow[SlotCount];
        private readonly bool[] _bound = new bool[SlotCount];

        private string _signature = string.Empty;

        /// <summary>Запрос графа: false — боевые, true — социальные.</summary>
        public Action<bool> OnOpen { get; set; }

        public VisualElement Root => _root;

        public CnSkillsView(CnTip tip)
        {
            _tip = tip;
            _root = CnStyle.Box("cn-skills");

            var head = CnStyle.Row("cn-skills__head");
            head.Add(CnStyle.Label("НАВЫКИ", "cn-skills__t"));
            head.Add(CnStyle.Box("cn-grow"));
            _count = CnStyle.Label(string.Empty, "cn-skills__n");
            head.Add(_count);
            _root.Add(head);

            var grid = CnStyle.Box("cn-skills__grid");
            for (int i = 0; i < SlotCount; i++)
            {
                var tile = CnStyle.Box("cn-skl");
                if (i % GridColumns == GridColumns - 1) tile.AddToClassList("cn-skl--row-end");
                if (i >= GridColumns * (GridRows - 1)) tile.AddToClassList("cn-skl--row-last");
                CnStyle.Fill(tile, CnStyle.Veil, 0.4f);

                var code = CnStyle.Label(string.Empty, "cn-skl__c");
                var name = CnStyle.Label(string.Empty, "cn-skl__n");
                tile.Add(code);
                tile.Add(name);

                tile.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    OnOpen?.Invoke(false);
                });

                // Карточка слота: клавиша, навык, перезарядка — или честное «навык назначается в окне персонажа (P)».
                int index = i;
                _tip?.Hover(tile, () =>
                {
                    var row = _slots[index];
                    string code = string.IsNullOrEmpty(row.code) ? "СЛОТ " + (index + 1) : row.code;
                    if (!_bound[index])
                        return CnTipCard.Make(code, "слот пуст").Foot("навык назначается в окне персонажа (P)");

                    var card = CnTipCard.Make(code, string.IsNullOrEmpty(row.displayName) ? row.skillId : row.displayName);
                    if (row.onCooldown) card.Row("состояние", "перезарядка");
                    card.Foot("клик — боевой граф навыков");
                    return card;
                });

                _tiles[i] = tile;
                _codes[i] = code;
                _names[i] = name;
                grid.Add(tile);
            }
            _root.Add(grid);

            var social = CnStyle.Box("cn-skills__btn");
            social.Add(CnStyle.Label("СОЦИАЛЬНЫЕ НАВЫКИ", "cn-skills__btn-t"));
            social.tooltip = null;
            _tip?.Hover(social, () => CnTipCard.Make("СОЦИАЛЬНЫЕ НАВЫКИ", "граф общения, торговли и убеждения")
                .Foot("клик — социальный граф"));
            social.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                OnOpen?.Invoke(true);
            });
            _root.Add(social);
        }

        public void Refresh(CnDataFeed feed)
        {
            var slots = feed != null ? feed.skillSlots : null;
            if (slots == null) slots = new CnSkillSlotRow[0];

            var sb = new StringBuilder(128);
            sb.Append(feed != null && feed.hasSkills ? '1' : '0');
            for (int i = 0; i < slots.Length; i++)
            {
                sb.Append(slots[i].slot).Append(slots[i].skillId).Append(slots[i].displayName)
                  .Append(slots[i].onCooldown ? 'c' : '-').Append(';');
            }

            string signature = sb.ToString();
            if (signature == _signature) return;
            _signature = signature;

            int bound = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                bool has = i < slots.Length && slots[i].bound;
                if (has) bound++;

                var row = i < slots.Length ? slots[i] : default;
                var tile = _tiles[i];

                tile.EnableInClassList("cn-skl--empty", !has);
                tile.EnableInClassList("is-cd", has && row.onCooldown);
                CnStyle.Fill(tile, CnStyle.Veil, has ? 0.72f : 0.4f);

                _codes[i].text = FitsInTile(row.code) ? row.code : string.Empty;
                _names[i].text = has ? Abbreviate(row.displayName) : string.Empty;

                _slots[i] = row;
                _bound[i] = has;
                tile.tooltip = null;
            }

            _count.text = bound + " / " + SlotCount;
        }

        /// <summary>Подпись слота остаётся в плитке, только если физически в неё влезает: иначе она есть в подсказке.</summary>
        private static bool FitsInTile(string code) => !string.IsNullOrEmpty(code) && code.Length <= MaxCodeChars;

        /// <summary>Подпись навыка в плитке 36 px — четыре первые буквы имени капсом (как у слотов экипировки).</summary>
        private static string Abbreviate(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            string trimmed = name.Trim();
            if (trimmed.Length == 0) return string.Empty;
            return trimmed.Substring(0, Mathf.Min(4, trimmed.Length)).ToUpperInvariant();
        }
    }
}
