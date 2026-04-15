#!/bin/bash
# Hook: validate-commit.sh
# Event: PreToolUse (Bash)
# Purpose: Validate before git commit

INPUT=$(cat)

# Extract command from input
COMMAND=$(echo "$INPUT" | grep -o '"command": "[^"]*"' | sed 's/"command": "//' | sed 's/"$//')

if echo "$COMMAND" | grep -q "git commit"; then
    echo "=== Validating commit ==="
    
    # Check for TODO/FIXME in staged changes
    STAGED_FILES=$(git diff --cached --name-only 2>/dev/null | head -20)
    
    if [ -n "$STAGED_FILES" ]; then
        # Check for hardcoded values
        echo "$STAGED_FILES" | while read -r file; do
            if [ -f "$file" ]; then
                # Check for TODO without owner
                grep -n "TODO" "$file" 2>/dev/null | grep -v "// TODO(@" | while read -r line; do
                    echo "⚠️  TODO without owner in $file: $line"
                done
                
                # Check for magic numbers
                grep -n "[0-9]\{4,\}" "$file" 2>/dev/null | grep -v "// " | while read -r line; do
                    echo "⚠️  Possible magic number in $file: $line"
                done
            fi
        done
    fi
    
    echo "Validation complete"
fi

exit 0