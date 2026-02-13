# Задача 11 — Resilience/ops

## Цель
Добавить эксплуатационную надежность и безопасную диагностику.

## Инструкция
- Реализовать retry/backoff, timeout и rate limit в общем pipeline.
- Реализовать redaction секретов в логах/событиях.
- Добавить fault-injection тесты на отказоустойчивость.
- После завершения задачи и мержа PR перенести этот файл из `docs/tasks/todo/` в `docs/tasks/done/`.

## Критерии приемки
- Retry/timeout/rate limit работают предсказуемо.
- Секреты не протекают в диагностику.
- `dotnet test MultiLlm.slnx` проходит.
