using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ProjectC.Trade;
using ProjectC.Player;

/// <summary>
/// UI доски контрактов НП — отдельный префаб, открывается у NPC-агента.
/// GDD_25 секция 6: Контрактная Система.
/// Утверждено решение 5B: отдельный префаб (не вкладка TradeUI).
///
/// Сессия 7: ContractSystem.
/// Поток: Игрок подходит к NPC-агенту → E → ContractBoardUI → RequestContracts → список → "Взять" → ServerRpc.
/// </summary>
public class ContractBoardUI : MonoBehaviour
{
    public static ContractBoardUI Instance { get; private set; }

    private GameObject _rootCanvas;
    private GameObject _boardPanel;
    private Transform _contentPanel;
    private Text _titleText;
    private Text _locationText;
    private Text _debtText;
    private Text _messageText;
    private List<GameObject> _contractRows = new List<GameObject>();
    private List<Button> _uiButtons = new List<Button>();

    private bool _isOpen;
    private int _selectedIndex = -1;
    private ContractData[] _currentContracts = new ContractData[0];
    private ContractData[] _activeContracts = new ContractData[0];
    private string _currentLocationId = "";
    private NetworkPlayer _player;
#pragma warning disable 0414
    private bool _showActiveTab = false;
#pragma warning restore 0414

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        _player = FindAnyObjectByType<ProjectC.Player.NetworkPlayer>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DestroyUI();
    }

    private void Update()
    {
        if (!_isOpen) return;
        HandleInput();
    }

    // ==================== СОЗДАНИЕ UI ====================

    private void BuildUI()
    {
        // --- Root Canvas ---
        _rootCanvas = new GameObject("[ContractBoard]_RootCanvas");
        _rootCanvas.layer = LayerMask.NameToLayer("UI");

        var canvas = _rootCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5100; // Выше TradeUI (5000)
        canvas.pixelPerfect = false;

        var scaler = _rootCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _rootCanvas.AddComponent<GraphicRaycaster>();

        // --- Панель ---
        _boardPanel = CreatePanel("ContractBoardPanel", _rootCanvas.transform, 0, 0, 600, 700);
        _boardPanel.SetActive(false);

        // --- Заголовок ---
        _titleText = MakeLabel("Title", _boardPanel.transform, "📋 КОНТРАКТЫ НП", 0, 310, 24, Color.yellow, 560);
        _locationText = MakeLabel("LocationText", _boardPanel.transform, "[Локация]", 0, 280, 14, Color.cyan, 300);

        // --- Долг ---
        _debtText = MakeLabel("DebtText", _boardPanel.transform, "", 0, 256, 12, Color.red, 560);

        // --- Scroll-зона ---
        var scrollGO = new GameObject("ScrollArea");
        scrollGO.transform.SetParent(_boardPanel.transform, false);
        var scrollRect = scrollGO.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.04f, 0.22f);
        scrollRect.anchorMax = new Vector2(0.96f, 0.80f);
        scrollRect.sizeDelta = Vector2.zero;

        var vpGO = new GameObject("Viewport");
        vpGO.transform.SetParent(scrollGO.transform, false);
        var vpRect = vpGO.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMax = new Vector2(-14, 0);

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(vpGO.transform, false);
        _contentPanel = contentGO.transform;

        var contentRect = contentGO.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        var layout = contentGO.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 3;
        layout.padding = new RectOffset(6, 6, 6, 6);

        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollGO.AddComponent<Mask>().showMaskGraphic = false;
        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.content = contentRect;
        sr.viewport = vpRect;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;

        // --- Кнопки ---
        _uiButtons.Add(MakeBtn("AcceptBtn", _boardPanel.transform, "ВЗЯТЬ (Enter)", 0, -190, 260, 36, OnAcceptClicked));
        _uiButtons.Add(MakeBtn("CompleteBtn", _boardPanel.transform, "СДАТЬ (Shift+Enter)", 0, -235, 260, 36, OnCompleteClicked));
        _uiButtons.Add(MakeBtn("CloseBtn", _boardPanel.transform, "ЗАКРЫТЬ (C)", 0, -280, 200, 36, OnCloseClicked));

        // --- Сообщение ---
        _messageText = MakeLabel("MsgText", _boardPanel.transform, "Выберите контракт и нажмите Enter", 0, -240, 13, new Color(0.9f, 0.9f, 0.4f), 560);
        MakeLabel("Hint1", _boardPanel.transform, "Up/Down - выбор | Enter - взять | C - закрыть", 0, -262, 11, Color.grey, 560);
        MakeLabel("Hint2", _boardPanel.transform, "📦 Стандартная | ⚡ Срочная (×1.5) | 📝 Под расписку", 0, -310, 11, Color.grey, 560);
    }

    private void DestroyUI()
    {
        if (_rootCanvas != null) { Destroy(_rootCanvas); _rootCanvas = null; }
        _boardPanel = null;
        _contentPanel = null;
        _titleText = null;
        _locationText = null;
        _debtText = null;
        _messageText = null;
        _contractRows.Clear();
        _uiButtons.Clear();
    }

    // ==================== UI ХЕЛПЕРЫ ====================

    private GameObject CreatePanel(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, y);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.03f, 0.05f, 0.08f, 0.97f);
        return go;
    }

    private Text MakeLabel(string name, Transform parent, string text, float x, float y, int fontSize, Color color, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 24);
        rect.anchoredPosition = new Vector2(x, y);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return txt;
    }

    private Button MakeBtn(string name, Transform parent, string label, float x, float y, float w, float h, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, y);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.22f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var cols = btn.colors;
        cols.highlightedColor = new Color(0.22f, 0.22f, 0.30f);
        cols.pressedColor = new Color(0.28f, 0.28f, 0.38f);
        btn.colors = cols;

        var tGo = new GameObject("Text");
        tGo.transform.SetParent(go.transform, false);
        var tRect = tGo.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero;
        tRect.offsetMax = Vector2.zero;
        var t = tGo.AddComponent<Text>();
        t.text = label;
        t.fontSize = 13;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return btn;
    }

    // ==================== ОТКРЫТИЕ / ЗАКРЫТИЕ ====================

    /// <summary>
    /// Открыть доску контрактов
    /// </summary>
    public void OpenBoard(LocationMarket market)
    {
        if (market == null) return;

        if (_boardPanel == null)
        {
            BuildUI();
        }
        if (_boardPanel == null) return;

        _isOpen = true;
        _selectedIndex = -1;
        _currentLocationId = market.locationId;

        _boardPanel.SetActive(true);
        _boardPanel.transform.SetAsLastSibling();

        if (_titleText != null) _titleText.text = $"📋 КОНТРАКТЫ НП — {market.locationName}";
        if (_locationText != null) _locationText.text = $"[{market.locationId.ToUpper()}]";

        ShowMessage("Загрузка контрактов...");

        // Найти локального игрока
        if (_player == null)
        {
            _player = FindAnyObjectByType<NetworkPlayer>();
        }

        // Запросить контракты у сервера (только от Owner)
        if (_player != null && _player.IsOwner)
        {
            _player.ContractRequestServerRpc(market.locationId);
        }
        else if (_player == null)
        {
            Debug.LogWarning("[ContractBoardUI] NetworkPlayer не найден!");
            ShowMessage("Ошибка: NetworkPlayer не найден");
        }
        else
        {
            Debug.LogWarning($"[ContractBoardUI] NetworkPlayer не Owner (IsOwner={_player.IsOwner})");
            ShowMessage("Ошибка: нет прав на запрос контрактов");
        }
    }

    /// <summary>
    /// Закрыть доску контрактов
    /// </summary>
    public void CloseBoard()
    {
        _isOpen = false;
        _selectedIndex = -1;
        if (_boardPanel != null) _boardPanel.SetActive(false);
        ShowMessage("");
    }

    // ==================== ПОЛУЧЕНИЕ ДАННЫХ ====================

    /// <summary>
    /// Вызывается из NetworkPlayer.ContractListClientRpc
    /// </summary>
    public void OnContractsReceived(string serializedContracts, string locationId)
    {
        // Парсим двойной формат: доступные|||активные
        string[] parts = serializedContracts.Split(new[] { "|||" }, System.StringSplitOptions.None);
        string availablePart = parts.Length > 0 ? parts[0] : "";
        string activePart = parts.Length > 1 ? parts[1] : "";

        _currentContracts = ContractSystem.DeserializeContracts(availablePart);
        _activeContracts = ContractSystem.DeserializeContracts(activePart);
        _currentLocationId = locationId;

        // Обновить отображение долга
        UpdateDebtDisplay();

        // Рендер контрактов
        RenderContracts();

        if (_currentContracts.Length == 0)
        {
            ShowMessage("Нет доступных контрактов");
        }
        else
        {
            ShowMessage($"Доступно контрактов: {_currentContracts.Length}. Выберите и нажмите Enter.");
        }
    }

    /// <summary>
    /// Вызывается из NetworkPlayer.ContractResultClientRpc
    /// </summary>
    public void OnContractResult(bool success, string message, float reward)
    {
        ShowMessage(message);
        if (success)
        {
            // Обновить список контрактов после успешного принятия
            if (_player != null && _player.IsOwner)
            {
                _player.ContractRequestServerRpc(_currentLocationId);
            }
        }
    }

    // ==================== РЕНДЕР ====================

    private void RenderContracts()
    {
        if (_contentPanel == null) return;

        // Очистка
        for (int i = _contentPanel.childCount - 1; i >= 0; i--)
            Destroy(_contentPanel.GetChild(i).gameObject);
        _contractRows.Clear();

        // === АКТИВНЫЕ КОНТРАКТЫ (вверху) ===
        if (_activeContracts.Length > 0)
        {
            MakeDividerRow("═══ МОИ КОНТРАКТЫ ═══");
            for (int i = 0; i < _activeContracts.Length; i++)
            {
                var c = _activeContracts[i];
                MakeActiveContractRow(c, i);
            }
        }

        // === ДОСТУПНЫЕ КОНТРАКТЫ ===
        MakeDividerRow("═══ ДОСТУПНЫЕ КОНТРАКТЫ ═══");
        if (_currentContracts.Length == 0)
        {
            MakeEmptyRow("Нет доступных контрактов");
        }
        else
        {
            int startIndex = _activeContracts.Length;
            for (int i = 0; i < _currentContracts.Length; i++)
            {
                var c = _currentContracts[i];
                MakeContractRow(c, i + startIndex);
            }
        }

        // Автовыбор первого доступного
        if (_selectedIndex < 0 && _currentContracts.Length > 0)
            _selectedIndex = _activeContracts.Length;
        else if (_selectedIndex < 0)
            _selectedIndex = 0;

        HighlightRow(_selectedIndex);
        UpdateCompleteButton();

        // Пересчёт layout
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_contentPanel as RectTransform);
    }

    private void UpdateCompleteButton()
    {
        bool canComplete = false;
        foreach (var c in _activeContracts)
        {
            if (c.toLocationId == _currentLocationId)
            {
                canComplete = true;
                break;
            }
        }

        foreach (var btn in _uiButtons)
        {
            if (btn != null && btn.gameObject.name == "CompleteBtn")
                btn.gameObject.SetActive(canComplete);
        }
    }

    private void MakeDividerRow(string text)
    {
        var go = new GameObject("DividerRow");
        go.transform.SetParent(_contentPanel, false);
        var r = go.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(0, 22);
        var t = go.AddComponent<Text>();
        t.text = text;
        t.fontSize = 11;
        t.color = Color.cyan;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void MakeActiveContractRow(ContractData contract, int index)
    {
        var rowGO = new GameObject($"ActiveRow_{index}");
        rowGO.transform.SetParent(_contentPanel, false);
        var rRect = rowGO.AddComponent<RectTransform>();
        rRect.anchorMin = Vector2.zero;
        rRect.anchorMax = Vector2.one;
        rRect.sizeDelta = Vector2.zero;

        var layoutElem = rowGO.AddComponent<UnityEngine.UI.LayoutElement>();
        layoutElem.preferredHeight = 40f;

        var bg = rowGO.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.10f, 0.05f); // тёплый фон для активных

        // Тип + товар
        MakeLabel("Type", rowGO.transform, contract.GetTypeDisplayName(), -240, 10, 11, contract.GetTypeColor(), 140);
        MakeLabel("Item", rowGO.transform, $"{contract.itemId} x{contract.quantity}", -80, 10, 11, Color.white, 180);

        // Таймер (красный если мало времени)
        Color timeColor = contract.GetTimePercent() < 0.3f ? Color.red : Color.yellow;
        MakeLabel("Time", rowGO.transform, contract.GetTimeRemainingString(), 200, 10, 13, timeColor, 60);

        _contractRows.Add(rowGO);
    }

    private void MakeContractRow(ContractData contract, int index)
    {
        var rowGO = new GameObject($"ContractRow_{index}");
        rowGO.transform.SetParent(_contentPanel, false);
        var rRect = rowGO.AddComponent<RectTransform>();
        rRect.anchorMin = Vector2.zero;
        rRect.anchorMax = Vector2.one;
        rRect.sizeDelta = Vector2.zero;

        var layoutElem = rowGO.AddComponent<UnityEngine.UI.LayoutElement>();
        layoutElem.preferredHeight = 50f;

        var bg = rowGO.AddComponent<Image>();
        bg.color = index % 2 == 0 ? new Color(0.06f, 0.06f, 0.10f) : new Color(0.10f, 0.10f, 0.15f);

        // Тип + иконка
        var typeText = MakeLabel("Type", rowGO.transform, contract.GetTypeDisplayName(), -240, 12, 12, contract.GetTypeColor(), 160);

        // Товар + количество
        var itemText = MakeLabel("Item", rowGO.transform, $"{contract.itemId} x{contract.quantity}", -60, 12, 12, Color.white, 200);

        // Маршрут
        var routeText = MakeLabel("Route", rowGO.transform, $"{contract.fromLocationId} → {contract.toLocationId}", -60, -6, 11, Color.grey, 260);

        // Награда
        var rewardText = MakeLabel("Reward", rowGO.transform, $"{contract.reward:F0} CR", 180, 12, 13, Color.yellow, 100);

        // Таймер
        var timeText = MakeLabel("Time", rowGO.transform, contract.GetTimeRemainingString(), 240, 12, 11, Color.white, 60);

        // Кнопка выбора
        var btn = rowGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        int ci = index;
        btn.onClick.AddListener(() => SelectContract(ci));

        _contractRows.Add(rowGO);
    }

    private void MakeEmptyRow(string msg)
    {
        var go = new GameObject("EmptyRow");
        go.transform.SetParent(_contentPanel, false);
        var r = go.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(0, 30);
        var t = go.AddComponent<Text>();
        t.text = msg;
        t.fontSize = 13;
        t.color = Color.gray;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void SelectContract(int index)
    {
        _selectedIndex = index;
        HighlightRow(index);

        if (index >= 0 && index < _currentContracts.Length)
        {
            var c = _currentContracts[index];
            ShowMessage($"{c.GetTypeDisplayName()} | {c.itemId} x{c.quantity} | {c.fromLocationId} → {c.toLocationId} | Награда: {c.reward:F0} CR | Время: {c.GetTimeRemainingString()}");
        }
    }

    private void HighlightRow(int index)
    {
        if (_contentPanel == null) return;
        for (int i = 0; i < _contentPanel.childCount; i++)
        {
            var child = _contentPanel.GetChild(i);
            var bg = child.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = (i == index) ? new Color(0.2f, 0.25f, 0.15f) : (i % 2 == 0 ? new Color(0.06f, 0.06f, 0.10f) : new Color(0.10f, 0.10f, 0.15f));
            }
        }
    }

    // ==================== ДИСПЛЕИ ====================

    private void UpdateDebtDisplay()
    {
        if (_debtText == null) return;

        // Найти PlayerDebt игрока
        if (_player != null)
        {
            var debt = _player.GetComponent<PlayerDebt>();
            if (debt != null && debt.CurrentDebt > 0f)
            {
                _debtText.text = $"⚠ ДОЛГ: {debt.CurrentDebt:F0} CR — {debt.GetDebtPenaltyString()}";
                _debtText.color = debt.GetDebtColor();
                return;
            }
        }

        _debtText.text = "";
    }

    private void ShowMessage(string msg)
    {
        if (_messageText != null) _messageText.text = msg;
    }

    // ==================== ВВОД ====================

    private void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.upArrowKey.wasPressedThisFrame)
        {
            _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
            HighlightRow(_selectedIndex);
            SelectContract(_selectedIndex);
        }
        if (kb.downArrowKey.wasPressedThisFrame)
        {
            _selectedIndex = Mathf.Min(_currentContracts.Length - 1, _selectedIndex + 1);
            HighlightRow(_selectedIndex);
            SelectContract(_selectedIndex);
        }
        if (kb.enterKey.wasPressedThisFrame && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
        {
            OnCompleteClicked();
        }
        else if (kb.enterKey.wasPressedThisFrame)
        {
            OnAcceptClicked();
        }
        if (kb.escapeKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame)
        {
            OnCloseClicked();
        }
    }

    // ==================== КНОПКИ ====================

    private void OnAcceptClicked()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _currentContracts.Length)
        {
            ShowMessage("Выберите контракт!");
            return;
        }

        var contract = _currentContracts[_selectedIndex];
        if (contract == null) return;

        ShowMessage($"Принимаю контракт: {contract.contractId}...");

        if (_player != null && _player.IsOwner)
        {
            _player.ContractAcceptServerRpc(contract.contractId);
        }
    }

    /// <summary>
    /// Сдать активный контракт в текущей локации
    /// </summary>
    private void OnCompleteClicked()
    {
        if (_activeContracts.Length == 0)
        {
            ShowMessage("Нет активных контрактов для сдачи!");
            return;
        }

        // Ищем контракт, целевая локация которого совпадает с текущей
        ContractData targetContract = null;
        foreach (var c in _activeContracts)
        {
            if (c.toLocationId == _currentLocationId)
            {
                targetContract = c;
                break;
            }
        }

        if (targetContract == null)
        {
            ShowMessage($"В этой локации нет контрактов для сдачи. Нужно: {_activeContracts[0].toLocationId}");
            return;
        }

        ShowMessage($"Сдаю контракт: {targetContract.contractId}...");

        if (_player != null && _player.IsOwner)
        {
            _player.ContractCompleteServerRpc(targetContract.contractId, _currentLocationId);
        }
    }

    private void OnCloseClicked()
    {
        CloseBoard();
    }
}
