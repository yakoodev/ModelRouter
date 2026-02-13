# Задача 06 — OpenAI-compatible provider

## Цель
Сделать первый рабочий production-провайдер с поддержкой chat и stream.

## Инструкция
- Реализовать `ChatAsync` и `ChatStreamAsync` в `MultiLlm.Providers.OpenAICompatible`.
- Поддержать конфиг `baseUrl + headers + model + timeout`.
- Добавить unit-тесты маппинга и integration-тесты через mock endpoint.
- После завершения задачи и мержа PR перенести этот файл из `docs/tasks/todo/` в `docs/tasks/done/`.

## Критерии приемки
- Один и тот же `ChatRequest` проходит через core -> provider.
- Streaming работает через единый контракт `ChatDelta`.
- `dotnet test MultiLlm.slnx` проходит.
