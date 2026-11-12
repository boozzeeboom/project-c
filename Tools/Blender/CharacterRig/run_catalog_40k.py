"""Пакетный запуск rig_character_v2.py по каталогу 40k-моделей.

Читает Tools/Blender/CharacterRig/_work/catalog_pairs.json, пропускает уже
готовые (player_done со свежим rig_report.json status=OK), обрабатывает
остальные по одному через Blender в фоне. Внутри одного вызова скрипта
падение одной модели не останавливает остальные.

Использование:
    python Tools/Blender/CharacterRig/run_catalog_40k.py --start 0 --limit 20

Лог пишется в Tools/Blender/CharacterRig/_work/catalog_40k/batch_log.json.
"""

import argparse
import json
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PLAN = ROOT / "Tools/Blender/CharacterRig/_work/catalog_pairs.json"
SCRIPT = ROOT / "Tools/Blender/CharacterRig/rig_character_v2.py"
WORK_BLEND = ROOT / "Tools/Blender/CharacterRig/_work/adjudicator_arp_detect.blend"
LOG = ROOT / "Tools/Blender/CharacterRig/_work/catalog_40k/batch_log.json"
BLENDER = r"W:/SteamLibrary/steamapps/common/Blender/blender.exe"
TIMEOUT_PER_MODEL = 240  # секунд на одну модель


def load_log():
    if LOG.exists():
        return json.loads(LOG.read_text(encoding="utf-8"))
    return {"entries": []}


def save_log(log):
    LOG.parent.mkdir(parents=True, exist_ok=True)
    LOG.write_text(json.dumps(log, ensure_ascii=False, indent=2), encoding="utf-8")


def already_ok(pair):
    report = Path(pair["player_report"])
    output = Path(pair["player_output"])
    if not (report.exists() and output.exists()):
        return False
    try:
        data = json.loads(report.read_text(encoding="utf-8"))
    except Exception:
        return False
    return data.get("status") == "OK"


def run_one(pair):
    group, name = pair["group"], pair["name"]
    out = Path(pair["player_output"])
    out.parent.mkdir(parents=True, exist_ok=True)
    report = Path(pair["player_report"])
    report.parent.mkdir(parents=True, exist_ok=True)
    renders = ROOT / "Tools/Blender/CharacterRig/_reports/catalog_40k" / group / name
    work_blend = ROOT / "Tools/Blender/CharacterRig/_work/catalog_40k" / group / f"{name}.blend"
    work_blend.parent.mkdir(parents=True, exist_ok=True)

    cmd = [
        BLENDER,
        "--factory-startup",
        "-b",
        str(WORK_BLEND),
        "--python-exit-code",
        "1",
        "-P",
        str(SCRIPT),
        "--",
        "--input",
        pair["player_input"],
        "--out",
        str(out),
        "--report",
        str(report),
        "--renders",
        str(renders),
        "--blend",
        str(work_blend),
    ]
    started = time.time()
    try:
        proc = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=TIMEOUT_PER_MODEL,
            cwd=str(ROOT),
        )
        rc = proc.returncode
        tail = "\n".join((proc.stdout or "").splitlines()[-15:])
        err_tail = "\n".join((proc.stderr or "").splitlines()[-10:])
    except subprocess.TimeoutExpired:
        rc = -9
        tail = "TIMEOUT"
        err_tail = ""

    status = "OK"
    detail = ""
    if rc != 0:
        status = "FAILED"
        detail = f"rc={rc}\nstdout tail:\n{tail}\nstderr tail:\n{err_tail}"
    elif not report.exists():
        status = "FAILED"
        detail = "report missing\n" + tail
    else:
        try:
            data = json.loads(report.read_text(encoding="utf-8"))
            status = data.get("status", "UNKNOWN")
            if status != "OK":
                detail = json.dumps(data, ensure_ascii=False)[:2000]
        except Exception as exc:
            status = "FAILED"
            detail = f"report unreadable: {exc}"

    return {
        "group": group,
        "name": name,
        "status": status,
        "seconds": round(time.time() - started, 1),
        "detail": detail,
        "output": pair["player_output"],
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--start", type=int, default=0)
    parser.add_argument("--limit", type=int, default=20)
    parser.add_argument("--include-failed", action="store_true",
                        help="перезапускать и те, что уже падали (после фиксов кода)")
    parser.add_argument("--force", action="store_true",
                        help="перепрогонять всех, игнорируя существующие отчёты")
    args = parser.parse_args()

    log = load_log()
    done_names = {e["name"] for e in log["entries"] if e["status"] == "OK"}
    failed_names = {e["name"] for e in log["entries"] if e["status"] != "OK"}

    plan = json.loads(PLAN.read_text(encoding="utf-8"))
    pairs = plan["pairs"] if args.force else [p for p in plan["pairs"] if not already_ok(p)]
    if not args.include_failed and not args.force:
        pairs = [p for p in pairs if p["name"] not in failed_names]
    chunk = pairs[args.start : args.start + args.limit]
    print(f"pending={len(pairs)} chunk={len(chunk)} start={args.start} limit={args.limit}")
    if not chunk:
        print("nothing to do")
        return
    for pair in chunk:
        if not args.force and pair["name"] in done_names:
            print(f"SKIP (already OK in log): {pair['name']}")
            continue
        if not args.include_failed and not args.force and pair["name"] in failed_names:
            print(f"SKIP (failed before, needs code fix): {pair['name']}")
            continue
        print(f"RUN {pair['group']}/{pair['name']} ...", flush=True)
        entry = run_one(pair)
        log["entries"] = [e for e in log["entries"] if e["name"] != entry["name"]]
        log["entries"].append(entry)
        save_log(log)
        print(f"  -> {entry['status']} in {entry['seconds']}s", flush=True)
        if entry["status"] != "OK" and entry["detail"]:
            print("  detail:", entry["detail"][:600], flush=True)

    summary = {}
    for e in log["entries"]:
        summary[e["status"]] = summary.get(e["status"], 0) + 1
    print("SUMMARY:", json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    main()
