using Npgsql;
using Xunit;

namespace App.Tests;

// Needs the skip-lab Postgres from TUTORIAL.md. Uses a TEMP jobs table, so the lab table is untouched.
public class ProgramTests
{
    [Fact]
    public async Task Expired_lease_is_reclaimed_and_old_owner_is_fenced()
    {
        await using var conn = new NpgsqlConnection("Host=localhost;Port=5432;Username=postgres;Password=postgres");
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await Exec(conn, """
            CREATE TEMP TABLE jobs (id int PRIMARY KEY, status text NOT NULL DEFAULT 'queued',
                                    attempt int NOT NULL DEFAULT 0, lease_until timestamptz);
            INSERT INTO jobs (id) VALUES (777);
            """);

        Assert.Equal((777, 1), await Claim(conn));
        Assert.Null(await Claim(conn)); // lease still valid: nothing to take

        await Exec(conn, "UPDATE jobs SET lease_until = now() - interval '1 second'"); // A is too slow
        Assert.Equal((777, 2), await Claim(conn));

        Assert.Equal(0, await Finish(conn, 777, attempt: 1)); // A: LOST
        Assert.Equal(1, await Finish(conn, 777, attempt: 2)); // B: done
    }

    static async Task<(int, int)?> Claim(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand(Program.ClaimSql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? (reader.GetInt32(0), reader.GetInt32(1)) : null;
    }

    static async Task<int> Finish(NpgsqlConnection conn, int id, int attempt)
    {
        await using var cmd = new NpgsqlCommand(Program.FinishSql, conn);
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(attempt);
        return await cmd.ExecuteNonQueryAsync();
    }

    static async Task Exec(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
