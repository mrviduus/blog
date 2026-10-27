# SKIP LOCKED lab: two terminals, step by step

Everything below was run for real (Postgres 16). Your times and timestamps will differ, everything else should match.
Reset between demos with the RESET block. Terminal A and Terminal B are two separate windows.

## 0. Start Postgres (any terminal)

```bash
docker run --name skip-lab -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16
```

Wait until Postgres is ready (the first start takes a few seconds):

```bash
until docker exec skip-lab pg_isready -U postgres; do sleep 1; done
```

Open psql in **Terminal A** and in **Terminal B**:

```bash
docker exec -it skip-lab psql -U postgres
```

## 1. Create the table (Terminal A)

```sql
CREATE TABLE jobs (
  id          int PRIMARY KEY,
  payload     text        NOT NULL,
  status      text        NOT NULL DEFAULT 'queued',
  attempt     int         NOT NULL DEFAULT 0,
  started_at  timestamptz,
  lease_until timestamptz
);

INSERT INTO jobs (id, payload)
SELECT g, 'pay winner #' || g
FROM generate_series(777, 786) AS g;

SELECT id, payload, status FROM jobs ORDER BY id LIMIT 3;
```

Expected:

```
 id  |     payload     | status
-----+-----------------+--------
 777 | pay winner #777 | queued
 778 | pay winner #778 | queued
 779 | pay winner #779 | queued
(3 rows)
```

RESET (use before every demo):

```sql
UPDATE jobs SET status = 'queued', attempt = 0, started_at = NULL, lease_until = NULL;
```

---

## Demo 1: FOR UPDATE without SKIP LOCKED (B waits)

**A:**
```sql
BEGIN;
SELECT id, payload FROM jobs WHERE status = 'queued' ORDER BY id LIMIT 1 FOR UPDATE;
```
```
 id  |     payload
-----+-----------------
 777 | pay winner #777
(1 row)
```

**B:**
```sql
BEGIN;
SELECT id, payload FROM jobs WHERE status = 'queued' ORDER BY id LIMIT 1 FOR UPDATE;
```
Nothing happens. The cursor just waits. B is blocked by A's lock on 777.

**A:**
```sql
UPDATE jobs SET status = 'running' WHERE id = 777;
COMMIT;
```

**B** wakes up only now:
```
 id  |     payload
-----+-----------------
 778 | pay winner #778
(1 row)
```
**B:** `COMMIT;`

Lesson: plain `FOR UPDATE` is safe, but every worker stands in line behind the first one.

---

## Demo 2: with SKIP LOCKED (B doesn't wait)

RESET first.

**A:**
```sql
BEGIN;
SELECT id, payload FROM jobs WHERE status = 'queued' ORDER BY id LIMIT 1 FOR UPDATE SKIP LOCKED;
```
```
 777 | pay winner #777
```

**B:**
```sql
BEGIN;
SELECT id, payload FROM jobs WHERE status = 'queued' ORDER BY id LIMIT 1 FOR UPDATE SKIP LOCKED;
```
Returns immediately:
```
 778 | pay winner #778
```

**A:** `COMMIT;`  **B:** `COMMIT;`

Lesson: B skipped the locked row and took the next one.

---

## Demo 3: the worker crashes after the claim (stuck job)

RESET first. This is the real claim: one statement, lock + status change together.

**A:**
```sql
UPDATE jobs
SET status = 'running', started_at = now(), attempt = attempt + 1
WHERE id = (
  SELECT id FROM jobs
  WHERE status = 'queued'
  ORDER BY id
  LIMIT 1
  FOR UPDATE SKIP LOCKED
)
RETURNING id, status, attempt;
```
```
 id  | status  | attempt
-----+---------+---------
 777 | running |       1
(1 row)

UPDATE 1
```

Now "crash" worker A: type `\q` in Terminal A (or close the window).

**B:** run the same claim:
```
 id  | status  | attempt
-----+---------+---------
 778 | running |       1
(1 row)
```

**B:**
```sql
SELECT id, status, attempt, started_at FROM jobs WHERE status = 'running' ORDER BY id;
```
```
 id  | status  | attempt |          started_at
-----+---------+---------+-------------------------------
 777 | running |       1 | 2026-10-26 21:14:11.65891-04
 778 | running |       1 | 2026-10-26 21:14:14.062933-04
(2 rows)
```

Lesson: nobody will ever take 777 again. It's `running`, and every worker looks only for `queued`. The lock disappeared at commit; only the status is left.

Open Terminal A again: `docker exec -it skip-lab psql -U postgres`

---

## Demo 4: lease + fencing token (the slow worker wakes up)

RESET first. Same claim, plus "or the lease is older than 10 seconds".

**A:**
```sql
UPDATE jobs
SET status = 'running', started_at = now(), attempt = attempt + 1
WHERE id = (
  SELECT id FROM jobs
  WHERE status = 'queued'
     OR (status = 'running' AND started_at < now() - interval '10 seconds')
  ORDER BY id
  LIMIT 1
  FOR UPDATE SKIP LOCKED
)
RETURNING id, status, attempt;
```
```
 777 | running |       1
```

