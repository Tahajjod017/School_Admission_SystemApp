# School Admission: Seat Hold & Confirm

First-come-first-served online admission. Applying holds a seat for a limited time; "paying" the admission fee confirms it; unpaid holds expire and free the seat automatically.

> **Status: draft.** Commands below are the intended ones and get verified at milestone M11.

## Requirements
Only **Docker** (with Docker Compose v2). Nothing else needs to be installed.

## Run
```bash
docker compose up --build
```
| Service | URL |
|---|---|
| Frontend (open this) | http://localhost:3000 |
| Backend API | http://localhost:8080 |
| PostgreSQL | internal to Docker (not published) |

Data is stored in a Docker volume, so `docker compose down` followed by `docker compose up` keeps everything.
To wipe all data: `docker compose down -v`.

### Configuration (environment variables)
| Variable | Used by | Default | Meaning |
|---|---|---|---|
| `ConnectionStrings__Default` | backend | points to the `db` service | PostgreSQL connection string |
| `HOLD_DURATION_SECONDS` | backend | `600` | How long a seat is held before it expires |
| `VITE_API_URL` | frontend (build arg) | `http://localhost:8080` | Backend URL as seen **from the browser** |

## How to use the app
1. Enter a 17-digit birth registration number in the box at the top (this is the student's identity; there is no login).
2. **Classes** page: admin creates a class (name + seats). Everyone sees live seat counts.
3. Press **Apply** on a class → a seat is held and a countdown starts.
4. Press **Pay admission fee** before the countdown ends to confirm, or **Cancel** to release the seat.
5. **My applications** lists all applications and their status.

## Tests
```bash
# Backend (unit/integration + concurrency), runs against a real PostgreSQL started by compose
docker compose --profile test run --rm backend-tests

# Frontend
docker compose --profile test run --rm frontend-tests
```

## API
See `docs/REQUIREMENTS.md` §5 for the contract and `ASSUMPTIONS.md` for status codes in unspecified cases.

## Project docs
- `ASSUMPTIONS.md`: decisions where the brief was unclear, and how concurrency is handled
- `AI_USAGE.md`: how AI tools were used
- `docs/REQUIREMENTS.md`, `docs/PLAN.md`: requirements breakdown and design plan
