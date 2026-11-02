using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.UIElements;
using ProjectC.UIv2.Core.Theming;

namespace ProjectC.UIv2.Core.UIFramework
{
    public interface IScreenProperties { }

    public interface IUIScreen
    {
        string ScreenId { get; }
        bool IsVisible { get; }
        IScreenProperties Properties { get; }
        void SetProperties(IScreenProperties props);
        System.Threading.Tasks.Task ShowAsync(CancellationToken ct = default);
        System.Threading.Tasks.Task HideAsync(CancellationToken ct = default);
        void OnFocusGained();
        void OnFocusLost();
    }

    public interface IUILayer
    {
        string LayerId { get; }
        int Priority { get; }
        void Mount(IUIScreen screen);
        void Unmount(IUIScreen screen);
        void SetInputEnabled(bool enabled);
        bool ClaimsInput(string action);
    }

    public interface IModalStack
    {
        void Push(IUIScreen modal);
        IUIScreen Pop();
        IUIScreen Peek();
        IReadOnlyList<IUIScreen> Stack { get; }
        event Action<IUIScreen> Pushed;
        event Action<IUIScreen> Popped;
    }

    public enum FocusDirection { Up, Down, Left, Right, Next, Prev }

    public interface IFocusNavigator
    {
        void SetScope(VisualElement scope);
        void MoveNext();
        void MovePrev();
        void MoveDirection(FocusDirection dir);
        VisualElement Current { get; }
        void SetDefault(VisualElement target);
        event Action<VisualElement> FocusChanged;
    }

    public interface IInputRouter
    {
        void RegisterLayer(IUILayer layer);
        void UnregisterLayer(IUILayer layer);
        bool Dispatch(string action, string phase);
        IUILayer TopClaimant(string action);
    }

    public interface IThemeService
    {
        ThemeData Current { get; }
        void Apply(ThemeData theme, VisualElement root);
        void Apply(string themeId, VisualElement root);
        event Action<ThemeData> Changed;
    }

    public interface ILocalizationService
    {
        string CurrentLocale { get; }
        void SetLocale(string locale);
        string Get(string table, string key);
        event Action<string> LocaleChanged;
    }

    public interface IUIPool<T> where T : VisualElement
    {
        T Get();
        void Release(T instance);
        void Prewarm(int count);
        int ActiveCount { get; }
        int PooledCount { get; }
    }

    public interface ITransition
    {
        System.Threading.Tasks.Task PlayInAsync(VisualElement target, CancellationToken ct = default);
        System.Threading.Tasks.Task PlayOutAsync(VisualElement target, CancellationToken ct = default);
    }

    public interface IUIFrame
    {
        void Initialize();
        void ShowScreen(string screenId, IScreenProperties props = null);
        void HideScreen(string screenId);
        void PushModal(string modalId, IScreenProperties props = null);
        void PopModal();
        void ClearModals();
        bool IsModalOpen { get; }
        IUIScreen CurrentModal { get; }
        T GetScreen<T>(string screenId) where T : IUIScreen;
        event Action<string> OnScreenShown;
        event Action<string> OnScreenHidden;
    }
}
