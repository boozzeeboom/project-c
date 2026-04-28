import json
import sys
from pathlib import Path

PYTHON = Path('graphify-out/.graphify_python').read_text().strip() if Path('graphify-out/.graphify_python').exists() else 'python'

print("Step 1: Structural extraction (AST)...")
import subprocess
result = subprocess.run([PYTHON, '-c', '''
import json
from graphify.extract import collect_files, extract
from pathlib import Path

code_files = []
detect = json.loads(Path("graphify-out/.graphify_detect.json").read_text())
for f in detect.get("files", {}).get("code", []):
    code_files.extend(collect_files(Path(f)) if Path(f).is_dir() else [Path(f)])

if code_files:
    result = extract(code_files)
    Path("graphify-out/.graphify_ast.json").write_text(json.dumps(result, indent=2))
    print(f"AST: {len(result["nodes"])} nodes, {len(result["edges"])} edges")
else:
    Path("graphify-out/.graphify_ast.json").write_text(json.dumps({"nodes":[],"edges":[],"input_tokens":0,"output_tokens":0}))
    print("No code files - skipping AST extraction")
'''], capture_output=True, text=True, cwd='C:/UNITY_PROJECTS/ProjectC_client')
print(result.stdout)
if result.returncode != 0:
    print("STDERR:", result.stderr)
    sys.exit(1)

print("\nStep 2: Merge AST + semantic into final extraction...")
result2 = subprocess.run([PYTHON, '-c', '''
import json
from pathlib import Path

ast = json.loads(Path("graphify-out/.graphify_ast.json").read_text())
sem = json.loads(Path("graphify-out/.graphify_semantic.json").read_text()) if Path("graphify-out/.graphify_semantic.json").exists() else {"nodes":[],"edges":[],"hyperedges":[]}

seen = {n["id"] for n in ast["nodes"]}
merged_nodes = list(ast["nodes"])
for n in sem["nodes"]:
    if n["id"] not in seen:
        merged_nodes.append(n)
        seen.add(n["id"])

merged_edges = ast["edges"] + sem["edges"]
merged_hyperedges = sem.get("hyperedges", [])
merged = {
    "nodes": merged_nodes,
    "edges": merged_edges,
    "hyperedges": merged_hyperedges,
    "input_tokens": sem.get("input_tokens", 0),
    "output_tokens": sem.get("output_tokens", 0),
}
Path("graphify-out/.graphify_extract.json").write_text(json.dumps(merged, indent=2))
total = len(merged_nodes)
edges = len(merged_edges)
print(f"Merged: {total} nodes, {edges} edges ({len(ast["nodes"])} AST + {len(sem["nodes"])} semantic)")
'''], capture_output=True, text=True, cwd='C:/UNITY_PROJECTS/ProjectC_client')
print(result2.stdout)
if result2.returncode != 0:
    print("STDERR:", result2.stderr)

print("\nStep 3: Build graph, cluster, analyze...")
result3 = subprocess.run([PYTHON, '-c', '''
import sys, json
from pathlib import Path

try:
    from graphify.build import build_from_json
    from graphify.cluster import cluster, score_all
    from graphify.analyze import god_nodes, surprising_connections, suggest_questions
    from graphify.report import generate
    from graphify.export import to_json
except ImportError as e:
    print(f"Import error: {e}")
    sys.exit(1)

extraction = json.loads(Path("graphify-out/.graphify_extract.json").read_text())
detection  = json.loads(Path("graphify-out/.graphify_detect.json").read_text())

G = build_from_json(extraction)
communities = cluster(G)
cohesion = score_all(G, communities)
tokens = {"input": extraction.get("input_tokens", 0), "output": extraction.get("output_tokens", 0)}
gods = god_nodes(G)
surprises = surprising_connections(G, communities)
labels = {cid: "Community " + str(cid) for cid in communities}
questions = suggest_questions(G, communities, labels)

report = generate(G, communities, cohesion, labels, gods, surprises, detection, tokens, "docs/world/LargeScaleMMO", suggested_questions=questions)
Path("graphify-out/GRAPH_REPORT.md").write_text(report)
to_json(G, communities, "graphify-out/graph.json")

analysis = {
    "communities": {str(k): v for k, v in communities.items()},
    "cohesion": {str(k): v for k, v in cohesion.items()},
    "gods": gods,
    "surprises": surprises,
    "questions": questions,
}
Path("graphify-out/.graphify_analysis.json").write_text(json.dumps(analysis, indent=2))
if G.number_of_nodes() == 0:
    print("ERROR: Graph is empty")
    sys.exit(1)
print(f"Graph: {G.number_of_nodes()} nodes, {G.number_of_edges()} edges, {len(communities)} communities")
'''], capture_output=True, text=True, cwd='C:/UNITY_PROJECTS/ProjectC_client')
print(result3.stdout)
if result3.returncode != 0:
    print("STDERR:", result3.stderr)

print("\nDone! Graph outputs in graphify-out/")
print("  - graph.html: Interactive graph")
print("  - GRAPH_REPORT.md: Audit report")
print("  - graph.json: Raw graph data")