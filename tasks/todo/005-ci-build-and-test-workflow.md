# 005 - CI Build and Test Workflow

## Goal
Add CI pipeline to enforce build and test quality gates on pull requests and main branch.

## Scope
- Add GitHub Actions workflow(s) for restore/build/test.
- Run core and integration test projects.
- Publish test results artifacts.

## Acceptance Criteria
- PRs trigger CI automatically on changed code.
- Workflow runs `dotnet build MultiLlm.slnx` and `dotnet test MultiLlm.slnx`.
- Failures clearly report which stage failed.
- Cache is configured for NuGet to keep CI runtime practical.
- Branch protection can rely on CI checks as required status checks.
