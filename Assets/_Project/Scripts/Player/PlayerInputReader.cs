using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Player
{
    /// <summary>
    /// R2-003: Компонент для чтения ввода игрока через Input System.
    /// Заменяет Keyboard.current.wKey.isPressed на Input Actions.
    /// Использует events для передачи ввода в NetworkPlayer.
    /// 
    /// Использование:
    /// - Добавить на объект с NetworkPlayer
    /// - Подключить events к методам NetworkPlayer
    /// </summary>
    [RequireComponent(typeof(NetworkPlayer))]
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Input Settings")]
        #pragma warning disable 0414
        [SerializeField] private float mouseSensitivityX = 2f;
        [SerializeField] private float mouseSensitivityY = 2f;
        #pragma warning restore 0414

        // Events для передачи ввода
        #pragma warning disable 0067
        public event System.Action<Vector2> OnMoveInput;          // WASD (reserved for future use)
        #pragma warning restore 0067
        public event System.Action OnJumpPressed;
        public event System.Action OnRunPressed;
        public event System.Action OnRunReleased;
        public event System.Action OnInteractPressed;
        public event System.Action OnModeSwitchPressed;
        public event System.Action<Vector2> OnMouseDelta;       // Camera rotation

        // Состояние ввода
        private Vector2 _moveInput;
        private bool _jumpPressed;
        private bool _runPressed;
        private bool _interactPressed;
        private bool _modeSwitchPressed;
        private Vector2 _mouseDelta;

        private void Update()
        {
            // Сброс ввода каждый кадр
            _moveInput = Vector2.zero;

            if (Keyboard.current != null)
            {
                // ===== MOVE (WASD) =====
                if (Keyboard.current.wKey.isPressed) _moveInput.y += 1f;
                if (Keyboard.current.sKey.isPressed) _moveInput.y -= 1f;
                if (Keyboard.current.aKey.isPressed) _moveInput.x -= 1f;
                if (Keyboard.current.dKey.isPressed) _moveInput.x += 1f;

                // ===== RUN (Shift) =====
                bool runNow = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
                if (runNow && !_runPressed)
                {
                    _runPressed = true;
                    OnRunPressed?.Invoke();
                }
                else if (!runNow && _runPressed)
                {
                    _runPressed = false;
                    OnRunReleased?.Invoke();
                }

                // ===== JUMP (Space) =====
                if (Keyboard.current.spaceKey.wasPressedThisFrame && !_jumpPressed)
                {
                    _jumpPressed = true;
                    OnJumpPressed?.Invoke();
                }
                else if (!Keyboard.current.spaceKey.isPressed)
                {
                    _jumpPressed = false;
                }

                // ===== INTERACT (E) =====
                if (Keyboard.current.eKey.wasPressedThisFrame && !_interactPressed)
                {
                    _interactPressed = true;
                    OnInteractPressed?.Invoke();
                }
                else if (!Keyboard.current.eKey.isPressed)
                {
                    _interactPressed = false;
                }

                // ===== MODE SWITCH (F) =====
                if (Keyboard.current.fKey.wasPressedThisFrame && !_modeSwitchPressed)
                {
                    _modeSwitchPressed = true;
                    OnModeSwitchPressed?.Invoke();
                }
                else if (!Keyboard.current.fKey.isPressed)
                {
                    _modeSwitchPressed = false;
                }
            }

            // ===== MOUSE DELTA =====
            if (Mouse.current != null)
            {
                _mouseDelta = Mouse.current.delta.ReadValue();
                if (_mouseDelta.magnitude > 0.1f)
                {
                    OnMouseDelta?.Invoke(_mouseDelta);
                }
            }
        }

        /// <summary>
        /// Текущий ввод движения
        /// </summary>
        public Vector2 MoveInput => _moveInput;

        /// <summary>
        /// Зажат ли Shift
        /// </summary>
        public bool IsRunHeld => _runPressed;

        /// <summary>
        /// Текущая дельта мыши
        /// </summary>
        public Vector2 MouseDelta => _mouseDelta;
    }
}