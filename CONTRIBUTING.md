# Contributing to FlexPipe

Thank you for your interest in contributing!

## Reporting issues

Use the issue templates on GitHub:

- **Bug** — something is not working as expected
- **Feature request** — suggest an addition or improvement
- **Question** — ask about usage or behaviour

## Development setup

**Prerequisites:** .NET 10 SDK

```
git clone https://github.com/CasperWSchmidt/FlexPipe.git
cd FlexPipe
dotnet restore FlexPipe.slnx
dotnet build FlexPipe.slnx
dotnet test FlexPipe.slnx
```

## Project structure

```
src/
  FlexPipe/                                 Core library — interfaces, executor, builder
  FlexPipe.SourceGeneration/                Roslyn source generator for IPipelineContext
  FlexPipe.Extensions.DependencyInjection/  MS DI adapter
  FlexPipe.Extensions.SimpleInjector/       SimpleInjector adapter
  FlexPipe.Samples/                         Runnable examples
tests/
  FlexPipe.Tests/                           Unit and integration tests
  FlexPipe.SourceGeneration.Tests/          Source generator tests
```

## Submitting a pull request

1. Open an issue first for anything beyond a trivial fix — this avoids wasted effort if the direction isn't right.
2. Fork the repository and create a branch off `main`.
3. Make your changes and ensure all tests pass (`dotnet test FlexPipe.slnx`).
4. Keep commits focused and write clear commit messages.
5. Open a pull request against `main`.

`main` requires a passing CI build before merging, so make sure tests are green locally before opening the PR.

## Versioning

Versions are derived automatically from the git history using [GitVersion](https://gitversion.net/). You do not need to update any version numbers manually.
