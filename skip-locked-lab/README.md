# SKIP LOCKED lab

Code for the post [What SKIP LOCKED Solves, and What It Leaves to You](https://vasyl.blog/?p=668).

A tiny job-queue worker on Postgres, in two versions with the same SQL and the same behaviour:

- [`worker.mjs`](worker.mjs): Node.js + `pg`
- [`worker-cs/src/App/Program.cs`](worker-cs/src/App/Program.cs): C# (.NET 10) + Npgsql, created with the [console-with-tests](https://github.com/mrviduus/ConsoleWithTests.Template) template

What the worker does:

1. **Claim** a job in one statement: `UPDATE ... WHERE id = (SELECT ... FOR UPDATE SKIP LOCKED) RETURNING id, attempt`
2. **Lease** it for 10 seconds (`lease_until`). An expired lease can be taken by another worker.
3. **Fence** the result: `attempt = attempt + 1` on every claim, and the finish only counts if the attempt still matches:
   `UPDATE jobs SET status = 'done' WHERE id = $1 AND attempt = $2`

[TUTORIAL.md](TUTORIAL.md) walks through it step by step in two psql terminals, from a plain `FOR UPDATE` that blocks (Demo 1) to two real workers (Demo 6).

## Try it yourself

![Workers A and B side by side: A is slow, B takes 777 back as attempt 2, A prints LOST](demo/demo.gif)

The GIF is a real run at 2x speed, recorded with [vhs](https://github.com/charmbracelet/vhs): `demo/record.sh`.

Start Postgres and create the table: [TUTORIAL.md, steps 0 and 1](TUTORIAL.md#0-start-postgres-any-terminal).

Run two workers in two terminals. A is slow on purpose: its first job takes 15 s, longer than the 10 s lease.

**C#** (from `worker-cs`):

```bash
dotnet run --project src/App -- A slow   # Terminal A
dotnet run --project src/App -- B        # Terminal B, right after A
```

**Node.js** (`npm i pg@8` first):

```bash
node worker.mjs A slow   # Terminal A
node worker.mjs B        # Terminal B, right after A
```

Real output (C#):

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

What you see:

- A and B never take the same job at the same time (SKIP LOCKED).
- A is too slow, its lease expires, and B takes 777 as attempt 2 (lease).
- A finishes later, its write changes 0 rows, and it prints LOST (fencing token).

Final state: every job is `done`, and 777 has `attempt = 2`.

## Test

`dotnet test` in `worker-cs` runs one test against the same Postgres: expired lease → attempt 2 → the old attempt's finish changes 0 rows. It uses a TEMP table, so your `jobs` table is not touched.
