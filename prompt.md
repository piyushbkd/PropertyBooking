# Prompt

I built this solution with **Claude Code** (an AI coding assistant) in a step-by-step conversation, reviewing and
testing every step myself. The prompt below brings together, in one place, the understanding, decisions and steps
I gave the assistant during that conversation.

---

## The prompt

You are a senior .NET developer working with me on a take-home test. Build a small HTTP API called
**Property Showing Booking**. Work step by step: after each step, build with zero warnings, run all tests,
try the new endpoints by hand, and only then move on. Explain your choices in plain language. Do not commit or
push unless I ask, and do not add any AI attribution to commit messages.

### 1. What the system does

- An **owner** publishes **showing slots** for a property: a time when people can come and view it.
  A slot has a `propertyId`, a start time, a length (`durationMinutes`, 15 to 120) and a **capacity**
  (how many people can attend, 1 to 20). The start time must be in the future.
- A **renter** books a slot for their **party** (the group coming together, 1 to 6 people). A booking starts as
  **Confirmed** and takes seats equal to its party size.
- A slot can **never** have more booked seats than its capacity, **even when several renters book the last seats
  at the same moment**.
- A renter can hold only **one active booking per slot**.
- Booking states:
  - Confirmed → **Cancelled**: by the renter or the owner, only **before** the slot starts. Cancelling frees the seats.
  - Confirmed → **Attended** or **NoShow**: by the **owner only**, only **after** the slot starts.
  - Cancelled, Attended and NoShow are **final**.
- Every state change is recorded in a **history**: from, to, actor, reason and time.

### 2. Endpoints

| Method and path | Purpose | Success |
|---|---|---|
| `POST /slots` | create a slot | `201` + `Location` |
| `GET /slots?propertyId=&from=&to=&page=&pageSize=` | list slots ordered by start time, with seats remaining; `pageSize` at most 100 | `200` |
| `POST /slots/{id}/bookings` | book a slot with `renterEmail` and `partySize` | `201` + `Location` |
| `POST /bookings/{id}/transitions` | body `{ "to": "Cancelled", "reason": "...", "actor": "owner" \| "renter" }` | `200` |
| `GET /bookings/{id}/history` | every state change | `200` |
| `GET /slots/{id}`, `GET /bookings/{id}` | extra, so `201` responses have a real `Location` | `200` |

Errors: **400** for bad input, **404** when the slot or booking does not exist, **409** when a booking or
transition is not allowed (for example, not enough seats left). Every error uses the standard **ProblemDetails**
format with a `traceId`, whatever `Accept` header the caller sends (Swagger UI sends `text/plain`).

### 3. Technology

- C#, **.NET 8**, ASP.NET Core Web API with **controllers**, Swagger.
- **EF Core 8 with SQLite**, Code First with migrations applied at start-up, so a reviewer needs nothing installed.
  Choose SQLite over EF Core InMemory because InMemory has no transactions or locking and cannot prove the
  overbooking protection; choose it over SQL Server in Docker because it needs no setup.
- Tests: **xUnit**, **FluentAssertions 7.2.2** (not 8.x, which is licensed for non-commercial use only),
  **AutoFixture**, **Moq**, and `FakeTimeProvider` for time.
- Deliberately **not** used: Repository pattern (EF Core's `DbSet` and `DbContext` already are a repository and a
  unit of work), MediatR (two services do not need it), authentication, UI, Docker.

### 4. Structure

One API project and one test project, in a flat layout (no `src/` or `tests/` folders). Inside the API:

- `Domain`: `Slot`, `Booking`, `BookingHistoryEntry`, `BookingStatus`, `Actor`. Plain C#, no EF Core or ASP.NET.
- `Services`: `SlotService` and `BookingService`, with their interfaces in `Services/Interfaces`, so the
  controllers can be unit-tested with Moq.
- `Controllers`: thin. Each action calls one service method and chooses the success status code.
- `Contracts`: one request or response class per file.
- `Data`: `AppDbContext`, a UTC date converter, migrations.
- `Errors`: `BadRequestException`, `NotFoundException`, `ConflictException`, and one `GlobalExceptionHandler`
  (a .NET 8 `IExceptionHandler`) that turns them into 400, 404 and 409.

### 5. Domain rules (Domain-Driven Design)

- `Slot` and `Booking` are **separate aggregates**, linked only by `SlotId`. `Booking` must not use the `Slot` type.
- `Slot.Create(...)` is the only way to create a slot and checks its own rules (future start, ranges).
  `slot.HasStarted(now)` decides when a slot has started (from its exact start time).
  `slot.EnsureCanBeBooked(now)` refuses a booking once the slot has started.
