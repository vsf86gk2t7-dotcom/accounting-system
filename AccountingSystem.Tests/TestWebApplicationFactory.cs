using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AccountingSystem.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
private readonly string _databaseName =
$"AccountingSystemTest_{Guid.NewGuid():N}";

private readonly string _connectionString;

private const string MasterConnectionString =
@"Server=.\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60";

public TestWebApplicationFactory()
{
_connectionString =
$@"Server=.\SQLEXPRESS;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60";
}

protected override void ConfigureWebHost(IWebHostBuilder builder)
{
builder.ConfigureAppConfiguration((_, config) =>
{
config.AddInMemoryCollection(new Dictionary<string, string?>
{
["ConnectionStrings:DefaultConnection"] = _connectionString
});
});
}

protected override void Dispose(bool disposing)
{
base.Dispose(disposing);

if (!disposing) return;

try
{
using var conn = new SqlConnection(MasterConnectionString);
conn.Open();

using var cmd = conn.CreateCommand();
cmd.CommandText = $@"
IF DB_ID(N'{_databaseName}') IS NOT NULL
BEGIN
ALTER DATABASE [{_databaseName}]
SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [{_databaseName}];
END";
cmd.ExecuteNonQuery();
}
catch
{
// best-effort cleanup
}
}
}
