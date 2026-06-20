// T-DOCK-06: PadTriggerReference — мини-маркер на каждом Pad_* GameObject.
// Для внешних систем: FindObjectsByType<PadTriggerReference>() → найдет collider
// → можно определить nearest pad.
//
// Паттерн: см. ShipRootReference (справочный маркер с кешированием).

using UnityEngine;

namespace ProjectC.Docking.Stations
{
    [DisallowMultipleComponent]
    public class PadTriggerReference : MonoBehaviour
    {
        public DockingPadTriggerBox PadBox { get; private set; }

        private void Awake()
        {
            PadBox = GetComponent<DockingPadTriggerBox>();
            if (PadBox == null)
            {
                Debug.LogError(
                    $"[PadTriggerReference] No DockingPadTriggerBox on {gameObject.name}. " +
                    $"Этот GameObject должен иметь DockingPadTriggerBox.", this);
            }
        }
    }
}
