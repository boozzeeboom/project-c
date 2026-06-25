# Combat Test Helpers — runtime скрипты для execute_code

> **Сценарий:** ты в Play Mode, нужно дать host player'у WoodenSword и экипировать для теста T-CB03.
> **Как использовать:** `tools/call execute_code` с `action=execute` и `code=...` (скопируй ниже).

---

## Helper 1: Give WoodenSword + Equip WeaponMain (для теста T-CB03)

**Когда:** сразу после `StartHost` в Play Mode, **до** нажатия K.

```csharp
// 1. Find WoodenSword
var sword = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Equipment.WeaponItemData>("Assets/_Project/Resources/Items/Weapons/Weapon_WoodenSword.asset");
if (sword == null) return "ERROR: WoodenSword asset not found";

// 2. Get host clientId (server's own clientId = 0)
ulong hostId = 0;

// 3. Get or create itemId in InventoryWorld
var inv = ProjectC.Items.InventoryWorld.Instance;
if (inv == null) return "ERROR: InventoryWorld.Instance==null. Wait for server init.";
// Use GetOrRegisterItemId (auto-registers)
int swordId = inv.GetOrRegisterItemId(sword);
if (swordId <= 0) return $"ERROR: GetOrRegisterItemId returned {swordId}";

// 4. AddItemDirect (server-side bypass; bypasses TryPickup rate limit + range)
var addMethod = typeof(ProjectC.Items.InventoryWorld).GetMethod("AddItemDirect",
    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
if (addMethod == null) return "ERROR: AddItemDirect method not found";
addMethod.Invoke(inv, new object[] { hostId, swordId, ProjectC.Items.ItemType.Equipment, 1 });

// 5. Equip via EquipmentServer.TryEquip
var equipServer = Unity.Netcode.NetworkManager.Singleton?.GetComponent<ProjectC.Equipment.EquipmentServer>();
if (equipServer == null) {
    // Fallback: directly use EquipmentWorld
    var equipWorld = ProjectC.Equipment.EquipmentWorld.Instance;
    if (equipWorld == null) return "ERROR: no EquipmentServer and no EquipmentWorld";
    string reason;
    bool ok = equipWorld.TryEquip(hostId, swordId, ProjectC.Equipment.EquipSlot.WeaponMain, out reason);
    return $"ok={ok}, reason='{reason}', swordId={swordId}";
}
string reason2;
bool ok2 = equipServer.TryEquipRpc != null ? false : false;  // server side only
// Use direct call on EquipmentWorld
var equipWorld2 = ProjectC.Equipment.EquipmentWorld.Instance;
string reason3;
bool ok3 = equipWorld2.TryEquip(hostId, swordId, ProjectC.Equipment.EquipSlot.WeaponMain, out reason3);
return $"Equip result: ok={ok3}, reason='{reason3}', swordId={swordId}, displayName={sword.itemName}";
```

**Ожидаемый ответ:** `Equip result: ok=True, reason='', swordId=<N>, displayName=Деревянный меч`

**Если `ok=False`:** проверь `reason` — обычно "Слот занят" или "Предмета нет в инвентаре".

---

## Helper 2: Show current equipment + attack sources

**Когда:** после экипировки WoodenSword, для verify что PlayerAttacker увидел.

```csharp
var sb = new System.Text.StringBuilder();
ulong hostId = 0;
var pa = ProjectC.Combat.CombatServer.Instance?.GetType()
    .GetField("_attackers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
    ?.GetValue(ProjectC.Combat.CombatServer.Instance) 
    as System.Collections.Generic.Dictionary<ulong, ProjectC.Combat.Core.IAttacker>;
if (pa != null && pa.TryGetValue(hostId, out var attacker)) {
    sb.AppendLine($"Player attacker: {attacker.GetType().Name}");
    var sources = attacker.GetActiveDamageSources();
    sb.AppendLine($"  Active sources: {sources.Count}");
    foreach (var s in sources) {
        sb.AppendLine($"    {s.GetType().Name}: id={s.GetSourceId()}, name={s.GetDisplayName()}, dice={s.GetDamageDice()}, base={s.GetBaseDamage()}, critMod={s.GetCritModifier()}, range={s.GetRange()}, type={s.GetDamageType()}");
    }
} else {
    sb.AppendLine("Player NOT registered in CombatServer (Instance==null or _attackers empty)");
}
return sb.ToString();
```

**Ожидаемый ответ:** `WeaponDamageSource: id=<N>, name=Деревянный меч, dice=d8, base=2, critMod=0, range=2, type=Physical`

---

