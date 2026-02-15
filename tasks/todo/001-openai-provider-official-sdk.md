# 001 - OpenAI Provider (official SDK)

## Goal
Implement `MultiLlm.Providers.OpenAI/OpenAiProvider` using the official OpenAI .NET SDK for chat and streaming.

## Scope
- Implement `ChatAsync`.
- Implement `ChatStreamAsync`.
- Map `ChatRequest` and message parts to official SDK request models.
- Preserve `providerId/model` routing behavior.

## Acceptance Criteria
- `OpenAiProvider` has no `NotImplementedException`.
- One `ChatRequest` works through `LlmClient` with `model = "openai/<model>"`.
- Streaming emits incremental `ChatDelta` items and final completion marker.
- Request metadata (`RequestId`, `CorrelationId`, `ProviderId`) is preserved in responses.
- Add focused tests for success path and streaming path.
- `README.md` and `README.en.md` are updated with supported OpenAI provider status.
