from graphify.build import build_from_json
from graphify.export import to_json, to_html
from graphify.cluster import cluster, score_all
from graphify.analyze import god_nodes, surprising_connections
from graphify.report import generate
import json
from pathlib import Path

extraction = json.loads(Path('graphify-out/.graphify_ast.json').read_text())

G = build_from_json(extraction)
communities = cluster(G)
cohesion = score_all(G, communities)
gods = god_nodes(G)
surprises = surprising_connections(G, communities)

labels = {cid: f"Community {cid}" for cid in communities}
questions = []

detection = {'total_files': 0, 'total_words': 0, 'needs_graph': False, 'warning': None, 'files': {'code': [], 'document': []}}
tokens = {'input': 0, 'output': 0}

report = generate(G, communities, cohesion, labels, gods, surprises, detection, tokens, '.')
Path('graphify-out/GRAPH_REPORT.md').write_text(report)
to_json(G, communities, 'graphify-out/graph.json')

analysis = {
    'communities': {str(k): v for k, v in communities.items()},
    'cohesion': {str(k): v for k, v in cohesion.items()},
    'gods': gods,
    'surprises': surprises,
}
Path('graphify-out/.graphify_analysis.json').write_text(json.dumps(analysis, indent=2))

print(f'Graph: {G.number_of_nodes()} nodes, {G.number_of_edges()} edges, {len(communities)} communities')
print()
for g in gods[:5]:
    print(f'  God: {g}')
print()
for s in surprises[:3]:
    print(f'  Surprise: {s}')