import json

# Read vis-network
with open('graphify-out/vis-network.min.js', 'r', encoding='utf-8') as f:
    vis_js = f.read()

# Read graph.json
with open('docs/graph.json', 'r', encoding='utf-8') as f:
    graph_json = f.read()

# Read roadmap
with open('docs/roadmap.html', 'r', encoding='utf-8') as f:
    html = f.read()

# 1. Change fetch URL to local
html = html.replace(
    "const GRAPH_RAW_URL = 'https://boozzeeboom.github.io/project-c/graph.json';",
    "const GRAPH_RAW_URL = 'graph.json';"
)

# 2. Replace fetch+json parse with direct data embed
old_block = """      fetch(GRAPH_RAW_URL, { cache: 'no-store' })
        .then(r => {
          if (!r.ok) throw new Error(`HTTP ${r.status}`);
          return r.json();
        })
        .then(data => {
          const nodes = data.nodes || [];
          const edges = data.edges || [];
          const communities = data.communities || {};
          const nodeCount = nodes.length;
          const edgeCount = edges.length;
          const commCount = Object.keys(communities).length;
          META.textContent = `${nodeCount} nodes · ${edgeCount} edges · ${commCount} communities`;

          const colors = ["""

new_block = """      const data = JSON.parse(GRAPH_DATA_JSON);
      const nodes = data.nodes || [];
      const edges = data.edges || [];
      const communities = data.communities || {};
      const nodeCount = nodes.length;
      const edgeCount = edges.length;
      const commCount = Object.keys(communities).length;
      META.textContent = `${nodeCount} nodes · ${edgeCount} edges · ${commCount} communities`;

      const colors = ["""

html = html.replace(old_block, new_block)

# 3. Fix RAW_LINK.href
html = html.replace(
    "RAW_LINK.href = GRAPH_RAW_URL.replace('/main/', '/main/');",
    "RAW_LINK.href = 'graph.json';"
)

# 4. Remove the external vis-network script src tag and inline the script
# The srcdoc template has the script tag like: <script src="https://boozzeeboom.github.io/project-c/vis-network.min.js"><\/script>
old_script = '<script src="https://boozzeeboom.github.io/project-c/vis-network.min.js"><\\/script>'
new_script = '<script>' + vis_js + '<\\/script>'
html = html.replace(old_script, new_script)

# 5. Insert GRAPH_DATA_JSON constant before initGraph
html = html.replace(
    "function initGraph() {",
    'const GRAPH_DATA_JSON = ' + json.dumps(graph_json) + ';\nfunction initGraph() {'
)

# 6. Remove the fetch catch block and simplify error handling since data is embedded
# The error display for fetch errors no longer applies

# Write output
with open('docs/roadmap.html', 'w', encoding='utf-8') as f:
    f.write(html)

print(f"Inlined vis-network ({len(vis_js)//1024}KB) and graph.json ({len(graph_json)//1024}KB)")
print(f"Updated docs/roadmap.html")
