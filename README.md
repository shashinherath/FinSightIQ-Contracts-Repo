# FinSightIQ.Contracts

Cross-service event contracts for the FinSightIQ platform. Distributed as the private NuGet package `FinSightIQ.Contracts` on GitHub Packages, with JSON Schemas as the language-neutral description of each payload.

- **Target framework:** `net8.0`
- **Package dependencies:** none (uses `System.Text.Json` from the framework)
- **Current version:** `0.1.0` (pre-1.0)

This library contains only data shapes. It has no business logic, persistence, HTTP or RabbitMQ code, and no event-framework abstractions. Per the architecture, events are the only types permitted to cross a service boundary (R-01, §5.2).

## Event catalogue

| Event | Routing key | Publisher | Consumers | Required fields | Schema |
|---|---|---|---|---|---|
| `PriceUpdated` | `market.price.updated` | Market Data Service | Alerting, cache invalidation, portfolio valuation | `symbol`, `price`, `volume`, `timestamp` | `schemas/price-updated.v1.json` |
| `PredictionGenerated` | `prediction.generated` | Prediction Service | Notification, analytics | `predictionId`, `symbol`, `confidence`, `recommendation` | `schemas/prediction-generated.v1.json` |
| `SentimentShifted` | `sentiment.score.shifted` | Sentiment Service | Alerting, retraining trigger | `symbol`, `score`, `previousScore`, `date` | `schemas/sentiment-shifted.v1.json` |

Not implemented (documented in the architecture but outside this scope): `DrawdownDetected`, `UserRegistered`.

Namespace: `FinSightIQ.Contracts.Events`. Each event is a `sealed record` with `required init` properties. Optional fields are nullable and omitted from JSON when null.

### Serialization rules

- Property names are camelCase, fixed with `[JsonPropertyName]`, so they do not depend on the caller's serializer options.
- Prices, bounds and scores are `decimal` (JSON numbers); volume is `long`.
- Timestamps are `DateTimeOffset` and **must be UTC**. Publishers must convert with `ToUniversalTime()`; the schema rejects non-UTC offsets. Dates (`date`, `targetDate`) are `DateOnly` serialized as `yyyy-MM-dd`.
- Consumers must ignore unknown fields (the default of `System.Text.Json`). Schemas set `additionalProperties: false` to describe what producers may emit today and to keep C# and schema in lockstep; they are not meant to make consumers reject newer producers.
- Delivery is at-least-once, so consumers must be idempotent.

### Documented decisions and assumptions

- **`PriceUpdated`**: the architecture lists `symbol, price, volume, timestamp`. Those are required, and `price` is the bar's close. `open`, `high` and `low` (present in `stock_prices`) are **optional additive** fields. Open decision: whether OHLC should become required in a future major version.
- **`PredictionGenerated`**: the architecture lists `predictionId, symbol, confidence, recommendation`. Forecast (`predictedPrice`, `trend`, `forecastDays`, `targetDate`), interval (`lowerBound`, `upperBound`), model metadata (`modelUsed`, `modelVersion`, `degraded`, `generatedAt`) and explainability (`topFeatures`) use the `POST /predictions` field names and are optional. Trend, recommendation and model are strings (values enforced by the schema, taken from the DB `CHECK` constraints) so a new value never breaks C# deserialization.
- **`SentimentShifted`**: exactly the documented fields. The ±0.3 threshold is publisher logic and is not encoded. Score bounds (−1 to 1) come from `sentiment_scores.daily_score`.
- **No event id / correlation id** is included because none is documented. Idempotency currently relies on natural keys (`symbol`+`timestamp`, `predictionId`, `symbol`+`date`). Adding an optional `eventId` would be a minor version.
- Symbol length (≤ 10) follows `VARCHAR(10)` in the database design.
- Schema bounds that are **assumptions, not stated in the documents**: prices, bounds and `predictedPrice` must be `> 0`; `volume` must be `>= 0`. The cross-field rule `lowerBound <= predictedPrice <= upperBound` (DB `ck_pred_bounds`) is not expressible in the schema and is not validated here.

## Repository structure

```
src/FinSightIQ.Contracts/Events/   C# contracts
schemas/                           JSON Schema (draft 2020-12), one file per event and major version
tests/FinSightIQ.Contracts.Tests/  xUnit tests
.github/workflows/                 ci.yml, publish.yml
global.json  nuget.config  .editorconfig
```

## Prerequisites

.NET 8 SDK (pinned in `global.json` to `8.0.408`, rolling forward to later 8.0.x feature bands).

## Build, test, pack

