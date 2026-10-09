using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Tests.Services;

public class DocumentNumberServiceTests : BaseTest
{
	private readonly DocumentNumberService _sut;

	public DocumentNumberServiceTests()
	{
		_sut = new DocumentNumberService(Context);
	}

	// =========================================
	// Helpers
	// =========================================

	private async Task SeedSalesInvoiceAsync(string invoiceNumber)
	{
		var invoice = new SalesInvoice
		{
			InvoiceNumber = invoiceNumber,
			InvoiceDate = DateTime.UtcNow,
			BranchId = 1,
			StoreId = 1,
			SubTotal = 100,
			TotalAmount = 100,
			Status = SalesInvoiceStatus.Confirmed,
			PaymentMethod = SalesPaymentMethod.Cash,
			CreatedAt = DateTime.UtcNow,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};
		Context.SalesInvoices.Add(invoice);
		await Context.SaveChangesAsync();
	}

	private async Task SeedPurchaseInvoiceAsync(string invoiceNumber)
	{
		var invoice = new PurchaseInvoice
		{
			InvoiceNumber = invoiceNumber,
			InvoiceDate = DateTime.UtcNow,
			StoreId = 1,
			SupplierId = 1,
			SubTotal = 100,
			TotalAmount = 100,
			Status = PurchaseInvoiceStatus.Posted,
			CreatedAt = DateTime.UtcNow,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};
		Context.PurchaseInvoices.Add(invoice);
		await Context.SaveChangesAsync();
	}

	private async Task SeedSalesReturnInvoiceAsync(string invoiceNumber)
	{
		var invoice = new SalesReturnInvoice
		{
			InvoiceNumber = invoiceNumber,
			OriginalSalesInvoiceId = 1,
			CustomerId = 1,
			StoreId = 1,
			BranchId = 1,
			ReturnDate = DateTime.UtcNow,
			TotalAmount = 100,
			IsPosted = true,
			CreatedAt = DateTime.UtcNow,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};
		Context.SalesReturnInvoices.Add(invoice);
		await Context.SaveChangesAsync();
	}

	private async Task SeedPurchaseReturnInvoiceAsync(string invoiceNumber)
	{
		var invoice = new PurchaseReturnInvoice
		{
			InvoiceNumber = invoiceNumber,
			StoreId = 1,
			SupplierId = 1,
			ReturnDate = DateTime.UtcNow,
			TotalAmount = 100,
			CreatedAt = DateTime.UtcNow
		};
		Context.PurchaseReturnInvoices.Add(invoice);
		await Context.SaveChangesAsync();
	}

	// =========================================
	// 1. أول رقم في حالة الجدول الفاضي
	// =========================================

	[Fact]
	public async Task GenerateNumberAsync_WhenTableEmpty_Returns0001()
	{
		var result = await _sut.GenerateNumberAsync(
			"SAL", "SalesInvoices", "20261005");

		result.Should().Be("SAL-20261005-0001");
	}

	[Fact]
	public async Task GenerateNumberAsync_WithDifferentTable_AlsoStartsWith0001()
	{
		var result = await _sut.GenerateNumberAsync(
			"PUR", "PurchaseInvoices", "20261005");

		result.Should().Be("PUR-20261005-0001");
	}

	[Fact]
	public async Task GenerateNumberAsync_WithUnknownTable_Returns0001()
	{
		var result = await _sut.GenerateNumberAsync(
			"XYZ", "UnknownTable", "20261005");

		result.Should().Be("XYZ-20261005-0001");
	}

	// =========================================
	// 2. الزيادة التدريجية
	// =========================================

	[Fact]
	public async Task GenerateNumberAsync_After0001_Returns0002()
	{
		await SeedSalesInvoiceAsync("SAL-20261005-0001");

		var result = await _sut.GenerateNumberAsync(
			"SAL", "SalesInvoices", "20261005");

		result.Should().Be("SAL-20261005-0002");
	}

	[Fact]
	public async Task GenerateNumberAsync_After0010_Returns0011()
	{
		await SeedSalesInvoiceAsync("SAL-20261005-0010");

		var result = await _sut.GenerateNumberAsync(
			"SAL", "SalesInvoices", "20261005");

		result.Should().Be("SAL-20261005-0011");
	}

	[Fact]
	public async Task GenerateNumberAsync_After0099_Returns0100()
	{
		await SeedSalesInvoiceAsync("SAL-20261005-0099");

		var result = await _sut.GenerateNumberAsync(
			"SAL", "SalesInvoices", "20261005");

		result.Should().Be("SAL-20261005-0100");
	}

	[Fact]
	public async Task GenerateNumberAsync_After9999_Returns10000()
	{
		await SeedSalesInvoiceAsync("SAL-20261005-9999");

		var result = await _sut.GenerateNumberAsync(
			"SAL", "SalesInvoices", "20261005");

		result.Should().Be("SAL-20261005-10000");
	}

