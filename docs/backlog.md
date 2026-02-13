# Декомпозиция ТЗ на задачи

## Эпик 1. Core-контракт и pipeline
1. Реализовать `LlmClient` и маршрутизацию запроса по `providerId/model`.
2. Реализовать нормализацию инструкций `system/developer/session/request` с единым приоритетом.
3. Реализовать redaction секретов в логах и событиях.
4. Добавить retry/backoff + timeout + rate limit базового уровня.
5. Покрыть unit-тестами контракты, приоритет инструкций и обработку ошибок.

## Эпик 2. Авторизация
1. Реализовать стратегии `NoAuth`, `ApiKeyAuth`, `BearerAuth`, `CustomHeadersAuth`.
2. Добавить `OAuthDeviceCodeAuth` + UX режимы `Interactive` и `Manual injection` через `ITokenStore`.
3. Подготовить безопасное хранилище токенов (in-memory + файловая dev-реализация).
4. Протестировать смену auth-конфига без изменения клиентского кода.

## Эпик 3. Провайдеры
1. OpenAI provider (официальный `openai-dotnet`) + stream.
2. OpenAI-compatible provider (`baseUrl + headers + model`) + stream.
3. Ollama native provider + stream.
4. Ollama OpenAI-compatible provider + stream.
5. Интеграционные тесты на общий `ChatRequest` для OpenAI-compatible и Ollama.

## Эпик 4. Codex dev-only
1. Реализовать `OfficialDeviceCodeBackend` (по официальной схеме).
2. Описать и зафиксировать контракты плагинной модели `ExperimentalAuthBackend`.
3. Добавить feature flag `EnableExperimentalAuthAdapters`.
4. Добавить guard rails, чтобы Codex-провайдер не использовался в prod-конфигурации.

## Эпик 5. Multimodal и файлы
1. Проверка прохождения `ImagePart` через core и минимум 2 провайдера.
2. Реализовать `FilePart` передачу в совместимых провайдерах.
3. В `MultiLlm.Extras.ImageProcessing` добавить resize/compress policy (png/jpg/webp).
4. Добавить тест-кейсы на ограничения размера изображений.

## Эпик 6. MCP tools
1. Интегрировать MCP C# SDK в `MultiLlm.Tools.Mcp`.
2. Реализовать подключение к MCP-серверу и получение списка tools.
3. Реализовать вызов tool и маппинг `ToolCallPart/ToolResultPart`.
4. Добавить пример `McpDemo` и интеграционный тест на 1 вызов tool.

## Эпик 7. Наблюдаемость и эксплуатация
1. События `start/end/error` с `requestId/correlationId`.
2. Опциональная интеграция OpenTelemetry.
3. (SHOULD) Record/Replay harness для регрессии.
4. (SHOULD) Кэширование и дедуп запросов по hash(prompt+attachments).

## Эпик 8. DX/релизный контур
1. Настроить CI (build + test + линтеры).
2. Добавить упаковку NuGet-пакетов по проектам.
3. Описать semver + release notes.
4. Дополнить примеры `ConsoleChat` сценариями для разных auth/provider.