```powershell
dotnet restore FinSightIQ.Contracts.sln
dotnet build FinSightIQ.Contracts.sln --configuration Release
dotnet test FinSightIQ.Contracts.sln --configuration Release
dotnet pack src/FinSightIQ.Contracts/FinSightIQ.Contracts.csproj --configuration Release --output ./artifacts
```

The restore step needs network access to nuget.org. `nuget.config` also lists the private feed; set `NUGET_TOKEN` (a PAT with `read:packages`) if your environment requires authenticating to it.

## JSON Schema validation

Schemas use draft 2020-12 and are validated with [JsonSchema.Net](https://www.nuget.org/packages/JsonSchema.Net) (test-only dependency, never shipped to consumers). The test suite checks that:

1. each schema is valid against the draft 2020-12 meta-schema and has a versioned `$id` matching its filename;
2. each schema's properties, required list and types match the C# event (by reflection), so they cannot drift silently;
3. serialized C# payloads validate against the schema, and invalid payloads (bounds, formats, non-UTC time, unknown fields) are rejected.

Run only these checks:

```powershell
dotnet test tests/FinSightIQ.Contracts.Tests --filter "FullyQualifiedName~SchemaAgreementTests|FullyQualifiedName~Schema_"
```

Limitations: format assertions (`date-time`, `date`, `uuid`) rely on the validator's implementation. Python services (AI/NLP engines) do not validate against these schemas automatically yet; the schemas are packed under `schemas/` in the NuGet package and live in this repo for that purpose.

## Versioning and compatibility policy

Semantic versioning, with the project's rule R-03:

| Change | Version bump |
|---|---|
| Add an optional field (schema and C# together) | Minor |
| Fix docs/tests, no contract change | Patch |
| Remove or rename a field, change a type, make an optional field required, narrow bounds or enum values | **Major** — requires a breaking-change assessment, a new `*.v2.json` schema, and a migration plan (producers publish both versions during the transition) |

Rules:

- Published package versions are immutable. Never re-tag or overwrite; publish a new version.
- Every contract change updates the C# type, the JSON Schema and the tests in the same pull request. The agreement tests enforce this.
- A compatible change edits the existing `*.v1.json` in place; only a breaking change creates `*.v2.json`.
- While below `1.0.0`, the first consuming services (Sprint 3) may still prompt contract corrections; treat any such breaking change as at least a minor bump and record it here.

## Consuming the package

Add to the consumer's `nuget.config` (the token is read from the environment, never stored):

```xml
<packageSources>
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  <add key="finsightiq" value="https://nuget.pkg.github.com/shashinherath/index.json" />
</packageSources>
<packageSourceCredentials>
  <finsightiq>
    <add key="Username" value="shashinherath" />
    <add key="ClearTextPassword" value="%NUGET_TOKEN%" />
  </finsightiq>
</packageSourceCredentials>
```

`NUGET_TOKEN` is a personal access token with `read:packages` locally, or `GITHUB_TOKEN` with `packages: read` in Actions (the consuming repository must be granted access in the package's settings).

```powershell
dotnet add package FinSightIQ.Contracts --version 0.1.0
```

## CI and release

- `ci.yml` (push/PR to `main`): restore, build (Release), test, schema validation, pack (validation only). Permissions: `contents: read`, `packages: read`.
- `publish.yml` runs only on a stable tag `vMAJOR.MINOR.PATCH`. It verifies the commit is on `main`, that the tag equals `<Version>` in the csproj, then restores, builds, tests, packs and pushes with `GITHUB_TOKEN` (`packages: write`). It does **not** skip duplicates, so republishing an existing version fails. Pre-release tags (e.g. `v0.2.0-rc.1`) do not trigger it.

Release procedure (do not run without a deliberate decision to release):

```powershell
# 1. Bump <Version> in src/FinSightIQ.Contracts/FinSightIQ.Contracts.csproj, update this README, merge to main
# 2. Tag the merged commit and push the tag
git tag v0.1.0
git push origin v0.1.0
```

Repository setting required: **Settings → Actions → General → Workflow permissions** must allow the workflow to request `packages: write`.

> The workflows have been validated locally only (the same commands run successfully); no remote GitHub Actions run has been observed yet, and no package has been published.

## Known limitations and outstanding decisions

- Remote CI and publishing are unverified until the first push and tag.
- Confirm whether `PriceUpdated` should also carry required OHLC fields (currently optional).
- Confirm whether events need an `eventId`/`occurredAt` envelope for idempotency and tracing.
- `DrawdownDetected` and `UserRegistered` contracts are not yet defined.
- Python engines have no generated models from the schemas yet.
