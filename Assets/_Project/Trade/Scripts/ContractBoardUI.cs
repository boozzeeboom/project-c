using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using ProjectC.Trade;
using ProjectC.Player;
using ProjectC.UI;

/// <summary>
/// UI доски контрактов НП — отдельный префаб, открывается у NPC-агента.
/// GDD_25 секция 6: Контрактная Система.
/// Утверждено решение 5B: отдельный префаб (не вкладка TradeUI).
///
/// Мигрировано на TextMeshProUGUI (Спринт 2).
/// Сессия 7: ContractSystem.
/// Поток: Игрок подходит к NPC-агенту → E → ContractBoardUI → RequestContracts → список → "Взять" → ServerRpc.
/// </summary>
public class ContractBoardUI : MonoBehaviour
{
    public static ContractBoardUI Instance { get; private set; }

    private GameObject _rootCanvas;
    private GameObject _boardPanel;
    private Transform _contentPanel;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _locationText;
    private TextMeshProUGUI _debtText;
    private TextMeshProUGUI _messageText;
    private List<GameObject> _contractRows = new List<GameObject>();
    private List<Button> _uiButtons = new List<Button>();

    private bool _isOpen;
    private int _selectedIndex = -1;
    private ContractData[] _currentContracts = new ContractData[0];
    private ContractData[] _activeContracts = new ContractData[0];
    private string _currentLocationId = "";
    private NetworkPlayer _player;

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
        var theme = UITheme.Default;

        // --- Root Canvas ---
        _rootCanvas = UIFactory.CreateRootCanvas("[ContractBoard]_RootCanvas", theme.ContractBoardUISortingOrder);

        // --- Панель ---
        _boardPanel = UIFactory.CreatePanel("ContractBoardPanel", _rootCanvas.transform, 0, 0, 600, 700);
        _boardPanel.SetActive(false);

        // --- Заголовок ---
        _titleText = MakeLabel("Title", _boardPanel.transform, "КОНТРАКТЫ НП", 0, 310, theme.FontSizeHeading, theme.TextTitle, 560);
        _locationText = MakeLabel("LocationText", _boardPanel.transform, "[Локация]", 0, 280, theme.FontSizeBody, theme.AccentInfo, 300);

        // --- Долг ---
        _debtText = MakeLabel("DebtText", _boardPanel.transform, "", 0, 256, theme.FontSizeCaption, theme.AccentDanger, 560);

