using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Shipping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	public class ShippingController : Controller
	{
		private readonly ApplicationDbContext _context;

		public ShippingController(ApplicationDbContext context)
		{
			_context = context;
		}

		// =========================================
		// قائمة مناديب الشحن
		// =========================================

		[HttpGet]
		[RequirePermission("shipping.view")]
		public async Task<IActionResult> Index(string? search, ShippingBillStatus? status)
		{
			var query = _context.ShippingBills
				.AsNoTracking()
				.Include(x => x.SalesInvoice)
					.ThenInclude(x => x!.Customer)
				.Include(x => x.ShippingCompany)
				.Include(x => x.Driver)
				.AsQueryable();

			if (!string.IsNullOrWhiteSpace(search))
			{
				search = search.Trim();
				query = query.Where(x =>
					x.BillNumber.Contains(search) ||
					  x.SalesInvoice!.InvoiceNumber.Contains(search));
			}

			if (status.HasValue)
			{
				query = query.Where(x => x.Status == status.Value);
			}

			var bills = await query
				.OrderByDescending(x => x.BillDate)
				.ThenByDescending(x => x.Id)
				.ToListAsync();

			var vm = new ShippingBillIndexViewModel
			{
				ShippingBills = bills,
				Search = search ?? "",
				StatusFilter = status
			};

			return View(vm);
		}

		// =========================================
		// إنشاء مندوب شحن جديد - GET
		// =========================================

		[HttpGet]
		[RequirePermission("shipping.create")]
		public async Task<IActionResult> Create(int? invoiceId)
		{
			var vm = new ShippingBillCreateViewModel();

			vm.ShippingCompanies = await _context.ShippingCompanies
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.ToListAsync();

			vm.Drivers = await _context.Employees
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.ToListAsync();

			if (invoiceId.HasValue)
			{
				var invoice = await _context.SalesInvoices
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.Id == invoiceId.Value);

				if (invoice != null)
				{
					vm.SalesInvoiceId = invoice.Id;
					vm.SalesInvoiceNumber = invoice.InvoiceNumber;
					vm.CustomerName = invoice.Customer?.Name ?? "";
					vm.InvoiceTotal = invoice.TotalAmount;
				}
			}

			return View(vm);
		}

		// =========================================
		// إنشاء مندوب شحن جديد - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("shipping.create")]
		public async Task<IActionResult> Create(ShippingBillCreateViewModel vm)
		{
			var invoiceForValidation = ModelState.IsValid
				? await _context.SalesInvoices
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.Id == vm.SalesInvoiceId)
				: null;

			if (ModelState.IsValid)
			{
				if (invoiceForValidation == null)
				{
					ModelState.AddModelError(
						nameof(vm.SalesInvoiceId),
						"فاتورة البيع غير موجودة.");
				}
				else if (invoiceForValidation.Status !=
					SalesInvoiceStatus.Confirmed)
				{
					ModelState.AddModelError(
						nameof(vm.SalesInvoiceId),
						"لا يمكن إنشاء بوليصة شحن لفاتورة غير معتمدة.");
				}
				else if (invoiceForValidation.ShippingBillId.HasValue)
				{
					ModelState.AddModelError(
						nameof(vm.SalesInvoiceId),
						"يوجد بالفعل بوليصة شحن لهذه الفاتورة.");
				}
			}

			if (!ModelState.IsValid)
			{
				vm.ShippingCompanies = await _context.ShippingCompanies
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.ToListAsync();

				vm.Drivers = await _context.Employees
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.ToListAsync();

				return View(vm);
			}

			// توليد رقم المندوب تلقائيًا
			var counter = await _context.SequenceCounters
				.FirstOrDefaultAsync(x => x.Name == "ShippingBill");

			if (counter == null)
			{
				counter = new SequenceCounter
				{
					Key = "ShippingBill",
					Name = "ShippingBill",
					Prefix = "SB",
					CurrentValue = 0,
					NextValue = 1,
					Digits = 5
				};
				_context.SequenceCounters.Add(counter);
			}

			counter.CurrentValue++;
			counter.NextValue = counter.CurrentValue + 1;
			counter.UpdatedAt = DateTime.UtcNow;

			var billNumber = counter.Prefix + counter.CurrentValue.ToString("D" + counter.Digits);

			var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			int? userId = userIdStr != null ? int.Parse(userIdStr) : null;

			var bill = new ShippingBill
			{
				BillNumber = billNumber,
				SalesInvoiceId = vm.SalesInvoiceId,
				DeliveryMethod = vm.DeliveryMethod,
				ShippingCompanyId = vm.DeliveryMethod == DeliveryMethod.ExternalCompany
					? vm.ShippingCompanyId
					: null,
				DriverId = vm.DeliveryMethod == DeliveryMethod.InternalDriver
					? vm.DriverId
					: null,
				Status = ShippingBillStatus.Pending,
				ScheduledDate = vm.ScheduledDate,
				Notes = vm.Notes,
				DeliveryNotes = vm.DeliveryNotes,
				CreatedByUserId = userId,
				BillDate = DateTime.UtcNow
			};

			_context.ShippingBills.Add(bill);
			await _context.SaveChangesAsync();

			// تحديث رقم الفاتورة المرتبطة
			if (bill.SalesInvoiceId > 0)
			{
				var invoice = await _context.SalesInvoices
					.FindAsync(bill.SalesInvoiceId);
				if (invoice != null)
				{
					invoice.ShippingBillId = bill.Id;
					await _context.SaveChangesAsync();
				}
			}

			return RedirectToAction(nameof(Details), new { id = bill.Id });
		}

		// =========================================
		// عرض تفاصيل مندوب الشحن
		// =========================================

		[HttpGet]
		[RequirePermission("shipping.view")]
		public async Task<IActionResult> Details(int id)
		{
						var bill = await _context.ShippingBills
				.Include(x => x.SalesInvoice)
					.ThenInclude(x => x!.Customer)
				.Include(x => x.SalesInvoice)
					.ThenInclude(x => x!.Branch)
				.Include(x => x.SalesInvoice)
					.ThenInclude(x => x!.Store)
				.Include(x => x.ShippingCompany)
				.Include(x => x.Driver)
				.Include(x => x.SalesInvoice)
					.ThenInclude(x => x!.Items)
						.ThenInclude(x => x.Product)
				.AsSplitQuery()
				.FirstOrDefaultAsync(x => x.Id == id);

			if (bill == null)
			{
				return NotFound();
			}

					// ✅ (null-safety) الفاتورة مضمونة بسبب الـ FK
			var salesInvoice = bill.SalesInvoice!;

			var vm = new ShippingBillDetailsViewModel
			{
				ShippingBill = bill,
				InvoiceNumber = salesInvoice.InvoiceNumber,
				CustomerName = salesInvoice.Customer?.Name ?? "",
				BranchName = salesInvoice.Branch?.Name ?? "",
				StoreName = salesInvoice.Store?.Name ?? "",
				InvoiceTotal = salesInvoice.TotalAmount,
				TotalItems = salesInvoice.Items.Count,
				DeliveryMethodLabel = bill.DeliveryMethod == DeliveryMethod.InternalDriver
					? "مندوب داخلي"
					: bill.ShippingCompany?.Name ?? "خارجي",
				StatusLabel = bill.Status switch
				{
					ShippingBillStatus.Pending => "معلق",
					ShippingBillStatus.InTransit => "في الطريق",
					ShippingBillStatus.Delivered => "تم التوصيل",
					ShippingBillStatus.Cancelled => "ملغى",
					_ => "-"
				}
			};

			return View(vm);
		}

		// =========================================
		// تحديث حالة التوصيل
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("shipping.edit")]
		public async Task<IActionResult> UpdateStatus(int id, ShippingBillStatus status)
		{
						var bill = await _context.ShippingBills
				.FirstOrDefaultAsync(x => x.Id == id);

			if (bill == null)
			{
				return NotFound();
			}

			bill.Status = status;
			bill.DeliveredDate = status == ShippingBillStatus.Delivered
				? DateTime.UtcNow
				: bill.DeliveredDate;

			if (status == ShippingBillStatus.Delivered)
			{
				bill.DeliveryNotes = $"تم التوصيل في {DateTime.UtcNow:yyyy/MM/dd HH:mm}";
			}

			// =========================================
			// ملحوظة مهمة: لا يوجد هنا أي لمس للمخزون عمدًا.
			//
			// المخزون بيتخصم فعليًا وبدقة (FIFO لكل لوط مع تتبّع
			// SalesInvoiceItemLot) وقت اعتماد فاتورة البيع نفسها
			// في SalesInvoiceController، قبل أي علاقة بالشحن خالص.
			//
			// بوليصة الشحن هنا مجرد تتبّع لوجستي (حالة التوصيل)
			// فوق بيع مُعتمَد ومخصوم مخزونه بالفعل. لو احتجتوا
			// إلغاء البيع نفسه فعليًا، استخدموا
			// SalesInvoiceController.Cancel المخصص لده، لأنه بيرجّع
			// الكمية بالظبط للوطات اللي اتخصمت منها (مش أقدم لوط
			// بشكل عشوائي زي ما كان بيحصل هنا قبل كده).
			//
			// (كان فيه هنا قبل كده خصم واسترجاع مخزون مباشر من
			//  StockLots بشكل مستقل — ده كان بيعمل خصم مزدوج فعلي
			//  للمخزون (مرة عند اعتماد الفاتورة ومرة تانية هنا)،
			//  وكان منطق الاسترجاع عند الإلغاء معطوب أصلاً (بيحط كل
			//  الكمية في أقدم لوط بدل اللوطات الصحيحة). اتشال نهائيًا.)
			// =========================================

			await _context.SaveChangesAsync();

			return RedirectToAction(nameof(Details), new { id = bill.Id });
		}
	}
}