---
name: project-stage-detect
description: "Automatically analyze project state, detect stage, identify gaps, and recommend next steps based on existing artifacts."
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash
---

When this skill is invoked:

1. **Scan project structure**:
   - Check for source code files
   - Check for design documents
   - Check for build artifacts
   - Check git history

2. **Detect project stage**:
   - 🌀 **Pre-production**: No code, no design
   - 🔨 **Prototype**: Minimal code, testing mechanics
   - 📋 **Production**: Full development, features being built
   - 🎯 **Polish**: Features complete, refining
   - 🚀 **Release**: Preparing launch

3. **Identify gaps**:

### Documentation Gaps
- [ ] Missing game concept?
- [ ] Missing architecture docs?
- [ ] Missing system designs?
- [ ] Missing API docs?

### Code Gaps
- [ ] Missing core systems?
- [ ] Missing tests?
- [ ] Missing CI/CD?

### Process Gaps
- [ ] Missing sprint planning?
- [ ] Missing bug tracking?
- [ ] Missing code review process?

4. **Generate report**:
   ```
   # Project Stage Report
   
   ## Current Stage: [Stage Name]
   
   ## Indicators
   - Code files: [N]
   - Design docs: [N]
   - Commits: [N]
   - Last activity: [date]
   
   ## Strengths
   - [What's working well]
   
   ## Gaps
   - [What's missing]
   
   ## Recommended Next Steps
   1. [Most important]
   2. [Second]
   3. [Third]
   ```

5. **Save to** `docs/project-stage-report.md`