# Задача 08 — MCP client MVP

## Цель
Реализовать минимально рабочую интеграцию tools через MCP.

## Инструкция
- Реализовать подключение к MCP-серверу.
- Реализовать `list tools` и `call tool`.
- Обновить `examples/McpDemo` под рабочий сценарий 1 tool call.
- Добавить integration-тест на `connect -> list -> call`.
- После завершения задачи и мержа PR перенести этот файл из `docs/tasks/todo/` в `docs/tasks/done/`.

## Критерии приемки
- MCP-сценарий воспроизводим локально.
- `ToolCallPart/ToolResultPart` корректно маппятся.
- `dotnet test MultiLlm.slnx` проходит.
