# User Management Minimal API

This project is a modern .NET 9 minimal API that exposes CRUD operations for managing users. It includes validation, structured error responses via RFC 7807 `ProblemDetails`, OpenAPI documentation, and integration tests built with xUnit and `WebApplicationFactory`.

## Prerequisites
- .NET SDK 9.0 or later

## Running the API
```bash
dotnet run
```
The API listens on the configured ASP.NET Core URLs (defaults to `http://localhost:5196`). Navigate to `/swagger` for the interactive documentation.

## Running Tests
```bash
dotnet test
```
The integration tests spin up the in-memory server and cover happy-path and error scenarios (empty state, creation, conflicts, deletions).

## API Surface
All routes are grouped under `/api/users`.

| Method | Route             | Description             |
| ------ | ----------------- | ----------------------- |
| GET    | `/api/users`      | List all users          |
| GET    | `/api/users/{id}` | Fetch a specific user   |
| POST   | `/api/users`      | Create a new user       |
| PUT    | `/api/users/{id}` | Update an existing user |
| DELETE | `/api/users/{id}` | Delete a user           |

### Sample Requests
```http
POST /api/users
Content-Type: application/json

{
  "email": "ada.lovelace@example.com",
  "fullName": "Ada Lovelace"
}
```

```http
PUT /api/users/{id}
Content-Type: application/json

{
  "email": "ada.lovelace@example.com",
  "fullName": "Augusta Ada King"
}
```

### Error Handling
- Validation problems return `400` with a `ValidationProblemDetails` payload.
- Missing entities return `404` with a `ProblemDetails` body that includes the requested identifier.
- Email conflicts return `409` with a `ProblemDetails` payload describing the conflict.

## Project Structure
```
Domain/                // User domain model
Contracts/             // Request/response DTOs + validation helpers
Infrastructure/        // In-memory repository implementation
csharp-user-management.Tests/
  CustomWebApplicationFactory.cs
  UnitTest1.cs         // Integration tests
Program.cs             // Minimal API setup and endpoints
```

## Next Steps
- Replace the in-memory repository with a persistent data store (EF Core, Dapper, etc.).
- Add authentication/authorization (e.g., JWT bearer) and role-based policies.
- Extend test coverage with negative cases for validation payloads and concurrency.
