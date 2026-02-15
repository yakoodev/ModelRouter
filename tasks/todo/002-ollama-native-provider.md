# 002 - Ollama Native Provider

## Goal
Implement `MultiLlm.Providers.Ollama/OllamaNativeProvider` for Ollama native API (non OpenAI-compat mode).

## Scope
- Implement native request/response mapping.
- Implement streaming support for native endpoint.
- Keep compatibility with core contracts (`ChatRequest`, `ChatResponse`, `ChatDelta`).

## Acceptance Criteria
- `OllamaNativeProvider` has no `NotImplementedException`.
- `LlmClient` successfully routes `model = "ollama-native/<model>"`.
- Native chat and native streaming both return valid core contract objects.
- Integration tests cover at least one sync and one streaming scenario (mock server allowed).
- No provider-specific leakage into `MultiLlm.Core`.
- `README.md` and `README.en.md` document native Ollama support and config example.
