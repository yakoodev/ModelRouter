# Задача 10 — Codex dev-only auth slot

## Цель
Завершить dev-only интеграцию Codex с безопасным разделением dev/prod.

## Инструкция
- Реализовать `OfficialDeviceCodeBackend`.
- Оставить `ExperimentalAuthBackend` только как extension point под feature flag.
- Добавить guard rails: Codex запрещен в prod-конфигурации.
- Добавить unit-тесты на флаг и guard rails.
- После завершения задачи и мержа PR перенести этот файл из `docs/tasks/todo/` в `docs/tasks/done/`.

## Критерии приемки
- Dev/prod поведение явно разделено.
- Experimental backend выключен по умолчанию.
- `dotnet test MultiLlm.slnx` проходит.
