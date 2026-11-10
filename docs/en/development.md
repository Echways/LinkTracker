# Development

[English](development.md) · [Русский](../ru/development.md) · [Documentation](README.md)

## Repository

| Path | Content |
| ---- | ------- |
| [src/](../../src/) | Services, shared libraries and tests |
| [migrations/](../../migrations/) | PostgreSQL schema, applied by DbUp when the Scrapper starts |
| [deploy/](../../deploy/) | Compose files, Prometheus and Grafana configuration, root `.env` template |
| [docs/](../) | This documentation and [PromQL examples](../example-pql.txt) |

Solution `linktracker.sln` targets .NET 9. Package versions are central in `src/Directory.Packages.props`; build settings, including nullable reference types and warnings as errors, are in `src/Directory.Build.props`.

## Build and run

```bash
dotnet build
dotnet run --project src/LinkTracker.Scrapper.Api
```

See [Getting started](getting-started.md) for the infrastructure and the order of services.

## Tests

```bash
dotnet test
```

Tests live in `src/LinkTracker.Tests` and carry two traits, `Module` (`Bot`, `Scrapper`, `AiAgent`, `Shared`) and `Category` (`Unit`, `Integration`). Integration tests start PostgreSQL, Kafka and Valkey with Testcontainers, so they need Docker.

```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Module=Scrapper&Category=Integration"
```

## Formatting

Style comes from `.editorconfig`, and CI rejects unformatted code:

```bash
dotnet format --verify-no-changes
```

## Continuous integration

`.github/workflows/ci.yml` runs on every pull request and on pushes to `main`:

| Job | What it does |
| --- | ------------ |
| `build` | Restores and builds in the .NET 9 SDK image |
| `linter` | `dotnet format --verify-no-changes` |
| `test` | Unit tests per module and shared integration tests, in parallel |
| `docker-test` | Bot, Scrapper and AI Agent integration tests on a runner with Docker |

## Adding a source

1. Implement the client and the parsing of the source in the Scrapper.
2. Register it in the Scrapper client module together with a rate-limit section.
3. Add the link format to link recognition in `LinkTracker.Shared/Links`.
4. Add unit tests and, if the source has its own API, an integration test.
