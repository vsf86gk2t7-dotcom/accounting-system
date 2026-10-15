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
		// إعدادات QuestPDF (تُستدعى مرة واحدة)
		// =========================================

		public static void Configure(
			string? webRootPath)
		{
			QuestPDF.Settings.License =
				LicenseType.Community;

			var fontPath =
				Path.Combine(
					webRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"),
					"fonts",
					"Cairo.ttf");

			if (File.Exists(fontPath))
			{
				FontManager.RegisterFontFromFile(fontPath);
			}
		}

		// =========================================
		// توليد PDF فاتورة البيع
		// =========================================

		public async Task<string?> GenerateSalesInvoicePdfAsync(
			int invoiceId)
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
					.FirstOrDefaultAsync(x =>
						x.Id == invoiceId);

			if (invoice == null)
			{
				return null;
			}

			var fileName =
				$"SalesInvoice_{invoice.InvoiceNumber.Replace('/', '_')}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

			var filePath =
				Path.Combine(Path.GetTempPath(), fileName);

			string PaymentLabel(SalesPaymentMethod m)
			{
				return m switch
				{
					SalesPaymentMethod.Cash => "نقدي",
					SalesPaymentMethod.BankTransfer => "تحويل بنكي",
					SalesPaymentMethod.Cheque => "شيك",
					SalesPaymentMethod.Credit => "آجل",
					SalesPaymentMethod.Wallet => "محفظة",
					_ => "-"
				};
			}

			await Task.Run(() =>
			{
				Document.Create(container =>
				{
					container.Page(page =>
					{
						page.Size(PageSizes.A4);
						page.Margin(30);
						page.DefaultTextStyle(x =>
							x.FontFamily("Cairo")
								.FontSize(11)
								.FontColor(Colors.Black));

						page.Header()
							.Column(column =>
							{
								column.Item()
									.Text("فاتورة بيع")
									.FontSize(20)
									.Bold()
									.AlignCenter();

								column.Item()
									.Text(
										$"رقم: {invoice.InvoiceNumber} • التاريخ: {invoice.InvoiceDate:yyyy-MM-dd}")
									.AlignCenter()
									.FontColor(Colors.Grey.Darken1);
							});

						page.Content()
							.PaddingVertical(16)
							.Column(column =>
							{
								column.Spacing(12);

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

								// الجدول
								column.Item()
									.Table(table =>
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

										table.Header(header =>
										{
											header.Cell().Element(HeaderCell)
												.Text("#");
											header.Cell().Element(HeaderCell)
												.Text("الصنف");
											header.Cell().Element(HeaderCell)
												.Text("الوحدة");
											header.Cell().Element(HeaderCell)
												.AlignRight().Text("الكمية");
											header.Cell().Element(HeaderCell)
												.AlignRight().Text("سعر الوحدة");
											header.Cell().Element(HeaderCell)
												.AlignRight().Text("الإجمالي");
										});

										var index = 1;

										foreach (var item in invoice.Items)
										{
											table.Cell().Element(BodyCell)
												.Text(index.ToString());
											table.Cell().Element(BodyCell)
												.Text(item.Product?.Name ?? "-");
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
											table.Cell().Element(BodyCell)
												.AlignRight().Text(item.Quantity.ToString("N2"));
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
											table.Cell().Element(BodyCell)
												.AlignRight().Text(item.TotalPrice.ToString("N2"));
											index++;
										}
									});

								// الإجماليات
								column.Item().AlignLeft().Width(260).Column(totals =>
								{
									totals.Spacing(4);

									totals.Item().Row(r =>
									{
										r.ConstantItem(110).Text("الإجمالي الفرعي");
										r.RelativeItem().AlignRight().Text(
											invoice.SubTotal.ToString("N2"));
									});

									totals.Item().Row(r =>
									{
										r.ConstantItem(110).Text("الخصم");
										r.RelativeItem().AlignRight().Text(
											invoice.DiscountAmount.ToString("N2"));
									});

									totals.Item().Row(r =>
									{
										r.ConstantItem(110).Text("الضريبة");
										r.RelativeItem().AlignRight().Text(
											invoice.TaxAmount.ToString("N2"));
									});

									totals.Item().PaddingTop(4).Row(r =>
									{
										r.ConstantItem(110).Text("الإجمالي")
											.Bold();
										r.RelativeItem().AlignRight().Text(
											invoice.TotalAmount.ToString("N2"))
											.Bold();
									});
								});

								if (!string.IsNullOrWhiteSpace(invoice.Notes))
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
			int invoiceId)
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
					.FirstOrDefaultAsync(x =>
						x.Id == invoiceId);

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
						page.Size(PageSizes.A4);
						page.Margin(30);
						page.DefaultTextStyle(x =>
							x.FontFamily("Cairo")
								.FontSize(11)
								.FontColor(Colors.Black));

						page.Header()
							.Column(column =>
							{
								column.Item()
									.Text("فاتورة شراء")
									.FontSize(20)
									.Bold()
									.AlignCenter();

								column.Item()
									.Text(
										$"رقم: {invoice.InvoiceNumber} • التاريخ: {invoice.InvoiceDate:yyyy-MM-dd}")
									.AlignCenter()
									.FontColor(Colors.Grey.Darken1);
							});

						page.Content()
							.PaddingVertical(16)
							.Column(column =>
							{
								column.Spacing(12);

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

								column.Item()
									.Table(table =>
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

										table.Header(header =>
										{
											header.Cell().Element(HeaderCell)
												.Text("#");
											header.Cell().Element(HeaderCell)
												.Text("الصنف");
											header.Cell().Element(HeaderCell)
												.Text("الوحدة");
											header.Cell().Element(HeaderCell)
												.AlignRight().Text("الكمية");
											header.Cell().Element(HeaderCell)
												.AlignRight().Text("سعر الوحدة");
											header.Cell().Element(HeaderCell)
												.AlignRight().Text("الإجمالي");
										});

										var index = 1;

										foreach (var item in invoice.Items)
										{
											table.Cell().Element(BodyCell)
												.Text(index.ToString());
											table.Cell().Element(BodyCell)
												.Text(item.Product?.Name ?? "-");
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
											table.Cell().Element(BodyCell)
												.AlignRight().Text(item.Quantity.ToString("N2"));
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
											table.Cell().Element(BodyCell)
												.AlignRight().Text(item.LineTotal.ToString("N2"));
											index++;
										}
									});

								column.Item().AlignLeft().Width(260).Column(totals =>
								{
									totals.Spacing(4);

									totals.Item().Row(r =>
									{
										r.ConstantItem(110).Text("الإجمالي الفرعي");
										r.RelativeItem().AlignRight().Text(
											invoice.SubTotal.ToString("N2"));
									});

									totals.Item().Row(r =>
									{
										r.ConstantItem(110).Text("الخصم");
										r.RelativeItem().AlignRight().Text(
											invoice.DiscountAmount.ToString("N2"));
									});

									totals.Item().Row(r =>
									{
										r.ConstantItem(110).Text("الضريبة");
										r.RelativeItem().AlignRight().Text(
											invoice.TaxAmount.ToString("N2"));
									});

									totals.Item().PaddingTop(4).Row(r =>
									{
										r.ConstantItem(110).Text("الإجمالي")
											.Bold();
										r.RelativeItem().AlignRight().Text(
											invoice.TotalAmount.ToString("N2"))
											.Bold();
									});
								});

								if (!string.IsNullOrWhiteSpace(invoice.Notes))
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