	// =========================================
	// 3. الاختيار من أرقام متعددة
	// =========================================

	[Fact]
	public async Task GenerateNumberAsync_WithMultipleInvoices_ReturnsMaxPlusOne()
	{
		await SeedSalesInvoiceAsync("SAL-20261005-0001");
		await SeedSalesInvoiceAsync("SAL-20261005-0005");
		await SeedSalesInvoiceAsync("SAL-20261005-0003");

		var result = await _sut.GenerateNumberAsync(
			"SAL", "SalesInvoices", "20261005");

		result.Should().Be("SAL-20261005-0006");
	}

	// =========================================
	// 4. عزل prefix/datePart
	// =========================================

	[Fact]
	public async Task GenerateNumberAsync_IgnoresDifferentPrefix()
	{
		await SeedSalesInvoiceAsync("PUR-20261005-0050");

		var result = await _sut.GenerateNumberAsync(
			"SAL", "SalesInvoices", "20261005");

		result.Should().Be("SAL-20261005-0001");
	}

	[Fact]
	public async Task GenerateNumberAsync_IgnoresDifferentDate()
	{
		await SeedSalesInvoiceAsync("SAL-20261004-0050");

		var result = await _sut.GenerateNumberAsync(
			"SAL", "SalesInvoices", "20261005");

		result.Should().Be("SAL-20261005-0001");
	}

	[Fact]
	public async Task GenerateNumberAsync_IsolatesDateParts()
	{
		await SeedSalesInvoiceAsync("SAL-20261005-0010");
		await SeedSalesInvoiceAsync("SAL-20261006-0020");

		var result1 = await _sut.GenerateNumberAsync(
			"SAL", "SalesInvoices", "20261005");
		var result2 = await _sut.GenerateNumberAsync(
			"SAL", "SalesInvoices", "20261006");

		result1.Should().Be("SAL-20261005-0011");
		result2.Should().Be("SAL-20261006-0021");
	}

	// =========================================
	// 5. حالات الأرقام غير الصحيحة
	// =========================================

	[Fact]
	public async Task GenerateNumberAsync_WithMalformedNumber_ResetsTo0001()
	{
		await SeedSalesInvoiceAsync("SAL-20261005-ABC");

		var result = await _sut.GenerateNumberAsync(
			"SAL", "SalesInvoices", "20261005");

		result.Should().Be("SAL-20261005-0001");
	}

	[Fact]
	public async Task GenerateNumberAsync_WithWrongFormat_ResetsTo0001()
	{
		await SeedSalesInvoiceAsync("SAL-20261005");

		var result = await _sut.GenerateNumberAsync(
			"SAL", "SalesInvoices", "20261005");

		result.Should().Be("SAL-20261005-0001");
	}

	// =========================================
	// 6. كل الجداول المدعومة
	// =========================================

	[Fact]
	public async Task GenerateNumberAsync_ForPurchaseInvoices_Works()
	{
		await SeedPurchaseInvoiceAsync("PUR-20261005-0005");

		var result = await _sut.GenerateNumberAsync(
			"PUR", "PurchaseInvoices", "20261005");

		result.Should().Be("PUR-20261005-0006");
	}

	[Fact]
	public async Task GenerateNumberAsync_ForSalesReturnInvoices_Works()
	{
		await SeedSalesReturnInvoiceAsync("SRT-20261005-0003");

		var result = await _sut.GenerateNumberAsync(
			"SRT", "SalesReturnInvoices", "20261005");

		result.Should().Be("SRT-20261005-0004");
	}

	[Fact]
	public async Task GenerateNumberAsync_ForPurchaseReturnInvoices_Works()
	{
		await SeedPurchaseReturnInvoiceAsync("PRT-20261005-0002");

		var result = await _sut.GenerateNumberAsync(
			"PRT", "PurchaseReturnInvoices", "20261005");

		result.Should().Be("PRT-20261005-0003");
	}

	// =========================================
	// 7. سيناريو متكامل
	// =========================================

	[Fact]
	public async Task GenerateNumberAsync_SequentialCalls_GenerateUniqueNumbers()
	{
		var numbers = new HashSet<string>();

		for (int i = 0; i < 5; i++)
		{
			var number = await _sut.GenerateNumberAsync(
				"SAL", "SalesInvoices", "20261005");

			numbers.Add(number).Should().BeTrue(
				$"الرقم {number} يجب أن يكون فريداً");

			// نحفظ الرقم في DB (محاكاة الحفظ الحقيقي)
			await SeedSalesInvoiceAsync(number);
		}

		numbers.Should().HaveCount(5);
		numbers.Should().Contain("SAL-20261005-0001");
		numbers.Should().Contain("SAL-20261005-0005");
	}
}