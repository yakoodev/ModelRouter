# ModelRouter / MultiLlm

Русскоязычная основная документация проекта.  
English version: `README.en.md`

`ModelRouter` (`MultiLlm`) — .NET 10 библиотека с единым контрактом для нескольких LLM-провайдеров:
- OpenAI-compatible endpoints (включая локальные и self-hosted),
- Ollama через OpenAI-compatible интерфейс,
- Codex (dev-only, через `codex login`/`auth.json`),
- MCP tools клиент по stdio.

## Содержание
- [1. Текущий статус](#1-текущий-статус)
- [2. Архитектура](#2-архитектура)
- [3. Структура репозитория](#3-структура-репозитория)
- [4. Быстрый старт](#4-быстрый-старт)
- [5. Пример использования API](#5-пример-использования-api)
- [6. Провайдеры и авторизация](#6-провайдеры-и-авторизация)
- [7. Контракты Core](#7-контракты-core)
- [8. Устойчивость и hooks](#8-устойчивость-и-hooks)
- [9. MCP](#9-mcp)
- [10. Примеры запуска](#10-примеры-запуска)
- [11. Тесты](#11-тесты)
- [12. Ограничения](#12-ограничения)
- [13. Git/Codex push setup](#13-gitcodex-push-setup)

## 1. Текущий статус

### Реализовано
- `MultiLlm.Core`:
  - единые контракты (`ChatRequest`, `ChatResponse`, `ChatDelta`, `Message`, `MessagePart`),
  - роутинг модели формата `providerId/model`,
  - resilience pipeline в `LlmClient` (retry/backoff/timeout/concurrency/rate delay),
  - instruction layers (`system`, `developer`, `session`, `request`),
  - hooks (`OnStart`, `OnEnd`, `OnError`) + редактирование секретов.
- `MultiLlm.Providers.OpenAI`:
  - официальный .NET SDK adapter (sync + streaming),
- `MultiLlm.Providers.OpenAICompatible`:
  - sync chat (`/chat/completions`),
  - streaming SSE (`data: ...`, `[DONE]`),
  - поддержка `TextPart`, `ImagePart`, `FilePart`, tool parts.
- `MultiLlm.Providers.Codex`:
  - dev-only guard,
  - auth backend slot (`official-device-code` + optional experimental),
  - ChatGPT backend adapter.
- `MultiLlm.Providers.Ollama`:
  - `OllamaOpenAiCompatProvider` (обертка над OpenAI-compatible).
- `MultiLlm.Tools.Mcp`:
  - stdio JSON-RPC client (`initialize`, `tools/list`, `tools/call`).
- Примеры:
  - `examples/ConsoleChat`,
  - `examples/McpDemo`.

### Не завершено
- `OllamaNativeProvider` пока `NotImplementedException`.
- `MultiLlm.Extras.ImageProcessing` содержит контракт, без полного pipeline.

## 2. Архитектура

Клиентский код работает с `ILlmClient`, а провайдер определяется по `ChatRequest.Model`:
- `openai-compatible/gpt-4.1-mini`
- `codex/gpt-5-codex`
- `ollama-openai-compat/llama3.1:8b`

`LlmClient`:
- выделяет `providerId` из `providerId/model`,
- выбирает `IModelProvider`,
- нормализует инструкции (`InstructionNormalizer`),
- применяет resilience-настройки,
- выставляет метаданные (`ProviderId`, `RequestId`, `CorrelationId`).

## 3. Структура репозитория

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

## 4. Быстрый старт

Требования:
- .NET SDK с поддержкой `net10.0`.

Сборка:

```bash
dotnet build MultiLlm.slnx
```

Тесты:

```bash
dotnet test MultiLlm.slnx
```

NuGet-пакеты (core + провайдеры):

```bash
dotnet pack MultiLlm.slnx -c Release
```

Собранные пакеты:
- `MultiLlm` (core-контракты и клиент)
- `MultiLlm.Provider.OpenAI`
- `MultiLlm.Provider.OpenAICompatible`
- `MultiLlm.Provider.Ollama`
- `MultiLlm.Provider.Codex`

## 5. Пример использования API

```csharp
using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.OpenAICompatible;

var provider = new OpenAiCompatibleProvider(new OpenAiCompatibleProviderOptions
{
    ProviderId = "openai-compatible",
    BaseUrl = "https://api.openai.com/v1",
    Model = "gpt-4.1-mini",
    Headers = new Dictionary<string, string>
    {
        ["Authorization"] = $"Bearer {Environment.GetEnvironmentVariable("OPENAI_API_KEY")}"
    }
});

var client = new LlmClient([provider]);

var request = new ChatRequest(
    Model: "openai-compatible/gpt-4.1-mini",
    Messages:
    [
        new Message(MessageRole.User, [new TextPart("Сделай краткое резюме...")])
    ]);

var response = await client.ChatAsync(request);
var text = string.Concat(response.Message.Parts.OfType<TextPart>().Select(x => x.Text));
Console.WriteLine(text);
```

Для stream-режима используйте `await foreach` по `client.ChatStreamAsync(request)`.

## 6. Провайдеры и авторизация

### OpenAI-compatible
- Реальный HTTP-провайдер к `POST {baseUrl}/chat/completions`.
- Авторизация через `Headers` (например, `Authorization: Bearer ...`).

### Codex (dev-only)
- По умолчанию использует `OfficialDeviceCodeBackend`, который читает:
  - `OPENAI_API_KEY`, либо
  - `tokens.access_token` из `CODEX_HOME/auth.json` (или `~/.codex/auth.json`).
- При `IsDevelopment = false` выбрасывается исключение.
- `EnableExperimentalAuthAdapters` включает дополнительный backend-слот (если backend зарегистрирован).

### Ollama
- `OllamaOpenAiCompatProvider` работает через совместимый OpenAI endpoint.
- `OllamaNativeProvider` пока не реализован.

### OpenAI
- `OpenAiProvider` implemented via official OpenAI .NET SDK (sync + streaming).

### Auth-стратегии в Core
- `NoAuth`
- `ApiKeyAuth`
- `BearerAuth`
- `CustomHeadersAuth`
- `ITokenStore` + `InMemoryTokenStore`

## 7. Контракты Core

### Интерфейсы
- `ILlmClient`
  - `Task<ChatResponse> ChatAsync(ChatRequest, CancellationToken)`
  - `IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest, CancellationToken)`
- `IModelProvider`
  - `ProviderId`
  - `Capabilities`
  - `ChatAsync(...)`
  - `ChatStreamAsync(...)`

### Chat-модели
- `ChatRequest`:
  - `Model` (`providerId/model`),
  - `Messages`,
  - `Instructions`,
  - `RequestId`, `CorrelationId` (опционально).
- `Message`:
  - `Role`: `System`, `Developer`, `User`, `Assistant`, `Tool`,
  - `Parts`: массив `MessagePart`.
- Типы `MessagePart`:
  - `TextPart`
  - `ImagePart`
  - `FilePart`
  - `ToolCallPart`
  - `ToolResultPart`

### Инструкции
- Поддерживаются 4 слоя: `system`, `developer`, `session`, `request`.
- Приоритет: `request > session > developer > system`.

## 8. Устойчивость и hooks

`LlmClientResilienceOptions`:
- `MaxRetries` (default `2`)
- `InitialBackoff` / `MaxBackoff` / `UseJitter`
- `RequestTimeout`
- `MaxConcurrentRequests`
- `MinDelayBetweenRequests`
- `ShouldRetry`

`ILlmEventHook`:
- `OnStartAsync`
- `OnEndAsync`
- `OnErrorAsync`

Безопасность:
- `SecretRedactor` маскирует bearer/api key/token/secret в сообщениях ошибок.

## 9. MCP

`MultiLlm.Tools.Mcp` содержит `StdioMcpClient`:
- запускает MCP server subprocess (`Command` + `Arguments`),
- делает handshake (`initialize` + `notifications/initialized`),
- предоставляет:
  - `GetToolsAsync()`
  - `CallToolAsync(toolName, argumentsJson)`

Опции:
- `Command`, `Arguments`, `WorkingDirectory`
- `EnvironmentVariables`
- `RequestTimeout`
- `ClientName`, `ClientVersion`, `ProtocolVersion`

## 10. Примеры запуска

Интерактивный режим (настройка маршрута в приложении):
```bash
dotnet run --project examples/ConsoleChat
```

Codex (прямой запуск):
```bash
dotnet run --project examples/ConsoleChat -- --model gpt-5-codex --auth codex
```

API key:
```bash
dotnet run --project examples/ConsoleChat -- --model gpt-5-mini --auth apikey --api-key <KEY>
```

Локальный endpoint без auth:
```bash
dotnet run --project examples/ConsoleChat -- --model llama3.1:8b --auth none --base-url http://localhost:11434/v1
```
Builder-style provider configuration:
```csharp
using MultiLlm.Core.Abstractions;
using MultiLlm.Providers.Codex;
using MultiLlm.Providers.Ollama;

var client = LlmClientBuilder.Create()
    .Configure(new CodexProviderOptions { ProviderId = "codex", Model = "gpt-5-codex" })
    .Configure(new OllamaProviderOptions { ProviderId = "ollama", Model = "llama3.1:8b" })
    .Build();
```


MCP demo:
```bash
dotnet run --project examples/McpDemo
```

## 11. Тесты

`MultiLlm.Core.Tests` покрывает:
- роутинг `LlmClient`,
- instruction layers,
- resilience pipeline,
- auth strategies,
- token store,
- Codex auth/backend slot,
- OpenAI-compatible mapper.

`MultiLlm.Integration.Tests` покрывает:
- OpenAI-compatible provider (sync + stream),
- кросс-провайдерные сценарии,
- MCP client end-to-end с mock server.

## 12. Ограничения

- Ollama native provider not implemented yet.
- Image processing extras пока на раннем этапе.
- Нет отдельного DI-пакета с extension methods для полной автоконфигурации.

## 13. Git/Codex push setup

Для глобальной и репозиторной настройки push/pull Codex CLI на Windows см.:
- `docs/codex-git-setup.md`

Критично:
- не коммитить `.git/.codex-credentials`,
- если PAT попал в логи или чат, немедленно отозвать и создать новый.

## Дополнительные документы

- Technical specification: `MultiLlm_TZ.ModelRouter.md`
- Codex workflow notes: `docs/codex-web-playbook.md`


