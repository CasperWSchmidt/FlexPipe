# FlexPipe — Claude Instructions

## Git Workflow

- **Never commit directly to `main`.** The remote rejects direct pushes (branch protection). Always work on a feature branch and open a PR.
- **Delete `docs/superpowers/` before opening a PR.** Specs and plans are development scaffolding — remove them with `git rm -r docs/superpowers/` before the final commit.
- Commit messages drive versioning — use Conventional Commits format (see Versioning below).

## Project Structure

| Path | Purpose |
|---|---|
| `src/FlexPipe/` | Core library — published to NuGet |
| `src/FlexPipe.Extensions.DependencyInjection/` | MS DI adapter — published to NuGet |
| `src/FlexPipe.Extensions.SimpleInjector/` | SimpleInjector adapter — published to NuGet |
| `src/FlexPipe.Samples/` | Runnable examples — not published |
| `tests/FlexPipe.Tests/` | xUnit test suite |

## Build & Test

```bash
# Build everything
dotnet build FlexPipe.slnx

# Run tests
dotnet test tests/FlexPipe.Tests/FlexPipe.Tests.csproj
```

## Versioning

Version is derived automatically by **GitVersion** from commit message prefixes. No version number exists in any file.

| Commit prefix | Version bump | Example |
|---|---|---|
| `feat!:` or `BREAKING CHANGE` | Major (`0.1.3` → `1.0.0`) | Breaking API rename |
| `feat:` | Minor (`0.1.3` → `0.2.0`) | New feature |
| `fix:`, `refactor:`, `docs:`, `chore:`, etc. | Patch (`0.1.3` → `0.1.4`) | Bug fix, docs |

## CI/CD

On **pull request** to `main`: build, test.

On **merge to `main`**: build, test, pack, tag the commit (`v<version>`), create a GitHub release, and **push all three NuGet packages to NuGet.org**.

> Merging to `main` = publishing a release. Make sure the version bump is intentional.