- All state rules live in one method: `Booking.TransitionTo(newStatus, actor, reason, nowUtc, slotHasStarted)`.
  The service asks the slot whether it has started and passes the answer in. The method throws
  `ConflictException` when a change is not allowed and adds a history entry when it is.

### 6. Overbooking and concurrency (the most important part)

- Take seats with **one SQL statement** that checks and updates at the same time, inside a transaction:
  `UPDATE Slots SET BookedSeats = BookedSeats + @partySize WHERE Id = @id AND BookedSeats + @partySize <= Capacity`
  (EF Core `ExecuteUpdateAsync`). 0 rows changed means not enough seats, so return 409. Never read the seats in C#
  and save afterwards: two requests could both read "2 left".
- Free seats on cancel the same way: `BookedSeats = BookedSeats - @partySize`.
- Use an **explicit transaction**, because `ExecuteUpdateAsync` runs outside the transaction that `SaveChanges`
  uses, and the seat update and the saved booking must succeed or fail together.
- Safety nets in the database: a **filtered unique index** on `Bookings (SlotId, RenterEmail) WHERE Status = 'Confirmed'`
  (one active booking per renter, but they can book again after cancelling), a **check constraint**
  `0 <= BookedSeats <= Capacity`, and `Bookings.Status` as an EF Core **concurrency token**, so two changes to the
  same booking at the same moment cannot both succeed. Turn these database errors into 409, not 500.
- Explain how locking differs: SQLite locks the whole database when the transaction starts (`BEGIN IMMEDIATE`);
  SQL Server locks only the changed rows. The same single `UPDATE` is safe on both.

### 7. Time

Store all times in UTC. Get "now" from .NET 8 `TimeProvider`, so tests can use `FakeTimeProvider` and move time past
a slot's start instead of waiting.

### 8. Tests

Keep them few and important: one or two per rule in the brief. Three layers, most important first:

1. **Domain tests**: every transition rule on plain objects, plus "start time must be in the future".
2. **Service tests** against a **real SQLite file** per test (not in-memory SQLite, which shares one connection):
   too many seats, one active booking per renter (also with different letter case), cancelling frees seats,
   no booking after the start, and **concurrency tests**: 20 renters for 5 seats at the same moment (exactly 5
   succeed), and 10 cancels of one booking at once (seats come back once).
3. **Controller tests** with a strict Moq mock of the service: correct service call and `201` with `Location`.

Style: `[MethodUnderTest]_[Scenario]_[ExpectedBehavior]` names, `// Arrange`, `// Act`, `// Assert`, FluentAssertions,
AutoFixture only for values the rules do not depend on. Prove the tests are useful by **breaking the code on
purpose** (remove the seat check, stop freeing seats, let a renter mark Attended, and so on) and confirming a test
fails each time.

### 9. Code style

- **XML summaries** on every public class and member, always on three lines, never on one:
  ```csharp
  /// <summary>
  /// The booking endpoints.
  /// </summary>
  ```
- **C# 12 collection expressions** for collections, wherever the target type is known:
  - `[]` instead of `new List<T>()` or `new()` for an empty collection: `private readonly List<BookingHistoryEntry> _history = [];`
  - `[.. items]` instead of `.ToList()` or `.ToArray()`: `return [.. page.Select(SlotResponse.From)];`
  - `["a", "b"]` instead of `new[] { "a", "b" }`.
  - Keep `new()` for objects that are not collections (for example `new Fixture()`), and keep `ToListAsync()` in
    EF Core queries, because it runs the query asynchronously.
- File-scoped namespaces, primary constructors for services and controllers, `sealed` classes where nothing
  inherits from them, and nullable reference types enabled.
- Build with **zero warnings**.

### 10. README

Cover: how to run the API and the tests, the database choice and why, the endpoints with examples, the rules and
status codes, the main design decisions, the assumptions where the brief left a choice open, the tests, a
step-by-step "try it by hand" list for Swagger, what I would do with more time, what I skipped, and an "AI use" section.

### 11. Steps

1. Create the solution, the API project and the test project (flat layout).
2. Domain classes and the state-change rules.
3. Domain tests for the state rules.
4. EF Core with SQLite: `AppDbContext`, UTC converter, first migration, unique index, check constraint.
5. Error handling: the three exceptions and the global handler with ProblemDetails.
6. Slot endpoints: create, list (filters and paging), get one.
7. Booking endpoints: book, transition, history, get one; the single-statement seat update; the concurrency token.
8. Service tests and the same-moment concurrency tests.
9. Controller tests with Moq.
10. README.
11. Final check: clone the repository into an empty folder, build, run the tests, run the API, and walk through the
    "try it by hand" list in Swagger.
