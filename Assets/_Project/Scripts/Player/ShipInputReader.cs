using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ProjectC.Player
{
    /// <summary>
    /// R2-002: Компонент для чтения ввода корабля через Input System.
    /// Заменяет IsKeyDown(KeyCode.X) на Input Actions.
    /// Использует events для передачи ввода в ShipController.
    /// 
    /// Использование:
    /// - Добавить на объект с ShipController
    /// - Подключить events к ShipController.SendShipInput
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public class ShipInputReader : MonoBehaviour
    {
        // Note: mouseSensitivityX/Y reserved for future pitch/yaw from mouse
        #pragma warning disable 0414
        [SerializeField] private float mouseSensitivityX = 2f;
        [SerializeField] private float mouseSensitivityY = 2f;
        #pragma warning restore 0414

        // Events для передачи ввода
        public event System.Action<float> OnThrustChanged;      // +1/-1/0
        public event System.Action<float> OnYawChanged;         // +1/-1/0
        public event System.Action<float> OnPitchChanged;       // +1/-1/0 (from mouse)
        public event System.Action<float> OnVerticalChanged;    // +1/-1/0
        public event System.Action OnBoostPressed;
        public event System.Action OnBoostReleased;
        public event System.Action OnMeziyPitchUp;
        public event System.Action OnMeziyPitchDown;
        public event System.Action OnMeziyRollLeft;
        public event System.Action OnMeziyRollRight;
        public event System.Action OnMeziyYawLeft;
        public event System.Action OnMeziyYawRight;
        public event System.Action OnMeziyThrustForward;
        public event System.Action OnMeziyThrustBackward;

        // Состояние ввода
        private float _currentThrust;
        private float _currentYaw;
        private float _currentPitch;
        private float _currentVertical;
        private bool _boostPressed;
        private bool _shiftHeld;

        // Mouse delta accumulator
        private float _pitchInput;

        // Meziy state
        private bool _meziyPitchUpHeld;
        private bool _meziyPitchDownHeld;
        private bool _meziyRollLeftHeld;
        private bool _meziyRollRightHeld;
        private bool _meziyYawLeftHeld;
        private bool _meziyYawRightHeld;
        private bool _meziyThrustFwdHeld;
        private bool _meziyThrustBwdHeld;

        private void Update()
        {
            // Сброс ввода каждый кадр
            float thrustInput = 0f;
            float yawInput = 0f;
            float verticalInput = 0f;
            _pitchInput = 0f;
            _shiftHeld = Keyboard.current != null && 
                         (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

            // ===== THRUST (W/S) =====
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) thrustInput += 1f;
                if (Keyboard.current.sKey.isPressed) thrustInput -= 1f;

                // ===== YAW (A/D) =====
                if (Keyboard.current.aKey.isPressed) yawInput -= 1f;
                if (Keyboard.current.dKey.isPressed) yawInput += 1f;

                // ===== VERTICAL (E/Q) =====
                if (Keyboard.current.eKey.isPressed) verticalInput += 1f;
                if (Keyboard.current.qKey.isPressed) verticalInput -= 1f;
            }

            // ===== PITCH (Mouse Y) =====
            if (Mouse.current != null)
            {
                float mouseDeltaY = Mouse.current.delta.y.ReadValue();
                if (mouseDeltaY > 1f)
                    _pitchInput = 1f;
                else if (mouseDeltaY < -1f)
                    _pitchInput = -1f;
            }

            // ===== BOOST (Shift) =====
            bool boostNow = _shiftHeld;
            if (boostNow && !_boostPressed)
            {
                _boostPressed = true;
                OnBoostPressed?.Invoke();
            }
            else if (!boostNow && _boostPressed)
            {
                _boostPressed = false;
                OnBoostReleased?.Invoke();
            }

            // ===== MEZIY MODULES (C/V/Z/X + Shift+A/D) =====
            ProcessMeziyInput();

            // Отправляем events если значения изменились
            if (Mathf.Abs(thrustInput - _currentThrust) > 0.01f)
            {
                _currentThrust = thrustInput;
                OnThrustChanged?.Invoke(thrustInput);
            }

            if (Mathf.Abs(yawInput - _currentYaw) > 0.01f)
            {
                _currentYaw = yawInput;
                OnYawChanged?.Invoke(yawInput);
            }

            if (Mathf.Abs(_pitchInput - _currentPitch) > 0.01f)
            {
                _currentPitch = _pitchInput;
                OnPitchChanged?.Invoke(_pitchInput);
            }

            if (Mathf.Abs(verticalInput - _currentVertical) > 0.01f)
            {
                _currentVertical = verticalInput;
                OnVerticalChanged?.Invoke(verticalInput);
            }
        }

        /// <summary>
        /// R2-002: Обработка ввода для мезиевых модулей.
        /// MODULE_MEZIY_PITCH: C (вверх), V (вниз)
        /// MODULE_MEZIY_ROLL: Z (влево), X (вправо)
        /// MODULE_MEZIY_YAW: Shift+A (влево), Shift+D (вправо)
        /// MODULE_MEZIY_THRUST: Shift+W (вперёд), Shift+S (назад)
        /// </summary>
        private void ProcessMeziyInput()
        {
            if (Keyboard.current == null) return;

            bool cHeld = Keyboard.current.cKey.isPressed;
            bool vHeld = Keyboard.current.vKey.isPressed;
            bool zHeld = Keyboard.current.zKey.isPressed;
            bool xHeld = Keyboard.current.xKey.isPressed;
            bool aHeld = Keyboard.current.aKey.isPressed;
            bool wHeld = Keyboard.current.wKey.isPressed;

            // MODULE_MEZIY_PITCH
            if (cHeld && !_meziyPitchUpHeld)
            {
                _meziyPitchUpHeld = true;
                OnMeziyPitchUp?.Invoke();
            }
            else if (!cHeld && _meziyPitchUpHeld)
            {
                _meziyPitchUpHeld = false;
            }

            if (vHeld && !_meziyPitchDownHeld)
            {
                _meziyPitchDownHeld = true;
                OnMeziyPitchDown?.Invoke();
            }
            else if (!vHeld && _meziyPitchDownHeld)
            {
                _meziyPitchDownHeld = false;
            }

            // MODULE_MEZIY_ROLL
            if (zHeld && !_meziyRollLeftHeld)
            {
                _meziyRollLeftHeld = true;
                OnMeziyRollLeft?.Invoke();
            }
            else if (!zHeld && _meziyRollLeftHeld)
            {
                _meziyRollLeftHeld = false;
            }

            if (xHeld && !_meziyRollRightHeld)
            {
                _meziyRollRightHeld = true;
                OnMeziyRollRight?.Invoke();
            }
            else if (!xHeld && _meziyRollRightHeld)
            {
                _meziyRollRightHeld = false;
            }

            // MODULE_MEZIY_YAW: Shift + A/D
            if (_shiftHeld && aHeld && !_meziyYawLeftHeld)
            {
                _meziyYawLeftHeld = true;
                OnMeziyYawLeft?.Invoke();
            }
            else if (!_shiftHeld || !aHeld)
            {
                _meziyYawLeftHeld = false;
            }

            if (_shiftHeld && Keyboard.current.dKey.isPressed && !_meziyYawRightHeld)
            {
                _meziyYawRightHeld = true;
                OnMeziyYawRight?.Invoke();
            }
            else if (!_shiftHeld || !Keyboard.current.dKey.isPressed)
            {
                _meziyYawRightHeld = false;
            }

            // MODULE_MEZIY_THRUST: Shift + W/S
            if (_shiftHeld && wHeld && !_meziyThrustFwdHeld)
            {
                _meziyThrustFwdHeld = true;
                OnMeziyThrustForward?.Invoke();
            }
            else if (!_shiftHeld || !wHeld)
            {
                _meziyThrustFwdHeld = false;
            }

            if (_shiftHeld && Keyboard.current.sKey.isPressed && !_meziyThrustBwdHeld)
            {
                _meziyThrustBwdHeld = true;
                OnMeziyThrustBackward?.Invoke();
            }
            else if (!_shiftHeld || !Keyboard.current.sKey.isPressed)
            {
                _meziyThrustBwdHeld = false;
            }
        }

        /// <summary>
        /// Проверка: зажата ли клавиша Shift
        /// </summary>
        public bool IsShiftHeld => _shiftHeld;

        /// <summary>
        /// Текущие значения ввода
        /// </summary>
        public float CurrentThrust => _currentThrust;
        public float CurrentYaw => _currentYaw;
        public float CurrentPitch => _currentPitch;
        public float CurrentVertical => _currentVertical;
        public bool IsBoostPressed => _boostPressed;
    }
}