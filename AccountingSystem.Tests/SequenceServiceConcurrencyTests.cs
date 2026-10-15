using System.Collections.Concurrent;
using AccountingSystem.Data;
using AccountingSystem.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Tests;

public sealed class SequenceServiceConcurrencyTests : IAsyncLifetime
{
private readonly string _databaseName =
$"SeqConcurrencyTest_{Guid.NewGuid():N}";

private readonly string _connectionString;

private const string MasterConnectionString =
@"Server=.\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60";

public SequenceServiceConcurrencyTests()
{
_connectionString =
$@"Server=.\SQLEXPRESS;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60";
}

public async Task InitializeAsync()
{
await using var context = CreateContext();
await context.Database.MigrateAsync();
}

public async Task DisposeAsync()
{
try
{
await using var conn = new SqlConnection(MasterConnectionString);
await conn.OpenAsync();

await using var cmd = conn.CreateCommand();
cmd.CommandText = $@"
IF DB_ID(N'{_databaseName}') IS NOT NULL
BEGIN
ALTER DATABASE [{_databaseName}]
SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [{_databaseName}];
END";
await cmd.ExecuteNonQueryAsync();
}
catch
{
// best-effort
}
}

private ApplicationDbContext CreateContext()
{
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
.UseSqlServer(_connectionString)
.Options;

return new ApplicationDbContext(options);
}

[Fact]
public async Task NextValueAsync_20Concurrent_ReturnsUniqueSequentialValues()
{
const int concurrency = 20;
var bag = new ConcurrentBag<long>();

var tasks = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
{
await using var ctx = CreateContext();
var service = new SequenceService(ctx);
var value = await service.NextValueAsync("TEST");
bag.Add(value);
}));

await Task.WhenAll(tasks);

var ordered = bag.OrderBy(v => v).ToArray();

Assert.Equal(concurrency, ordered.Length);
Assert.Equal(
Enumerable.Range(1, concurrency).Select(i => (long)i).ToArray(),
ordered);
}

[Fact]
public async Task NextFormattedAsync_20Concurrent_ReturnsUniqueFormattedValues()
{
const int concurrency = 20;
var bag = new ConcurrentBag<string>();

var tasks = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
{
await using var ctx = CreateContext();
var service = new SequenceService(ctx);
var value = await service.NextFormattedAsync("INV", "SALES", digits: 4);
bag.Add(value);
}));

await Task.WhenAll(tasks);

var ordered = bag.OrderBy(v => v).ToArray();

Assert.Equal(concurrency, ordered.Length);

var numbers = ordered
.Select(s => int.Parse(s.Split('-').Last()))
.OrderBy(n => n)
.ToArray();

Assert.Equal(Enumerable.Range(1, concurrency).ToArray(), numbers);

var today = DateTime.Today.ToString("yyyyMMdd");
Assert.All(ordered, s => Assert.Contains(today, s));
}
}
