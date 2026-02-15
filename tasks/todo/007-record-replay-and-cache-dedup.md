# 007 - Record/Replay and Cache Dedup

## Goal
Reduce regression risk and repeated token usage with optional record/replay and request deduplication.

## Scope
- Add optional record/replay layer for deterministic regression tests.
- Add cache key strategy based on prompt + attachments hash.
- Add in-flight deduplication for identical concurrent requests.

## Acceptance Criteria
- Record/replay mode can run integration tests without live provider dependency.
- Cache and dedup are opt-in and do not change default behavior.
- Cache key generation is deterministic and collision-safe for supported inputs.
- Tests cover cache hit/miss, in-flight dedup, and replay correctness.
- Docs describe operational trade-offs and when to enable each feature.
