using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Core.Performance
{
    public class UIPool<T> : ProjectC.UIv2.Core.UIFramework.IUIPool<T> where T : VisualElement
    {
        private readonly Func<T> _factory;
        private readonly Stack<T> _pool = new Stack<T>();
        private int _active;

        public UIPool(Func<T> factory, int prewarm = 0)
        {
            _factory = factory;
            Prewarm(prewarm);
        }

        public int ActiveCount => _active;
        public int PooledCount => _pool.Count;

        public T Get()
        {
            T instance = _pool.Count > 0 ? _pool.Pop() : _factory();
            _active++;
            return instance;
        }

        public void Release(T instance)
        {
            if (instance == null) return;
            instance.style.display = DisplayStyle.None;
            _pool.Push(instance);
            _active = Mathf.Max(0, _active - 1);
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++) _pool.Push(_factory());
        }
    }
}
