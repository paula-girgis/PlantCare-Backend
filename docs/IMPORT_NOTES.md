# Source import notes

The backend was supplied by the owner from the original graduation-project archive. This repository is a new sanitized import; its Git history does not reconstruct the original development timeline.

## Included

- The four .NET source projects, solution, EF Core migrations and development launch settings.
- The supplied entity-relationship and database-schema diagrams. They contain schema structure, not database rows.

## Excluded

- Database backup and data files.
- `bin`, `obj`, IDE-local files, publishing profiles and generated binaries.
- Original database connection credentials, JWT signing material, SMTP credentials, provider keys and service-account identifiers from application settings.

## Configuration changes

- Replaced original application settings with blank provider values and a local development database example.
- Replaced hardcoded Gemini keys in the chat, classification and environmental-analysis controllers with `IConfiguration` lookups through `Gemini:ApiKey`.
- Made the SMTP sender configurable rather than embedding the historical project mailbox.
- Added `.gitignore` rules and setup documentation.

The original framework, package versions, routes, data model and general business logic remain as imported. No features, performance results, cloud deployment or automated test coverage are claimed as new work during this import.

Any credentials distributed in the original archive should be revoked or rotated by their owners. Removing them from this repository does not revoke existing keys. Do not copy the original private configuration into Git commits.
