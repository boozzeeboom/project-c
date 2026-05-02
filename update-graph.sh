#!/bin/bash
# Update graph.json in docs/ from graphify-out/, commit and push

set -e

GRAPHIFY_OUT="graphify-out/graph.json"
TARGET="docs/graph.json"

if [ ! -f "$GRAPHIFY_OUT" ]; then
  echo "Error: $GRAPHIFY_OUT not found. Run 'graphify .' first."
  exit 1
fi

# Copy fresh graph.json to docs/
cp "$GRAPHIFY_OUT" "$TARGET"

# Check if there's anything to commit
if git diff --quiet "$TARGET" && git diff --cached --quiet "$TARGET"; then
  echo "No changes to commit (graph.json is unchanged)"
  exit 0
fi

git add "$TARGET"
git commit -m "update graph $(date +'%Y-%m-%d %H:%M')" --allow-empty
git push

echo "Done: graph.json updated, committed, pushed to GitHub Pages"
echo "Wait ~2 min for GitHub Pages to rebuild, then refresh roadmap page"