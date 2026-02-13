# Codex Web Playbook для ModelRouter

Документ адаптирует backlog под режим «разработка через агента»:
- маленькие PR,
- строгий Definition of Done,
- единые промпты,
- архитектурные guard rails.

## 1) Принципы работы

1. **1 PR = 1 законченный вертикальный срез**
   - не «скелет», а рабочая функциональность + тесты + документация.
2. **Single Source of Truth**
   - публичные контракты только в `MultiLlm.Core`.
3. **Provider-agnostic core**
   - провайдеры не дублируют доменные модели.
4. **Стабильность при частых правках**
   - все cross-cutting concerns (retry, timeout, redaction, hooks) живут в общем pipeline.
5. **Готовность к расширению**
   - новые auth/provider/tool integrations добавляются через интерфейсы и options.

## 2) Gate на каждый PR

Перед merge PR считается готовым только если:

- [ ] реализована заявленная бизнес-функция;
- [ ] добавлены/обновлены тесты;
- [ ] зелёные проверки `dotnet test MultiLlm.slnx`;
- [ ] документация обновлена (минимум changelog в PR и notes в docs);
- [ ] нет заглушек `NotImplementedException` в изменённом вертикальном срезе;
- [ ] нет утечек секретов в логах.

## 3) Очередь задач (Codex-ready)

> Источник задач: `docs/tasks/todo/`. После завершения и merge файл задачи переносится в `docs/tasks/done/`.


> Формат: каждая задача — отдельный PR с конкретным DoD.

### Wave 1 — Core runtime
1. **Core Router + LlmClient**
   - Реализовать runtime-маршрутизацию по `providerId/model`.
   - DoD: unit tests на маршрутизацию, ошибки unknown provider, cancellation.

2. **Instruction normalizer**
   - Единая сборка `system/developer/session/request` в приоритете ТЗ.
   - DoD: тесты на порядок и детерминизм маппинга.

3. **Execution pipeline**
   - Hooks (`start/end/error`) + `requestId/correlationId`.
   - DoD: тесты на вызовы hook в success/fail сценариях.

### Wave 2 — Auth
4. **Auth strategies v1**
   - `NoAuth`, `ApiKeyAuth`, `BearerAuth`, `CustomHeadersAuth`.
   - DoD: unit tests на заголовки/поведение.

5. **Token store + manual injection**
   - In-memory `ITokenStore`, API ручной подкладки токенов.
   - DoD: тесты на set/get/expiry.

### Wave 3 — Первый рабочий провайдер
6. **OpenAI-compatible provider (production backbone)**
   - Реализовать `baseUrl + headers + model`, chat + stream.
   - DoD: integration tests с тестовым endpoint/mocks.

7. **Ollama provider**
   - Native или compat (с последующим добором второго режима).
   - DoD: общий `ChatRequest` проходит как минимум для 2 провайдеров.

### Wave 4 — MCP и multimodal
8. **MCP client MVP**
   - connect → list tools → call tool.
   - DoD: интеграционный сценарий на 1 tool call.

9. **Image/File pipeline**
   - `ImagePart` и `FilePart` end-to-end минимум в 2 провайдерах.
   - DoD: integration tests + ограничения размеров.

### Wave 5 — Codex dev-only и эксплуатация
10. **Codex dev-only auth slot**
    - `OfficialDeviceCodeBackend`, feature flag для experimental adapter.
    - DoD: test guards (запрет prod-конфига).

11. **Resilience/ops**
    - retry/backoff, timeout, rate limit, redaction.
    - DoD: fault-injection tests.

## 4) Базовый промпт для любого PR (шаблон)

