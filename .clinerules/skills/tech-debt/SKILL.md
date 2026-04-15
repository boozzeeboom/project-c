---
name: tech-debt
description: "Track, categorize, and prioritize technical debt across the codebase. Scans for debt indicators, maintains a debt register, and recommends repayment scheduling."
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash
---

When this skill is invoked:

1. **Scan for debt indicators**:

### Code Smells
- [ ] Long methods (>50 lines)
- [ ] Large classes (>300 lines)
- [ ] Deep inheritance hierarchies
- [ ] Circular dependencies

### Unity-specific
- [ ] `Find()` / `GetComponent()` in Update()
- [ ] `SendMessage()` usage
- [ ] Hardcoded magic numbers
- [ ] Missing null checks

### Architecture
- [ ] God classes
- [ ] Feature envy
- [ ] Data clumps
- [ ] Shotgun surgery (one change requires many edits)

### Technical
- [ ] TODO/FIXME comments
- [ ] Copy-pasted code
- [ ] Missing abstractions
- [ ] Performance violations

2. **Categorize debt**:
   - 🔴 **Critical**: Causes bugs, blocks features
   - 🟡 **High**: Makes changes difficult
   - 🔵 **Medium**: Technical quality concern
   - ⚪ **Low**: Nice to have

3. **Create debt register**:
   ```
   # Technical Debt Register
   
   ## Critical
   | ID | Location | Issue | Impact | Fix Effort |
   |----|----------|-------|--------|------------|
   | TD-001 | ... | ... | ... | ... |
   
   ## High
   ... etc
   ```

4. **Recommend repayment schedule**:
   - "Boy scout rule": Leave code cleaner than found
   - Allocate 20% of sprint capacity
   - Tackle critical before high

5. **Save to** `docs/tech-debt-register.md`