# -*- coding: utf-8 -*-
"""
T-CHAR-UVRB: пакетный прогон rebake_character.py по папкам персонажей.

  blender.exe -b --factory-startup -P batch_rebake.py -- ^
      --root C:\\Projects\\ProjectC_client\\Assets\\Generated_Models --pattern "PC_Char_*" [--force] ^
      [--exclude "Female_Pilot_Rigged,PadRing"] ^
      [--extra "--res 2048 --gpu"]

Каждый персонаж обрабатывается ОТДЕЛЬНЫМ процессом Blender (изоляция памяти/сбоев).
Модель папки: <папка>/<папка>.fbx|.glb (иначе первый .fbx/.glb в папке).
Результат: <папка>/Rebaked/<имя>_Rebaked.{fbx|glb} + _BaseColor.png;
отчёт и лог — Tools/Blender/CharacterRebake/_reports/<папка>/ (вне Assets).
Сводка: Tools/Blender/CharacterRebake/_uvrb_summary.csv (вне Assets, чтобы Unity её не импортировал).
"""
import sys, os, glob, fnmatch, argparse, subprocess, json, time, shlex, csv

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
p = argparse.ArgumentParser()
p.add_argument("--root", required=True)
p.add_argument("--pattern", default="*", help="glob по имени папки персонажа")
p.add_argument("--exclude", default="", help="через запятую: glob имён папок, которые пропустить")
p.add_argument("--force", action="store_true", help="перепечь, даже если Rebaked уже есть")
p.add_argument("--extra", default="", help="доп. аргументы для rebake_character.py")
p.add_argument("--summary", default="", help="путь к CSV-сводке (по умолчанию <root>/_uvrb_summary.csv)")
A = p.parse_args(argv)

try:
    import bpy
    blender = bpy.app.binary_path
except ImportError:
    blender = os.environ.get("BLENDER_EXE", "blender")
script = os.path.join(os.path.dirname(os.path.abspath(__file__)), "rebake_character.py")
excl = [e.strip() for e in A.exclude.split(",") if e.strip()]

jobs = []
for d in sorted(glob.glob(os.path.join(os.path.abspath(A.root), "*"))):
    base = os.path.basename(d)
    if not os.path.isdir(d) or not fnmatch.fnmatch(base, A.pattern) or any(fnmatch.fnmatch(base, e) for e in excl):
        continue
    model = next((os.path.join(d, base + e) for e in (".fbx", ".glb") if os.path.isfile(os.path.join(d, base + e))), None)
    if model is None:
        c = sorted(glob.glob(os.path.join(d, "*.fbx")) + glob.glob(os.path.join(d, "*.glb")))
        model = c[0] if c else None
    if model is None:
        print(f"[BATCH] skip {base}: no .fbx/.glb")
        continue
    jobs.append((base, model))

rows = []
rep_root = os.path.join(os.path.dirname(script), "_reports")
for i, (base, model) in enumerate(jobs, 1):
    out_dir = os.path.join(os.path.dirname(model), "Rebaked")
    name = os.path.splitext(os.path.basename(model))[0]
    rep_dir = os.path.join(rep_root, base)
    rep = os.path.join(rep_dir, name + "_Rebaked_report.json")
    if os.path.isfile(rep) and not A.force:
        print(f"[BATCH] {i}/{len(jobs)} {base}: already rebaked (use --force)")
    else:
        os.makedirs(out_dir, exist_ok=True)
        os.makedirs(rep_dir, exist_ok=True)
        cmd = [blender, "-b", "--factory-startup", "--python-exit-code", "1", "-P", script, "--",
               "--src", model, "--out-dir", out_dir, "--report-dir", rep_dir] + shlex.split(A.extra, posix=False)
        print(f"[BATCH] {i}/{len(jobs)} {base} ...", flush=True)
        t = time.time()
        with open(os.path.join(rep_dir, name + "_Rebaked_log.txt"), "w", encoding="utf-8", errors="replace") as lf:
            rc = subprocess.call(cmd, stdout=lf, stderr=subprocess.STDOUT)
        print(f"[BATCH]   rc={rc} {time.time() - t:.0f}s", flush=True)
    row = {"character": base, "model": model, "ok": os.path.isfile(rep)}
    if row["ok"]:
        r = json.load(open(rep, encoding="utf-8"))
        for k in ("islands", "seam_length", "uv_coverage", "texel_density_cv", "flipped_faces"):
            row["src_" + k] = r["uv_src"][k]
            row["new_" + k] = r["uv_new"][k]
        row["faces"] = r["mesh"]["faces"]
        row["roundtrip_ok"] = r.get("roundtrip", {}).get("ok")
        row["non_disk_charts_left"] = r.get("non_disk_charts_left")
        row["total_sec"] = r["total_sec"]
    rows.append(row)

summary = A.summary or os.path.join(os.path.dirname(script), "_uvrb_summary.csv")
keys = sorted({k for r in rows for k in r}, key=lambda k: (k not in ("character", "ok"), k))
with open(summary, "w", newline="", encoding="utf-8") as f:
    w = csv.DictWriter(f, fieldnames=keys)
    w.writeheader()
    w.writerows(rows)
print(f"[BATCH] done: {sum(r['ok'] for r in rows)}/{len(rows)} ok -> {summary}")