## Helper 3: Quick K-attack without K-key (для теста без клавиатуры)

```csharp
// Find NetworkPlayer and call DebugAttackNearestNpc via reflection
var players = UnityEngine.Object.FindObjectsByType<ProjectC.Player.NetworkPlayer>(UnityEngine.FindObjectsSortMode.None);
if (players.Length == 0) return "NO PLAYER";
var p = players[0];
var method = typeof(ProjectC.Player.NetworkPlayer).GetMethod("DebugAttackNearestNpc",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
if (method == null) return "METHOD NOT FOUND";
method.Invoke(p, null);
return "OK: DebugAttackNearestNpc called";
```

---

## Helper 4: Give all 4 weapons + cycle equips (для теста всех 4)

```csharp
ulong hostId = 0;
var inv = ProjectC.Items.InventoryWorld.Instance;
var equipWorld = ProjectC.Equipment.EquipmentWorld.Instance;
if (inv == null || equipWorld == null) return "ERROR: worlds not init";

string[] names = { "Weapon_WoodenSword", "Weapon_IronDagger", "Weapon_IronSpear", "Weapon_AntigravBlade" };
var sb = new System.Text.StringBuilder();
foreach (var n in names) {
    var w = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectC.Equipment.WeaponItemData>($"Assets/_Project/Resources/Items/Weapons/{n}.asset");
    if (w == null) { sb.AppendLine($"  SKIP: {n} not found"); continue; }
    int id = inv.GetOrRegisterItemId(w);
    inv.GetType().GetMethod("AddItemDirect").Invoke(inv, new object[] { hostId, id, ProjectC.Items.ItemType.Equipment, 1 });
    string reason;
    bool ok = equipWorld.TryEquip(hostId, id, ProjectC.Equipment.EquipSlot.WeaponMain, out reason);
    sb.AppendLine($"  {n}: id={id}, equip ok={ok}, reason='{reason}'");
}
return sb.ToString();
```

---

## Helper 5: Reset player equipment (если запутался)

```csharp
ulong hostId = 0;
var inv = ProjectC.Items.InventoryWorld.Instance;
var equipWorld = ProjectC.Equipment.EquipmentWorld.Instance;
if (inv == null || equipWorld == null) return "ERROR";

// Unequip all slots
foreach (ProjectC.Equipment.EquipSlot slot in System.Enum.GetValues(typeof(ProjectC.Equipment.EquipSlot))) {
    if (slot == ProjectC.Equipment.EquipSlot.None) continue;
    string reason;
    equipWorld.TryUnequip(hostId, slot, out reason);
}

// Clear inventory (для всех 4 weapons + their default items)
var invData = inv.GetOrCreate(hostId);
foreach (var itemIds in new[] { invData.Resources, invData.Food, invData.Fuel, invData.Antigrav, invData.Mezium, invData.Medical, invData.Tech, invData.Equipment, invData.Key }) {
    itemIds?.Clear();
}

return "RESET: all slots unequipped, inventory cleared";
```

---

## Flow для verify T-CB03

1. **Press Play** → **Start Host** → жди 1 сек.
2. **Helper 1** → получаешь WoodenSword в WeaponMain.
3. **Helper 2** → проверяешь что PlayerAttacker видит `WeaponDamageSource: name=Деревянный меч, dice=d8, base=2`.
4. **Helper 3** (или K) → атакуешь NPC.
5. **Damage log** должен показать:
   - До T-CB03 (unarmed): `baseAttack=15, isHit=True, final=15` (d6, base=1, no weapon).
   - **После T-CB03 (WoodenSword)**: `baseAttack=23` (d8=avg 4.5, но rolled min=1/max=8, real=4.5 + 2 + 10=16.5, rolled 4+2+10=16 — но min roll d8=1, max=8, реальный будет [13..20]. Бывший unarmed был d6, base=1, avg 3.5+1+10=14.5, real [14..17]). **Урон вырастет на ~2-3 пункта**.
6. Если урон вырос — T-CB03 работает ✓.

---

## Что делать если Helper 1 вернул "no EquipmentServer" или "TryEquip ok=False"

См. `60_NEXT_STEPS_T-CB01.md §3` и `Battle/30_PITFALLS §R2`. Возможные причины:
- WeaponItemData не зарегистрирован в `_itemDatabase` (нет в `Resources/Items/Weapons/`).
- EquipmentServer не спавнился (в `BootstrapScene`).
- TryEquip в EquipmentWorld требует, чтобы предмет был в инвентаре — сначала AddItem, потом TryEquip.

---

## История

- 2026-06-25: created for T-CB03 testing.
