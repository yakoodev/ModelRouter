# Декомпозиция ТЗ на задачи (разбивка по файлам)

Общий backlog разбит на отдельные task-файлы в каталоге `docs/tasks/`.

## Структура
- `docs/tasks/todo/` — активные задачи.
- `docs/tasks/done/` — завершённые задачи.
- `docs/tasks/README.md` — правила жизненного цикла задачи.

## Правило
После выполнения задачи и мержа PR соответствующий файл нужно перенести из `docs/tasks/todo/` в `docs/tasks/done/`.

## Список задач
1. `docs/tasks/todo/01-core-router-llmclient.md`
2. `docs/tasks/todo/02-instruction-normalizer.md`
3. `docs/tasks/todo/03-execution-pipeline-hooks.md`
4. `docs/tasks/todo/04-auth-strategies-v1.md`
5. `docs/tasks/todo/05-token-store-manual-injection.md`
6. `docs/tasks/todo/06-openai-compatible-provider.md`
7. `docs/tasks/todo/07-ollama-provider.md`
8. `docs/tasks/todo/08-mcp-client-mvp.md`
9. `docs/tasks/todo/09-image-file-pipeline.md`
10. `docs/tasks/todo/10-codex-dev-only-auth-slot.md`
11. `docs/tasks/todo/11-resilience-ops.md`

## Промпты для агента
Шаблон и готовые prompt’ы: `docs/codex-web-playbook.md`.
