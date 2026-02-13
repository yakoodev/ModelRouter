# Декомпозиция ТЗ на задачи (Codex Web Ready)

Основной оперативный документ для работы агентом: `docs/codex-web-playbook.md`.

## Правила исполнения
- 1 задача = 1 PR = 1 законченный вертикальный срез.
- Каждая задача должна иметь явный Definition of Done.
- Без «пустых» заглушек в реализуемом срезе.
- Все изменения через `dotnet test MultiLlm.slnx`.

## Очередь задач

### Wave 1 — Core runtime
1. Core Router + `LlmClient` + тесты маршрутизации.
2. Нормализация instruction layers (`request > session > developer > system`) + тесты.
3. Execution pipeline (hooks/events/correlation ids) + тесты.

### Wave 2 — Auth
4. `NoAuth`, `ApiKeyAuth`, `BearerAuth`, `CustomHeadersAuth` + тесты.
5. `ITokenStore` (in-memory) + manual token injection + тесты.

### Wave 3 — Providers
6. OpenAI-compatible provider (chat + stream) + integration tests.
7. Ollama provider (native/compat) + integration tests на общий `ChatRequest`.

### Wave 4 — MCP + multimodal
8. MCP connect/list/call + demo + integration tests.
9. `ImagePart`/`FilePart` end-to-end минимум через 2 провайдера.

### Wave 5 — Codex dev-only + ops
10. Codex `OfficialDeviceCodeBackend` + feature flag для experimental.
11. Retry/backoff, timeout, rate limit, redaction + fault tests.

## Готовые промпты
См. раздел с шаблонами и готовыми prompt’ами в `docs/codex-web-playbook.md`.
