# AGENTS.md

Repository-level guidance for coding agents working in `ModelRouter`.

## Scope
- This file applies to the whole repository.
- Prefer minimal, targeted changes over broad refactors.

## Workflow
- Use feature branches for all non-trivial work.
- Keep commits focused and atomic.
- Do not rewrite or squash existing commits unless explicitly requested.

## Build and Test
- Build solution:
  - `dotnet build MultiLlm.slnx`
- Run all tests:
  - `dotnet test MultiLlm.slnx`
- Run focused tests when touching provider behavior:
  - `dotnet test tests/MultiLlm.Core.Tests/MultiLlm.Core.Tests.csproj --filter "CodexProviderChatGptBackendTests"`

## Project Conventions
- `MultiLlm.Core` is the canonical contract surface.
- Providers should adapt to core contracts; avoid provider-specific leakage into core.
- Keep `providerId/model` routing behavior intact.
- New provider behavior should include at least one focused unit/integration test.

## ConsoleChat Expectations
- `examples/ConsoleChat` is a test app, but should remain functional and ergonomic.
- CLI changes must update usage/help text.
- If adding message part support (images/files/tools), ensure provider payload mapping is updated and tested.

## Git and Push from Codex CLI (Windows)
- Default remote may remain HTTPS.
- If Codex session cannot open interactive auth prompt, use local credential store for this repo:
  - `git config --local credential.helper "store --file=.git/.codex-credentials"`
- Keep `.git/.codex-credentials` excluded from commits.
- If a token is exposed in terminal logs/chat, rotate it immediately.

## Documentation
- Keep Russian docs in `README.md`.
- Keep English docs in `README.en.md`.
- When behavior changes, update both READMEs in the same change set.
