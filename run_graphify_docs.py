import sys; sys.stdout.reconfigure(encoding='utf-8')
import json
from pathlib import Path

from graphify.detect import detect
from graphify.build import build_from_json
from graphify.cluster import cluster, score_all
from graphify.analyze import god_nodes, surprising_connections, suggest_questions
from graphify.report import generate
from graphify.export import to_json

target = Path('docs/world/LargeScaleMMO/2_iteration_scene-mode')
out_dir = target / 'editor_errors' / 'graphify-out'
out_dir.mkdir(parents=True, exist_ok=True)

detect_result = detect(target)
print(f"Detect: {detect_result['total_files']} files, {detect_result['total_words']} words")
(target / '.graphify_detect.json').write_text(json.dumps(detect_result))

all_files = []
for ext in ['.cs', '.md']:
    all_files.extend(target.rglob(f'*{ext}'))

print(f"Code/docs: {len(all_files)} files")

from graphify.extract import collect_files, extract
extracted = extract(all_files)
(target / '.graphify_ast.json').write_text(json.dumps(extracted, indent=2))

merged = extracted
(target / '.graphify_extract.json').write_text(json.dumps(merged, indent=2))

G = build_from_json(merged)
print(f"Graph: {G.number_of_nodes()} nodes, {G.number_of_edges()} edges")

communities = cluster(G)
cohesion = score_all(G, communities)
tokens = {'input': 0, 'output': 0}
gods = god_nodes(G)
surprises = surprising_connections(G, communities)
labels = {cid: 'Community ' + str(cid) for cid in communities}
questions = suggest_questions(G, communities, labels)

report = generate(G, communities, cohesion, labels, gods, surprises, detect_result, tokens, str(target), suggested_questions=questions)
(target / 'GRAPH_REPORT.md').write_text(report)
to_json(G, communities, str(out_dir / 'graph.json'))

analysis = {
    'communities': {str(k): v for k, v in communities.items()},
    'cohesion': {str(k): v for k, v in cohesion.items()},
    'gods': gods,
    'surprises': surprises,
    'questions': questions,
}
(target / '.graphify_analysis.json').write_text(json.dumps(analysis, indent=2))

print(f"Done: {G.number_of_nodes()} nodes, {G.number_of_edges()} edges, {len(communities)} communities")