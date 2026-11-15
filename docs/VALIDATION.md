# Security and validation

## Publication checks

- Excluded database contents, binaries, IDE state and publishing profiles.
- Checked the publication tree for exact original credential values without displaying those values.
- `dotnet restore` completed for all four projects using an isolated official .NET SDK 8.0.428.
- `dotnet build WebApplication1.Solution.sln --no-restore -c Release` completed with **0 errors and 73 warnings**. Warnings include nullable references, old .NET Framework dependency compatibility (`NU1701`) and Windows-only image APIs. They have not been hidden or treated as resolved.
- A bounded local startup returned **HTTP 200** for `/swagger/v1/swagger.json`, describing **14 paths**.
- The local check deliberately used an unavailable loopback-only SQL Server endpoint. Database migration failure was logged and did not prevent Swagger startup; database-backed behavior remains unverified.
- Original database credentials, real user data, SMTP delivery, image classification and paid AI providers were not used or exercised. No automated feature-test suite was present in the supplied snapshot, and no claim of passing feature tests is made.

## Known limitations in the academic snapshot

Do not expose this application to untrusted users as-is. A source review identified work needed before production deployment:

- Restrict user-list access and use response DTOs that exclude Identity security fields.
- Stop returning password-reset links in API responses and logging confirmation tokens; review account-enumeration behavior and rate limits.
- Enforce token revocation consistently: token validity records alone do not guarantee JWT middleware checks revocation.
- Review data validation, provider error handling and JSON construction for user-supplied text.
- Review CORS origins, external URLs and historical model identifiers.
- Upgrade the unsupported target and review old/mixed dependency versions.
- Add automated unit/integration tests and exercise account, database and external-provider flows against isolated test services.

These observations are not a complete security audit. Publication does not include using the original credentials, connecting to the original database, making paid AI calls or deploying the application.
