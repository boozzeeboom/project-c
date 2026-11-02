using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.UIv2.Core.UIFramework
{
    public class UIFrame : IUIFrame
    {
        private readonly Dictionary<string, IUIScreen> _screens = new Dictionary<string, IUIScreen>();
        private readonly ModalStack _modalStack = new ModalStack();
        private readonly InputRouter _inputRouter = new InputRouter();

        public bool IsModalOpen => _modalStack.Peek() != null;
        public IUIScreen CurrentModal => _modalStack.Peek();

        public event Action<string> OnScreenShown;
        public event Action<string> OnScreenHidden;

        public void Initialize()
        {
            Debug.Log("[UIFrame] Initialized (M1 skeleton)");
        }

        public void ShowScreen(string screenId, IScreenProperties props = null)
        {
            if (_screens.TryGetValue(screenId, out var screen))
            {
                screen.SetProperties(props);
                _ = screen.ShowAsync();
                OnScreenShown?.Invoke(screenId);
            }
            else
            {
                Debug.LogWarning($"[UIFrame] Screen not registered: {screenId}");
            }
        }

        public void HideScreen(string screenId)
        {
            if (_screens.TryGetValue(screenId, out var screen))
            {
                _ = screen.HideAsync();
                OnScreenHidden?.Invoke(screenId);
            }
        }

        public void PushModal(string modalId, IScreenProperties props = null)
        {
            if (_screens.TryGetValue(modalId, out var modal))
            {
                modal.SetProperties(props);
                _modalStack.Push(modal);
                _ = modal.ShowAsync();
            }
        }

        public void PopModal()
        {
            var popped = _modalStack.Pop();
            if (popped != null) _ = popped.HideAsync();
        }

        public void ClearModals()
        {
            while (_modalStack.Peek() != null) PopModal();
        }

        public T GetScreen<T>(string screenId) where T : IUIScreen
        {
            if (_screens.TryGetValue(screenId, out var s) && s is T t) return t;
            return default;
        }

        public void RegisterScreen(string id, IUIScreen screen)
        {
            if (screen == null) return;
            _screens[id] = screen;
        }
    }
}
