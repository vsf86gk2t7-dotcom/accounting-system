using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AccountingSystem.Tests
{
	public class SalesInvoiceServiceTests
	{
		private readonly Mock<IPostingService> _postingMock;

		public SalesInvoiceServiceTests()
		{
			_postingMock = new Mock<IPostingService>();
		}

		private static ApplicationDbContext CreateContext()
		{
			var options = new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
				.ConfigureWarnings(w => w.Ignore(
					Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
				.Options;
			return new ApplicationDbContext(options);
		}

		[Fact]
		public async Task ConfirmInvoiceAsync_InvoiceNotFound_ReturnsFail()
		{
			using var context = CreateContext();
			var service = new SalesInvoiceService(context, _postingMock.Object);

			var result = await service.ConfirmInvoiceAsync(999, null);

			Assert.False(result.Success);
			Assert.Equal("الفاتورة غير موجودة.", result.Error);
		}

		[Fact]
		public async Task ConfirmInvoiceAsync_InvoiceHasNoItems_ReturnsFail()
		{
			using var context = CreateContext();
			var service = new SalesInvoiceService(context, _postingMock.Object);

			var invoice = new SalesInvoice
			{
				Id = 1,
				Status = SalesInvoiceStatus.Draft,
				Items = new List<SalesInvoiceItem>()
			};
			context.SalesInvoices.Add(invoice);
			await context.SaveChangesAsync();

			var result = await service.ConfirmInvoiceAsync(1, null);

			Assert.False(result.Success);
			Assert.Equal("لا يمكن تأكيد فاتورة بدون بنود.", result.Error);
		}

		[Fact]
		public async Task CancelInvoiceAsync_InvoiceNotFound_ReturnsFail()
		{
			using var context = CreateContext();
			var service = new SalesInvoiceService(context, _postingMock.Object);

			var result = await service.CancelInvoiceAsync(999, null);

			Assert.False(result.Success);
			Assert.Equal("الفاتورة غير موجودة.", result.Error);
		}

		[Fact]
		public async Task CancelInvoiceAsync_InvoiceNotConfirmed_ReturnsFail()
		{
			using var context = CreateContext();
			var service = new SalesInvoiceService(context, _postingMock.Object);

			var invoice = new SalesInvoice
			{
				Id = 1,
				Status = SalesInvoiceStatus.Draft,
				Items = new List<SalesInvoiceItem>()
			};
			context.SalesInvoices.Add(invoice);
			await context.SaveChangesAsync();

			var result = await service.CancelInvoiceAsync(1, null);

			Assert.False(result.Success);
			Assert.Equal("لا يمكن إلغاء الفاتورة إلا وهي مؤكدة.", result.Error);
		}

		[Fact]
		public async Task ConfirmInvoiceAsync_ValidInvoice_ReturnsSuccess()
		{
			using var context = CreateContext();
			var service = new SalesInvoiceService(context, _postingMock.Object);

			var invoice = new SalesInvoice
			{
				Id = 1,
				Status = SalesInvoiceStatus.Draft,
				Items = new List<SalesInvoiceItem>
				{
					new() { Id = 1, Quantity = 1, UnitPrice = 100m }
				}
			};
			context.SalesInvoices.Add(invoice);
			await context.SaveChangesAsync();

			var result = await service.ConfirmInvoiceAsync(1, null);

			Assert.True(result.Success);
			Assert.Null(result.Error);
			Assert.NotNull(result.Invoice);
			Assert.Equal(SalesInvoiceStatus.Confirmed, result.Invoice!.Status);
		}

		[Fact]
		public async Task CancelInvoiceAsync_ValidInvoice_ReturnsSuccess()
		{
			using var context = CreateContext();
			var service = new SalesInvoiceService(context, _postingMock.Object);

			var invoice = new SalesInvoice
			{
				Id = 1,
				Status = SalesInvoiceStatus.Confirmed,
				Items = new List<SalesInvoiceItem>
				{
					new() { Id = 1, Quantity = 1, UnitPrice = 100m }
				}
			};
			context.SalesInvoices.Add(invoice);
			await context.SaveChangesAsync();

			var result = await service.CancelInvoiceAsync(1, null);

			Assert.True(result.Success);
			Assert.Null(result.Error);
		}
	}
}
