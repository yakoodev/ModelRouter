# Задача 03 — Execution pipeline (hooks/events)

## Цель
Добавить наблюдаемость и единое событие выполнения запроса в core.

## Инструкция
- Реализовать вызовы hooks `start/end/error`.
- Передавать `requestId/correlationId` в каждое событие.
- Добавить тесты на success/failure сценарии.
- После завершения задачи и мержа PR перенести этот файл из `docs/tasks/todo/` в `docs/tasks/done/`.

## Критерии приемки
- Hooks вызываются в корректном порядке.
- В `error` передается исходная ошибка.
- `dotnet test MultiLlm.slnx` проходит.
