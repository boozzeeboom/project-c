# -*- coding: utf-8 -*-
"""T-CHAR-UVRB v2: один процесс Blender на модель, строгие exit codes и версия отчёта.
Кэш проверяет worker после импорта (включая внешние текстуры), не наличие старого JSON.
"""
import argparse
import csv
import fnmatch
import json
import os
from pathlib import Path
import shlex
import subprocess
import sys
import time

HERE = Path(__file__).resolve().parent


def split_extra(text):
    # posix=False сохраняет Windows backslashes, но оставляет внешние кавычки.
    return [v[1:-1] if len(v) >= 2 and v[0] == v[-1] and v[0] in '\"\'' else v
            for v in shlex.split(text, posix=False)]


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root', required=True)
    p.add_argument('--pattern', required=True, help='Явный glob; нет неявного запуска всей библиотеки')
    p.add_argument('--exclude', default='')
    p.add_argument('--force', action='store_true')
    p.add_argument('--extra', default='')
    p.add_argument('--summary', default=str(HERE/'_uvrb_summary_v2.csv'))
    a = p.parse_args(argv)
    extra = split_extra(a.extra)
    # Batch владеет маршрутизацией: не разрешаем extra перезаписать src/out/report/suffix.
    reserved = {'--src','--out-dir','--report-dir','--suffix','--audit-only','--no-export'}
    if any(v.split('=')[0] in reserved for v in extra):
        p.error('extra содержит маршрутизацию/diagnostic-флаги; используйте rebake_model.py напрямую')
    summary = Path(a.summary).resolve()
    assets = HERE.parents[2]/'Assets'
    if summary == assets or assets in summary.parents:
        p.error('CSV не должен находиться в Assets')
    try:
        import bpy
        blender = bpy.app.binary_path
    except ImportError:
        blender = os.environ.get('BLENDER_EXE', 'blender')
    excludes = [v.strip() for v in a.exclude.split(',') if v.strip()]
    rows = []
    for folder in sorted(Path(a.root).resolve().iterdir()):
        if not folder.is_dir() or not fnmatch.fnmatch(folder.name, a.pattern) or any(fnmatch.fnmatch(folder.name, e) for e in excludes):
            continue
        extensions = ('.fbx','.glb','.gltf','.blend','.obj','.ply','.stl')
        model = next((folder/(folder.name+e) for e in extensions if (folder/(folder.name+e)).is_file()), None)
        if model is None:
            choices = sorted(p for p in folder.iterdir() if p.suffix.lower() in extensions)
            if len(choices) == 1:
                model = choices[0]
            elif choices:
                rows.append({'model': str(folder), 'ok': False, 'error': 'Неоднозначно: несколько исходных моделей'})
                continue
            else:
                continue
        rep_dir = HERE/'_reports'/folder.name
        rep_dir.mkdir(parents=True, exist_ok=True)
        rep_path = rep_dir/(model.stem+'_Rebaked_v2_report.json')
        cmd = [blender,'-b','--factory-startup','--python-exit-code','1','-P',str(HERE/'rebake_model.py'),'--',
               '--src',str(model),'--out-dir',str(folder/'Rebaked_v2'),'--report-dir',str(rep_dir)] + extra
        if a.force:
            cmd.append('--force')
        start = time.time_ns()
        with open(rep_dir/(model.stem+'_Rebaked_v2_log.txt'), 'w', encoding='utf8') as log:
            rc = subprocess.call(cmd, stdout=log, stderr=subprocess.STDOUT)
        row = {'model':str(model), 'returncode':rc, 'ok':False}
        try:
            report = json.loads(rep_path.read_text(encoding='utf8'))
            fresh = rep_path.stat().st_mtime_ns >= start
            row['ok'] = bool(rc == 0 and fresh and report.get('pipeline_version') == '2.0.0'
                             and report.get('status') == 'success' and report.get('native_roundtrip',{}).get('ok')
                             and report.get('outputs') and all(Path(v).is_file() for v in report['outputs']))
            row.update(status=report.get('status'), rebaked=sum(o.get('status')=='rebaked' for o in report.get('objects',[])),
                       unchanged=sum(o.get('status')=='unchanged' for o in report.get('objects',[])),
                       seconds=report.get('total_sec'), error=report.get('error',''))
        except (OSError, ValueError) as exc:
            row['error'] = str(exc)
        rows.append(row)
        print('[BATCH]', model.name, row['ok'], flush=True)
    summary.parent.mkdir(parents=True, exist_ok=True)
    fields = sorted({k for r in rows for k in r}) or ['model','ok']
    with summary.open('w', encoding='utf8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(rows)
    if not rows:
        raise RuntimeError('Нет моделей по заданному pattern')
    if not all(r['ok'] for r in rows):
        raise RuntimeError('Batch содержит ошибки; см. CSV и per-model report')


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sys.argv[1:]
    main(argv)
