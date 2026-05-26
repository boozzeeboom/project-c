# Hook Input/Output Schemas

This documents the JSON payloads each Claude Code hook receives on stdin for every event type.

## PreToolUse

Fired before a tool is executed. Can **allow** (exit 0) or **block** (exit 2).

### PreToolUse: Bash

```json
{
  "tool_name": "Bash",
  "tool_input": {
    "command": "git commit -m 'feat: add player health system'",
    "description": "Commit changes with message",
    "timeout": 120000
  }
}
```

### PreToolUse: Write

```json
{
  "tool_name": "Write",
  "tool_input": {
    "file_path": "src/gameplay/health.cs",
    "content": "..."
  }
}
```

### PreToolUse: Edit

```json
{
  "tool_name": "Edit",
  "tool_input": {
    "file_path": "src/gameplay/health.cs",
    "old_string": "var health = 100",
    "new_string": "var health: int = 100"
  }
}
```

## PostToolUse

Fired after a tool completes. **Cannot block** (exit code ignored for blocking).

### PostToolUse: Write

```json
{
  "tool_name": "Write",
  "tool_input": {
    "file_path": "assets/data/enemy_stats.json",
    "content": "{\"goblin\": {\"health\": 50}}"
  },
  "tool_output": "File written successfully"
}
```

## SessionStart

Fired when a session begins. **No stdin input** — the hook just runs.

## Stop

Fired when the session ends. **No stdin input** — the hook runs for cleanup.

## Exit Code Reference

| Exit Code | Meaning | Applicable Events |
|-----------|---------|-------------------|
| 0 | Allow / Success | All events |
| 2 | Block (stderr shown) | PreToolUse only |
| Other | Treated as error | All events |