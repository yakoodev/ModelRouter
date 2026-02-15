# 003 - Image and File Pipeline Hardening

## Goal
Move image/file handling from basic support to production-ready behavior.

## Scope
- Implement concrete image preprocessing in `MultiLlm.Extras.ImageProcessing`.
- Define and enforce attachment size/format constraints.
- Ensure provider payload mapping is consistent for `ImagePart` and `FilePart`.

## Acceptance Criteria
- `IImagePreprocessor` has at least one concrete implementation used by examples or providers.
- Image resize/compression behavior is deterministic and covered by tests.
- Oversized/unsupported attachments fail with clear, redacted errors.
- End-to-end tests verify `ImagePart` through at least two providers.
- End-to-end tests verify `FilePart` through at least one provider.
- `examples/ConsoleChat` help text documents attachment constraints.
