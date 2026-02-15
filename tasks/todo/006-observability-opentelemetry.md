# 006 - Observability (OpenTelemetry)

## Goal
Expose standard traces/metrics for request lifecycle across providers.

## Scope
- Instrument `LlmClient` pipeline and provider calls.
- Emit attributes for provider id, model, latency, retry count, and outcome.
- Keep secret redaction guarantees in diagnostic payloads.

## Acceptance Criteria
- OpenTelemetry instrumentation can be enabled without breaking existing API.
- At least one trace span is emitted per chat request.
- Metrics include request count, latency, and error count.
- Tests verify instrumentation hooks are called and do not leak secrets.
- Docs include setup example with `OpenTelemetry` SDK.
