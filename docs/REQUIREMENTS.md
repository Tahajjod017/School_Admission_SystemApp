# Requirements (my reading of the brief)

## 1. The problem in one paragraph
A private school opens online admission for a new academic year. Admission is **first-come-first-served, no lottery**. Every *class* (a shift is its own class, e.g. "Class 6 – Morning Shift") has a limited number of seats. When admission opens, thousands of parents apply at the same moment.

Applying **holds** a seat in the student's name for a limited time. If the admission fee is "paid" in time, the admission is **confirmed**. Otherwise the seat is released automatically for others.

I must build the **backend** (C#/.NET + PostgreSQL) and the **frontend** (React). School admin and parents do everything from the UI.

## 2. Core domain
| Concept | Meaning |
|---|---|
| Class | `id`, `name`, `totalSeats`. One shift = one class. |
| Hold (application) | `holdId`, `classId`, `studentId`, `status`, `expiresAt` |
| Student | No account. Identified only by a **17-digit birth registration number** sent as `X-Student-Id`. |
| Status | `HELD` → `CONFIRMED` / `EXPIRED` / `CANCELLED` |

Seat math: `availableSeats = totalSeats - heldSeats - confirmedSeats`
(`heldSeats` counts only holds that are still unexpired.)

## 3. Technical constraints
- Backend: C# (.NET 8+), PostgreSQL, any ORM. Listens on `http://localhost:8080`.
- Frontend: React 18+, JS or TS, Vite recommended, any UI library.
- DB connection string and frontend→backend URL are **environment variables**.
- Whole thing runs with **`docker compose up`** from the repo root (PostgreSQL + backend + frontend). Only Docker may be assumed on the grader's machine.
- Anything unclear: pick a reasonable option and explain in `ASSUMPTIONS.md`.

## 4. UI requirements
| Screen | What it does |
|---|---|
| Student ID box (top of every page) | Enter 17-digit birth reg. number; used as identity. No login. |
| Create class (admin) | Form: class name + seat count |
| Class list | Per class: total, held, confirmed, available seats; **Apply** button |
| Application | Status, time left until expiry, **Pay admission fee** and **Cancel** buttons |
| My applications | All of this student's applications with status |

UI rules:
- Everything works from the UI (no Postman, no direct DB).
- When a hold expires, the page shows `EXPIRED` **without reload** and disables "Pay admission fee".
- Class seat numbers update **by themselves every few seconds**.
- After refresh or closing/reopening the browser, the student still sees their active applications.
- Every error (no seats, expired, bad input, server down) is shown in plain language. No blank page, no console-only errors.
- "Pay admission fee" just simulates a successful payment.
- Pretty is not required; **correct and easy to use** is.

## 5. API contract (must be followed exactly)
Extra response fields are allowed. Removing or renaming specified fields is **not**. Request/response is JSON. IDs (`c_123`, `h_789`) are examples; format is my choice.

| # | Method & Path | Header | Success | Notes |
|---|---|---|---|---|
| 1 | `POST /classes` | – | 201 `{id,name,totalSeats}` | request `{name,totalSeats}` |
| 2 | `GET /classes` | – | 200 `[{id,name,totalSeats,heldSeats,confirmedSeats,availableSeats}]` | |
| 3 | `GET /classes/{classId}` | – | 200 same object | |
| 4 | `POST /classes/{classId}/holds` | `X-Student-Id` | 201 `{holdId,classId,studentId,status:"HELD",expiresAt}` | **409** if no seat |
| 5 | `GET /holds/{holdId}` | – | 200 `{holdId,classId,status,expiresAt}` | status ∈ HELD, CONFIRMED, EXPIRED, CANCELLED |
| 6 | `POST /holds/{holdId}/confirm` | `X-Student-Id` | 200 `{holdId,status:"CONFIRMED"}` | |
| 7 | `DELETE /holds/{holdId}` | `X-Student-Id` | **204** | cancel |
| 8 | `GET /me/holds` | `X-Student-Id` | 200 `[{holdId,classId,className,status,expiresAt}]` | |

`X-Student-Id` = student's 17-digit birth registration number.

## 6. Backend rules (mandatory)
1. `held + confirmed` **never exceeds** `totalSeats`, no matter how many requests arrive at once.
2. Hold duration = env var `HOLD_DURATION_SECONDS` (default `600`). After expiry the seat returns **by itself**, no API call needed.
3. One student: at most **one active (HELD or CONFIRMED)** application per class.
4. Only the **owner** can confirm, only while still `HELD` and **not expired**.
5. Confirming the same hold twice does **not** increase `confirmedSeats` twice (idempotent).
6. Data survives restarts (`docker compose down` then `up`).

## 7. Bonus (optional)
- Two backend processes on different ports, **same DB**, still never exceed `totalSeats`. Explain how in `ASSUMPTIONS.md`.
- Automated frontend test.

## 8. Deliverables checklist
- [ ] Git repo with `backend/` and `frontend/`, **many commits during the work** (not one big commit at the end)
- [ ] `Dockerfile` for backend and for frontend
- [ ] `docker-compose.yml` at repo root
- [ ] `README.md`: how to run with Docker, frontend URL, how to run tests
- [ ] `ASSUMPTIONS.md`: each unclear point, what I chose, why
- [ ] Backend automated tests: **at least 3**, one of them a **concurrency test** (many parallel requests, never more holds than seats)
- [ ] `AI_USAGE.md`: which AI tool, where used, where AI was wrong, how I caught it
- [ ] Bonus items (optional)

## 9. Submission rules
- Public GitHub repo; submit the link before the deadline.
- The repo state **at the deadline** is what gets graded; later commits/pushes are ignored.
- Do **not** share the assignment PDF publicly or with anyone.
