// Тестовая клавиша нового окна — шаг 2 плана переноса (docs/UI/full rebuild plans/Cloudy_redesign/02 §3).
//
// Клавиша читается напрямую из Keyboard.current (как AdminRuntimeWindow делает для F12), поэтому
// InputBindingsConfig и NetworkPlayer не трогаются вовсе: пока окно отлаживается, P остаётся за легаси.
// Решение пользователя (2026-10-28): клавиша "[" — Key.LeftBracket (на русской раскладке та же физическая
// клавиша даёт "х"); в Assets/_Project совпадений по Key.LeftBracket не найдено — клавиша свободна.
//
// Условия срабатывания те же, что у P: есть сетевая сессия и заспавнен локальный игрок.
// Пока фокус в поле ввода, клавиша не перехватывается.
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectCCloudyNavigator
{
    [RequireComponent(typeof(CloudyNavigatorHost))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public class CloudyNavigatorHotkey : MonoBehaviour
    {
        [Tooltip("Клавиша открытия/закрытия окна. По умолчанию \"[\" (Key.LeftBracket).")]
        [SerializeField] private Key toggleKey = Key.LeftBracket;

        private CloudyNavigatorHost _host;

        public Key ToggleKey => toggleKey;

        private void Awake()
        {
            _host = GetComponent<CloudyNavigatorHost>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _host == null) return;

            if (!keyboard[toggleKey].wasPressedThisFrame) return;
            if (!CanToggle()) return;

            _host.Toggle();
        }

        /// <summary>Открывать можно только в живой сессии с локальным игроком — как Toggle() у легаси-окна.</summary>
        private static bool CanToggle()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return false;
            if (nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return false;

            return !IsTypingInField();
        }

        private static bool IsTypingInField()
        {
            var es = EventSystem.current;
            if (es == null) return false;

            var selected = es.currentSelectedGameObject;
            if (selected == null) return false;

            if (selected.TryGetComponent(out InputField _)) return true;

            // TMP_InputField живёт в пакетной сборке TextMeshPro: ищем по имени типа, без прямой ссылки.
            return selected.GetComponent("TMP_InputField") != null;
        }
    }
}
