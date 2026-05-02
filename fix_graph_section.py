with open('docs/roadmap.html', 'r', encoding='utf-8') as f:
    content = f.read()

# Find the start of the graph script section
marker = "/* ================================================================\n   5. KNOWLEDGE GRAPH"
idx = content.find(marker)
if idx == -1:
    print("Could not find marker!")
    exit(1)

new_script = """/* ================================================================
   5. KNOWLEDGE GRAPH (simple iframe to graph.html)
   ================================================================ */
(function() {
  var SECTION = document.getElementById('graph-section');
  var IFRAME  = document.getElementById('graph-iframe');
  var LOADING = document.getElementById('graph-loading');
  if (!SECTION) return;
  SECTION.style.display = '';

  window.toggleGraph = function() {
    var isOpen = SECTION.classList.contains('graph-section--open');
    if (!isOpen && !IFRAME.src) {
      IFRAME.src = 'graph.html';
    }
    SECTION.classList.toggle('graph-section--open');
    if (SECTION.classList.contains('graph-section--open')) {
      LOADING.style.display = 'none';
      IFRAME.style.display = '';
    } else {
      IFRAME.style.display = 'none';
    }
  };
})();
</script>

</body>
</html>"""

# Replace from marker to end
new_content = content[:idx] + new_script

with open('docs/roadmap.html', 'w', encoding='utf-8') as f:
    f.write(new_content)

print(f"Fixed roadmap.html: replaced {len(content) - len(new_content)} chars of inline graph data")