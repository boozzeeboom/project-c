#!/bin/bash
# Hook: detect-gaps.sh
# Event: SessionStart
# Purpose: Detect missing documentation when code/prototypes exist

set +e

echo "=== Checking for Documentation Gaps ==="

# Fresh project detection
FRESH_PROJECT=true

if [ -f ".cline/CLAUDE.md" ]; then
    FRESH_PROJECT=false
fi

if [ -d "Assets/_Project/Scripts" ]; then
    SRC_CHECK=$(find Assets/_Project/Scripts -name "*.cs" 2>/dev/null | head -1)
    if [ -n "$SRC_CHECK" ]; then
        FRESH_PROJECT=false
    fi
fi

if [ "$FRESH_PROJECT" = true ]; then
    echo ""
    echo "🚀 NEW PROJECT: Running /start to begin onboarding"
    echo "==================================="
    exit 0
fi

# Check for design docs
if [ -d "docs/gdd" ]; then
    DESIGN_FILES=$(find docs/gdd -name "*.md" 2>/dev/null | wc -l)
    DESIGN_FILES=$(echo "$DESIGN_FILES" | tr -d ' ')
    
    if [ "$DESIGN_FILES" -lt 3 ]; then
        echo "⚠️  GAP: Sparse design docs ($DESIGN_FILES files)"
        echo "    Suggested action: /reverse-document or /project-stage-detect"
    fi
fi

# Check for architecture docs
if [ -d "docs/architecture" ]; then
    ADR_COUNT=$(find docs/architecture -name "*.md" 2>/dev/null | wc -l)
    ADR_COUNT=$(echo "$ADR_COUNT" | tr -d ' ')
    
    if [ "$ADR_COUNT" -lt 2 ]; then
        echo "⚠️  GAP: Architecture docs needed ($ADR_COUNT found)"
        echo "    Suggested action: /architecture-decision"
    fi
fi

echo ""
echo "💡 Run /project-stage-detect for comprehensive analysis"
echo "==================================="

exit 0