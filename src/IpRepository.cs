using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

public class IpRepository
{
    private readonly string _connectionString;
    private readonly ILogger<IpRepository> _logger;

    public IpRepository(ILogger<IpRepository> logger)
    {
        _logger = logger;
        var dbPath = Path.Combine("/data", "ip_history.db");
        _connectionString = $"Data Source={dbPath}";
        EnsureTable();
    }

    private void EnsureTable()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ip_history (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                ip        TEXT    NOT NULL,
                recorded_at TEXT  NOT NULL
            );
        """;
        cmd.ExecuteNonQuery();
    }

    public string? GetLastIp()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ip FROM ip_history ORDER BY id DESC LIMIT 1;";
        return cmd.ExecuteScalar() as string;
    }

    public void SaveIp(string ip)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO ip_history (ip, recorded_at) VALUES ($ip, $ts);";
        cmd.Parameters.AddWithValue("$ip", ip);
        cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
        _logger.LogInformation("[DB] IP {Ip} salvo.", ip);
    }
}

