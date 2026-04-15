---
name: code-review
description: "Performs an architectural and quality code review on a specified file or set of files. Checks for coding standard compliance, architectural pattern adherence, SOLID principles, testability, and performance concerns."
argument-hint: "[file path or directory to review]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Bash
---

When this skill is invoked:

1. **Parse the argument** for the file path or directory to review

2. **Identify files to review**:
   - If directory, find all `.cs` files
   - If specific file, review just that file

3. **Review categories**:

### Architecture & Patterns
- [ ] Follows Project C architecture patterns?
- [ ] Uses composition over inheritance?
- [ ] Proper separation of concerns?
- [ ] Dependencies point correctly (gameplay → engine, not reverse)?

### C# / Unity Standards
- [ ] No `Find()`, `FindObjectOfType()`, `SendMessage()` in production
- [ ] Component references cached in `Awake()`, not `GetComponent<>()` in `Update()`
- [ ] `[SerializeField] private` for inspector fields
- [ ] `readonly` and `const` where applicable

### Memory & Performance
- [ ] No allocations in hot paths (`Update()`, physics callbacks)
- [ ] Uses `StringBuilder` for concatenation in loops
- [ ] Uses `NonAlloc` API variants where applicable
- [ ] Objects pooled with `ObjectPool<T>` if frequently instantiated

### Network (if applicable)
- [ ] `NetworkVariable` for persistent state
- [ ] RPCs for one-shot actions
- [ ] No `GetComponent<>()` calls in `Update()`

### Error Handling
- [ ] Proper null checks (use `== null` not `is null` for Unity objects)
- [ ] Coroutines properly stopped
- [ ] Script execution order respected

4. **Generate report** with:
   - Files reviewed
   - Issues found (with line numbers)
   - Severity: 🔴 Critical, 🟡 Warning, 🔵 Suggestion
   - Recommended fixes

5. **Output summary**:
   - Overall code health score
   - Must-fix issues
   - Nice-to-have improvements
   - Performance concerns