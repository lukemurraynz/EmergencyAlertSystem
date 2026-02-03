# Local Lint Scripts

Run these scripts locally before pushing to avoid CI failures.

## Quick Start

```powershell
# Run all lints (backend + frontend)
.\scripts\lint-all.ps1

# Auto-fix formatting issues
.\scripts\lint-all.ps1 -Fix
```

## Individual Scripts

### Backend Lint
```powershell
# Check only
.\scripts\lint-backend.ps1

# Auto-fix formatting
.\scripts\lint-backend.ps1 -Fix

# Skip test compilation (only when tests are broken)
.\scripts\lint-backend.ps1 -SkipTests
```

**Checks:**
- Code formatting (`dotnet format --verify-no-changes`)
- Build src projects with warnings as errors
- **Test project compilation** (validates tests compile)

**Note**: Test compilation is now required by default (Definition of Done). Use `-SkipTests` only when tests are temporarily broken and documented.

### Frontend Lint
```powershell
# Check only
.\scripts\lint-frontend.ps1

# Auto-fix ESLint issues
.\scripts\lint-frontend.ps1 -Fix
```

**Checks:**
- ESLint (`npm run lint`)
- TypeScript type checking (`tsc --noEmit`)

## Pre-commit Hook (Optional)

Add to `.git/hooks/pre-commit`:
```bash
#!/bin/sh
pwsh -File scripts/lint-all.ps1
```

## Test Validation

All test projects must compile before code can be merged (Definition of Done requirement). If you encounter test compilation errors:

1. Fix the tests to match implementation signatures
2. Validate with: `dotnet build backend/tests/<Project>/<Project>.csproj`
3. Use the `test-validation-specialist` agent for help identifying mismatches

See `.github/agents/test-validation-specialist.agent.md` for detailed guidance.
