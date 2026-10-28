# Pull Request

## Summary of Changes

<!-- Provide a concise description of what changed and the motivation behind it. -->

## Affected Layers

- [ ] `burroQueue.Domain` (Pure business logic, 0 dependencies)
- [ ] `burroQueue.Infrastructure` (Audio, YouTube, Network, Persistence, Telephony)
- [ ] `burroQueue.Presentation` (WPF UI, ViewModels, Systray)
- [ ] `burroQueue.Tests` (Unit, integration, system tests)
- [ ] Documentation & Build scripts

## Pre-Submission Checklist

- [ ] **Architecture Audit:** `./scripts/verify-architecture.ps1` ran and passed with 0 errors.
- [ ] **1CPF (Single Component Per File):** Every `.cs` file defines exactly one component, with matching filename.
- [ ] **TDD:** Every production code modification is backed by a failing xUnit test written beforehand.
- [ ] **Build & Tests:** `dotnet test` runs cleanly with 0 warnings under `-warnaserror`.
- [ ] **Code Formatting:** `dotnet format --verify-no-changes` reports 0 formatting issues.
- [ ] **Conventional Commits:** Commit messages strictly follow Conventional Commits 1.0.0 (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`).
- [ ] **Memory & ADR:** `memory.md` and `decisions.md` updated for architectural changes.
