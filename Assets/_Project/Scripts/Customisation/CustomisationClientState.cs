// Project C: Character Customisation — T-CUS-02 + T-CUS-04
// CustomisationClientState: client-side projection + facade для UI/Applier.
// Pattern: copy EquipmentClientState (T-P10) — singleton + event + manual apply.
// Variant A (client-only persistence): данные хранятся локально в CharacterSaveData.
// CustomisationClientState работает как in-memory кэш + точка подписки для UI/Applier.

using System;
using ProjectC.Customisation.Dto;
using UnityEngine;

namespace ProjectC.Customisation
{
    /// <summary>
    /// Singleton-state для customisation. Подписка через OnCustomisationUpdated event.
    /// </summary>
    /// <remarks>
    /// Variant A (client-only persistence, MVP):
    ///   - Snapshot обновляется через ApplyCustomisationSnapshot(snapshot) — вызывает UI после
    ///     изменения (toggle/slider/color-picker → save → build snapshot → invoke event).
    ///   - Никакого RPC — сервер не знает о кастомизации.
    ///   - Persistence через JsonCharacterDataRepository (CharacterSaveData.customisation).
    ///
    /// Variant B (server-replicated, будущее): T-CUS-08 добавит RPC + NetworkVariable.
    /// </remarks>
    public class CustomisationClientState : MonoBehaviour
    {
        public static CustomisationClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        // === State ===
        public CustomisationSnapshotDto CurrentSnapshot { get; private set; }

        // === Events ===
        /// <summary>Snapshot обновился (UI toggle / slider / save). UI и CharacterCustomisationApplier подписываются.</summary>
        public event Action<CustomisationSnapshotDto> OnCustomisationUpdated;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Применить новый snapshot (UI → state). Fire'ит OnCustomisationUpdated.
        /// </summary>
        public void ApplyCustomisationSnapshot(CustomisationSnapshotDto snapshot)
        {
            CurrentSnapshot = snapshot;
            OnCustomisationUpdated?.Invoke(snapshot);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[CustomisationClientState] Snapshot: body={snapshot.bodyType}, " +
                          $"h={snapshot.heightScale:F2}, w={snapshot.widthScale:F2}, " +
                          $"hair={snapshot.hairStyle}, " +
                          $"clothingOverrides={(snapshot.clothingOverrides == null ? 0 : snapshot.clothingOverrides.Length)}");
            }
        }

        /// <summary>Сброс state (для тестов / scene reload).</summary>
        public void ClearState() => CurrentSnapshot = default;
    }
}