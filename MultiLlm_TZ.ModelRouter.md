# Multi‑Provider LLM SDK (.NET 10) — Техническое задание
**Название репозитория/библиотеки (рабочее): `ModelRouter`**

> Единая кроссплатформенная библиотека (NuGet) для работы с LLM/мультимодальностью и tools (через MCP).  
> В **dev** — возможность использовать Codex через **device code / ChatGPT sign‑in** (*dev‑only*).  
> В **prod** — переключение на **OpenAI API / OpenAI‑compatible (vLLM/SGLang) / Ollama**.

---

## Навигация
- [0. Цель](#0-цель)
- [1. Нефункциональные требования](#1-нефункциональные-требования)
- [2. Основные требования (MUST)](#2-основные-требования-must)
  - [2.1 Провайдеры](#21-провайдеры)
  - [2.2 Авторизация](#22-авторизация)
  - [2.3 Единый контракт общения](#23-единый-контракт-общения)
  - [2.4 Инструкции (слои)](#24-инструкции-слои)
  - [2.5 Мультимодальность](#25-мультимодальность)
  - [2.6 Tools (MCP)](#26-tools-mcp)
- [3. Доп. требования (MUST/SHOULD)](#3-доп-требования-mustshould)
- [4. Архитектура](#4-архитектура)
  - [4.1 Проекты / NuGet‑пакеты](#41-проекты--nugetпакеты)
  - [4.2 Основные интерфейсы](#42-основные-интерфейсы)
  - [4.3 Codex auth slot](#43-codex-auth-slot)
- [5. Acceptance Criteria (приёмка)](#5-acceptance-criteria-приёмка)
- [6. Рекомендуемая структура репозитория](#6-рекомендуемая-структура-репозитория)
- [A. Приложение: инициализация репозитория на GitHub](#a-приложение-инициализация-репозитория-на-github)
- [B. Приложение: быстрый старт по .NET CLI](#b-приложение-быстрый-старт-по-net-cli)

---

## 0) Цель
Сделать кроссплатформенную библиотеку для **.NET 10** (NuGet), которая даёт единый API для работы с:
- **LLM** (чат/инструкции/стриминг),
- **мультимодальностью** (в первую очередь картинки),
- **tools** через **MCP**,

и позволяет:
- в **dev** использовать Codex через **device code / ChatGPT sign‑in** (*dev-only*),
- в **prod** безболезненно переключаться на OpenAI API / OpenAI‑compatible (vLLM/SGLang) / Ollama.

---

## 1) Нефункциональные требования
- **Target framework**: `net10.0`
- **OS**: Windows / Linux / macOS (кроссплатформенно)
- **Доставка**: набор NuGet‑пакетов (core + провайдеры + MCP + optional extras)
- **DI-friendly**: всё регистрируется через `Microsoft.Extensions.DependencyInjection`
- **Безопасность**:
  - секреты (ключи/токены) **никогда** не пишутся в логи,
  - обязательный **redaction** (маскирование).

---

## 2) Основные требования (MUST)

### 2.1 Провайдеры
1) **OpenAI API** — через официальный `openai-dotnet`.  
2) **OpenAI‑compatible endpoints** — универсальный провайдер (`baseUrl + headers + model`) под vLLM/SGLang и прочие совместимые API.  
3) **Ollama** — два режима:
   - `OllamaNativeProvider` (нативный API),
   - `OllamaOpenAiCompatProvider` (через OpenAI‑compat интерфейс Ollama).
4) **Codex (dev-only)** — провайдер с device code авторизацией (ChatGPT sign‑in).

> Примечание: Codex‑провайдер **строго dev-only** (см. [4.3 Codex auth slot](#43-codex-auth-slot)).

### 2.2 Авторизация
Поддержать стратегии:
- `NoAuth` (локальные эндпойнты)
- `ApiKeyAuth`
- `BearerAuth`
- `CustomHeadersAuth` (любой набор заголовков)
- `OAuthDeviceCodeAuth` (для Codex dev-only)

**Два UX‑режима для dev‑авторизации:**
1) **Interactive**: библиотека отдаёт `verification_uri` + `user_code`, UI показывает человеку.
2) **Manual injection**: библиотека позволяет положить **уже полученные** токены в `ITokenStore` (без описания способа добычи).

### 2.3 Единый контракт общения
- `ChatRequest` / `ChatResponse`
- `Message { role, parts[] }`
- `Part`:
  - `TextPart`
  - `ImagePart`
  - `FilePart`
  - `ToolCallPart`
  - `ToolResultPart`
- Streaming:
  - `IAsyncEnumerable<ChatDelta>`
  - + `CancellationToken`

### 2.4 Инструкции (слои)
Только 4 слоя:
- `system`
- `developer`
- `session`
- `request`

Приоритет (сверху важнее):
`request > session > developer > system`

### 2.5 Мультимодальность
- Приоритет: **картинки** (`png/jpg/webp`)
- Файлы: произвольные вложения как `FilePart` (`mime + filename + stream`)
- Опция **автосжатия/ресайза** картинок (кроссплатформенно, через optional пакет)

### 2.6 Tools (MCP)
MVP: **MCP**
- подключение к MCP‑серверам,
- получение списка tools,
- вызов tool.

---

## 3) Доп. требования (MUST/SHOULD)

### MUST
- Retry/backoff, timeouts, rate limiting (минимально достаточные)
- Hooks событий: `start / end / error` + `requestId / correlationId`
- Redaction секретов

### SHOULD (опционально)
- OpenTelemetry (traces/metrics)
- Record/Replay для регрессии
- Кэширование (hash `prompt+attachments`) и дедуп запросов

---

## 4) Архитектура

### 4.1 Проекты / NuGet‑пакеты
- `MultiLlm.Core`
- `MultiLlm.Providers.OpenAI`
- `MultiLlm.Providers.OpenAICompatible`
- `MultiLlm.Providers.Ollama`
- `MultiLlm.Providers.Codex` *(dev-only)*
- `MultiLlm.Tools.Mcp`
- `MultiLlm.Extras.ImageProcessing` *(optional)*

### 4.2 Основные интерфейсы

**Core**
- `ILlmClient`
  - `Task<ChatResponse> ChatAsync(ChatRequest, CancellationToken)`
  - `IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest, CancellationToken)`

**Providers**
- `IModelProvider`
  - `ProviderId`
  - `Capabilities`
  - `ChatAsync(...)`
  - `ChatStreamAsync(...)`

**Auth**
- `IAuthStrategy`
- `ITokenStore`

**MCP**
- `IMcpClient`
- `IMcpToolProvider` *(обёртка над MCP C# SDK)*

### 4.3 Codex auth slot
В `MultiLlm.Providers.Codex` предусмотреть 2 “движка”:

1) `OfficialDeviceCodeBackend`  
   Для будущей/официальной third‑party схемы (или доступных параметров) — device flow по документации.

2) `ExperimentalAuthBackend`  
   Подключаемый адаптер (плагин/assembly), **выключен по умолчанию**, включается флагом:
   - `EnableExperimentalAuthAdapters=true`

**Важно:**  
- **Без описания реализации** экспериментального бэкенда.  
- Только точка расширения и границы ответственности.

---

## 5) Acceptance Criteria (приёмка)
1) Один и тот же `ChatRequest` работает на:
   - OpenAI API,
   - OpenAI‑compatible (vLLM),
   - Ollama (native или compat).
2) Авторизация переключается конфигом: `none / apiKey / bearer / oauth`.
3) `system/developer/session/request` влияет на ответ на всех провайдерах одинаково.
4) `ImagePart` проходит через Core и минимум через **2** провайдера.
5) MCP:
   - можно подключить MCP‑сервер,
   - получить tools,
   - вызвать один tool.

---

## 6) Рекомендуемая структура репозитория
```text
/src
  /MultiLlm.Core
  /MultiLlm.Providers.OpenAI
  /MultiLlm.Providers.OpenAICompatible
  /MultiLlm.Providers.Ollama
  /MultiLlm.Providers.Codex
  /MultiLlm.Tools.Mcp
  /MultiLlm.Extras.ImageProcessing
/examples
  /ConsoleChat
  /McpDemo
/tests
  /MultiLlm.Core.Tests
  /MultiLlm.Integration.Tests
```

---

## A) Приложение: инициализация репозитория на GitHub

### Вариант A: создать репо на GitHub (web), потом пушнуть
1) На GitHub: **New repository** → имя → (опционально) README/.gitignore/license.
2) Локально:
```bash
git init
git add .
git commit -m "init"
git branch -M main
git remote add origin https://github.com/OWNER/REPO.git
git push -u origin main
```

### Вариант B: “пуш существующего репозитория”
Если GitHub‑репо уже создано и пустое — те же команды `remote add` + `push -u`.

---

## B) Приложение: быстрый старт по .NET CLI (скелет)
Минимальный каркас:
```bash
dotnet new sln -n MultiLlm
dotnet new classlib -n MultiLlm.Core -o src/MultiLlm.Core
dotnet sln MultiLlm.sln add src/MultiLlm.Core/MultiLlm.Core.csproj
```