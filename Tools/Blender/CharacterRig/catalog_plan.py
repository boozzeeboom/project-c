import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PLAYER_ROOT = ROOT / "Assets/Generated_Models_Retopo/01_Characters"
NPC_ROOT = ROOT / "Assets/Generated_Models_Retopo_NPC/01_Characters"
PLAYER_OUT = ROOT / "Assets/Generated_Models_Rigged/01_Characters"
NPC_OUT = ROOT / "Assets/Generated_Models_Rigged_NPC/01_Characters"
REPORTS = ROOT / "Tools/Blender/CharacterRig/_reports"


def collect(root: Path, suffix: str):
    models = {}
    for path in sorted(root.rglob(f"*{suffix}.fbx")):
        rel = path.relative_to(root)
        group = rel.parts[0]
        name = rel.parts[1]
        models[(group, name)] = path
    return models


def source_texture(model: Path):
    for candidate in sorted(model.parent.glob("*BaseColor*.png")):
        return candidate
    return None


def main():
    players = collect(PLAYER_ROOT, "_Retopo")
    npcs = collect(NPC_ROOT, "_RetopoNPC")
    player_keys = set(players)
    npc_keys = set(npcs)
    pairs = []

    for key in sorted(player_keys & npc_keys):
        group, name = key
        player = players[key]
        npc = npcs[key]
        pair = {
            "group": group,
            "name": name,
            "player_input": str(player),
            "npc_input": str(npc),
            "player_texture": str(source_texture(player)) if source_texture(player) else None,
            "npc_texture": str(source_texture(npc)) if source_texture(npc) else None,
            "player_output": str(PLAYER_OUT / group / name / f"{name}_Rigged_Humanoid.fbx"),
            "npc_output": str(NPC_OUT / group / name / f"{name}_Rigged_NPC_Humanoid.fbx"),
            "player_report": str(REPORTS / "catalog_40k" / group / name / "rig_report.json"),
            "npc_report": str(REPORTS / "catalog_npc" / group / name / "transfer_report.json"),
            "player_done": (PLAYER_OUT / group / name / f"{name}_Rigged_Humanoid.fbx").exists(),
            "npc_done": (NPC_OUT / group / name / f"{name}_Rigged_NPC_Humanoid.fbx").exists(),
            "status": "PAIRED",
        }
        if pair["player_done"]:
            pair["status"] = "PLAYER_DONE" if pair["npc_done"] else "PLAYER_DONE_NPC_PENDING"
        pairs.append(pair)

    result = {
        "counts": {
            "player_models": len(players),
            "npc_models": len(npcs),
            "paired": len(player_keys & npc_keys),
            "player_only": len(player_keys - npc_keys),
            "npc_only": len(npc_keys - player_keys),
            "player_done": sum(1 for item in pairs if item["player_done"]),
            "npc_done": sum(1 for item in pairs if item["npc_done"]),
        },
        "player_only": sorted(["/".join(key) for key in player_keys - npc_keys]),
        "npc_only": sorted(["/".join(key) for key in npc_keys - player_keys]),
        "pairs": pairs,
    }
    output = ROOT / "Tools/Blender/CharacterRig/_work/catalog_pairs.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result["counts"], ensure_ascii=False, indent=2))
    if result["player_only"]:
        print("player_only:", "; ".join(result["player_only"]))
    if result["npc_only"]:
        print("npc_only:", "; ".join(result["npc_only"]))
    print(output)


if __name__ == "__main__":
    main()
