#!/bin/bash
# Hook: validate-assets.sh
# Event: PostToolUse (Write/Edit)
# Purpose: Check naming conventions for asset files

INPUT=$(cat)

# Extract file path
FILE_PATH=$(echo "$INPUT" | grep -o '"file_path": "[^"]*"' | sed 's/"file_path": "//' | sed 's/"$//')

if [ -n "$FILE_PATH" ]; then
    # Check if it's an asset file
    if echo "$FILE_PATH" | grep -qE "\.(json|asset|prefab|mat|shader)$"; then
        echo "=== Validating asset: $FILE_PATH ==="
        
        # Check naming convention (PascalCase)
        FILENAME=$(basename "$FILE_PATH")
        if ! echo "$FILENAME" | grep -qE "^[A-Z]"; then
            echo "⚠️  NAMING: Asset should use PascalCase (e.g., PlayerData.json)"
        fi
        
        # Validate JSON files
        if echo "$FILE_PATH" | grep -q "\.json$"; then
            if [ -f "$FILE_PATH" ]; then
                python -c "import json; json.load(open('$FILE_PATH'))" 2>/dev/null
                if [ $? -ne 0 ]; then
                    echo "⚠️  JSON: Invalid JSON syntax in $FILE_PATH"
                fi
            fi
        fi
    fi
fi

exit 0