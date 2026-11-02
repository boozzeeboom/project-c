using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.UIv2.Core.UIFramework
{
    public class InputRouter : IInputRouter
    {
        private readonly List<IUILayer> _layers = new List<IUILayer>();

        public void RegisterLayer(IUILayer layer)
        {
            if (layer == null || _layers.Contains(layer)) return;
            _layers.Add(layer);
            _layers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public void UnregisterLayer(IUILayer layer)
        {
            _layers.Remove(layer);
        }

        public bool Dispatch(string action, string phase)
        {
            foreach (var layer in _layers)
            {
                if (layer.ClaimsInput(action))
                {
                    // Placeholder: forward to layer in M1.x
                    return true;
                }
            }
            return false;
        }

        public IUILayer TopClaimant(string action)
        {
            foreach (var layer in _layers)
            {
                if (layer.ClaimsInput(action)) return layer;
            }
            return null;
        }
    }
}
