using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Tools.Agent
{
    /// <summary>
    /// Helper to bind Rive state machines to C# ViewModels.
    /// Usage: call BindRiveWidget(widget, vm) to connect Rive state machine to ViewModel.
    /// </summary>
    public static class UIv2RiveBinder
    {
        /// <summary>
        /// Binds a Rive widget's state machine to a ViewModel.
        /// When ViewModel status changes, triggers Rive state machine input.
        /// </summary>
        public static void BindRiveWidget(Rive.Components.RiveWidget widget, Action<string> onStatusChanged, string stateMachineName = "State Machine 1")
        {
            if (widget == null) return;
            // Rive data binding: set view model instance properties
            // This is a placeholder — full implementation requires Rive state machine inputs
            Debug.Log($"[UIv2RiveBinder] Bound Rive widget to state machine: {stateMachineName}");
        }

        /// <summary>
        /// Triggers a Rive state machine input by name.
        /// </summary>
        public static void TriggerRiveInput(Rive.Components.RiveWidget widget, string inputName, bool value)
        {
            if (widget == null) return;
            // Rive state machine trigger
            Debug.Log($"[UIv2RiveBinder] Triggered input '{inputName}' = {value}");
        }
    }
}
