# 004 - DI Package and Auto-Configuration

## Goal
Provide first-class dependency injection integration for registering client, providers, and options.

## Scope
- Add extension methods for `IServiceCollection`.
- Support options-based configuration for core providers.
- Keep manual `LlmClientBuilder` flow intact.

## Acceptance Criteria
- New DI entry points register `ILlmClient` and configured providers.
- Providers can be configured from options without manual factory wiring.
- Misconfiguration fails fast with clear error messages.
- Unit tests cover registration and resolution scenarios.
- `README.md` and `README.en.md` include DI usage examples.
