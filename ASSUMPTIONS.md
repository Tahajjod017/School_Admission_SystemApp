# ASSUMPTIONS

The brief leaves several points open. Below is each one, what I chose, and why.
*(Draft: finalized after implementation. Items are updated if the code ends up different.)*

## API behaviour

| # | Not specified | My choice | Why |
|---|---|---|---|
| 1 | Error body format | `{ "code": "...", "message": "..." }` | Simple, lets the UI show friendly text by code |
| 2 | Missing or malformed `X-Student-Id` (not exactly 17 digits) | **400** | It is a bad request, not an auth system |
| 3 | `POST /classes` validation | `name` non-empty (trimmed, max 100 chars), `totalSeats` integer 1–100000, else **400** | Prevents junk and absurd values |
| 4 | Duplicate class names | **Allowed** | Two classes may legitimately share a name across years, and tests/admins may re-create the same name |
| 5 | Unknown class / hold id | **404** | Standard |
| 6 | Student already has an active (HELD/CONFIRMED) application in that class | **409** `ALREADY_APPLIED` | Rule says only one active per class |
| 7 | Student applies again after a hold was cancelled or expired | **Allowed**, new hold created | Old one no longer counts as active |
| 8 | Confirm/cancel by someone who doesn't own the hold | **403** | Rule: only the owner may act |
| 9 | Confirm an expired hold | **409** `HOLD_EXPIRED`; status shown as `EXPIRED` | Time-expired holds are treated as expired even if the cleanup job hasn't run yet |
| 10 | Confirm a `CANCELLED` / `EXPIRED` hold | **409** `INVALID_STATE` | Only `HELD` can be confirmed |
| 11 | Confirm an already `CONFIRMED` hold | **200** `CONFIRMED`, no extra seat counted | Rule: idempotent |
| 12 | `DELETE` on a `HELD` hold | Becomes `CANCELLED`, **204**, seat freed immediately | Per brief |
| 13 | `DELETE` on `CANCELLED` or `EXPIRED` hold | **204** (no-op) | Idempotent; nothing left to cancel |
| 14 | `DELETE` on a `CONFIRMED` hold | **409** `INVALID_STATE` | Refunds/withdrawals aren't in scope |
| 15 | `GET /holds/{id}` requires header? | **No** (brief lists no header). Returns holdId, classId, status, expiresAt | Follows the contract table |
| 16 | `GET /me/holds` contents and order | **All** of the student's holds incl. expired/cancelled, newest first | Brief: "all applications and the status of each" |
| 17 | What counts as `heldSeats` | Only `HELD` holds with `expiresAt` still in the future | Seat returns automatically at expiry |
| 18 | ID format | `c_` / `h_` + 12 random lowercase hex chars | Opaque, collision-safe, matches examples |
| 19 | Timestamps | UTC, ISO-8601 (`...Z`) | As in the example |
| 20 | Extra response fields | `serverTime` added to hold responses | Lets the UI compute a correct countdown even if the user's clock is wrong (contract allows extra fields) |
| 21 | CORS | Browser may call backend from the frontend origin | UI (port 3000) calls the API (port 8080) directly |

## Concurrency and time
- **Seat limit under load:** each class's hold/applications run inside a PostgreSQL transaction that first locks the class row (`SELECT ... FOR UPDATE`). Requests for the same class are serialized *in the database*, so the rule `held + confirmed ≤ totalSeats` holds across threads **and across multiple backend processes** sharing one DB (bonus). A partial unique index also guarantees a student can't have two active rows in a class even if code had a bug.
- **Clock:** all expiry decisions use the database clock (`clock_timestamp()`), so two backend instances can't disagree.
- **Automatic release:** no API call is needed. Counts ignore holds past `expiresAt`, and a background job tidies statuses every few seconds.

## UI
| # | Not specified | My choice |
|---|---|---|
| 1 | Admin protection | None; the brief says there is no login. Create-class form is visible to everyone |
| 2 | Student ID entry | One field at the top; validated as exactly 17 digits; remembered in the browser (`localStorage`) so refresh/reopen works |
| 3 | Refresh intervals | Class list ~3 s, open application ~5 s, countdown every second |
| 4 | Language | English UI with plain-language messages *(change here if Bangla is required)* |
| 5 | Frontend URL | `http://localhost:3000` |
| 6 | Payment | "Pay admission fee" button only calls `confirm`; no real gateway |
| 7 | Applying while no student ID is set | Button prompts the user to enter the ID first; no request is sent |

## Out of scope (deliberately)
Authentication, real payments, refunds, email/SMS, admin editing/deleting classes, pagination, i18n.
