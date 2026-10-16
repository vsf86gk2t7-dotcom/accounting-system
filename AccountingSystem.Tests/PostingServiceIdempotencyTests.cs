using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AccountingSystem.Tests;

public sealed class PostingServiceIdempotencyTests : IAsyncLifetime
{
private readonly string _databaseName =
$"PostingIdempotencyTest_{Guid.NewGuid():N}";

private readonly string _connectionString;

private const string MasterConnectionString =
@"Server=.\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60";

public PostingServiceIdempotencyTests()
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

// =========================================
// تجهيز: FiscalPeriod مفتوحة + حسابين
// =========================================
private async Task<(int debitAccountId, int creditAccountId)>
SeedRequiredDataAsync(ApplicationDbContext ctx)
{
var today = DateTime.Today;

// فترة مالية مفتوحة
ctx.FiscalPeriods.Add(new FiscalPeriod
{
PeriodName = today.Year.ToString(),
StartDate = new DateTime(today.Year, 1, 1),
EndDate = new DateTime(today.Year, 12, 31),
IsClosed = false,
CreatedAt = DateTime.UtcNow
});

// حسابين مستقلين (مش أب)
var debitAccount = new ChartAccount
{
Code = "9001",
Name = "حساب اختبار مدين",
Type = AccountType.Asset,
IsActive = true,
IsSystem = false,
CreatedAt = DateTime.UtcNow
};

var creditAccount = new ChartAccount
{
Code = "9002",
Name = "حساب اختبار دائن",
Type = AccountType.Liability,
IsActive = true,
IsSystem = false,
CreatedAt = DateTime.UtcNow
};

ctx.ChartAccounts.Add(debitAccount);
ctx.ChartAccounts.Add(creditAccount);

await ctx.SaveChangesAsync();

return (debitAccount.Id, creditAccount.Id);
}

private static List<JournalLineRequest> BuildLines(
int debitId, int creditId, decimal amount)
{
return new List<JournalLineRequest>
{
new()
{
ChartAccountId = debitId,
Debit = amount,
Description = "اختبار مدين"
},
new()
{
ChartAccountId = creditId,
Credit = amount,
Description = "اختبار دائن"
}
};
}

// =========================================
// الاختبار 1: استدعاء PostAsync مرتين
// =========================================
[Fact]
public async Task PostAsync_CalledTwiceWithSameKey_ReturnsSameEntry()
{
// Arrange
await using var setupCtx = CreateContext();
var (debitId, creditId) =
await SeedRequiredDataAsync(setupCtx);

var sourceType = JournalSourceType.Manual;
var sourceId = 12345;
var purpose = "TEST";

long firstEntryId;
string firstEntryNumber;

// Act 1 — النداء الأول
await using (var ctx1 = CreateContext())
{
var service = new PostingService(ctx1, new SequenceService(ctx1));

var result1 = await service.PostAsync(
sourceType,
sourceId,
DateTime.Today,
"اختبار Idempotency",
BuildLines(debitId, creditId, 100m),
isPosted: true,
userId: null,
purpose: purpose);

Assert.True(result1.Success,
$"First PostAsync failed: {result1.Error}");
Assert.NotNull(result1.JournalEntryId);

firstEntryId = result1.JournalEntryId!.Value;
firstEntryNumber = result1.EntryNumber!;
}

// Act 2 — النداء الثاني بنفس المفاتيح
await using (var ctx2 = CreateContext())
{
var service = new PostingService(ctx2, new SequenceService(ctx2));

var result2 = await service.PostAsync(
sourceType,
sourceId,
DateTime.Today,
"اختبار Idempotency (مكرر)",
BuildLines(debitId, creditId, 100m),
isPosted: true,
userId: null,
purpose: purpose);

Assert.True(result2.Success,
$"Second PostAsync failed: {result2.Error}");

// ✅ لازم يرجع نفس القيد
Assert.Equal(firstEntryId, (long?)result2.JournalEntryId);
Assert.Equal(firstEntryNumber, result2.EntryNumber);
}

// Assert — قيد واحد فقط في الداتابيز
await using (var verifyCtx = CreateContext())
{
var entries = await verifyCtx.JournalEntries
.Where(x =>
x.SourceType == sourceType &&
x.SourceId == sourceId &&
x.Purpose == purpose)
.ToListAsync();

Assert.Single(entries);
}
}

// =========================================
// الاختبار 2: Purpose مختلف → قيدان
// =========================================
[Fact]
public async Task PostAsync_DifferentPurpose_CreatesTwoEntries()
{
await using var setupCtx = CreateContext();
var (debitId, creditId) =
await SeedRequiredDataAsync(setupCtx);

var sourceType = JournalSourceType.Manual;
var sourceId = 99999;

await using (var ctx1 = CreateContext())
{
var service = new PostingService(ctx1, new SequenceService(ctx1));
var r1 = await service.PostAsync(
sourceType, sourceId, DateTime.Today,
"اختبار Purpose=REV",
BuildLines(debitId, creditId, 100m),
true, null, "REV");
Assert.True(r1.Success);
}

await using (var ctx2 = CreateContext())
{
var service = new PostingService(ctx2, new SequenceService(ctx2));
var r2 = await service.PostAsync(
sourceType, sourceId, DateTime.Today,
"اختبار Purpose=COGS",
BuildLines(debitId, creditId, 50m),
true, null, "COGS");
Assert.True(r2.Success);
}

await using (var verifyCtx = CreateContext())
{
var count = await verifyCtx.JournalEntries
.CountAsync(x =>
x.SourceType == sourceType &&
x.SourceId == sourceId);

Assert.Equal(2, count);
}
}
}
