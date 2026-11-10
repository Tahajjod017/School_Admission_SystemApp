# Plan: architecture and delivery

## 1. Repository layout
```
/
├── backend/
│   ├── Dockerfile
│   ├── src/Admission.Api/        # ASP.NET Core minimal API
│   └── tests/Admission.Tests/    # xUnit integration + concurrency tests
├── frontend/
│   ├── Dockerfile                # build with Node, serve with nginx
│   ├── nginx.conf
│   └── src/                      # React + Vite + TypeScript
├── docker-compose.yml
├── README.md
├── ASSUMPTIONS.md
├── AI_USAGE.md
└── docs/ (REQUIREMENTS.md, PLAN.md)
```

## 2. Stack choices
| Layer | Choice | Why |
|---|---|---|
| Backend | .NET 8 LTS (or newer), ASP.NET Core minimal API | Simple, matches the brief |
| Data access | Npgsql + Dapper (raw SQL) | The core logic needs `FOR UPDATE` and `clock_timestamp()`; raw SQL keeps it explicit and easy to explain |
| DB | PostgreSQL 16, named Docker volume | Persistence across `down`/`up` |
| Frontend | React 18 + Vite + TypeScript, plain CSS, `react-router-dom` (HashRouter) | No heavy UI library; hash routing needs no nginx rewrite rules |
| Frontend tests | Vitest + React Testing Library | Standard with Vite |
| Backend tests | xUnit + `WebApplicationFactory`, real PostgreSQL | Concurrency behaviour cannot be trusted on an in-memory fake |

Ports: backend `8080`, frontend `3000`, PostgreSQL internal only (`5432`).

## 3. Database schema
```sql
classes(
  id          text primary key,
  name        text not null,
  total_seats int  not null check (total_seats > 0),
  created_at  timestamptz not null default now()
)

holds(
  id           text primary key,
  class_id     text not null references classes(id),
  student_id   char(17) not null,
  status       text not null check (status in ('HELD','CONFIRMED','EXPIRED','CANCELLED')),
  created_at   timestamptz not null default now(),
  expires_at   timestamptz not null,
  confirmed_at timestamptz null
)

-- safety net: a student can never have 2 active rows in a class
create unique index ux_holds_active_student
  on holds(class_id, student_id) where status in ('HELD','CONFIRMED');
create index ix_holds_class_status_exp on holds(class_id, status, expires_at);
create index ix_holds_student on holds(student_id, created_at desc);
```

## 4. Concurrency design (the heart of the project)
**Goal:** `held + confirmed <= totalSeats` under any load, also with two backend processes on one DB.

**Approach: per-class row lock in a short transaction** (`SELECT ... FOR UPDATE` on the `classes` row). The lock lives in PostgreSQL, so it works across threads *and* across processes.

`POST /classes/{id}/holds`:
1. `BEGIN`
2. `SELECT total_seats FROM classes WHERE id=@c FOR UPDATE` → 404 if missing (all holds on this class now queue here)
3. `UPDATE holds SET status='EXPIRED' WHERE class_id=@c AND status='HELD' AND expires_at <= clock_timestamp()`
4. Student already has HELD/CONFIRMED in this class → 409 `ALREADY_APPLIED`
5. `count(*)` of HELD+CONFIRMED ≥ `total_seats` → 409 `NO_SEATS`
6. `INSERT ... expires_at = clock_timestamp() + @seconds` → `RETURNING`
7. `COMMIT`

Notes:
- `clock_timestamp()`, not `now()`: `now()` is frozen at transaction start, so a request waiting on the lock would get an earlier expiry than it should.
- **All time comparisons use the database clock**, so two backend processes can't disagree.
- Expiry is lazy: reads count only `expires_at > clock_timestamp()` holds; a background sweeper additionally flips old rows to `EXPIRED` every few seconds for tidy data. No API call is ever required to free a seat.
- `confirm`: `SELECT ... FROM holds WHERE id=@h FOR UPDATE`; owner check; `CONFIRMED` already → 200 (idempotent, no double count since the row is just the same row); `HELD` and unexpired → set `CONFIRMED`; otherwise 409.
- Reads (`GET /classes`) take no locks.

## 5. Error format
```json
{ "code": "NO_SEATS", "message": "Sorry, all seats in this class are taken." }
```
Codes: `VALIDATION`, `NOT_FOUND`, `FORBIDDEN`, `NO_SEATS`, `ALREADY_APPLIED`, `HOLD_EXPIRED`, `INVALID_STATE`.
The UI maps codes to friendly messages, and has a separate message for "cannot reach server".

## 6. Frontend design
- `StudentIdBar` (always visible): input + validation (exactly 17 digits), saved in `localStorage`.
- Routes: `#/classes` (list + admin create form), `#/applications` (mine), `#/applications/:holdId` (one application).
- Data: `apiClient` (adds `X-Student-Id`, base URL from `VITE_API_URL`), small polling hook (classes every ~3 s, application every ~5 s).
- Countdown: computed from `expiresAt` and a server-time offset (I add an extra `serverTime` field to hold responses, which the contract allows), so a wrong local clock doesn't mislead. At 0 the UI flips to `EXPIRED` and disables Pay with no reload.
- Every failed call goes through one error → message translator; one global banner/toast, never a blank page.
- `VITE_API_URL` is a build-time variable in Vite. It is passed as a Docker build arg in compose (default `http://localhost:8080`), because the browser, not the nginx container, calls the backend.

## 7. Test plan
**Backend (≥ 3 required; I plan more)**
1. Create class → list/get show correct counts.
2. Hold → seat count changes; hold on a full class → 409.
3. Same student can't hold twice in one class.
4. Confirm: owner OK; other student rejected; twice → `confirmedSeats` stays 1.
5. Expiry: with `HOLD_DURATION_SECONDS=1`, seat returns after the wait.
6. **Concurrency:** class with N seats, 200+ parallel requests from distinct students → exactly N × 201, rest × 409; `held+confirmed == N`.

**Frontend:** Vitest test for the application screen (countdown reaches 0 → shows EXPIRED, Pay disabled) and student-ID validation.

**Bonus:** compose profile starting `backend2` on `8081` with the same DB; concurrency test hitting both ports.

## 8. Milestones and commits (commit at the end of each)
| # | Milestone | Done when |
|---|---|---|
| M0 | Repo scaffold, Dockerfiles, compose, health endpoint | `docker compose up` starts all 3 services |
| M1 | DB schema/migrations + `POST/GET /classes` | endpoints 1–3 work |
| M2 | Holds with locking | endpoint 4 works; 409 on full |
| M3 | Confirm, cancel, get hold, my holds, sweeper | endpoints 5–8 work |
| M4 | Backend tests incl. concurrency | tests green via compose |
| M5 | Frontend shell + student ID + API client | ID persists after refresh |
| M6 | Class list (polling) + create class | seat numbers update by themselves |
| M7 | Apply, application screen, countdown, pay, cancel, my applications | full flow in browser |
| M8 | Error handling pass | every failure case shows a readable message |
| M9 | Frontend test | `npm test` green |
| M10 | Bonus: 2 backend instances | concurrency test across 2 ports |
| M11 | Final docs: README, ASSUMPTIONS, AI_USAGE | all checklist items ticked |

## 9. Risks I'm watching
- Hidden API tests: status codes for unspecified cases (see `ASSUMPTIONS.md`) are my best guesses.
- Docker first-run: backend must wait for PostgreSQL (healthcheck + retry on startup).
- Data loss: never `down -v` in docs except as a "reset" instruction.
- Student IDs are not secret and not authenticated; this is accepted by the brief.
