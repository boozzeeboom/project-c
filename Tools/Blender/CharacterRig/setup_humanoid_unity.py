"""Настройка Humanoid-аватара для готовых ригованных FBX через anklebreaker MCP.

Читает _work/catalog_pairs.json, чанками вызывает unity_execute_code:
ModelImporter -> Human + CreateFromThisModel + reimport, затем проверка
Avatar.isValid/isHuman. Результаты пишет в _work/unity_humanoid_log.json.

Использование:
    python Tools/Blender/CharacterRig/setup_humanoid_unity.py --which player --start 0 --limit 10
"""

import argparse
import json
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
RUN_ENV = r"C:/Users/leon7/AppData/Roaming/kimi-desktop/daimon-share/daimon/runtime/kimi-code/home/plugins/managed/unity/skills/unity-game-from-scratch/scripts/run-env.py"
CALLER = ROOT / "Tools/UnityMcp/call_anklebreaker.py"
PLAN = ROOT / "Tools/Blender/CharacterRig/_work/catalog_pairs.json"
LOG = ROOT / "Tools/Blender/CharacterRig/_work/unity_humanoid_log.json"
PORT = 7890


def mcp(tool, payload):
    payload = dict(payload)
    payload["port"] = PORT
    proc = subprocess.run(
        [sys.executable, "-I", RUN_ENV, "python", "-I", str(CALLER), tool, json.dumps(payload)],
        capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=240,
    )
    out = proc.stdout or ""
    try:
        return json.loads(out[out.index("{"):])
    except Exception:
        return {"raw": out[-2000:], "stderr": (proc.stderr or "")[-1000:]}


def setup_chunk(paths):
    literal = ",\n".join('"%s"' % p.replace("\\", "/") for p in paths)
    code = """
UnityEditor.AssetDatabase.Refresh();
var paths = new string[] {
%s
};
int ok = 0; var bad = new System.Text.StringBuilder();
foreach (var p in paths) {
    var imp = UnityEditor.AssetImporter.GetAtPath(p) as UnityEditor.ModelImporter;
    if (imp == null) { bad.AppendLine("NOIMP " + p); continue; }
    imp.animationType = UnityEditor.ModelImporterAnimationType.Human;
    imp.avatarSetup = UnityEditor.ModelImporterAvatarSetup.CreateFromThisModel;
    imp.importAnimation = false;
    imp.SaveAndReimport();
    UnityEngine.Avatar avatar = null;
    foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(p)) {
        if (a is UnityEngine.Avatar av) { avatar = av; break; }
    }
    if (avatar == null) { bad.AppendLine("NOAVATAR " + p); continue; }
    if (!avatar.isValid) { bad.AppendLine("INVALID " + p); continue; }
    if (!avatar.isHuman) { bad.AppendLine("NOTHUMAN " + p); continue; }
    ok++;
}
UnityEngine.Debug.Log("[RIG_IMPORT] chunk done ok=" + ok + " issues=" + bad.Length + "\\n" + bad.ToString());
""" % literal
    return mcp("unity_execute_code", {"code": code})


def console_errors():
    res = mcp("unity_console_log", {"type": "error", "count": 30, "includeStackTrace": "none"})
    text = res.get("raw") or json.dumps(res, ensure_ascii=False)
    return text


def console_info_marker():
    res = mcp("unity_console_log", {"type": "info", "count": 15, "includeStackTrace": "none"})
    text = res.get("raw") or json.dumps(res, ensure_ascii=False)
    return text


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--which", choices=("player", "npc"), default="player")
    parser.add_argument("--start", type=int, default=0)
    parser.add_argument("--limit", type=int, default=10)
    args = parser.parse_args()

    plan = json.loads(PLAN.read_text(encoding="utf-8"))
    key = "player_output" if args.which == "player" else "npc_output"
    paths = [p[key].replace("\\", "/").split("ProjectC_client/", 1)[-1]
             for p in plan["pairs"]]
    chunk = paths[args.start : args.start + args.limit]
    print(f"chunk={len(chunk)} start={args.start}")
    if not chunk:
        print("nothing")
        return

    log = json.loads(LOG.read_text(encoding="utf-8")) if LOG.exists() else {"chunks": []}
    t0 = time.time()
    result = setup_chunk(chunk)
    entry = {"which": args.which, "start": args.start, "paths": chunk,
             "seconds": round(time.time() - t0, 1),
             "result": result}
    errors = console_errors()
    entry["console_errors"] = errors[-3000:]
    marker = console_info_marker()
    entry["marker"] = marker[-1500:]
    log["chunks"].append(entry)
    LOG.parent.mkdir(parents=True, exist_ok=True)
    LOG.write_text(json.dumps(log, ensure_ascii=False, indent=1), encoding="utf-8")
    import re
    m = re.findall(r"chunk done ok=(\d+) issues=(\d+)", marker)
    print("marker:", m[-1] if m else "NOT FOUND")
    print("console errors:", "none" if '"count":0' in errors.replace(" ", "") else errors[:400])


if __name__ == "__main__":
    main()