A is now "slow". **Wait 10 seconds**, then run the same claim in **B**:
```
 777 | running |       2
```
B took 777 back, because A's lease expired. Note attempt = 2.

**A** wakes up and tries to save its result:
```sql
UPDATE jobs SET status = 'done' WHERE id = 777 AND attempt = 1;
```
```
UPDATE 0
```

**B** saves its result:
```sql
UPDATE jobs SET status = 'done' WHERE id = 777 AND attempt = 2;
SELECT id, status, attempt FROM jobs WHERE id = 777;
```
```
UPDATE 1
 id  | status | attempt
-----+--------+---------
 777 | done   |       2
(1 row)
```

Lesson: `UPDATE 0` is the fencing token at work. The old owner can't overwrite the new one.
Try it without `AND attempt = 1` and you get `UPDATE 1`: A silently overwrites B's result.

---

## Demo 5: heartbeat (a slow but alive worker keeps its job)

RESET first. Now the lease is stored as `lease_until`.

**A:**
```sql
UPDATE jobs
SET status = 'running', lease_until = now() + interval '10 seconds', attempt = attempt + 1
WHERE id = (
  SELECT id FROM jobs
  WHERE status = 'queued'
     OR (status = 'running' AND lease_until < now())
  ORDER BY id
  LIMIT 1
  FOR UPDATE SKIP LOCKED
)
RETURNING id, attempt, lease_until;
```
```
 id  | attempt |          lease_until
-----+---------+-------------------------------
 777 |       1 | 2026-10-26 21:14:59.185474-04
```

After ~5 seconds, **A** sends a heartbeat:
```sql
UPDATE jobs SET lease_until = now() + interval '10 seconds' WHERE id = 777 AND attempt = 1;
```
```
UPDATE 1
```

After ~6 more seconds (11 s since the claim), **B** runs the same claim:
```
 id  | attempt |          lease_until
-----+---------+-------------------------------
 778 |       1 | 2026-10-26 21:15:12.588453-04
```

Lesson: without the heartbeat, B would have taken 777 (like in Demo 4). With it, A keeps its job and B moves on to 778.

---

## Demo 6: a real worker, two copies

Needs Node.js. In a new folder (Terminal A, outside psql):

```bash
mkdir skip-worker && cd skip-worker
npm init -y && npm i pg@8
```

Save `worker.mjs` from this folder there. RESET the table in psql first.

**Terminal A** (a slow worker: its first job takes 15 s, longer than the 10 s lease):
```bash
node worker.mjs A slow
```
**Terminal B** (start right after A):
```bash
node worker.mjs B
```

Expected (real run):

```
--- A
21:17:13 [A] took job 777 (attempt 1)
21:17:28 [A] LOST job 777: someone else owns it now, my result is ignored

--- B
21:17:14 [B] took job 778 (attempt 1)
21:17:15 [B] done job 778
...
21:17:22 [B] took job 786 (attempt 1)
21:17:23 [B] done job 786
21:17:25 [B] took job 777 (attempt 2)
21:17:26 [B] done job 777
21:17:36 [B] no jobs, bye
```

What you see:
- A and B never take the same job at the same time (SKIP LOCKED).
- A is too slow, its lease expires, B takes 777 as attempt 2 (lease).
- A finishes later and its write changes 0 rows, so it prints LOST (fencing token).

---

## Demo 6 (C#): the same worker in .NET

Needs the .NET 10 SDK. The code is in `worker-cs/src/App/Program.cs`: same SQL, same behaviour, Npgsql instead of `pg`.
It was created with the [console-with-tests](https://github.com/mrviduus/ConsoleWithTests.Template) template:

```bash
dotnet new install console-with-tests
dotnet new console-with-tests -n worker-cs -o worker-cs
cd worker-cs && dotnet add src/App package Npgsql
```

Optional: `dotnet test` runs one test against the same Postgres. It uses a TEMP table, so your `jobs` table is not touched.

RESET the table in psql first. Then from `worker-cs`:

**Terminal A:**
```bash
dotnet run --project src/App -- A slow
```
**Terminal B** (start right after A):
```bash
dotnet run --project src/App -- B
```

Real run:

```
--- A
21:36:26 [A] took job 777 (attempt 1)
21:36:41 [A] LOST job 777: someone else owns it now, my result is ignored
21:36:51 [A] no jobs, bye

--- B
21:36:27 [B] took job 778 (attempt 1)
21:36:28 [B] done job 778
...
21:36:35 [B] took job 786 (attempt 1)
21:36:36 [B] done job 786
21:36:38 [B] took job 777 (attempt 2)
21:36:39 [B] done job 777
21:36:49 [B] no jobs, bye
```

```sql
SELECT id, status, attempt FROM jobs ORDER BY id;
```
```
 id  | status | attempt
-----+--------+---------
 777 | done   |       2
 778 | done   |       1
 779 | done   |       1
 780 | done   |       1
 781 | done   |       1
 782 | done   |       1
 783 | done   |       1
 784 | done   |       1
 785 | done   |       1
 786 | done   |       1
(10 rows)
```

Same story as the Node.js run: B never takes a job A holds, 777 comes back as attempt 2, and A's late write is fenced out.

Clean up when done:
```bash
docker rm -f skip-lab
```