```text
Ты работаешь в репозитории ModelRouter.

Цель PR:
<кратко: одна законченная функциональность>

Контекст:
- Следуй ТЗ из MultiLlm_TZ.ModelRouter.md.
- Следуй архитектуре: контракты в MultiLlm.Core, провайдеры тонкие.

Сделай:
1) Реализуй функциональность.
2) Добавь/обнови тесты.
3) Обнови docs (кратко, по делу).

Definition of Done:
- Нет NotImplementedException в изменяемом потоке.
- dotnet test MultiLlm.slnx — green.
- API расширяемое (через interfaces/options), без дублирования моделей.

Ограничения:
- Не трогай unrelated файлы.
- Не ломай существующие публичные контракты без явной причины.

В конце:
- Покажи Summary изменений.
- Перечисли команды проверок и результат.
- Сделай git commit и подготовь PR title/body.
```

## 5) Готовые промпты для ближайших PR

### Prompt A — Core Router + LlmClient

```text
Реализуй PR: Core Router + LlmClient.

Нужно:
- Добавить рабочую реализацию ILlmClient в MultiLlm.Core.
- Поддержать маршрутизацию по providerId/model через реестр провайдеров.
- Добавить единый RequestContext (requestId/correlationId).
- Добавить unit tests:
  1) успешная маршрутизация,
  2) unknown provider -> контролируемая ошибка,
  3) cancellation token корректно прокидывается.

DoD:
- Код компилируется.
- dotnet test MultiLlm.slnx зелёный.
- Нет заглушек в затронутом runtime-пути.
- Изменения минимальные и расширяемые.
```

### Prompt B — Auth strategies v1

```text
Реализуй PR: Auth strategies v1.

Нужно в MultiLlm.Core/Auth:
- Реализовать NoAuth, ApiKeyAuth, BearerAuth, CustomHeadersAuth.
- Сделать единообразное применение к HttpRequestMessage.
- Добавить redaction helper для секретных значений в диагностике.

Тесты:
- Проверить формирование заголовков для каждой стратегии.
- Проверить что redaction скрывает секреты.

DoD:
- dotnet test MultiLlm.slnx зелёный.
- Публичный API компактный и расширяемый.
```

### Prompt C — OpenAI-compatible provider

```text
Реализуй PR: OpenAI-compatible provider (первый рабочий провайдер).

Нужно:
- В MultiLlm.Providers.OpenAICompatible реализовать ChatAsync и ChatStreamAsync.
- Поддержать конфиг: baseUrl, model, auth headers, timeout.
- Реализовать маппинг из ChatRequest/MessagePart в совместимый формат запроса.

Тесты:
- Unit-тесты маппинга.
- Integration-тест через mock HTTP endpoint.

DoD:
- Один и тот же ChatRequest проходит через core -> provider.
- dotnet test MultiLlm.slnx зелёный.
```

### Prompt D — MCP MVP

```text
Реализуй PR: MCP MVP.

Нужно:
- В MultiLlm.Tools.Mcp реализовать клиент: подключение, список tools, вызов tool.
- Добавить адаптер в core для ToolCallPart/ToolResultPart.
- Обновить examples/McpDemo для демонстрации 1 tool call.

Тесты:
- Integration test: connect + list + call.

DoD:
- Сценарий воспроизводим локально.
- dotnet test MultiLlm.slnx зелёный.
```

### Prompt E — Codex dev-only slot

```text
Реализуй PR: Codex dev-only auth slot.

Нужно:
- Реализовать OfficialDeviceCodeBackend (без experimental логики).
- Оставить ExperimentalAuthBackend как extension point behind feature flag.
- Добавить guard rails: в prod конфиге Codex provider запрещён.

Тесты:
- Unit tests на флаг и guard rails.

DoD:
- Поведение dev/prod явно разделено.
- dotnet test MultiLlm.slnx зелёный.
```

## 6) Режим работы с большими правками

Если задача большая, сначала запускай «планирующий промпт», потом имплементацию:

```text
Сначала составь пошаговый план реализации (5-9 шагов) для задачи <...>.
Не меняй код, пока не покажешь план и риски.
После согласования плана приступай к реализации в рамках одного PR.
```

Это снижает риск архитектурных отклонений при длинных сессиях агента.
