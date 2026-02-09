# Parameter Schema Future Upgrades

Date: 2026-02-09
Status: Deferred (implement on demand)

## Current State

The action parameter system already supports a unified JSON-like schema and runtime validation for:

- scalar types (`string`, `integer`, `number`, `boolean`, `null`)
- `object` with nested properties and required checks
- `array` with recursive item validation
- unknown field rejection

This is sufficient for current app count and interaction complexity.

## Deferred Enhancements

When real needs appear, extend schema and validator with:

1. Enum constraints
- string/number/integer allowed value set

2. Numeric constraints
- `minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum`

3. String constraints
- `minLength`, `maxLength`, `pattern`

4. Array constraints
- `minItems`, `maxItems`, unique item support

5. Object constraints
- explicit additional-properties policy
- schema-driven partial update rules

6. Better prompt projection
- richer compact schema string for nested object/array structures

## Implementation Notes

- Keep runtime parameter payload as `JsonElement` end-to-end.
- Keep validation centralized in one validator.
- Add constraints to schema model first, then validator, then app-side DSL helpers.
- Only add features with concrete product scenarios to avoid premature complexity.
