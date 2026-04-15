# Basic Coding Rules

## Language & Characters

- **NO Cyrillic in code** — Use only ASCII characters for code (variables, methods, class names, strings). Cyrillic is allowed in comments.
- **NO CJK characters (ideograms/hanzi/kanji)** — Same rule as Cyrillic. Allowed in comments only.
- **NO emoji in code** — Emojis do not belong in production code, comments, or variable names. Zero tolerance.

## Unity Meta Files

- **DO NOT edit `.meta` files** — Unity generates and manages these automatically. Manual edits will be overwritten or cause GUID conflicts.

## Assembly Definitions

- **DO NOT create or modify `.asmdef` files without explicit approval** — Assembly definition files can break the entire project build if misconfigured. Always consult before introducing new assembly references.
