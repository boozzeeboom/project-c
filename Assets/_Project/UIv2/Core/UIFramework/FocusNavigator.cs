using System;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Core.UIFramework
{
    public class FocusNavigator : IFocusNavigator
    {
        private VisualElement _scope;
        private VisualElement _current;
        private VisualElement _default;

        public VisualElement Current => _current;
        public event Action<VisualElement> FocusChanged;

        public void SetScope(VisualElement scope) { _scope = scope; }
        public void SetDefault(VisualElement target) { _default = target; if (_current == null) { _current = target; FocusChanged?.Invoke(_current); } }

        public void MoveNext() { /* M1.x: implement tab order */ }
        public void MovePrev() { /* M1.x: implement tab order */ }
        public void MoveDirection(FocusDirection dir) { /* M1.x: implement geometric nav */ }
    }
}
