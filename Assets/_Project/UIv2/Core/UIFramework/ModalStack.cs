using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Core.UIFramework
{
    public class ModalStack : IModalStack
    {
        private readonly List<IUIScreen> _stack = new List<IUIScreen>();

        public IReadOnlyList<IUIScreen> Stack => _stack;
        public event Action<IUIScreen> Pushed;
        public event Action<IUIScreen> Popped;

        public void Push(IUIScreen modal)
        {
            if (modal == null) return;
            _stack.Add(modal);
            Pushed?.Invoke(modal);
        }

        public IUIScreen Pop()
        {
            if (_stack.Count == 0) return null;
            var top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            Popped?.Invoke(top);
            return top;
        }

        public IUIScreen Peek()
        {
            if (_stack.Count == 0) return null;
            return _stack[_stack.Count - 1];
        }
    }
}
