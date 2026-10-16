using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AccountingSystem.Services.Pdf
{
	public class PdfInvoiceService : IPdfInvoiceService
	{
		private readonly ApplicationDbContext _context;
		private readonly IWebHostEnvironment _env;

		public PdfInvoiceService(
			ApplicationDbContext context,
			IWebHostEnvironment env)
		{
			_context = context;
			_env = env;
		}

		// =========================================
		// إعداد QuestPDF (ترخيص + خط عربي)
		// =========================================

		public static void Configure(string? webRootPath)
		{
			QuestPDF.Settings.License = LicenseType.Community;

			var fontPath = Path.Combine(
				webRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"),
				"fonts",
				"Cairo.ttf");

			if (File.Exists(fontPath))
			{
				FontManager.RegisterFontFromFile(fontPath);
			}
		}

		// =========================================
		// Helpers: page size / margin / font
		// =========================================

		private const float MmToPt = 2.8346f;

		private static PageSize GetPageSize(InvoicePageSize size) =>
			size switch
			{
				InvoicePageSize.A5 => PageSizes.A5,
				InvoicePageSize.Receipt80 => new PageSize(80 * MmToPt, 1400),
				InvoicePageSize.Receipt58 => new PageSize(58 * MmToPt, 1400),
				_ => PageSizes.A4
			};

		private static float GetMargin(InvoicePageSize size) =>
			size switch
			{
				InvoicePageSize.A5 => 20,
				InvoicePageSize.Receipt80 => 6,
				InvoicePageSize.Receipt58 => 4,
				_ => 30
			};

		private static float GetFontSize(InvoicePageSize size) =>
			size switch
			{
				InvoicePageSize.A5 => 10,
				InvoicePageSize.Receipt80 => 8,
				InvoicePageSize.Receipt58 => 6.5f,
				_ => 11
			};

		private static float GetTitleFontSize(InvoicePageSize size) =>
			size switch
			{
				InvoicePageSize.A5 => 16,
				InvoicePageSize.Receipt80 => 12,
				InvoicePageSize.Receipt58 => 10,
				_ => 20
			};

		private static bool IsReceipt(InvoicePageSize size) =>
			size == InvoicePageSize.Receipt80 ||
			size == InvoicePageSize.Receipt58;

		// =========================================
		// توليد PDF فاتورة البيع
		// =========================================

		public async Task<string?> GenerateSalesInvoicePdfAsync(
			int invoiceId,
			InvoicePageSize pageSize = InvoicePageSize.A4)
		{
			var invoice =
				await _context.SalesInvoices
					.AsNoTracking()
					.Include(x => x.Branch)
					.Include(x => x.Store)
					.Include(x => x.Customer)
					.Include(x => x.Items)
						.ThenInclude(x => x.Product)
					.Include(x => x.Items)
						.ThenInclude(x => x.Unit)
					.FirstOrDefaultAsync(x => x.Id == invoiceId);

			if (invoice == null)
			{
				return null;
			}

			var fileName =
				$"SalesInvoice_{invoice.InvoiceNumber.Replace('/', '_')}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

			var filePath =
				Path.Combine(Path.GetTempPath(), fileName);

			string PaymentLabel(SalesPaymentMethod m) =>
				m switch
				{
					SalesPaymentMethod.Cash => "نقدي",
					SalesPaymentMethod.BankTransfer => "تحويل بنكي",
					SalesPaymentMethod.Cheque => "شيك",
					SalesPaymentMethod.Credit => "آجل",
					SalesPaymentMethod.Wallet => "محفظة",
					_ => "-"
				};

			await Task.Run(() =>
			{
				Document.Create(container =>
				{
					container.Page(page =>
					{
						page.Size(GetPageSize(pageSize));
						page.Margin(GetMargin(pageSize));
						page.DefaultTextStyle(x =>
							x.FontFamily("Cairo")
								.FontSize(GetFontSize(pageSize))
								.FontColor(Colors.Black));

						// ===== Header =====
						page.Header().Column(column =>
						{
							column.Item()
								.Text("فاتورة بيع")
								.FontSize(GetTitleFontSize(pageSize))
								.Bold()
								.AlignCenter();

							column.Item()
								.Text($"رقم: {invoice.InvoiceNumber} • التاريخ: {invoice.InvoiceDate:yyyy-MM-dd}")
								.FontSize(GetFontSize(pageSize) * 0.9f)
								.AlignCenter()
								.FontColor(Colors.Grey.Darken1);
						});

						// ===== Content =====
						page.Content()
							.PaddingVertical(IsReceipt(pageSize) ? 4 : 16)
							.Column(column =>
							{
								column.Spacing(IsReceipt(pageSize) ? 4 : 12);

								// Info block
								if (IsReceipt(pageSize))
								{
									column.Item().Text($"الفرع: {invoice.Branch?.Name ?? "-"}");
									column.Item().Text($"المخزن: {invoice.Store?.Name ?? "-"}");
									column.Item().Text($"العميل: {invoice.Customer?.Name ?? "نقدي"}");
									if (!string.IsNullOrWhiteSpace(invoice.Customer?.Phone))
										column.Item().Text($"هاتف: {invoice.Customer.Phone}");
									column.Item().Text($"الدفع: {PaymentLabel(invoice.PaymentMethod)}");
								}
								else
								{
									column.Item().Row(row =>
									{
										row.RelativeItem().Column(left =>
										{
											left.Item().Text(
												$"الفرع: {invoice.Branch?.Name ?? "-"}");
											left.Item().Text(
												$"المخزن: {invoice.Store?.Name ?? "-"}");
										});

										row.RelativeItem().Column(right =>
										{
											right.Item().AlignRight().Text(
												$"العميل: {invoice.Customer?.Name ?? "— نقدي —"}");
											right.Item().AlignRight().Text(
												$"هاتف: {invoice.Customer?.Phone ?? "-"}");
											right.Item().AlignRight().Text(
												$"الدفع: {PaymentLabel(invoice.PaymentMethod)}");
										});
									});
								}

								// Table
								column.Item()
									.Table(table =>
									{
										if (IsReceipt(pageSize))
										{
											table.ColumnsDefinition(columns =>
											{
												columns.RelativeColumn(0.3f);
												columns.RelativeColumn(2.0f);
												columns.RelativeColumn(0.6f);
												columns.RelativeColumn(0.9f);
											});
										}
										else
										{
											table.ColumnsDefinition(columns =>
											{
												columns.RelativeColumn(0.6f);
												columns.RelativeColumn(2.5f);
												columns.RelativeColumn(0.8f);
												columns.RelativeColumn(0.9f);
												columns.RelativeColumn(1.1f);
												columns.RelativeColumn(1.1f);
											});
										}

										table.Header(header =>
										{
											header.Cell().Element(HeaderCell).Text("#");
											header.Cell().Element(HeaderCell).Text("الصنف");
											if (!IsReceipt(pageSize))
												header.Cell().Element(HeaderCell).Text("الوحدة");
											header.Cell().Element(HeaderCell).AlignRight().Text("الكمية");
											if (!IsReceipt(pageSize))
												header.Cell().Element(HeaderCell).AlignRight().Text("سعر الوحدة");
											header.Cell().Element(HeaderCell).AlignRight().Text("الإجمالي");
										});

										var index = 1;

										foreach (var item in invoice.Items)
										{
											table.Cell().Element(BodyCell)
												.Text(index.ToString());
											table.Cell().Element(BodyCell)
												.Text(item.Product?.Name ?? "-");

											if (!IsReceipt(pageSize))
											{
												table.Cell().Element(BodyCell)
													.Text(text =>
													{
														text.Span(item.Unit?.Name ?? "-");

														if (item.ConversionFactor > 0 &&
															item.ConversionFactor != 1)
														{
															text.Span(
																$" (× {item.ConversionFactor:0.###} قطعة)");
														}
													});
											}

											table.Cell().Element(BodyCell)
												.AlignRight().Text(item.Quantity.ToString("N2"));

											if (!IsReceipt(pageSize))
											{
												table.Cell().Element(BodyCell)
													.AlignRight().Text(text =>
													{
														text.Span(item.UnitPrice.ToString("N2"));

														if (item.ConversionFactor > 0 &&
															item.ConversionFactor != 1)
														{
															text.Span(
																$" ({item.UnitPrice / item.ConversionFactor:0.##})");
														}
													});
											}

											table.Cell().Element(BodyCell)
												.AlignRight().Text(item.TotalPrice.ToString("N2"));

											index++;
										}
									});

								// Totals
								column.Item().AlignLeft()
									.Width(IsReceipt(pageSize) ? 180 : 260)
									.Column(totals =>
									{
										totals.Spacing(4);

										totals.Item().Row(r =>
										{
											r.ConstantItem(IsReceipt(pageSize) ? 80 : 110)
												.Text("الإجمالي الفرعي");
											r.RelativeItem().AlignRight().Text(
												invoice.SubTotal.ToString("N2"));
										});

										totals.Item().Row(r =>
										{
											r.ConstantItem(IsReceipt(pageSize) ? 80 : 110)
												.Text("الخصم");
											r.RelativeItem().AlignRight().Text(
												invoice.DiscountAmount.ToString("N2"));
										});

										totals.Item().Row(r =>
										{
											r.ConstantItem(IsReceipt(pageSize) ? 80 : 110)
												.Text("الضريبة");
											r.RelativeItem().AlignRight().Text(
												invoice.TaxAmount.ToString("N2"));
										});

										totals.Item().PaddingTop(4).Row(r =>
										{
											r.ConstantItem(IsReceipt(pageSize) ? 80 : 110)
												.Text("الإجمالي").Bold();
											r.RelativeItem().AlignRight().Text(
												invoice.TotalAmount.ToString("N2")).Bold();
										});
									});

								if (!IsReceipt(pageSize) &&
									!string.IsNullOrWhiteSpace(invoice.Notes))
								{
									column.Item().Text($"ملاحظات: {invoice.Notes}")
										.FontColor(Colors.Grey.Darken2);
								}
							});
					});
				}).GeneratePdf(filePath);
			});

			return filePath;
		}

		// =========================================
		// توليد PDF فاتورة الشراء
		// =========================================

		public async Task<string?> GeneratePurchaseInvoicePdfAsync(
			int invoiceId,
			InvoicePageSize pageSize = InvoicePageSize.A4)
		{
			var invoice =
				await _context.PurchaseInvoices
					.AsNoTracking()
					.Include(x => x.Store)
					.Include(x => x.Supplier)
					.Include(x => x.Items)
						.ThenInclude(x => x.Product)
					.Include(x => x.Items)
						.ThenInclude(x => x.Unit)
					.FirstOrDefaultAsync(x => x.Id == invoiceId);

			if (invoice == null)
			{
				return null;
			}

			var fileName =
				$"PurchaseInvoice_{invoice.InvoiceNumber.Replace('/', '_')}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

			var filePath =
				Path.Combine(Path.GetTempPath(), fileName);

			await Task.Run(() =>
			{
				Document.Create(container =>
				{
					container.Page(page =>
					{
						page.Size(GetPageSize(pageSize));
						page.Margin(GetMargin(pageSize));
						page.DefaultTextStyle(x =>
							x.FontFamily("Cairo")
								.FontSize(GetFontSize(pageSize))
								.FontColor(Colors.Black));

						page.Header().Column(column =>
						{
							column.Item()
								.Text("فاتورة شراء")
								.FontSize(GetTitleFontSize(pageSize))
								.Bold()
								.AlignCenter();

							column.Item()
								.Text($"رقم: {invoice.InvoiceNumber} • التاريخ: {invoice.InvoiceDate:yyyy-MM-dd}")
								.FontSize(GetFontSize(pageSize) * 0.9f)
								.AlignCenter()
								.FontColor(Colors.Grey.Darken1);
						});

						page.Content()
							.PaddingVertical(IsReceipt(pageSize) ? 4 : 16)
							.Column(column =>
							{
								column.Spacing(IsReceipt(pageSize) ? 4 : 12);

								if (IsReceipt(pageSize))
								{
									column.Item().Text($"المورد: {invoice.Supplier?.Name ?? "-"}");
									if (!string.IsNullOrWhiteSpace(invoice.Supplier?.Phone))
										column.Item().Text($"هاتف: {invoice.Supplier.Phone}");
									column.Item().Text($"المخزن: {invoice.Store?.Name ?? "-"}");
								}
								else
								{
									column.Item().Row(row =>
									{
										row.RelativeItem().Column(left =>
										{
											left.Item().Text(
												$"المورد: {invoice.Supplier?.Name ?? "-"}");
											left.Item().Text(
												$"هاتف: {invoice.Supplier?.Phone ?? "-"}");
										});

										row.RelativeItem().AlignRight().Text(
											$"المخزن: {invoice.Store?.Name ?? "-"}");
									});
								}

								column.Item()
									.Table(table =>
									{
										if (IsReceipt(pageSize))
										{
											table.ColumnsDefinition(columns =>
											{
												columns.RelativeColumn(0.3f);
												columns.RelativeColumn(2.0f);
												columns.RelativeColumn(0.6f);
												columns.RelativeColumn(0.9f);
											});
										}
										else
										{
											table.ColumnsDefinition(columns =>
											{
												columns.RelativeColumn(0.6f);
												columns.RelativeColumn(2.5f);
												columns.RelativeColumn(0.8f);
												columns.RelativeColumn(0.9f);
												columns.RelativeColumn(1.1f);
												columns.RelativeColumn(1.1f);
											});
										}

										table.Header(header =>
										{
											header.Cell().Element(HeaderCell).Text("#");
											header.Cell().Element(HeaderCell).Text("الصنف");
											if (!IsReceipt(pageSize))
												header.Cell().Element(HeaderCell).Text("الوحدة");
											header.Cell().Element(HeaderCell).AlignRight().Text("الكمية");
											if (!IsReceipt(pageSize))
												header.Cell().Element(HeaderCell).AlignRight().Text("سعر الوحدة");
											header.Cell().Element(HeaderCell).AlignRight().Text("الإجمالي");
										});

										var index = 1;

										foreach (var item in invoice.Items)
										{
											table.Cell().Element(BodyCell)
												.Text(index.ToString());
											table.Cell().Element(BodyCell)
												.Text(item.Product?.Name ?? "-");

											if (!IsReceipt(pageSize))
											{
												table.Cell().Element(BodyCell)
													.Text(text =>
													{
														text.Span(item.Unit?.Name ?? "-");

														if (item.ConversionFactor > 0 &&
															item.ConversionFactor != 1)
														{
															text.Span(
																$" (× {item.ConversionFactor:0.###} قطعة)");
														}
													});
											}

											table.Cell().Element(BodyCell)
												.AlignRight().Text(item.Quantity.ToString("N2"));

											if (!IsReceipt(pageSize))
											{
												table.Cell().Element(BodyCell)
													.AlignRight().Text(text =>
													{
														if (item.ConversionFactor > 0 &&
															item.ConversionFactor != 1)
														{
															var setPrice =
																item.UnitPrice * item.ConversionFactor;
															text.Span(setPrice.ToString("N2"));
															text.Span($" ({item.UnitPrice:0.##})");
														}
														else
														{
															text.Span(item.UnitPrice.ToString("N2"));
														}
													});
											}

											table.Cell().Element(BodyCell)
												.AlignRight().Text(item.LineTotal.ToString("N2"));

											index++;
										}
									});

								column.Item().AlignLeft()
									.Width(IsReceipt(pageSize) ? 180 : 260)
									.Column(totals =>
									{
										totals.Spacing(4);

										totals.Item().Row(r =>
										{
											r.ConstantItem(IsReceipt(pageSize) ? 80 : 110)
												.Text("الإجمالي الفرعي");
											r.RelativeItem().AlignRight().Text(
												invoice.SubTotal.ToString("N2"));
										});

										totals.Item().Row(r =>
										{
											r.ConstantItem(IsReceipt(pageSize) ? 80 : 110)
												.Text("الخصم");
											r.RelativeItem().AlignRight().Text(
												invoice.DiscountAmount.ToString("N2"));
										});

										totals.Item().Row(r =>
										{
											r.ConstantItem(IsReceipt(pageSize) ? 80 : 110)
												.Text("الضريبة");
											r.RelativeItem().AlignRight().Text(
												invoice.TaxAmount.ToString("N2"));
										});

										totals.Item().PaddingTop(4).Row(r =>
										{
											r.ConstantItem(IsReceipt(pageSize) ? 80 : 110)
												.Text("الإجمالي").Bold();
											r.RelativeItem().AlignRight().Text(
												invoice.TotalAmount.ToString("N2")).Bold();
										});
									});

								if (!IsReceipt(pageSize) &&
									!string.IsNullOrWhiteSpace(invoice.Notes))
								{
									column.Item().Text($"ملاحظات: {invoice.Notes}")
										.FontColor(Colors.Grey.Darken2);
								}
							});
					});
				}).GeneratePdf(filePath);
			});

			return filePath;
		}

		// =========================================
		// عناصر الجدول
		// =========================================

		private static IContainer HeaderCell(IContainer container)
		{
			return container
				.PaddingVertical(6)
				.PaddingHorizontal(4)
				.Background(Colors.Grey.Darken3)
				.BorderColor(Colors.Grey.Darken2)
				.BorderBottom(1)
				.DefaultTextStyle(x =>
					x.FontColor(Colors.White)
						.SemiBold());
		}

		private static IContainer BodyCell(IContainer container)
		{
			return container
				.PaddingVertical(5)
				.PaddingHorizontal(4)
				.BorderBottom(0.5f)
				.BorderColor(Colors.Grey.Lighten2);
		}
	}
}