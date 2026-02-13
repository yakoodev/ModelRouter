# Задача 01 — Core Router + LlmClient

## Цель
Реализовать рабочий runtime-маршрутизатор и `LlmClient` в `MultiLlm.Core` как основу для всех провайдеров.

## Инструкция
- Реализовать `ILlmClient` с маршрутизацией по `providerId/model` через реестр провайдеров.
- Добавить `requestId/correlationId` в pipeline выполнения.
- Покрыть unit-тестами маршрутизацию, unknown provider и cancellation.
- После завершения задачи и мержа PR перенести этот файл из `docs/tasks/todo/` в `docs/tasks/done/`.

## Критерии приемки
- `ILlmClient` выполняет корректную маршрутизацию.
- Unknown provider возвращает контролируемую ошибку.
- Cancellation корректно прокидывается.
- `dotnet test MultiLlm.slnx` проходит.