        // --- Scroll-зона ---
        var scrollArea = UIFactory.CreateScrollArea(_boardPanel.transform, out RectTransform content);
        var scrollRect = scrollArea.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.04f, 0.22f);
        scrollRect.anchorMax = new Vector2(0.96f, 0.80f);
        scrollRect.sizeDelta = Vector2.zero;
        _contentPanel = content;

        // --- Кнопки ---
        _uiButtons.Add(MakeBtn("AcceptBtn", _boardPanel.transform, "ВЗЯТЬ (Enter)", 0, -190, 260, 36, OnAcceptClicked));
        _uiButtons.Add(MakeBtn("CompleteBtn", _boardPanel.transform, "СДАТЬ (Shift+Enter)", 0, -235, 260, 36, OnCompleteClicked));
        _uiButtons.Add(MakeBtn("CloseBtn", _boardPanel.transform, "ЗАКРЫТЬ (C)", 0, -280, 200, 36, OnCloseClicked));

        // --- Сообщение ---
        _messageText = MakeLabel("MsgText", _boardPanel.transform, "Выберите контракт и нажмите Enter", 0, -240, theme.FontSizeList, theme.TextMessage, 560);
        MakeLabel("Hint1", _boardPanel.transform, "Up/Down - выбор | Enter - взять | C - закрыть", 0, -262, theme.FontSizeCaption, theme.TextMuted, 560);
        MakeLabel("Hint2", _boardPanel.transform, "[Стандарт] [Срочный] [Расписка]", 0, -310, theme.FontSizeCaption, theme.TextMuted, 560);
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

    // ==================== UI FACTORY WRAPPERS ====================

    private GameObject CreatePanel(string name, Transform parent, float x, float y, float w, float h)
    {
        return UIFactory.CreatePanel(name, parent, (int)x, (int)y, (int)w, (int)h);
    }

    private TextMeshProUGUI MakeLabel(string name, Transform parent, string text, float x, float y, int fontSize, Color color, float width)
    {
        return UIFactory.CreateLabel(name, parent, text, (int)x, (int)y, fontSize, color, (int)width);
    }

    private Button MakeBtn(string name, Transform parent, string label, float x, float y, float w, float h, UnityEngine.Events.UnityAction onClick)
    {
        return UIFactory.CreateButton(name, parent, label, onClick, new Vector2((int)w, (int)h), (int)x, (int)y);
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

        // Разблокируем курсор для UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _boardPanel.SetActive(true);
        _boardPanel.transform.SetAsLastSibling();

        if (_titleText != null) _titleText.text = $"КОНТРАКТЫ НП — {market.locationName}";
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

        // Блокируем курсор обратно
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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
            // Обновить список контрактов после успешного принятия/завершения
            if (_player != null && _player.IsOwner)
            {
                _player.ContractRequestServerRpc(_currentLocationId);
            }

            // Сессия 8F: Обновляем данные UI из PlayerDataStore (единый источник)
            if (TradeUI.Instance != null && TradeUI.Instance.playerStorage != null)
            {
                TradeUI.Instance.playerStorage.LoadFromPlayerDataStore(_player.OwnerClientId);
                TradeUI.Instance.UpdateDisplays();
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
        var theme = UITheme.Default;
        UIFactory.CreateDividerRow(_contentPanel, text, theme.FontSizeCaption, theme.AccentInfo);
    }

    private void MakeActiveContractRow(ContractData contract, int index)
    {
        var theme = UITheme.Default;
        var rowGO = UIFactory.CreateListRow(_contentPanel, $"{contract.itemId} x{contract.quantity}", theme.TextPrimary, index, isActive: true);
        rowGO.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 40f;

        var bg = rowGO.GetComponent<Image>();
        if (bg != null) bg.color = theme.ActiveContractRow;

        // Тип + товар
        MakeLabel("Type", rowGO.transform, contract.GetTypeDisplayName(), -240, 10, theme.FontSizeCaption, contract.GetTypeColor(), 140);

        // Таймер (красный если мало времени)
        Color timeColor = contract.GetTimePercent() < 0.3f ? theme.AccentDanger : theme.AccentWarning;
        MakeLabel("Time", rowGO.transform, contract.GetTimeRemainingString(), 200, 10, theme.FontSizeList, timeColor, 60);

        _contractRows.Add(rowGO);
    }

    private void MakeContractRow(ContractData contract, int index)
    {
        var theme = UITheme.Default;
        string rowText = $"{contract.GetTypeDisplayName()} | {contract.itemId} x{contract.quantity}";
        var rowGO = UIFactory.CreateListRow(_contentPanel, rowText, theme.TextPrimary, index);
        rowGO.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 50f;

        // Маршрут
        MakeLabel("Route", rowGO.transform, $"{contract.fromLocationId} → {contract.toLocationId}", -60, -6, theme.FontSizeCaption, theme.TextSecondary, 260);

        // Награда
        MakeLabel("Reward", rowGO.transform, $"{contract.reward:F0} CR", 180, 12, theme.FontSizeList, theme.TextTitle, 100);

        // Кнопка выбора
        var btn = rowGO.GetComponent<Button>();
        if (btn == null) btn = rowGO.AddComponent<Button>();
        var bg = rowGO.GetComponent<Image>();
        if (bg != null) btn.targetGraphic = bg;
        int ci = index;
        btn.onClick.AddListener(() => SelectContract(ci));

        _contractRows.Add(rowGO);
    }

    private void MakeEmptyRow(string msg)
    {
        UIFactory.CreateEmptyRow(_contentPanel, msg);
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
        var theme = UITheme.Default;
        for (int i = 0; i < _contentPanel.childCount; i++)
        {
            var child = _contentPanel.GetChild(i);
            var bg = child.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = (i == index) ? theme.SelectedRow : theme.GetContractRowColor(i);
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
                _debtText.text = $"[ДОЛГ] {debt.CurrentDebt:F0} CR — {debt.GetDebtPenaltyString()}";
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
