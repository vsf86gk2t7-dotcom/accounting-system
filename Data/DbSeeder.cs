using AccountingSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Data
{
	public static class DbSeeder
	{
		private sealed record AccountSeed(
			string Code,
			string Name,
			AccountType Type,
			string? Parent,
			bool System);

		public static async Task SeedAsync(
			IServiceProvider services,
			IConfiguration configuration)
		{
			const int maxAttempts = 8;

			string? lastFailure = null;

			for (var attempt = 1; attempt <= maxAttempts; attempt++)
			{
				using var scope = services.CreateScope();

				var context = scope.ServiceProvider
					.GetRequiredService<ApplicationDbContext>();

				try
				{
					var pendingMigrations =
						await context.Database
							.GetPendingMigrationsAsync();

					if (pendingMigrations.Any())
					{
						await context.Database.MigrateAsync();
					}

					await SeedCoreAsync(
						context,
						configuration);

					return;
				}
				catch (Exception ex)
				{
					lastFailure = ex.Message;

					if (!IsTransient(ex))
					{
						throw new InvalidOperationException(
							"فشل بدء التشغيل (خطأ غير عابر): " +
							ex.Message,
							ex);
					}

					if (attempt >= maxAttempts)
					{
						break;
					}

					await Task.Delay(
						TimeSpan.FromSeconds(5));
				}
			}

			throw new InvalidOperationException(
				"تعذر الاتصال بقاعدة البيانات بعد " +
				maxAttempts +
				" محاولات عند بدء التشغيل. آخر خطأ: " +
				lastFailure);
		}

		private static bool IsTransient(Exception ex)
		{
			if (ex.InnerException != null)
			{
				return IsTransient(ex.InnerException);
			}

			if (ex is Microsoft.Data.SqlClient.SqlException sqlEx)
			{
				if (sqlEx.Number == 0 || sqlEx.Number == -1)
					return true;

				int[] transientNumbers =
				{
					-2, 20, 64, 233,
					10053, 10054, 10060,
					40197, 40501, 40613,
					49918, 49919, 49920,
					4060, 10928, 10929,
					9003, 5181
				};

				if (transientNumbers.Contains(sqlEx.Number))
					return true;

				var msg = sqlEx.Message ?? "";
				if (msg.Contains("transport-level", StringComparison.OrdinalIgnoreCase) ||
					msg.Contains("Physical connection", StringComparison.OrdinalIgnoreCase) ||
					msg.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
					msg.Contains("MARS", StringComparison.OrdinalIgnoreCase) ||
					msg.Contains("Shared Memory", StringComparison.OrdinalIgnoreCase) ||
					msg.Contains("Named Pipes", StringComparison.OrdinalIgnoreCase) ||
					msg.Contains("pipe", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			if (ex is System.Net.Sockets.SocketException)
				return true;

			if (ex is System.ComponentModel.Win32Exception)
				return true;

			if (ex is Microsoft.EntityFrameworkCore.DbUpdateException &&
				ex.InnerException is Microsoft.Data.SqlClient.SqlException innerSql)
			{
				return IsTransient(innerSql);
			}

			if (ex is InvalidOperationException inv)
			{
				var m = inv.Message ?? "";
				if (m.Contains("transient", StringComparison.OrdinalIgnoreCase) ||
					m.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
					m.Contains("transport", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		private static async Task SeedCoreAsync(
			ApplicationDbContext context,
			IConfiguration configuration)
		{
			var passwordHasher = new PasswordHasher<User>();

			// =========================================
			// الأدوار
			// =========================================

			var existingRoleCodes =
				await context.Roles
					.AsNoTracking()
					.Select(x => x.Code)
					.ToHashSetAsync();

			var roles = new[]
			{
				new Role { Name = "مدير النظام", Code = "Admin", Description = "صلاحيات كاملة على النظام." },
				new Role { Name = "المدير العام", Code = "GeneralManager", Description = "مدير عام يتم تحديد صلاحياته بواسطة مدير النظام." },
				new Role { Name = "مدير فرع", Code = "BranchManager", Description = "مسؤول عن إدارة الفرع والعمليات المسموح بها داخل الفرع." },
				new Role { Name = "كاشير", Code = "Cashier", Description = "مسؤول عن عمليات البيع والتحصيل والخزينة حسب الصلاحيات الممنوحة." },
				new Role { Name = "مخزنجي", Code = "Storekeeper", Description = "مسؤول عن عمليات المخزون والاستلام والصرف والتحويل والجرد حسب الصلاحيات." },
				new Role { Name = "محاسب", Code = "Accountant", Description = "مسؤول عن العمليات المحاسبية والحسابات والتقارير المالية." },
				new Role { Name = "مبيعات", Code = "Sales", Description = "مسؤول عن عمليات البيع والعملاء ومرتجعات المبيعات." },
				new Role { Name = "مشتريات", Code = "Purchasing", Description = "مسؤول عن المشتريات والموردين ومرتجعات المشتريات." },
				new Role { Name = "أمين خزينة", Code = "Treasury", Description = "مسؤول عن الخزينة والمقبوضات والمدفوعات." },
				new Role { Name = "مراقب مخزون", Code = "InventoryController", Description = "مسؤول عن متابعة المخزون والجرد والتقارير." },
				new Role { Name = "موظف", Code = "Employee", Description = "موظف عام بصلاحيات محدودة." }
			};

			foreach (var roleData in roles)
			{
				if (!existingRoleCodes.Contains(roleData.Code))
				{
					context.Roles.Add(roleData);
				}
			}

			var rolesAdded = context.ChangeTracker
				.Entries<Role>()
				.Any(x => x.State == EntityState.Added);

			if (rolesAdded)
			{
				await context.SaveChangesAsync();
			}

			// =========================================
			// شجرة الحسابات
			// =========================================

			await SeedChartOfAccountsAsync(context);

			// =========================================
			// الفترات المالية
			// =========================================

			var currentYear = DateTime.UtcNow.Year;
			var targetYears = new[] { currentYear, currentYear + 1 };

			var existingPeriodNames =
				await context.FiscalPeriods
					.AsNoTracking()
					.Select(x => x.PeriodName)
					.ToHashSetAsync();

			var periodsAdded = false;

			foreach (var year in targetYears)
			{
				var periodName = year.ToString();

				if (existingPeriodNames.Contains(periodName))
				{
					continue;
				}

				context.FiscalPeriods.Add(new FiscalPeriod
				{
					PeriodName = periodName,
					StartDate = new DateTime(year, 1, 1),
					EndDate = new DateTime(year, 12, 31),
					IsClosed = false,
					CreatedAt = DateTime.UtcNow
				});

				periodsAdded = true;
			}

			if (periodsAdded)
			{
				await context.SaveChangesAsync();
			}

			// =========================================
			// الصلاحيات
			// =========================================

			var permissions = new[]
			{
				new Permission { Code = "company.view", Name = "عرض الشركات", Group = "Company", Description = "عرض بيانات الشركات." },
				new Permission { Code = "company.create", Name = "إضافة شركة", Group = "Company", Description = "إنشاء شركة جديدة." },
				new Permission { Code = "company.edit", Name = "تعديل شركة", Group = "Company", Description = "تعديل بيانات الشركة." },
				new Permission { Code = "company.delete", Name = "حذف شركة", Group = "Company", Description = "حذف الشركة إذا سمحت قواعد النظام بذلك." },
				new Permission { Code = "company.activate", Name = "تفعيل / تعطيل شركة", Group = "Company", Description = "تغيير حالة نشاط الشركة." },

				new Permission { Code = "branch.view", Name = "عرض الفروع", Group = "Branch", Description = "عرض الفروع." },
				new Permission { Code = "branch.create", Name = "إضافة فرع", Group = "Branch", Description = "إنشاء فرع جديد." },
				new Permission { Code = "branch.edit", Name = "تعديل فرع", Group = "Branch", Description = "تعديل بيانات الفرع." },
				new Permission { Code = "branch.delete", Name = "حذف فرع", Group = "Branch", Description = "حذف الفرع إذا لم توجد مخازن مرتبطة به." },
				new Permission { Code = "branch.activate", Name = "تفعيل / تعطيل فرع", Group = "Branch", Description = "تغيير حالة نشاط الفرع." },

				new Permission { Code = "store.view", Name = "عرض المخازن", Group = "Store", Description = "عرض المخازن." },
				new Permission { Code = "store.create", Name = "إضافة مخزن", Group = "Store", Description = "إنشاء مخزن جديد." },
				new Permission { Code = "store.edit", Name = "تعديل مخزن", Group = "Store", Description = "تعديل بيانات المخزن." },
				new Permission { Code = "store.delete", Name = "حذف مخزن", Group = "Store", Description = "حذف المخزن وفق قواعد البيانات والمخزون." },
				new Permission { Code = "store.activate", Name = "تفعيل / تعطيل مخزن", Group = "Store", Description = "تغيير حالة نشاط المخزن." },

				new Permission { Code = "user.view", Name = "عرض المستخدمين", Group = "User", Description = "عرض حسابات المستخدمين." },
				new Permission { Code = "user.create", Name = "إضافة مستخدم", Group = "User", Description = "إنشاء مستخدم." },
				new Permission { Code = "user.edit", Name = "تعديل مستخدم", Group = "User", Description = "تعديل بيانات المستخدم." },
				new Permission { Code = "user.activate", Name = "تفعيل / تعطيل مستخدم", Group = "User", Description = "تغيير حالة المستخدم." },

				new Permission { Code = "role.view", Name = "عرض الأدوار", Group = "Role", Description = "عرض الأدوار." },
				new Permission { Code = "role.create", Name = "إضافة دور", Group = "Role", Description = "إنشاء دور جديد." },
				new Permission { Code = "role.edit", Name = "تعديل دور", Group = "Role", Description = "تعديل بيانات الدور." },
				new Permission { Code = "role.delete", Name = "حذف دور", Group = "Role", Description = "حذف دور وفق قواعد النظام." },

				new Permission { Code = "permission.view", Name = "عرض الصلاحيات", Group = "Permission", Description = "عرض الصلاحيات." },
				new Permission { Code = "permission.assign", Name = "منح الصلاحيات", Group = "Permission", Description = "منح أو سحب الصلاحيات من الأدوار والمستخدمين وفق السياسة." },

				new Permission { Code = "customer.view", Name = "عرض العملاء", Group = "Customer", Description = "عرض بيانات العملاء." },
				new Permission { Code = "customer.create", Name = "إضافة عميل", Group = "Customer", Description = "إضافة عميل جديد." },
				new Permission { Code = "customer.edit", Name = "تعديل عميل", Group = "Customer", Description = "تعديل بيانات العميل." },
				new Permission { Code = "customer.activate", Name = "تفعيل / تعطيل عميل", Group = "Customer", Description = "تغيير حالة العميل." },
				new Permission { Code = "customer.portal.manage", Name = "إدارة بوابة العميل", Group = "CustomerPortal", Description = "طلب وموافقة وإنشاء حساب بوابة العميل." },
				new Permission { Code = "customer.portal.view", Name = "الدخول إلى بوابة العميل", Group = "CustomerPortal", Description = "الوصول إلى بيانات العميل من البوابة." },

				new Permission { Code = "supplier.view", Name = "عرض الموردين", Group = "Supplier", Description = "عرض بيانات الموردين." },
				new Permission { Code = "supplier.create", Name = "إضافة مورد", Group = "Supplier", Description = "إضافة مورد جديد." },
				new Permission { Code = "supplier.edit", Name = "تعديل مورد", Group = "Supplier", Description = "تعديل بيانات المورد." },
				new Permission { Code = "supplier.activate", Name = "تفعيل / تعطيل مورد", Group = "Supplier", Description = "تغيير حالة المورد." },
				new Permission { Code = "supplier.portal.manage", Name = "إدارة بوابة المورد", Group = "SupplierPortal", Description = "طلب وموافقة وإنشاء حساب بوابة المورد." },
				new Permission { Code = "supplier.portal.view", Name = "الدخول إلى بوابة المورد", Group = "SupplierPortal", Description = "الوصول إلى بيانات المورد من البوابة." },

				new Permission { Code = "employee.view", Name = "عرض الموظفين", Group = "Employee", Description = "عرض الموظفين." },
				new Permission { Code = "employee.create", Name = "إضافة موظف", Group = "Employee", Description = "إضافة موظف." },
				new Permission { Code = "employee.edit", Name = "تعديل موظف", Group = "Employee", Description = "تعديل بيانات الموظف." },
				new Permission { Code = "employee.activate", Name = "تفعيل / تعطيل موظف", Group = "Employee", Description = "تغيير حالة الموظف." },
				new Permission { Code = "employee.portal.manage", Name = "إدارة بوابة الموظف", Group = "EmployeePortal", Description = "طلب وموافقة وإنشاء حساب بوابة الموظف." },
				new Permission { Code = "employee.portal.view", Name = "الدخول إلى بوابة الموظف", Group = "EmployeePortal", Description = "الوصول إلى بيانات الموظف من البوابة." },

				new Permission { Code = "hr.setup.view", Name = "عرض إعدادات الموارد البشرية", Group = "HR", Description = "عرض الأقسام والمسميات الوظيفية." },
				new Permission { Code = "hr.setup.manage", Name = "إدارة إعدادات الموارد البشرية", Group = "HR", Description = "إضافة وتعطيل الأقسام والمسميات الوظيفية." },

				new Permission { Code = "payroll.view", Name = "عرض الرواتب", Group = "Payroll", Description = "عرض قائمة أشهر الرواتب." },
				new Permission { Code = "payroll.manage", Name = "إدارة الرواتب", Group = "Payroll", Description = "إنشاء وتعديل أشهر الرواتب." },
				new Permission { Code = "payroll.approve", Name = "اعتماد الرواتب", Group = "Payroll", Description = "اعتماد قيد استحقاق الرواتب." },
				new Permission { Code = "payroll.pay", Name = "صرف الرواتب", Group = "Payroll", Description = "صرف الرواتب من الخزينة." },

				new Permission { Code = "advance.view", Name = "عرض سلف الموظفين", Group = "Advance", Description = "عرض سجل السلف." },
				new Permission { Code = "advance.create", Name = "صرف سلفة", Group = "Advance", Description = "صرف سلفة لموظف." },

				new Permission { Code = "salesrep.view", Name = "عرض المناديب", Group = "SalesRep", Description = "عرض قائمة المناديب وأدائهم." },
				new Permission { Code = "salesrep.manage", Name = "إدارة المناديب", Group = "SalesRep", Description = "إضافة وتعديل المناديب والعمولات." },

				new Permission { Code = "shipping.view", Name = "عرض الشحن والتوصيل", Group = "Shipping", Description = "عرض بوالص الشحن وحالتها." },
				new Permission { Code = "shipping.create", Name = "إنشاء بوليصة شحن", Group = "Shipping", Description = "إنشاء بوليصة شحن جديدة لفاتورة بيع." },
				new Permission { Code = "shipping.edit", Name = "تحديث حالة الشحن", Group = "Shipping", Description = "تحديث حالة التوصيل وإسناد المندوب/الشركة." },
				new Permission { Code = "shipping.company.manage", Name = "إدارة شركات الشحن", Group = "Shipping", Description = "إضافة وتعديل شركات الشحن الخارجية." },

				new Permission { Code = "sales.view", Name = "عرض المبيعات", Group = "Sales", Description = "عرض فواتير وحركات المبيعات." },
				new Permission { Code = "sales.create", Name = "إنشاء فاتورة بيع", Group = "Sales", Description = "إنشاء فاتورة مبيعات جديدة." },
				new Permission { Code = "sales.edit", Name = "تعديل فاتورة بيع", Group = "Sales", Description = "تعديل فاتورة مبيعات قبل اعتمادها." },
				new Permission { Code = "sales.confirm", Name = "اعتماد فاتورة بيع", Group = "Sales", Description = "اعتماد فاتورة المبيعات." },
				new Permission { Code = "sales.cancel", Name = "إلغاء فاتورة بيع", Group = "Sales", Description = "إلغاء فاتورة المبيعات." },

				new Permission { Code = "sales.override_loss", Name = "التجاوز على خسارة البيع", Group = "Sales", Description = "السماح ببيع منتجات بخسارة فوق التكلفة." },

				new Permission { Code = "sales.return.view", Name = "عرض مرتجعات المبيعات", Group = "SalesReturn", Description = "عرض مرتجعات المبيعات." },
				new Permission { Code = "sales.return.create", Name = "إنشاء مرتجع مبيعات", Group = "SalesReturn", Description = "إنشاء مرتجع مبيعات." },
				new Permission { Code = "sales.return.approve", Name = "اعتماد مرتجع مبيعات", Group = "SalesReturn", Description = "اعتماد مرتجع المبيعات." },
				new Permission { Code = "sales.return.complete", Name = "إتمام مرتجع مبيعات", Group = "SalesReturn", Description = "إتمام مرتجع المبيعات." },

				new Permission { Code = "purchase.view", Name = "عرض المشتريات", Group = "Purchase", Description = "عرض فواتير وحركات المشتريات." },
				new Permission { Code = "purchase.create", Name = "إنشاء فاتورة شراء", Group = "Purchase", Description = "إنشاء فاتورة مشتريات جديدة." },
				new Permission { Code = "purchase.edit", Name = "تعديل فاتورة شراء", Group = "Purchase", Description = "تعديل فاتورة المشتريات قبل اعتمادها." },
				new Permission { Code = "purchase.confirm", Name = "اعتماد فاتورة شراء", Group = "Purchase", Description = "اعتماد فاتورة المشتريات." },
				new Permission { Code = "purchase.cancel", Name = "إلغاء فاتورة شراء", Group = "Purchase", Description = "إلغاء فاتورة المشتريات." },

				new Permission { Code = "purchase.return.view", Name = "عرض مرتجعات المشتريات", Group = "PurchaseReturn", Description = "عرض مرتجعات المشتريات." },
				new Permission { Code = "purchase.return.create", Name = "إنشاء مرتجع شراء", Group = "PurchaseReturn", Description = "إنشاء مرتجع مشتريات." },
				new Permission { Code = "purchase.return.approve", Name = "اعتماد مرتجع شراء", Group = "PurchaseReturn", Description = "اعتماد مرتجع المشتريات." },
				new Permission { Code = "purchase.return.complete", Name = "إتمام مرتجع شراء", Group = "PurchaseReturn", Description = "إتمام مرتجع المشتريات." },

				new Permission { Code = "inventory.view", Name = "عرض المخزون", Group = "Inventory", Description = "عرض أرصدة وحركات المخزون." },
				new Permission { Code = "inventory.receive", Name = "استلام مخزون", Group = "Inventory", Description = "استلام الأصناف إلى المخزن." },
				new Permission { Code = "inventory.issue", Name = "صرف مخزون", Group = "Inventory", Description = "صرف الأصناف من المخزن." },
				new Permission { Code = "inventory.transfer", Name = "تحويل مخزون", Group = "Inventory", Description = "تحويل الأصناف بين المخازن." },
				new Permission { Code = "inventory.adjust", Name = "تسوية مخزون", Group = "Inventory", Description = "إجراء تسويات المخزون." },
				new Permission { Code = "inventory.count", Name = "جرد المخزون", Group = "Inventory", Description = "تنفيذ ومراجعة عمليات الجرد." },
				new Permission { Code = "inventory.transaction.view", Name = "عرض حركات المخزون", Group = "Inventory", Description = "عرض سجل حركات المخزون." },

				new Permission { Code = "treasury.view", Name = "عرض الخزينة", Group = "Treasury", Description = "عرض أرصدة وحركات الخزينة." },
				new Permission { Code = "treasury.receive", Name = "إيصال قبض", Group = "Treasury", Description = "تسجيل المقبوضات." },
				new Permission { Code = "treasury.pay", Name = "إذن صرف", Group = "Treasury", Description = "تسجيل المدفوعات." },
				new Permission { Code = "treasury.transfer", Name = "تحويل خزينة", Group = "Treasury", Description = "تحويل الأموال بين الخزن والحسابات المسموح بها." },
				new Permission { Code = "treasury.close", Name = "إغلاق الخزينة", Group = "Treasury", Description = "إغلاق وردية أو دورة الخزينة." },
				new Permission { Code = "treasury.adjust", Name = "تسوية الخزينة", Group = "Treasury", Description = "إجراء تسويات الخزينة." },
				new Permission { Code = "treasury.account.manage", Name = "إدارة مراكز الخزينة", Group = "Treasury", Description = "إضافة وتعديل مراكز الخزينة النقدية." },

				new Permission { Code = "accounting.view", Name = "عرض الحسابات", Group = "Accounting", Description = "عرض البيانات والحركات المحاسبية." },
				new Permission { Code = "accounting.manage", Name = "إدارة شجرة الحسابات", Group = "Accounting", Description = "إضافة وتعديل وتعطيل حسابات شجرة الحسابات." },
				new Permission { Code = "accounting.journal.view", Name = "عرض القيود", Group = "Accounting", Description = "عرض القيود المحاسبية." },
				new Permission { Code = "accounting.journal.create", Name = "إنشاء قيد", Group = "Accounting", Description = "إنشاء قيد محاسبي." },
				new Permission { Code = "accounting.journal.edit", Name = "تعديل قيد", Group = "Accounting", Description = "تعديل القيد وفق سياسة النظام." },
				new Permission { Code = "accounting.journal.approve", Name = "اعتماد قيد", Group = "Accounting", Description = "اعتماد القيود المحاسبية." },

				new Permission { Code = "product.view", Name = "عرض الأصناف", Group = "Product", Description = "عرض الأصناف والبيانات الأساسية." },
				new Permission { Code = "product.create", Name = "إضافة صنف", Group = "Product", Description = "إضافة صنف جديد." },
				new Permission { Code = "product.edit", Name = "تعديل صنف", Group = "Product", Description = "تعديل بيانات الصنف." },
				new Permission { Code = "product.activate", Name = "تفعيل / تعطيل صنف", Group = "Product", Description = "تغيير حالة الصنف." },

				new Permission { Code = "report.sales", Name = "تقارير المبيعات", Group = "Reports", Description = "عرض تقارير المبيعات." },
				new Permission { Code = "report.purchase", Name = "تقارير المشتريات", Group = "Reports", Description = "عرض تقارير المشتريات." },
				new Permission { Code = "report.inventory", Name = "تقارير المخزون", Group = "Reports", Description = "عرض تقارير المخزون." },
				new Permission { Code = "report.treasury", Name = "تقارير الخزينة", Group = "Reports", Description = "عرض تقارير الخزينة." },
				new Permission { Code = "report.accounting", Name = "التقارير المحاسبية", Group = "Reports", Description = "عرض التقارير المحاسبية." },
				new Permission { Code = "report.customer.statement", Name = "كشف حساب عميل", Group = "Reports", Description = "عرض كشوف حسابات العملاء." },

				new Permission { Code = "report.invoiceprofit", Name = "تقارير ربحية الفواتير", Group = "Reports", Description = "عرض تقارير ربحية الفواتير." },
				new Permission { Code = "report.supplier.statement", Name = "كشف حساب مورد", Group = "Reports", Description = "عرض كشوف حسابات الموردين." },

				new Permission { Code = "sales.return", Name = "مرتجعات البيع", Group = "Returns", Description = "إنشاء وإدارة مرتجعات البيع." },
				new Permission { Code = "purchase.return", Name = "مرتجعات الشراء", Group = "Returns", Description = "إنشاء وإدارة مرتجعات الشراء." }
			};

			var existingPermissionCodes =
				await context.Permissions
					.AsNoTracking()
					.Select(x => x.Code)
					.ToHashSetAsync();

			foreach (var permissionData in permissions)
			{
				if (!existingPermissionCodes.Contains(permissionData.Code))
				{
					context.Permissions.Add(permissionData);
				}
			}

			var permissionsAdded =
				context.ChangeTracker
					.Entries<Permission>()
					.Any(x => x.State == EntityState.Added);

			if (permissionsAdded)
			{
				await context.SaveChangesAsync();
			}

			// =========================================
			// Admin Role + Permissions
			// =========================================

			var adminRole = await context.Roles
				.FirstAsync(x => x.Code == "Admin");

			var allPermissions = await context.Permissions
				.Where(x => x.IsActive)
				.ToListAsync();

			var adminAssignedPermissionIds =
				await context.RolePermissions
					.AsNoTracking()
					.Where(x => x.RoleId == adminRole.Id)
					.Select(x => x.PermissionId)
					.ToHashSetAsync();

			foreach (var permission in allPermissions)
			{
				if (adminAssignedPermissionIds.Contains(permission.Id))
				{
					continue;
				}

				context.RolePermissions.Add(
					new RolePermission
					{
						RoleId = adminRole.Id,
						PermissionId = permission.Id,
						IsGranted = true,
						CreatedAt = DateTime.Now
					});
			}

			var adminPermissionsAdded =
				context.ChangeTracker
					.Entries<RolePermission>()
					.Any(x => x.State == EntityState.Added);

			if (adminPermissionsAdded)
			{
				await context.SaveChangesAsync();
			}

			// =========================================
			// ضمان وجود Admin
			// =========================================

			var adminUser = await context.Users
				.FirstOrDefaultAsync(x => x.UserType == UserType.Admin);

			var adminPhone = configuration["InitialAdmin:Phone"];
			var adminPassword = configuration["InitialAdmin:Password"];
			var adminForceReset = configuration
				.GetValue("InitialAdmin:ForceResetPassword", false);

			// ✅ تم إزالة [SEED-DEBUG] — كان يطبع كلمة المرور في الـ logs

			if (adminUser == null)
			{
				if (string.IsNullOrWhiteSpace(adminPhone) ||
					string.IsNullOrWhiteSpace(adminPassword))
				{
					throw new InvalidOperationException(
						"بيانات المدير الأول غير موجودة. " +
						"أضف InitialAdmin:Phone و InitialAdmin:Password في User Secrets.");
				}

				adminUser = new User
				{
					Phone = adminPhone.Trim(),
					UserType = UserType.Admin,
					RoleId = adminRole.Id,
					Status = UserStatus.Approved,
					IsActive = true,
					IsPasswordSet = true,
					CreatedAt = DateTime.Now
				};

				adminUser.PasswordHash =
					passwordHasher.HashPassword(adminUser, adminPassword);

				context.Users.Add(adminUser);
				await context.SaveChangesAsync();
			}
			else
			{
				var changed = false;

				if (adminUser.RoleId != adminRole.Id)
				{
					adminUser.RoleId = adminRole.Id;
					changed = true;
				}

				if (!string.IsNullOrWhiteSpace(adminPhone) &&
					adminUser.Phone != adminPhone.Trim())
				{
					adminUser.Phone = adminPhone.Trim();
					changed = true;
				}

				if (adminForceReset &&
					!string.IsNullOrWhiteSpace(adminPassword))
				{
					adminUser.PasswordHash =
						passwordHasher.HashPassword(adminUser, adminPassword);
					adminUser.IsPasswordSet = true;
					adminUser.IsActive = true;
					adminUser.Status = UserStatus.Approved;
					adminUser.FailedLoginAttempts = 0;
					adminUser.LockoutUntil = null;
					changed = true;
				}

				if (changed)
				{
					await context.SaveChangesAsync();
				}
			}

			// =========================================
			// ضمان وجود المدير العام الأول
			// =========================================

			var generalManagerRole =
				await context.Roles
					.FirstAsync(x => x.Code == "GeneralManager");

			var generalManagerName = configuration["InitialGeneralManager:Name"];
			var generalManagerPhone = configuration["InitialGeneralManager:Phone"];
			var generalManagerPassword = configuration["InitialGeneralManager:Password"];
			var generalManagerForceReset = configuration
				.GetValue("InitialGeneralManager:ForceResetPassword", false);

			var generalManagerEnabled =
				bool.TryParse(
					configuration["InitialGeneralManager:Enabled"],
					out var gmEnabled)
					? gmEnabled
					: true;

			if (generalManagerEnabled &&
				!string.IsNullOrWhiteSpace(generalManagerName) &&
				!string.IsNullOrWhiteSpace(generalManagerPhone) &&
				!string.IsNullOrWhiteSpace(generalManagerPassword))
			{
				var generalManagerUser =
					await context.Users
						.FirstOrDefaultAsync(x =>
							x.Phone == generalManagerPhone.Trim());

				Employee? generalManagerEmployee = null;

				if (generalManagerUser?.EmployeeId != null)
				{
					generalManagerEmployee =
						await context.Employees
							.FirstOrDefaultAsync(x =>
								x.Id == generalManagerUser.EmployeeId.Value);
				}

				if (generalManagerEmployee == null)
				{
					generalManagerEmployee =
						await context.Employees
							.FirstOrDefaultAsync(x =>
								x.Phone == generalManagerPhone.Trim());
				}

				if (generalManagerEmployee == null)
				{
					generalManagerEmployee = new Employee
					{
						Name = generalManagerName.Trim(),
						JobTitle = "مدير عام",
						Phone = generalManagerPhone.Trim(),
						IsActive = true,
						CreatedAt = DateTime.Now
					};

					context.Employees.Add(generalManagerEmployee);
					await context.SaveChangesAsync();
				}
				else
				{
					generalManagerEmployee.Name = generalManagerName.Trim();
					generalManagerEmployee.JobTitle = "مدير عام";
					generalManagerEmployee.Phone = generalManagerPhone.Trim();
					generalManagerEmployee.IsActive = true;

					await context.SaveChangesAsync();
				}

				if (generalManagerUser == null)
				{
					generalManagerUser = new User
					{
						Phone = generalManagerPhone.Trim(),
						UserType = UserType.Employee,
						RoleId = generalManagerRole.Id,
						EmployeeId = generalManagerEmployee.Id,
						Status = UserStatus.Approved,
						IsActive = true,
						IsPasswordSet = true,
						CreatedAt = DateTime.Now
					};

					generalManagerUser.PasswordHash =
						passwordHasher.HashPassword(
							generalManagerUser,
							generalManagerPassword);

					context.Users.Add(generalManagerUser);
					await context.SaveChangesAsync();
				}
				else
				{
					generalManagerUser.UserType = UserType.Employee;
					generalManagerUser.RoleId = generalManagerRole.Id;
					generalManagerUser.EmployeeId = generalManagerEmployee.Id;
					generalManagerUser.Status = UserStatus.Approved;
					generalManagerUser.IsActive = true;

					if (generalManagerForceReset)
					{
						generalManagerUser.PasswordHash =
							passwordHasher.HashPassword(
								generalManagerUser,
								generalManagerPassword);
						generalManagerUser.IsPasswordSet = true;
						generalManagerUser.FailedLoginAttempts = 0;
						generalManagerUser.LockoutUntil = null;
					}

					await context.SaveChangesAsync();
				}
			}

			// =========================================
			// الصلاحيات الافتراضية للأدوار
			// =========================================

			var rolePermissionsMap =
				new Dictionary<string, string[]>
				{
					["BranchManager"] = new[]
					{
						"branch.view", "branch.edit",
						"store.view", "user.view",
						"customer.view", "supplier.view",
						"product.view",
						"report.sales", "report.purchase", "report.inventory", "report.treasury"
					},

					["Cashier"] = new[]
					{
						"sales.view", "sales.create",
						"sales.return.view", "sales.return.create",
						"customer.view",
						"treasury.view", "treasury.receive"
					},

					["Storekeeper"] = new[]
					{
						"inventory.view", "inventory.receive", "inventory.issue",
						"inventory.transfer", "inventory.count", "inventory.transaction.view",
						"store.view", "product.view"
					},

					["Accountant"] = new[]
					{
						"accounting.view", "accounting.manage",
						"accounting.journal.view", "accounting.journal.create",
						"report.accounting", "report.customer.statement", "report.supplier.statement",
						"customer.view", "supplier.view",
						"treasury.view"
					},

					["Sales"] = new[]
					{
						"sales.view", "sales.create", "sales.edit",
						"sales.return.view", "sales.return.create",
						"customer.view", "customer.create", "customer.edit",
						"shipping.view", "shipping.create",
						"report.sales"
					},

					["Purchasing"] = new[]
					{
						"purchase.view", "purchase.create", "purchase.edit",
						"purchase.return.view", "purchase.return.create",
						"supplier.view", "supplier.create", "supplier.edit",
						"report.purchase"
					},

					["Treasury"] = new[]
					{
						"treasury.view", "treasury.receive", "treasury.pay",
						"treasury.transfer", "treasury.close",
						"report.treasury"
					},

					["InventoryController"] = new[]
					{
						"inventory.view", "inventory.transaction.view", "inventory.count",
						"product.view",
						"report.inventory"
					}
				};

			foreach (var item in rolePermissionsMap)
			{
				var role =
					await context.Roles
						.FirstOrDefaultAsync(x => x.Code == item.Key);

				if (role == null)
				{
					continue;
				}

				var roleAlreadyConfigured =
					await context.RolePermissions
						.AnyAsync(x => x.RoleId == role.Id);

				if (roleAlreadyConfigured)
				{
					continue;
				}

				var defaultPermissions =
					await context.Permissions
						.Where(x =>
							x.IsActive &&
							item.Value.Contains(x.Code))
						.ToListAsync();

				foreach (var permission in defaultPermissions)
				{
					context.RolePermissions.Add(
						new RolePermission
						{
							RoleId = role.Id,
							PermissionId = permission.Id,
							IsGranted = true,
							CreatedAt = DateTime.Now
						});
				}
			}

			var defaultRolePermissionsAdded =
				context.ChangeTracker
					.Entries<RolePermission>()
					.Any(x => x.State == EntityState.Added);

			if (defaultRolePermissionsAdded)
			{
				await context.SaveChangesAsync();
			}
		}

		// =========================================
		// شجرة الحسابات
		// =========================================

		public static async Task SeedChartOfAccountsAsync(
			ApplicationDbContext context)
		{
			var rawAccounts = new AccountSeed[]
			{
				new("1", "الأصول", AccountType.Asset, null, false),
				new("11", "الأصول المتداولة", AccountType.Asset, "1", false),
				new("1101", "الصندوق", AccountType.Asset, "11", true),
				new("1102", "البنك", AccountType.Asset, "11", true),
				new("1103", "العملاء", AccountType.Asset, "11", true),
				new("1104", "المخزون", AccountType.Asset, "11", true),
				new("1105", "ضريبة القيمة المضافة (المشتريات)", AccountType.Asset, "11", true),
				new("1106", "ضريبة المبيعات (المشتريات)", AccountType.Asset, "11", true),
				new("1107", "سلف موظفين", AccountType.Asset, "11", true),

				new("2", "الالتزامات", AccountType.Liability, null, false),
				new("21", "الالتزامات المتداولة", AccountType.Liability, "2", false),
				new("2101", "ضريبة القيمة المضافة", AccountType.Liability, "21", true),
				new("2102", "الموردون", AccountType.Liability, "21", true),
				new("2103", "ضريبة المبيعات (المبيعات)", AccountType.Liability, "21", true),
				new("2105", "رواتب مستحقة", AccountType.Liability, "21", true),

				new("3", "حقوق الملكية", AccountType.Equity, null, false),
				new("3001", "رأس المال", AccountType.Equity, "3", true),

				new("4", "الإيرادات", AccountType.Revenue, null, false),
				new("4001", "المبيعات", AccountType.Revenue, "4", true),
				new("4901", "خصومات المبيعات", AccountType.ContraRevenue, "4", true),

				new("5", "المصروفات", AccountType.Expense, null, false),
				new("5001", "تكلفة البضاعة المباعة", AccountType.Expense, "5", true),
				new("5101", "مصروف رواتب", AccountType.Expense, "5", true)
			};

			var existing =
				await context.ChartAccounts
					.Where(x => x.Code != null)
					.ToListAsync();

			var byCode =
				existing.ToDictionary(x => x.Code!);

			var addedAny = false;

			foreach (var raw in rawAccounts)
			{
				if (byCode.ContainsKey(raw.Code))
				{
					continue;
				}

				var added = new ChartAccount
				{
					Code = raw.Code,
					Name = raw.Name,
					Type = raw.Type,
					IsSystem = raw.System,
					IsActive = true,
					CreatedAt = DateTime.Now
				};

				context.ChartAccounts.Add(added);

				byCode[raw.Code] = added;

				addedAny = true;
			}

			if (addedAny)
			{
				await context.SaveChangesAsync();
			}

			var linkChanged = false;

			foreach (var raw in rawAccounts)
			{
				if (raw.Parent == null ||
					!byCode.TryGetValue(raw.Parent, out var parent) ||
					!byCode.TryGetValue(raw.Code, out var child))
				{
					continue;
				}

				if (child.ParentId == parent.Id)
				{
					continue;
				}

				child.ParentId = parent.Id;

				linkChanged = true;
			}

			if (linkChanged)
			{
				await context.SaveChangesAsync();
			}

			var cashBoxAccount =
				byCode.TryGetValue("1101", out var cashBox)
					? cashBox
					: await context.ChartAccounts
						.FirstOrDefaultAsync(x =>
							x.Code == "1101" &&
							x.Type == AccountType.Asset);

			if (cashBoxAccount != null)
			{
				var unlinkedCashAccount =
					await context.CashAccounts
						.Where(x =>
							x.IsActive &&
							x.ChartAccountId == null)
						.OrderBy(x => x.Id)
						.FirstOrDefaultAsync();

				if (unlinkedCashAccount != null)
				{
					unlinkedCashAccount.ChartAccountId =
						cashBoxAccount.Id;

					await context.SaveChangesAsync();
				}
			}
		}
	}
}