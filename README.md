# Property Showing Booking API

A small HTTP API where property owners publish **showing slots** (a visit time, a length and a maximum
number of people) and renters **book seats** in a slot for their party. Bookings move through
`Confirmed → Cancelled / Attended / NoShow`, every change is recorded in a history, and a slot is never
overbooked, even when many renters book the last seats at the same moment.

Built with **C#, .NET 8, ASP.NET Core (controllers), EF Core 8 and SQLite**, with **xUnit** tests.

---

## Contents

1. [How to run it](#how-to-run-it) (and [try it by hand](#try-it-by-hand))
2. [How to run the tests](#how-to-run-the-tests)
3. [Database](#database)
4. [Endpoints](#endpoints)
5. [Rules and status codes](#rules-and-status-codes)
6. [Main design decisions](#main-design-decisions)
7. [Assumptions](#assumptions-where-the-brief-left-a-choice-open)
8. [Tests](#tests)
9. [What I would do with more time](#what-i-would-do-with-more-time)
10. [What I skipped](#what-i-skipped)
11. [AI use](#ai-use)
12. [Project layout](#project-layout)

---

## How to run it

**Needs:** the .NET 8 SDK, or a newer SDK with the .NET 8 runtime installed. Nothing else: SQLite is
included as a NuGet package, and the database is created automatically.

```bash
git clone https://github.com/piyushbkd/PropertyBooking.git
cd PropertyBooking
dotnet run --project PropertyShowingBooking.Api
```

Then open **http://localhost:5081/swagger** to see and try every endpoint in the browser.

- On first start the API creates the SQLite file `PropertyShowingBooking.Api/showings.db` and its tables
  (EF Core migrations are applied at start-up). Delete the file to start again with an empty database.
- [`PropertyShowingBooking.Api/PropertyShowingBooking.Api.http`](PropertyShowingBooking.Api/PropertyShowingBooking.Api.http)
  has ready-made example requests for Visual Studio or the VS Code REST Client.
- In Development the log shows the SQL that EF Core runs, including the single-statement seat check described below.

### Try it by hand

Start with an empty database (delete `PropertyShowingBooking.Api/showings.db` if it exists), run the API, and send
these requests in order from Swagger. The ids below assume an empty database.

| # | Request | Body | Expected |
|---|---|---|---|
| 1 | `POST /slots` | `{ "propertyId": "P-101", "startsAt": "2026-12-05T10:00:00+05:30", "durationMinutes": 30, "capacity": 5 }` | `201`, slot `id: 1`, `seatsRemaining: 5`, `startsAt: "2026-12-05T04:30:00Z"` |
| 2 | `POST /slots` | the same, with `"capacity": 0` | `400`, the error names `Capacity` |
| 3 | `POST /slots/1/bookings` | `{ "renterEmail": "asha@mail.com", "partySize": 3 }` | `201`, `status: "Confirmed"`; `GET /slots/1` now shows `seatsRemaining: 2` |
| 4 | `POST /slots/1/bookings` | `{ "renterEmail": "bina@mail.com", "partySize": 3 }` | `409`, "Not enough seats left in slot 1: 2 left, 3 requested." |
| 5 | `POST /slots/1/bookings` | `{ "renterEmail": "ASHA@Mail.com", "partySize": 1 }` | `409`, "asha@mail.com already has an active booking for slot 1." |
| 6 | (look at steps 4 and 5 in Swagger) | | Swagger sends `Accept: text/plain`; the response body must still be the ProblemDetails JSON with the real message and a `traceId`, not a generic "An error occurred" |
| 7 | `POST /slots/99/bookings` | `{ "renterEmail": "asha@mail.com", "partySize": 1 }` | `404`, "Slot 99 was not found." |
| 8 | `POST /bookings/1/transitions` | `{ "to": "Attended", "actor": "owner" }` | `409`, the slot has not started yet |
| 9 | `POST /bookings/1/transitions` | `{ "to": "Cancelled", "actor": "renter", "reason": "Plans changed" }` | `200`, `status: "Cancelled"`; `GET /slots/1` shows `seatsRemaining: 5` again |
| 10 | `POST /bookings/1/transitions` | `{ "to": "Cancelled", "actor": "owner" }` | `409`, the booking is already Cancelled, which is a final state |
| 11 | `GET /bookings/1/history` | | `200`, two entries: `null → Confirmed` and `Confirmed → Cancelled` with the reason |
| 12 | `POST /bookings/1/transitions` | `{ "to": 1, "actor": "owner" }` | `400`: states must be words, not numbers |

Marking a booking Attended or NoShow needs a slot that has already started, so it cannot be tried by hand without
waiting. Those rules are covered by the domain tests, which pass "the slot has started" in directly.

## How to run the tests

```bash
dotnet test
```

15 tests (24 test runs), a few seconds. They need nothing installed: each test that uses a database creates its
own temporary SQLite file and deletes it afterwards. They never touch `showings.db`.

## Database

**SQLite**, through EF Core 8 (Code First, with migrations in `PropertyShowingBooking.Api/Data/Migrations`).

Why SQLite, out of the three options in the brief:

| Option | Real transactions and locking | Setup for a reviewer | Choice |
|---|---|---|---|
| **SQLite** | Yes | None: one file, created on start-up | **Used** |
| SQL Server in Docker | Yes | Needs Docker running | Heavier than needed |
| EF Core InMemory | **No** | None | Cannot prove the overbooking protection |

EF Core InMemory has no transactions and no locking, so two requests could both see "2 seats left" and both
book them. It could not show that the "must not be overbooked" requirement really holds.

Moving to SQL Server would mean changing `UseSqlite` to `UseSqlServer`, the connection string, and creating
a new migration for SQL Server. The seat-booking code itself would not change (see
[Concurrency](#2-concurrency-the-slot-is-never-overbooked)).

**Tables:** `Slots`, `Bookings` and `BookingHistory`. Statuses are stored as words (`"Confirmed"`), and
all times are stored in UTC.

## Endpoints

Times are ISO 8601 with a time zone, for example `2026-12-05T10:00:00+05:30` or `2026-12-05T04:30:00Z`.
Responses return times in UTC (`...Z`).

| Method and path | What it does | Success |
|---|---|---|
| `POST /slots` | Create a slot | `201` + `Location` |
| `GET /slots?propertyId=&from=&to=&page=&pageSize=` | List slots by start time, with `seatsRemaining` | `200` |
| `POST /slots/{id}/bookings` | Book seats for a renter's party | `201` + `Location` |
| `POST /bookings/{id}/transitions` | Change a booking's state | `200` |
| `GET /bookings/{id}/history` | Every state change with time, actor and reason | `200` |
| `GET /slots/{id}` *(extra)* | One slot | `200` |
| `GET /bookings/{id}` *(extra)* | One booking | `200` |

The two extra `GET` endpoints are not in the brief. They give the `201 Created` responses a real address
for their `Location` header.

**Create a slot**

```http
POST /slots
{ "propertyId": "P-101", "startsAt": "2026-12-05T10:00:00+05:30", "durationMinutes": 30, "capacity": 5 }
```
```json
201 Created
{ "id": 1, "propertyId": "P-101", "startsAt": "2026-12-05T04:30:00Z", "endsAt": "2026-12-05T05:00:00Z",
  "durationMinutes": 30, "capacity": 5, "seatsRemaining": 5 }
```

**List slots.** All filters are optional: `GET /slots?propertyId=P-101&from=2026-12-01T00:00:00Z&to=2026-12-08T00:00:00Z&page=1&pageSize=20`
```json
200 OK
{ "items": [ { "id": 1, "...": "...", "seatsRemaining": 2 } ], "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1 }
```

**Book a slot**

```http
POST /slots/1/bookings
{ "renterEmail": "asha@mail.com", "partySize": 3 }
```
```json
201 Created
{ "id": 1, "slotId": 1, "renterEmail": "asha@mail.com", "partySize": 3, "status": "Confirmed", "createdAt": "..." }
```

**Change a booking's state**

```http
POST /bookings/1/transitions
{ "to": "Cancelled", "reason": "Plans changed", "actor": "renter" }
```

**History**: `GET /bookings/1/history`
```json
[ { "fromStatus": null,        "toStatus": "Confirmed", "actor": "renter", "reason": null,            "at": "..." },
  { "fromStatus": "Confirmed", "toStatus": "Cancelled", "actor": "renter", "reason": "Plans changed", "at": "..." } ]
```

**Errors** all use the standard ProblemDetails format (`application/problem+json`) with a `traceId`:
```json
409 Conflict
{ "title": "Conflict", "status": 409, "detail": "Not enough seats left in slot 1: 2 left, 3 requested.", "traceId": "..." }
```

## Rules and status codes

**Booking states**

| From | To | Who | When |
|---|---|---|---|
| Confirmed | Cancelled | renter or owner | before the slot starts (cancelling frees the seats) |
| Confirmed | Attended | owner only | after the slot starts |
| Confirmed | NoShow | owner only | after the slot starts |
| Cancelled, Attended, NoShow | anything | nobody | never: these are final |

**Status codes**

| Code | Meaning in this API | Examples |
|---|---|---|
| `400` | The request itself is wrong, whatever is in the database | `capacity: 0`, `partySize: 7`, `startsAt` in the past, `pageSize: 101`, `"to": "Foo"`, `"actor": "admin"` |
| `404` | The slot or booking in the URL does not exist | `POST /slots/99/bookings` |
| `409` | The request is valid, but the current data does not allow it | not enough seats; the renter already has an active booking; the slot has started; the booking is already final; a renter marking Attended |

## Main design decisions

### 1. Structure: one API project with clear folders

```
Controllers → Services → Domain
                 ↓
               Data (EF Core + SQLite)
```

- **Domain** (`Slot`, `Booking`, `BookingHistoryEntry`): plain C# with no EF Core or ASP.NET code. All the
  booking state rules live in one method, `Booking.TransitionTo(...)`, so they can be unit-tested without a
  database.
- **`Slot` and `Booking` are separate aggregates** (in Domain-Driven Design terms), linked only by `SlotId`.
  `Booking` never reads or changes a `Slot`. When a booking changes state, the service asks the slot
  (`slot.HasStarted(now)`) and passes the answer to `Booking.TransitionTo(..., slotHasStarted)`. So the rule
  "when has a slot started" lives only in `Slot`, and the booking rules depend only on the booking's own state
  plus the facts handed to them.
- **`Slot` checks its own rules.** `Slot.Create(...)` is the only way to make a slot: it refuses a start time that
  is not in the future and values outside the allowed ranges, and trims the property id. `slot.EnsureCanBeBooked(now)`
  refuses a booking once the slot has started. The seat count is not checked in `Slot` on purpose: only one SQL
  statement can check and take seats safely when many people book at the same moment.
- **Services** (`SlotService`, `BookingService`): the use cases. They run the database work in transactions.
- **Controllers**: thin. Each action is one or two lines that call a service and choose the success code.
  They depend on the service interfaces (`ISlotService`, `IBookingService`), not on the classes.
- **Errors**: services throw `BadRequestException`, `NotFoundException` or `ConflictException`. One
  handler (`GlobalExceptionHandler`, a .NET 8 `IExceptionHandler`) turns them into 400, 404
  or 409. No other code decides HTTP status codes for errors.

This is a light version of Clean Architecture: the domain does not depend on the database or the web.
I did not split it into separate Domain, Application and Infrastructure projects, because for five
endpoints that would mostly add wiring. The folders keep the same boundaries, so splitting them later is a
mechanical change.

### 2. Concurrency: the slot is never overbooked

Three layers of protection:

1. **Seats are checked and taken in one statement**, inside a transaction:
   ```sql
   UPDATE Slots SET BookedSeats = BookedSeats + @partySize
   WHERE Id = @slotId AND BookedSeats + @partySize <= Capacity
   ```
   If it changes 0 rows, there were not enough seats, and the request gets `409`. Because the check and the
   update are a single statement, a second request always checks against the updated number. Reading the
   seats in C# and saving afterwards would let two requests both read "2 left". (Written with EF Core's
   `ExecuteUpdateAsync`, so no raw SQL.)
2. **Transactions.** SQLite allows one write transaction at a time. Microsoft.Data.Sqlite starts transactions
   as `BEGIN IMMEDIATE`, so the checks inside a transaction ("slot started?", "renter already booked?")
   always see the latest data. The others wait their turn, up to 30 seconds.
3. **Safety nets in the database**:
   - a **filtered unique index** on `Bookings (SlotId, RenterEmail) WHERE Status = 'Confirmed'`, which
     allows one active booking per renter per slot (and still lets them book again after a cancel);
   - a **check constraint** `BookedSeats >= 0 AND BookedSeats <= Capacity`;
   - **`Bookings.Status` is a concurrency token.** EF Core adds `AND Status = <the status it read>` to every
     update, so if two people change the same booking at once, only one succeeds. Without it, a double
     cancel could give the seats back twice.

On SQLite the whole database file is locked during a write. On SQL Server the same `UPDATE` would lock
only that one slot's row, so bookings for different slots would not wait for each other.

### 3. Time: UTC everywhere, and an injectable clock

- All times are stored as `DateTime` in UTC. EF Core with SQLite cannot sort or compare `DateTimeOffset`
  in queries, because SQLite has no real date type. A small value converter (`UtcDateTimeConverter`) also
  makes sure times read back from SQLite are marked as UTC.
- "Now" comes from .NET 8's `TimeProvider`. In tests it is replaced with `FakeTimeProvider`, so a test can
  move time past a slot's start in one line instead of waiting.
- A slot counts as started from its exact start time: at 10:00:00 a 10:00 slot has started.

### 4. No Repository pattern, no MediatR

- **Repository pattern: not used.** EF Core's `DbSet` already acts as a repository, and `DbContext` as a unit
  of work. The key concurrency logic uses `ExecuteUpdateAsync` inside a transaction, which a generic
  repository would hide. The tests use a real SQLite database instead of fake repositories, which is what
  makes the concurrency tests meaningful. In a codebase that already uses repositories, I would follow that style.
- **MediatR: not used.** The brief lists it as optional. With two services, plain service classes are
  simpler to read. MediatR version 13 and later also needs a paid licence for commercial use. It would be
  worth adding in a bigger codebase with cross-cutting behaviours such as validation or logging pipelines.
- **Service interfaces: used.** The controllers depend on `ISlotService` and `IBookingService`, registered in
  `Program.cs` as `AddScoped<IBookingService, BookingService>()`. This lets each controller be unit-tested on its
  own with a mocked service (Moq), and another implementation could be swapped in without touching the controllers.
  The services themselves are still tested for real against SQLite, because the overbooking protection lives in the
  service and the database, and a mock could not prove it.

### 5. Errors and logging

- Every error is ProblemDetails with a `traceId`, including automatic validation errors and unknown URLs.
- Expected refusals (400, 404, 409) are logged at Information level. Unexpected exceptions are logged at
  Error level with the stack trace, and return a plain `500` without internal details. The .NET 8
  exception middleware's own log entry is switched off in `appsettings.json`, because it logs every
  handled exception, including every normal `409`, as a failure.

## Assumptions (where the brief left a choice open)

1. **A slot that has started cannot be booked** (`409`).
2. **"Active booking" means `Confirmed`.** After a cancel, the same renter can book the same slot again.
3. **Exactly the start time counts as started.** So cancelling is allowed until the second before.
4. **Seats remaining** = capacity minus the party sizes of all bookings that are not Cancelled. Attended and
   NoShow still count, because those seats were used.
5. **`GET /slots`**:
   - all filters are optional;
   - `from` includes its time and `to` excludes its time;
   - the default `page` is 1 and the default `pageSize` is 20;
   - `pageSize` above 100, `page` below 1, or `from` later than `to` gives `400` (rather than silently
     changing the request).
6. **`reason`** is optional, with at most 500 characters. Blank reasons are stored as no reason.
7. **Emails** are trimmed and stored in lowercase, so `Asha@Mail.com` and `asha@mail.com` are the same renter.
8. **There is no login.** The API trusts the `actor` field (`"owner"` or `"renter"`) as the brief's request body
   suggests. The system does not know which owner owns which property: it only stores a `propertyId` per slot.
9. **Transition errors:** an unknown state name, an unknown actor, or a number instead of a word gives `400`.
   A real state that the rules do not allow, for example `"to": "Confirmed"`, gives `409`.
10. **Refused transitions are not written to the history**, because nothing changed.
11. **Time zones:** send `startsAt`, `from` and `to` with a time zone (`Z` or `+05:30`). A time without one is
    read by .NET as the server's local time.

## Tests

```
dotnet test   →   15 tests (24 test runs: some tests run several times with different inputs)
```

The tests are kept small on purpose: one or two tests for each rule the brief asks for. They are written in three
layers, most important first:

1. **Domain tests**: the booking and slot rules, on plain objects. No database, no web server, no clock.
2. **Service tests**: the use cases against a real SQLite file, including requests at the same moment.
3. **Controller tests**: each controller on its own, with a mocked service (Moq).

The test project mirrors the API project's folders, and each test class is named after the class it tests
(`Domain/Booking.cs` → `Domain/BookingTests.cs`). The shared database helper is in `TestSupport/`.

**How the tests are written**
- Names follow `[MethodUnderTest]_[Scenario]_[ExpectedBehavior]`, for example
  `TransitionTo_CancelAfterSlotStarts_ShouldThrowConflictExceptionAndKeepStatus`.
- Every test has `// Arrange`, `// Act` and `// Assert` sections and checks one behaviour.
- Assertions use **FluentAssertions 7.2.2** (`booking.Status.Should().Be(...)`,
  `act.Should().Throw<ConflictException>().WithMessage(...)`). Version 7 is used on purpose: from version 8,
  FluentAssertions is licensed for non-commercial use only, while 7.2.2 is Apache 2.0 and has the same syntax.
- Test data uses **AutoFixture 4.18.1** where any value works (reasons, ids, email addresses). Values that the
  rules depend on, such as party size, capacity and times, are written out in the test so its meaning is clear.

| Test class | Tests (runs) | What it proves | Database |
|---|---|---|---|
| `Domain/BookingTests` | 6 (13) | The transition rules: renter or owner can cancel before the start, nobody after; only the owner marks Attended or NoShow, and only after the start; Cancelled, Attended and NoShow never change again | none |
| `Domain/SlotTests` | 1 | A slot cannot be created with a start time that is not in the future (exactly "now" is refused too) | none |
| `Services/BookingServiceTests` | 4 (6) | Too many seats is refused and changes nothing; one active booking per renter per slot (also with a different letter case or spaces); cancelling frees the seats and allows booking again; a slot that has started cannot be booked (checked at exactly its start time) | real SQLite file |
| `Services/BookingServiceConcurrencyTests` | 2 | Requests at the same moment (below) | real SQLite file |
| `Controllers/SlotsControllerUnitTests` | 1 | `SlotsController` with a strict Moq mock of `ISlotService`: `POST /slots` returns `201` with a location to `GetById` | none |
| `Controllers/BookingsControllerUnitTests` | 1 | `BookingsController` with a strict Moq mock of `IBookingService`: booking passes the slot id on and returns `201` with a location | none |

The controller tests create the controller directly with a strict Moq mock, so any service call the test did not
expect fails the test. They do not start a web server: routing, JSON, validation (`400`) and the error handler
(`404` and `409` responses) are checked by hand through Swagger (see [Try it by hand](#try-it-by-hand)).

**Concurrency tests.** Each runs many operations on separate threads, each with its own `DbContext` like
separate web requests, and releases them all at once:

| Test | Result checked |
|---|---|
| 20 renters book 1 seat each in a 5-seat slot | exactly 5 succeed, the slot is full and not over |
| 10 cancels of one booking at once (owner and renter) | 1 succeeds, the seats come back once, the history shows one cancel |

Any error other than a normal `409` (for example "database is locked") fails these tests. The seat tests
and the concurrency tests also check that `BookedSeats` equals the seats held by non-cancelled bookings.

A file database is used instead of in-memory SQLite on purpose: in-memory SQLite lives on one shared
connection, so requests could never really overlap and the tests would prove nothing.

**Checking the tests themselves.** I broke the code on purpose and confirmed that these tests still catch it:
- seat check removed from the `UPDATE` (overbooking) → the overbooking test and the concurrency test fail;
- cancelling no longer frees the seats → the cancel test and the concurrent-cancel test fail;
- a started slot can still be booked → the "slot has started" test fails;
- a renter may mark Attended → the transition-rule test fails;
- `POST /slots` returning `200` instead of `201` → the slots controller unit test fails.

Not covered by the automated tests, because there are no tests over real HTTP: the error handler's status codes
and messages, the Swagger `Accept: text/plain` fix, and JSON settings such as rejecting numbers for state names.
These are checked by hand (see [Try it by hand](#try-it-by-hand)).

## What I would do with more time

- **Authentication and real identities.** Use a login (for example JWT) so the API knows who the owner and the
  renter are, instead of trusting the `actor` field, and check that only a property's owner can create its
  slots or mark attendance.
- **Owners and properties** as real tables, so slots belong to a known property and owner.
- **SQL Server** for production, with row-level locking and the same seat logic. Add a test run against it
  using Testcontainers.
- **More slot rules**, for example no overlapping slots for the same property, and a minimum notice time
  for cancelling.
- **Idempotency keys** on `POST` requests, so a retried booking request cannot create a second booking.
- **Rate limiting**, health checks, structured logging and metrics.
- **Stricter time input**: reject times without a time zone instead of using the server's local time.
- **CI pipeline** (GitHub Actions) running build and tests on every push.

## What I skipped

- No user interface: the brief asks for an HTTP API, and Swagger is enough to try it.
- No authentication (see above).
- No Docker setup: SQLite needs none.
- No MediatR and no Repository pattern, on purpose (see design decisions).
- No automated tests over real HTTP. Each controller is unit-tested with a mocked service (Moq), and the
  full HTTP behaviour (routing, JSON, validation, error responses) is checked by hand through Swagger. With more
  time I would add a few `WebApplicationFactory` tests for the `400`, `404` and `409` responses.

All endpoints and requirements in the brief are implemented. The booking and transition rules, the overbooking
protection and the controllers have automated tests.

## AI use

I used **Claude Code** (an AI coding assistant) throughout, as I would at work, and reviewed every step.

**The prompt** is in [`prompt.md`](prompt.md). It brings together, in one place, the understanding, decisions,
code style and steps I gave the assistant during the work.

**What I used it for**
- Reading the brief and turning it into a written plan: functional requirements, the open questions, and the
  decisions for each (see [Assumptions](#assumptions-where-the-brief-left-a-choice-open)).
- Talking through design choices before coding: SQLite against InMemory or SQL Server, Code First, how much
  Clean Architecture, no Repository pattern, and how to prevent overbooking.
- Writing the code and tests step by step. After each step: build, run all tests, test the new endpoints by
  hand against the running API, then commit and push.
- Checking the tests themselves by breaking the code on purpose (see [Tests](#tests)).

## Project layout

```
PropertyBooking/
├─ PropertyShowingBooking.sln
├─ dotnet-tools.json                  ← pins the EF Core command-line tool (dotnet-ef 8.0.34)
├─ prompt.md                          ← the prompt given to the AI assistant (see AI use)
├─ PropertyShowingBooking.Api/
│  ├─ Program.cs                      ← start-up: database, clock, services, JSON, errors, Swagger
│  ├─ Controllers/                    ← SlotsController, BookingsController (thin)
│  ├─ Services/                       ← SlotService, BookingService (the use cases)
│  │  └─ Interfaces/                  ← ISlotService, IBookingService
│  ├─ Domain/                         ← Slot, Booking (state rules), BookingHistoryEntry, BookingStatus, Actor
│  ├─ Contracts/                      ← request and response classes (the JSON shapes), one class per file
│  │  ├─ PagedResponse.cs             ← one page of results (used by GET /slots)
│  │  ├─ Slots/                       ← CreateSlotRequest, ListSlotsQuery, SlotResponse
│  │  └─ Bookings/                    ← CreateBookingRequest, TransitionRequest, BookingResponse, BookingHistoryEntryResponse
│  ├─ Json/                           ← LowerCaseEnumConverter: writes the actor as "owner" / "renter"
│  ├─ Data/                           ← AppDbContext, UtcDateTimeConverter, Migrations/
│  ├─ Errors/                         ← BadRequest, NotFound, Conflict exceptions and the ProblemDetails handler
│  └─ PropertyShowingBooking.Api.http ← example requests
└─ PropertyShowingBooking.Tests/
   ├─ Domain/                         ← BookingTests, SlotTests (no database)
   ├─ Services/                       ← BookingServiceTests, BookingServiceConcurrencyTests
   ├─ Controllers/                    ← SlotsControllerUnitTests, BookingsControllerUnitTests (Moq, no HTTP)
   └─ TestSupport/
      └─ TestDatabase.cs              ← fresh SQLite file and fake clock per test
```
