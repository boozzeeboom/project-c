# -*- coding: utf-8 -*-
"""T-CHAR-UVRB: реальные subprocess-проверки batch на fixture вне Assets.
Требует успешный test_uvrb_v2.py. Третья команда намеренно ошибочна: старый success не должен засчитаться.
"""
from pathlib import Path
import csv
import json
import subprocess
import sys
import bpy

HERE=Path(__file__).resolve().parent
fixture=json.loads((HERE/'_reports'/'v2_test_summary.json').read_text(encoding='utf8'))
if not fixture['ok']:
    raise RuntimeError('Сначала должны пройти геометрические fixtures')
root=Path(fixture['root'])
summary=root/'batch_summary.csv'
base=[bpy.app.binary_path,'-b','--factory-startup','--python-exit-code','1','-P',str(HERE/'batch_rebake.py'),'--',
      '--root',str(root),'--pattern','split_connectivity','--summary',str(summary)]
results=[]
for name,extra,force,expected in [('first','--res 64 --supersample 1 --samples 1',True,True),
                                 ('cached','--res 64 --supersample 1 --samples 1',False,True),
                                 ('reject_stale','--res -1',True,False)]:
    cmd=base+['--extra',extra]+(['--force'] if force else [])
    with (root/('batch_'+name+'.log')).open('w',encoding='utf8') as log:
        code=subprocess.call(cmd,stdout=log,stderr=subprocess.STDOUT)
    with summary.open(encoding='utf8') as f:
        rows=list(csv.DictReader(f))
    actual=code==0 and len(rows)==1 and rows[0]['ok']=='True'
    assert actual==expected,(name,code,rows)
    if name=='cached':
        report=json.loads((HERE/'_reports'/'split_connectivity'/'split_connectivity_Rebaked_v2_report.json').read_text(encoding='utf8'))
        assert report.get('cache_hit'),report
    if not expected:
        assert code!=0 and rows[0]['ok']=='False'
    results.append({'name':name,'ok':True,'exit_code':code,'expected_success':expected})
(HERE/'_reports'/'v2_batch_summary.json').write_text(json.dumps(results,indent=2),encoding='utf8')
print('BATCH_TESTS_PASS',len(results))
