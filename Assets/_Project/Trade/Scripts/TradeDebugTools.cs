using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using ProjectC.Trade;
using ProjectC.Player;
using System.Collections.Generic;

// PlayerDataStore в namespace ProjectC.Trade

namespace ProjectC.Trade
{

/// <summary>
/// Debug UI: Принудительный склад клиента.
/// Всегда отображается в правной части экрана.
/// Не зависит от TradeUI.
/// 
/// Создаёт и обновляет UI даже если TradeUI закрыт.
/// Помогает диагностировать проблемы с синхронизацией.
/// </summary>
public class TradeDebugTools : MonoBehaviour
{
    public static TradeDebugTools Instance { get; private set; }

    [Header("UI Settings")]
    [SerializeField] private bool showOnScreen = true;
    [SerializeField] private float panelWidth = 350f;
    [SerializeField] private float panelHeight = 500f;
    [SerializeField] private float padding = 20f;

    [Header("Update Settings")]
    [SerializeField] private float updateInterval = 0.5f;

    private Canvas _canvas;
    private RectTransform _panel;
    private TextMeshProUGUI _creditsText;
    private TextMeshProUGUI _warehouseText;
    private TextMeshProUGUI _debugText;
    private TextMeshProUGUI _titleText;

    private float _updateTimer = 0f;
    private PlayerTradeStorage _storage;
    private NetworkPlayer _player;
    private bool _isInitialized = false;

    private float _blinkTimer = 0f;
    private bool _showUpdateIndicator = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        CreateUI();
        _isInitialized = true;
    }

