#!/bin/bash
# Cline SessionStart hook: Load project context at session start
# Автоматически загружает recovery информацию

echo "=== Project C — Session Context ==="

# 1. Branch & Commits
BRANCH=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
if [ -n "$BRANCH" ]; then
    echo "📍 Branch: $BRANCH"
    
    echo ""
    echo "📜 Recent commits:"
    git log --oneline -5 2>/dev/null | while read -r line; do
        echo "  $line"
    done
fi

# 2. Session Recovery
RECOVERY_FILE=".cline/session-recovery.md"
if [ -f "$RECOVERY_FILE" ]; then
    echo ""
    echo "=== 📋 SESSION RECOVERY ==="
    echo "Quick summary:"
    # Показываем ключевые секции
    grep -A 10 "## ✅ Последние Достижения" "$RECOVERY_FILE" 2>/dev/null | head -15
    grep -A 10 "## 📋 TODO" "$RECOVERY_FILE" 2>/dev/null | head -10
    grep -A 5 "## 🔴 Критичные" "$RECOVERY_FILE" 2>/dev/null | head -8
    echo "========================"
fi

# 3. Changed Files Since Last Commit
echo ""
echo "📁 Changed files:"
STAGED=$(git diff --name-only --staged 2>/dev/null | head -5)
CHANGED=$(git diff --name-only 2>/dev/null | head -10)
UNTRACKED=$(git ls-files --others --exclude-standard 2>/dev/null | head -5)

if [ -n "$STAGED" ]; then
    echo "  Staged: $(echo "$STAGED" | wc -l) files"
fi
if [ -n "$CHANGED" ]; then
    echo "  Changed: $(echo "$CHANGED" | wc -l) files"
    echo "$CHANGED" | head -5 | sed 's/^/    /'
fi
if [ -n "$UNTRACKED" ]; then
    echo "  New: $(echo "$UNTRACKED" | wc -l) files"
fi

# 4. Code Health Check
if [ -d "Assets/_Project" ]; then
    TODO_COUNT=$(grep -r "TODO" Assets/_Project/ 2>/dev/null | wc -l)
    FIXME_COUNT=$(grep -r "FIXME" Assets/_Project/ 2>/dev/null | wc -l)
    if [ "$TODO_COUNT" -gt 0 ] || [ "$FIXME_COUNT" -gt 0 ]; then
        echo ""
        echo "⚠️  Code health: ${TODO_COUNT} TODOs, ${FIXME_COUNT} FIXMEs"
    fi
fi

# 5. Quick Command Hint
echo ""
echo "💡 Чтобы узнать больше:"
echo "   → Прочитай .cline/session-recovery.md"
echo "   → Прочитай docs/QWEN_CONTEXT.md"
echo "==================================="
exit 0
