# -*- coding: utf-8 -*-
"""Численная v2-регрессия на трёх известных исходниках проекта, без записи в Assets.
По умолчанию только измерения (--no-export); --bake включает запись результатов в _work.
"""
from pathlib import Path
import hashlib
import json
import subprocess
import sys
import time
import bpy
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
NAMES=('CL_F_WPN_KNIFE_01_STEEL','CL_F_GADGET_MEZIMETER_01','PC_Char_City_LowerCraftsman_APOSE_v02')
argv=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
bake='--bake' in argv
names=[v for v in argv if v in NAMES] or list(NAMES)
work=HERE/'_work'/('project_v2_'+str(time.time_ns()))
work.mkdir(parents=True)
rows=[]
for name in names:
    src=ROOT/'Assets'/'Generated_Models'/name/(name+'.fbx')
    before=hashlib.sha256(src.read_bytes()).hexdigest()
    report_dir=work/name
    report_dir.mkdir()
    cmd=[bpy.app.binary_path,'-b','--factory-startup','--python-exit-code','1','-P',str(HERE/'rebake_model.py'),'--',
         '--src',str(src),'--out-dir',str(report_dir/'output'),'--report-dir',str(report_dir),'--max-res','4096']
    if not bake:
        cmd.append('--no-export')
    with (report_dir/'run.log').open('w',encoding='utf8') as log:
        code=subprocess.call(cmd,stdout=log,stderr=subprocess.STDOUT)
    paths=list(report_dir.glob('*report.json'))
    report=json.loads(paths[0].read_text(encoding='utf8')) if paths else {}
    row={'name':name,'code':code,'source_unchanged':before==hashlib.sha256(src.read_bytes()).hexdigest(),
         'report':str(paths[0]) if paths else None,'status':report.get('status'),'error':report.get('error'),
         'objects':report.get('objects',[]),'seconds':report.get('total_sec')}
    rows.append(row)
    (HERE/'_reports'/'v2_project_summary.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')
    print('PROJECT_MODEL',name,code,row['status'],flush=True)
if any(r['code']!=0 or not r['source_unchanged'] for r in rows):
    raise RuntimeError('Отклонённые исходники перечислены в v2_project_summary.json')
