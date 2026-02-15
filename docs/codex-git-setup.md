# Codex Git Setup (Windows)

This guide configures Git pushes for Codex CLI sessions on Windows in non-interactive mode.

## Why this is needed
- Codex sessions may fail on interactive credential prompts in Windows shell wrappers.
- Non-interactive credentials (PAT in a local store file) avoid prompt dependencies.

## 0. Global baseline (all repositories)
Configure sane global defaults once:

```powershell
git config --global credential.helper manager-core
git config --global fetch.prune true
git config --global http.sslbackend openssl
```

Notes:
- This keeps regular Git usage intact outside Codex sessions.
- Repository-local `credential.helper` can still override global behavior where needed.

## 1. Create a fine-grained PAT in GitHub
- GitHub -> Settings -> Developer settings -> Personal access tokens -> Fine-grained tokens.
- Select repository access for needed repos.
- Minimum permissions for pushing code:
  - `Contents: Read and write`
  - `Pull requests: Read and write` (if PR workflows are needed)

## 2. Configure per-repo credential store
From repository root:

```powershell
git config --local credential.helper "store --file=.git/.codex-credentials"
```

Write a single-line credential entry:

```powershell
Set-Content -Path .git/.codex-credentials -Encoding ascii -NoNewline -Value "https://<GITHUB_LOGIN>:<PAT>@github.com"
```

Protect file permissions:

```powershell
icacls ".git\.codex-credentials" /inheritance:r /grant:r "Yakoo:F" "CodexSandboxOffline:R"
```

## 3. Prevent accidental commits

```powershell
Add-Content .git/info/exclude ".git/.codex-credentials"
```

## 4. Verify

```powershell
git ls-remote origin
git push --dry-run origin main
```

If `origin` push still triggers prompt-script errors in a Codex session, use explicit URL from store:

```powershell
$base = (Get-Content .git/.codex-credentials -Raw).Trim().TrimEnd('/')
git push --dry-run "$base/<owner>/<repo>.git" <branch>
```

## Security notes
- Never commit `.git/.codex-credentials`.
- If PAT appears in terminal output or chat logs, revoke and recreate it immediately.
- Prefer short-lived, least-privilege fine-grained tokens.
