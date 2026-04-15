#!/bin/bash
# Hook: validate-push.sh
# Event: PreToolUse (Bash)
# Purpose: Warn on pushes to protected branches

INPUT=$(cat)

COMMAND=$(echo "$INPUT" | grep -o '"command": "[^"]*"' | sed 's/"command": "//' | sed 's/"$//')

if echo "$COMMAND" | grep -q "git push"; then
    echo "=== Validating push ==="
    
    # Check target branch
    if echo "$COMMAND" | grep -qE "main|master|develop"; then
        echo "⚠️  WARNING: Pushing to protected branch"
        echo "   Ensure you have authorization"
    fi
    
    # Show what will be pushed
    echo ""
    echo "Files to be pushed:"
    git diff --stat HEAD 2>/dev/null | head -10
    
    echo "=== Push validation complete ==="
fi

exit 0