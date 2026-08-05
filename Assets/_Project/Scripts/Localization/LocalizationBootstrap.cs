// Project C: Localization Bootstrap (LOC-01)
// Initializes locale before first UI. Attach to BootstrapScene GameObject.
using UnityEngine;

namespace ProjectC.Localization
{
    /// <summary>
    /// Bootstrap: loads saved locale before UIManager (ExecutionOrder -200).
    /// Attach to a GameObject in BootstrapScene.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public class LocalizationBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[LocalizationBootstrap] Initializing locale...");
            LocaleSelector.LoadSaved();
        }
    }
}
