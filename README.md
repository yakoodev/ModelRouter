# ModelRouter / MultiLlm

Единая .NET-библиотека для маршрутизации чатовых запросов к нескольким LLM-провайдерам через общий контракт:
- OpenAI-compatible endpoints (включая локальные и self-hosted),
- Ollama через OpenAI-compatible интерфейс,
- Codex (dev-only, через `codex login` и `~/.codex/auth.json`),
- MCP tools клиент по stdio.

Проект таргетирует `net10.0` и построен модульно: `Core` + отдельные provider-пакеты + tooling.

## Содержание
- [1. Текущий статус реализации](#1-текущий-статус-реализации)
- [2. Архитектура](#2-архитектура)
- [3. Структура репозитория](#3-структура-репозитория)
- [4. Быстрый старт](#4-быстрый-старт)
- [5. Базовое использование API](#5-базовое-использование-api)
- [6. Провайдеры и режимы авторизации](#6-провайдеры-и-режимы-авторизации)
- [7. Контракты Core](#7-контракты-core)
- [8. Устойчивость, лимиты и hooks](#8-устойчивость-лимиты-и-hooks)
- [9. MCP tools](#9-mcp-tools)
- [10. Примеры](#10-примеры)
- [11. Тестирование](#11-тестирование)
- [12. Ограничения и roadmap](#12-ограничения-и-roadmap)

## 1. Текущий статус реализации

### Реализовано
- `MultiLlm.Core`:
  - единые контракты (`ChatRequest`, `ChatResponse`, `ChatDelta`, `Message`, `MessagePart`),
  - роутинг по модели формата `providerId/model`,
  - retry/backoff/timeout/concurrency/rate-delay в `LlmClient`,
  - instruction layers (`system`, `developer`, `session`, `request`) с нормализацией,
  - hooks (`OnStart`, `OnEnd`, `OnError`) и редактирование секретов в ошибках.
- `MultiLlm.Providers.OpenAICompatible`:
  - sync chat (`/chat/completions`),
  - streaming через SSE (`data: ...`, `[DONE]`),
  - маппинг message parts в OpenAI-compatible payload.
- `MultiLlm.Providers.Codex`:
  - dev-only runtime guard,
  - backend slot для auth (`official-device-code` + optional experimental),
  - чтение токена/API key из `auth.json` Codex CLI,
  - ChatGPT backend адаптер.
- `MultiLlm.Providers.Ollama`:
  - `OllamaOpenAiCompatProvider` как обертка над OpenAI-compatible провайдером.
- `MultiLlm.Tools.Mcp`:
  - stdio JSON-RPC клиент,
  - `initialize`, `tools/list`, `tools/call`.
- Примеры:
  - `examples/ConsoleChat` (interactive chat),
  - `examples/McpDemo` (поднятие mock MCP server + вызов tool).

### Пока заглушки / не завершено
- `MultiLlm.Providers.OpenAI.OpenAiProvider` выбрасывает `NotImplementedException`.
- `MultiLlm.Providers.Ollama.OllamaNativeProvider` выбрасывает `NotImplementedException`.
- `MultiLlm.Extras.ImageProcessing` содержит контракт, но без полноценной реализации пайплайна.

## 2. Архитектура

Ключевая идея: приложение работает только с `ILlmClient`, а конкретный backend выбирается через префикс в `ChatRequest.Model`.

Пример:
- `openai-compatible/gpt-4.1-mini`
- `codex/gpt-5-mini`
- `ollama-openai-compat/llama3.1:8b`

Внутри `LlmClient`:
- из `providerId/model` выделяется `providerId`,
- запрашивается нужный `IModelProvider`,
- `InstructionNormalizer` добавляет инструкции в начало `Messages`,
- применяется resilience pipeline,
- на выход проставляются `ProviderId`, `Model`, `RequestId`, `CorrelationId`.

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
```

## 4. Быстрый старт

### Требования
- .NET SDK с поддержкой `net10.0`.

### Сборка

```bash
dotnet build MultiLlm.slnx
```

### Запуск тестов

```bash
dotnet test MultiLlm.slnx
```

## 5. Базовое использование API

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
        new Message(MessageRole.User, [new TextPart("Сделай краткое резюме текста...")])
    ]);

var response = await client.ChatAsync(request);
var text = string.Concat(response.Message.Parts.OfType<TextPart>().Select(x => x.Text));
Console.WriteLine(text);
```

Для stream-режима используйте `await foreach` по `client.ChatStreamAsync(request)`.

## 6. Провайдеры и режимы авторизации

### OpenAI-compatible (`MultiLlm.Providers.OpenAICompatible`)
- Реальный рабочий HTTP-провайдер к `POST {baseUrl}/chat/completions`.
- Авторизация передается через `Headers` (например, `Authorization: Bearer ...`).

### Codex (`MultiLlm.Providers.Codex`, dev-only)
- Предназначен для development-сценариев.
- По умолчанию использует `OfficialDeviceCodeBackend`, который читает:
  - `OPENAI_API_KEY`, либо
  - `tokens.access_token` из `CODEX_HOME/auth.json` (или `~/.codex/auth.json`).
- При `IsDevelopment = false` выбрасывается исключение (runtime guard).
- `EnableExperimentalAuthAdapters` включает дополнительный backend-слот, если backend зарегистрирован.

### Ollama
- `OllamaOpenAiCompatProvider` работает через совместимый OpenAI endpoint Ollama.
- `OllamaNativeProvider` пока не реализован.

### OpenAI
- `OpenAiProvider` пока не реализован (планируется адаптер поверх official SDK).

### Доступные auth-стратегии в Core
- `NoAuth`
- `ApiKeyAuth`
- `BearerAuth`
- `CustomHeadersAuth`
- `ITokenStore` + `InMemoryTokenStore` для хранения токенов/refresh token.

## 7. Контракты Core

### Основные интерфейсы
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
  - `Model` в формате `providerId/model`,
  - `Messages`,
  - `Instructions`,
  - опционально `RequestId`, `CorrelationId`.
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
- `InstructionLayers` поддерживает 4 слоя: `system`, `developer`, `session`, `request`.
- При нормализации добавляются префиксные сообщения с приоритетом:
  - `request` > `session` > `developer` > `system`.

## 8. Устойчивость, лимиты и hooks

Параметры `LlmClientResilienceOptions`:
- `MaxRetries` (default `2`)
- `InitialBackoff` / `MaxBackoff` / `UseJitter`
- `RequestTimeout`
- `MaxConcurrentRequests`
- `MinDelayBetweenRequests`
- `ShouldRetry` (кастомный predicate)

События `ILlmEventHook`:
- `OnStartAsync(requestId, correlationId, ...)`
- `OnEndAsync(requestId, correlationId, ...)`
- `OnErrorAsync(requestId, correlationId, exception, ...)`

Безопасность:
- `SecretRedactor` маскирует bearer/api key/token/secret в текстах и исключениях.

## 9. MCP tools

`MultiLlm.Tools.Mcp` содержит `StdioMcpClient`:
- запускает MCP server как subprocess (`Command` + `Arguments`),
- выполняет handshake (`initialize` + `notifications/initialized`),
- дает API:
  - `GetToolsAsync()`
  - `CallToolAsync(toolName, argumentsJson)`.

Опции подключения:
- `Command`, `Arguments`, `WorkingDirectory`,
- `EnvironmentVariables`,
- `RequestTimeout`,
- `ClientName`, `ClientVersion`, `ProtocolVersion`.

## 10. Примеры

### ConsoleChat (Codex auth)
```bash
dotnet run --project examples/ConsoleChat -- --model gpt-5-codex --auth codex
```

### ConsoleChat (API key)
```bash
dotnet run --project examples/ConsoleChat -- --model gpt-5-mini --auth apikey --api-key <KEY>
```

### ConsoleChat (без auth, для локального endpoint)
```bash
dotnet run --project examples/ConsoleChat -- --model llama3.1:8b --auth none --base-url http://localhost:11434/v1
```

### McpDemo
```bash
dotnet run --project examples/McpDemo
```

## 11. Тестирование

В проекте есть unit и integration тесты (xUnit):
- `MultiLlm.Core.Tests`:
  - роутинг и поведение `LlmClient`,
  - instruction layers,
  - resilience pipeline,
  - auth strategies,
  - token store,
  - Codex auth/backend slot,
  - mapper OpenAI-compatible.
- `MultiLlm.Integration.Tests`:
  - OpenAI-compatible provider (sync + stream),
  - кросс-провайдерные сценарии,
  - MCP client end-to-end через mock server.

## 12. Ограничения и roadmap

### Ограничения на текущем этапе
- OpenAI official provider и Ollama native provider пока не реализованы.
- Обработка изображений вынесена в отдельный модуль, но находится на ранней стадии.
- DI-расширения для автоконфигурации провайдеров пока не вынесены в отдельные extension methods.

### Ближайшие шаги
- Реализовать `OpenAiProvider` на official SDK.
- Реализовать `OllamaNativeProvider`.
- Расширить интеграцию image/file multimodal flow.
- Добавить publish/packaging workflow для NuGet-пакетов.

## Дополнительные документы
- Техническое задание: `MultiLlm_TZ.ModelRouter.md`
- Процесс разработки: `docs/codex-web-playbook.md`
