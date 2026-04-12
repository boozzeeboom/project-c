using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Тип слота модуля — определяет какие модули можно установить.
    /// </summary>
    public enum SlotType
    {
        Propulsion,   // Слот для модулей движения
        Utility,      // Слот для утилит
        Special       // Слот для специальных модулей
    }

    /// <summary>
    /// ModuleSlot — компонент на корабле представляющий слот для установки модуля.
    /// Слот определяет совместимость типа и валидацию установки.
    /// Сессия 4: Module System Foundation.
    /// </summary>
    public class ModuleSlot : MonoBehaviour
    {
        [Header("Конфигурация слота")]
        [Tooltip("Тип слота — определяет какие модули можно установить")]
        public SlotType slotType;

        [Tooltip("Установленный модуль (null = пусто)")]
        public ShipModule installedModule;

        /// <summary>
        /// Занят ли слот модулем.
        /// </summary>
        public bool isOccupied => installedModule != null;

        /// <summary>
        /// ID установленного модуля (или null).
        /// </summary>
        public string installedModuleId => installedModule != null ? installedModule.moduleId : null;

        /// <summary>
        /// Установить модуль в слот.
        /// </summary>
        /// <param name="module">Модуль для установки</param>
        /// <returns>true если установка успешна, false если несовместим</returns>
        public bool InstallModule(ShipModule module)
        {
            if (module == null)
            {
                Debug.LogWarning("[ModuleSlot] Cannot install null module.");
                return false;
            }

            if (isOccupied)
            {
                Debug.LogWarning($"[ModuleSlot] Slot '{gameObject.name}' is already occupied by '{installedModule.moduleId}'.");
                return false;
            }

            if (!ValidateCompatibility(module))
            {
                Debug.LogWarning($"[ModuleSlot] Module '{module.moduleId}' is not compatible with slot '{gameObject.name}' (type: {slotType}).");
                return false;
            }

            installedModule = module;
            Debug.Log($"[ModuleSlot] Installed module '{module.moduleId}' in slot '{gameObject.name}'.");
            return true;
        }

        /// <summary>
        /// Удалить модуль из слота.
        /// </summary>
        public void RemoveModule()
        {
            if (installedModule != null)
            {
                Debug.Log($"[ModuleSlot] Removed module '{installedModule.moduleId}' from slot '{gameObject.name}'.");
                installedModule = null;
            }
        }

        /// <summary>
        /// Проверить совместимость модуля с этим слотом.
        /// Модуль совместим если его тип совпадает с типом слота.
        /// </summary>
        public bool ValidateCompatibility(ShipModule module)
        {
            if (module == null) return false;

            // Проверяем соответствие типа слота и типа модуля
            return module.type == (ModuleType)slotType;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-валидация: срабатывает при изменении полей в Inspector.
        /// Предотвращает установку несовместимого модуля через drag-and-drop.
        /// </summary>
        private void OnValidate()
        {
            if (installedModule != null && !ValidateCompatibility(installedModule))
            {
                Debug.LogWarning($"[ModuleSlot] Incompatible module '{installedModule.moduleId}' (type: {installedModule.type}) for slot '{gameObject.name}' (type: {slotType}). Clearing.");
                installedModule = null;
            }
        }
#endif
    }
}
