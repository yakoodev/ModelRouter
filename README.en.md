# ModelRouter / MultiLlm

A .NET 10 multi-provider LLM SDK with a unified contract for:
- OpenAI-compatible endpoints,
- Ollama via OpenAI-compatible API,
- Codex (dev-only mode),
- MCP tools over stdio.

## Current State

Implemented:
- `MultiLlm.Core` contracts and routing (`providerId/model`),
- resilience pipeline (`retry`, backoff, timeout, concurrency, min delay),
- instruction layers (`system`, `developer`, `session`, `request`),
- OpenAI-compatible provider (sync + streaming),
- Codex provider with ChatGPT backend adapter and auth backend slot,
- MCP stdio client,
- Console and MCP demo applications.

Not implemented yet:
- `OpenAiProvider` (official SDK adapter),
- `OllamaNativeProvider`,
- full image processing pipeline in extras package.

## Architecture

- `ILlmClient` is the application-facing entry point.
- Provider is resolved from `ChatRequest.Model` in format `providerId/model`.
- `InstructionNormalizer` prepends normalized instruction messages.
- Request/response metadata (`ProviderId`, `RequestId`, `CorrelationId`) is consistently populated.

## Repository Layout

```text
/src
  /MultiLlm.Core
  /MultiLlm.Providers.OpenAI
  /MultiLlm.Providers.OpenAICompatible
  /MultiLlm.Providers.Ollama
  /MultiLlm.Providers.Codex
  /MultiLlm.Tools.Mcp
  /MultiLlm.Extras.ImageProcessing
/examples
  /ConsoleChat
  /McpDemo
/tests
  /MultiLlm.Core.Tests
  /MultiLlm.Integration.Tests
/docs
  codex-web-playbook.md
  codex-git-setup.md
```

## Quick Start

Requirements:
- .NET SDK with `net10.0` support.

Build:

```bash
dotnet build MultiLlm.slnx
```

Test:

```bash
dotnet test MultiLlm.slnx
```

## ConsoleChat Examples

Interactive setup (configure route in-app):

```bash
dotnet run --project examples/ConsoleChat
```

Codex mode (direct launch):

```bash
dotnet run --project examples/ConsoleChat -- --model gpt-5-codex --auth codex
```

API key mode:

```bash
dotnet run --project examples/ConsoleChat -- --model gpt-5-mini --auth apikey --api-key <KEY>
```

Local endpoint without auth:

```bash
dotnet run --project examples/ConsoleChat -- --model llama3.1:8b --auth none --base-url http://localhost:11434/v1
```

## MCP Example

```bash
dotnet run --project examples/McpDemo
```

## Security and Operations

- Secrets are redacted in exception/error flows (`SecretRedactor`).
- Keep auth tokens out of source control.
- For Codex CLI push/pull setup on Windows, see `docs/codex-git-setup.md`.

## Additional Docs

- Russian primary README: `README.md`
- Technical specification: `MultiLlm_TZ.ModelRouter.md`
- Codex process notes: `docs/codex-web-playbook.md`
