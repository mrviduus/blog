using Npgsql;

var name = args.Length > 0 ? args[0] : "A";
var slowFirstJob = args.Contains("slow"); // first job takes 15 s, longer than the lease
await using var db = NpgsqlDataSource.Create("Host=localhost;Port=5432;Username=postgres;Password=postgres");

var idle = 0;
void Log(string msg) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} [{name}] {msg}");

while (true)
{
    // 1. Claim: one statement, SKIP LOCKED, 10-second lease, attempt = fencing token
    (int Id, int Attempt)? job = null;
    await using (var claim = db.CreateCommand(Program.ClaimSql))
    await using (var reader = await claim.ExecuteReaderAsync())
        if (await reader.ReadAsync()) job = (reader.GetInt32(0), reader.GetInt32(1));

    if (job is null)
    {
        if (++idle > 5) { Log("no jobs, bye"); break; }
        await Task.Delay(TimeSpan.FromSeconds(2)); // nothing to do, ask again later
        continue;
    }
    idle = 0;
    var (id, attempt) = job.Value;
    Log($"took job {id} (attempt {attempt})");

    // 2. Work: pretend to pay the winner
    await Task.Delay(TimeSpan.FromSeconds(slowFirstJob ? 15 : 1));
    slowFirstJob = false;

    // 3. Finish: only if I'm still the owner
    await using var finish = db.CreateCommand(Program.FinishSql);
    finish.Parameters.AddWithValue(id);
    finish.Parameters.AddWithValue(attempt);
    Log(await finish.ExecuteNonQueryAsync() == 1
        ? $"done job {id}"
        : $"LOST job {id}: someone else owns it now, my result is ignored");
}

public partial class Program
{
    public const string ClaimSql = """
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
        RETURNING id, attempt
        """;

    public const string FinishSql = "UPDATE jobs SET status = 'done' WHERE id = $1 AND attempt = $2";
}
