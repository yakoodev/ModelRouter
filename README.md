# ModelRouter / MultiLlm

Стартовая инициализация репозитория под ТЗ `MultiLlm_TZ.ModelRouter.md`.

## Что уже сделано
- Создано solution `MultiLlm.slnx`.
- Поднят каркас проектов из ТЗ:
  - `src/MultiLlm.Core`
  - `src/MultiLlm.Providers.OpenAI`
  - `src/MultiLlm.Providers.OpenAICompatible`
  - `src/MultiLlm.Providers.Ollama`
  - `src/MultiLlm.Providers.Codex`
  - `src/MultiLlm.Tools.Mcp`
  - `src/MultiLlm.Extras.ImageProcessing`
  - `examples/ConsoleChat`
  - `examples/McpDemo`
  - `tests/MultiLlm.Core.Tests`
  - `tests/MultiLlm.Integration.Tests`
- В `MultiLlm.Core` добавлены базовые контракты (`ChatRequest/ChatResponse/ChatDelta`, message parts, instruction layers, auth, hooks).
- В provider-проектах добавлены расширяемые заглушки для реализаций.

## Архитектурные принципы
- Единый контракт в `MultiLlm.Core` — чтобы изменения происходили в одном месте.
- Провайдеры как отдельные пакеты/модули — для независимой эволюции.
- Codex dev-only вынесен в собственный проект с точкой расширения для auth backend.
- Optional возможности (image processing) — отдельным extras-пакетом.

## Следующие шаги
- Декомпозиция задач: `docs/backlog.md`
- Операционный процесс под Codex Web + готовые prompt-шаблоны: `docs/codex-web-playbook.md`
