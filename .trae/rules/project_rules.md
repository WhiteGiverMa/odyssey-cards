# OdysseyCards Project Rules

## Build Commands

- Build solution: `dotnet build`
- Build (release): `dotnet build -c Release`
- Clean build: `dotnet clean; dotnet build`

## Lint and Format Commands

- Check code style (lint): `dotnet format OdysseyCards.sln --verify-no-changes`
- Auto-format code: `dotnet format OdysseyCards.sln`
- Format with verbosity: `dotnet format OdysseyCards.sln --verbosity detailed`

## Code Style

- Private fields: `_camelCase` with underscore prefix
- Public members: `PascalCase`
- Local variables: `camelCase`
- Parameters: `camelCase`
- Interfaces: `IPascalCase` with I prefix
- Use 4 spaces for indentation
- Use CRLF line endings
- UTF-8 encoding

## Analyzer Settings

- .NET Analyzers: Enabled (latest rules)
- Code style enforcement: Enabled on build
- Nullable reference types: Enabled
- Implicit usings: Disabled (explicit using statements required)
