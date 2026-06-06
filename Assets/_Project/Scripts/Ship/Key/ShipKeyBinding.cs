// =====================================================================================
// ShipKeyBinding.cs — DEPRECATED АЛИАС → ProjectC.MetaRequirement.MetaRequirement
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md
//
// Этот файл сохранён для backward-compat со сценами и префабами, которые ссылаются
// на этот класс через .meta-GUID. НЕ УДАЛЯТЬ пока в проекте есть хоть одна ссылка
// на ShipKeyBinding в .unity/.prefab файлах.
//
// Grep перед удалением:
//   grep -r "ShipKeyBinding\|ShipKeyServer\|ShipKeyClientState\|ShipKeyToast" Assets/
//   --include="*.cs" --include="*.unity" --include="*.prefab"
//
// Через 1-2 релиз-цикла: переименовать сцены/префабы на MetaRequirement* и удалить файл.
// =====================================================================================

using System;

namespace ProjectC.Ship.Key
{
    /// <summary>
    /// DEPRECATED: устаревший алиас. Используйте ProjectC.MetaRequirement.MetaRequirement.
    /// Ship Key Subsystem мигрировал на обобщённую MetaRequirement подсистему.
    /// </summary>
    [Obsolete("Use ProjectC.MetaRequirement.MetaRequirement. ShipKeyBinding kept as alias for backward compat with existing scenes.")]
    public class ShipKeyBinding : ProjectC.MetaRequirement.MetaRequirement
    {
    }
}
