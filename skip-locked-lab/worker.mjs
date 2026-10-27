import pg from "pg";

const name = process.argv[2] ?? "A";
let slowFirstJob = process.argv.includes("slow"); // first job takes 15 s, longer than the lease
const db = new pg.Pool({ host: "localhost", port: 5432, user: "postgres", password: "postgres" });

let idle = 0;
const log = (msg) => console.log(`${new Date().toTimeString().slice(0, 8)} [${name}] ${msg}`);
const sleep = (s) => new Promise((r) => setTimeout(r, s * 1000));

while (true) {
  // 1. Claim: one statement, SKIP LOCKED, 10-second lease, attempt = fencing token
  const { rows } = await db.query(`
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
    RETURNING id, attempt`);
  if (rows.length === 0) {
    if (++idle > 5) { log("no jobs, bye"); break; }
    await sleep(2); // nothing to do, ask again later
    continue;
  }
  idle = 0;
  const { id, attempt } = rows[0];
  log(`took job ${id} (attempt ${attempt})`);

  // 2. Work: pretend to pay the winner
  await sleep(slowFirstJob ? 15 : 1);
  slowFirstJob = false;

  // 3. Finish: only if I'm still the owner
  const res = await db.query(
    "UPDATE jobs SET status = 'done' WHERE id = $1 AND attempt = $2", [id, attempt]);
  log(res.rowCount === 1
    ? `done job ${id}`
    : `LOST job ${id}: someone else owns it now, my result is ignored`);
}
await db.end();