    private void Update()
    {
        _updateTimer += Time.deltaTime;
        _blinkTimer += Time.deltaTime;

        if (_blinkTimer > 0.5f)
        {
            _blinkTimer = 0f;
            _showUpdateIndicator = !_showUpdateIndicator;
        }

        if (_updateTimer >= updateInterval)
        {
            _updateTimer = 0f;
            UpdateUI();
        }

        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            showOnScreen = !showOnScreen;
            _panel?.gameObject?.SetActive(showOnScreen);
        }
    }

    private void CreateUI()
    {
        var canvasObj = new GameObject("TradeDebugCanvas");
        canvasObj.transform.SetParent(transform);
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;

        var canvasScaler = canvasObj.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        var panelObj = new GameObject("DebugPanel");
        panelObj.transform.SetParent(_canvas.transform);
        _panel = panelObj.AddComponent<RectTransform>();

        _panel.anchorMin = new Vector2(1f, 0.5f);
        _panel.anchorMax = new Vector2(1f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.anchoredPosition = new Vector2(-panelWidth / 2f - padding, 0f);
        _panel.sizeDelta = new Vector2(panelWidth, panelHeight);

        var panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

        var outline = panelObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.6f, 1f);
        outline.effectDistance = new Vector2(3f, 3f);

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(_panel);
        var content = contentObj.AddComponent<RectTransform>();
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(15f, 15f);
        content.offsetMax = new Vector2(-15f, -15f);

        var titleObj = CreateTextObject("Title", content, "СКЛАД КЛИЕНТА", 24, TextAlignmentOptions.Center);
        _titleText = titleObj.GetComponent<TextMeshProUGUI>();
        _titleText.color = new Color(0.3f, 0.8f, 1f);
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(0f, -50f);
        titleRect.offsetMax = new Vector2(0f, 0f);

        var creditsObj = CreateTextObject("Credits", content, "Кредиты: ---", 20, TextAlignmentOptions.Left);
        _creditsText = creditsObj.GetComponent<TextMeshProUGUI>();
        _creditsText.color = new Color(1f, 0.8f, 0.2f);
        var creditsRect = creditsObj.GetComponent<RectTransform>();
        creditsRect.anchorMin = new Vector2(0f, 0.9f);
        creditsRect.anchorMax = new Vector2(1f, 0.95f);
        creditsRect.offsetMin = Vector2.zero;
        creditsRect.offsetMax = Vector2.zero;

        var updateObj = CreateTextObject("UpdateIndicator", content, "●", 16, TextAlignmentOptions.Right);
        var updateText = updateObj.GetComponent<TextMeshProUGUI>();
        updateText.color = new Color(0.2f, 1f, 0.2f);
        var updateRect = updateObj.GetComponent<RectTransform>();
        updateRect.anchorMin = new Vector2(0f, 0.95f);
        updateRect.anchorMax = new Vector2(1f, 0.98f);
        updateRect.offsetMin = Vector2.zero;
        updateRect.offsetMax = Vector2.zero;

        var warehouseLabelObj = CreateTextObject("WarehouseLabel", content, "═══ СКЛАД ═══", 18, TextAlignmentOptions.Center);
        var warehouseLabelText = warehouseLabelObj.GetComponent<TextMeshProUGUI>();
        warehouseLabelText.color = new Color(0.6f, 0.6f, 1f);
        var warehouseLabelRect = warehouseLabelObj.GetComponent<RectTransform>();
        warehouseLabelRect.anchorMin = new Vector2(0f, 0.85f);
        warehouseLabelRect.anchorMax = new Vector2(1f, 0.9f);
        warehouseLabelRect.offsetMin = Vector2.zero;
        warehouseLabelRect.offsetMax = Vector2.zero;

        var warehouseObj = CreateTextObject("Warehouse", content, "Пусто", 16, TextAlignmentOptions.Left);
        _warehouseText = warehouseObj.GetComponent<TextMeshProUGUI>();
        _warehouseText.color = new Color(0.9f, 0.9f, 0.9f);
        var warehouseRect = warehouseObj.GetComponent<RectTransform>();
        warehouseRect.anchorMin = new Vector2(0f, 0.1f);
        warehouseRect.anchorMax = new Vector2(1f, 0.85f);
        warehouseRect.offsetMin = Vector2.zero;
        warehouseRect.offsetMax = Vector2.zero;

        var debugObj = CreateTextObject("Debug", content, "Статус: ---", 14, TextAlignmentOptions.Left);
        _debugText = debugObj.GetComponent<TextMeshProUGUI>();
        _debugText.color = new Color(0.5f, 0.5f, 0.5f);
        var debugRect = debugObj.GetComponent<RectTransform>();
        debugRect.anchorMin = new Vector2(0f, 0f);
        debugRect.anchorMax = new Vector2(1f, 0.1f);
        debugRect.offsetMin = Vector2.zero;
        debugRect.offsetMax = Vector2.zero;

    }

    private GameObject CreateTextObject(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent);

        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var textComp = obj.AddComponent<TextMeshProUGUI>();
        textComp.text = text;
        textComp.fontSize = fontSize;
        textComp.alignment = alignment;
        textComp.textWrappingMode = TextWrappingModes.Normal;

        return obj;
    }

    private void UpdateUI()
    {
        if (!_isInitialized) return;

        if (_player == null || !_player.IsOwner)
        {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
            foreach (var p in players)
            {
                if (p.IsOwner)
                {
                    _player = p;
                    break;
                }
            }
        }

        _storage = GetOrCreateStorage();

        float credits = 0f;
        if (PlayerDataStore.Instance != null && _player != null)
        {
            credits = PlayerDataStore.Instance.GetCredits(_player.OwnerClientId);
        }
        _creditsText.text = $"Кредиты: {credits:N0}";

        string currentLocationId = GetCurrentLocationId();

        string warehouseInfo = "";
        if (_storage != null)
        {
            warehouseInfo = $"Вес: {_storage.CurrentWeight:N1}/{_storage.maxWeight:N1}\n";
            warehouseInfo += $"Объём: {_storage.CurrentVolume:N1}/{_storage.maxVolume:N1}\n";
            warehouseInfo += $"Слоты: {_storage.warehouse.Count}/{_storage.maxItemTypes}\n";
            warehouseInfo += $"\n── Товары ({currentLocationId ?? "?"}) ──\n";

            if (_storage.warehouse.Count == 0)
            {
                warehouseInfo += "Пусто";
            }
            else
            {
                foreach (var item in _storage.warehouse)
                {
                    if (item.item != null)
                    {
                        warehouseInfo += $"{item.item.displayName} x{item.quantity}\n";
                    }
                }
            }
        }
        else
        {
            warehouseInfo = "Нет данных о складе\n(Storage = null)";
        }
        _warehouseText.text = warehouseInfo;

        string debugInfo = "";
        debugInfo += $"NetworkPlayer: {(_player != null ? "OK" : "NULL")}\n";
        debugInfo += $"Storage: {(_storage != null ? "OK" : "NULL")}\n";
        debugInfo += $"Credits: {credits:N0}\n";
        debugInfo += $"Location: {currentLocationId ?? "null"}\n";
        debugInfo += $"Market: {(TradeUI.Instance?.currentMarket?.locationId ?? "null")}\n";
        debugInfo += $"F3 = toggle\n";

        _debugText.text = debugInfo;
        _titleText.text = $"СКЛАД КЛИЕНТА [{currentLocationId ?? "?"}]";
    }

    private PlayerTradeStorage GetOrCreateStorage()
    {
        if (TradeUI.Instance?.playerStorage != null)
        {
            return TradeUI.Instance.playerStorage;
        }

        if (_player != null)
        {
            var storage = _player.GetComponent<PlayerTradeStorage>();
            if (storage != null) return storage;
            storage = _player.gameObject.AddComponent<PlayerTradeStorage>();
            Debug.Log("[TradeDebugTools] Created PlayerTradeStorage on NetworkPlayer");
            return storage;
        }

        return null;
    }

    private string GetCurrentLocationId()
    {
        if (TradeUI.Instance?.currentMarket != null)
        {
            return TradeUI.Instance.currentMarket.locationId;
        }

        if (_storage != null && !string.IsNullOrEmpty(_storage.currentLocationId))
        {
            return _storage.currentLocationId;
        }

        var markets = FindObjectsByType<LocationMarket>(FindObjectsInactive.Exclude);
        if (markets.Length > 0)
        {
            return markets[0].locationId;
        }

        return null;
    }

    [ContextMenu("Force Refresh")]
    public void ForceRefresh()
    {
        if (_player != null && PlayerDataStore.Instance != null)
        {
            Debug.Log("[TradeDebugTools] ForceRefresh: updating from PlayerDataStore");
            float credits = PlayerDataStore.Instance.GetCredits(_player.OwnerClientId);
            Debug.Log($"[TradeDebugTools] Credits = {credits}");

            if (_storage != null)
            {
                _storage.LoadFromPlayerDataStore(_player.OwnerClientId);
                Debug.Log($"[TradeDebugTools] Storage loaded, warehouse count = {_storage.warehouse.Count}");
            }
        }
        else
        {
            Debug.LogWarning("[TradeDebugTools] ForceRefresh: cannot update (null refs)");
        }

        _updateTimer = 0f;
        UpdateUI();
    }

    [ContextMenu("Log State")]
    public void LogState()
    {
        Debug.Log("=== TradeDebugTools State ===");
        Debug.Log($"_player: {(_player != null ? $"OwnerClientId={_player.OwnerClientId}" : "null")}");
        Debug.Log($"_storage: {(_storage != null ? $"warehouse.Count={_storage.warehouse.Count}" : "null")}");

        if (PlayerDataStore.Instance != null && _player != null)
        {
            float credits = PlayerDataStore.Instance.GetCredits(_player.OwnerClientId);
            Debug.Log($"PlayerDataStore.GetCredits({_player.OwnerClientId}) = {credits}");
        }
        else
        {
            Debug.Log("PlayerDataStore.Instance = null or _player = null");
        }

        Debug.Log($"TradeMarketServer.Instance = {(TradeMarketServer.Instance != null ? "yes" : "no")}");
        Debug.Log($"TradeUI.Instance = {(TradeUI.Instance != null ? "yes" : "no")}");
        Debug.Log("===========================");
    }
}

} // namespace