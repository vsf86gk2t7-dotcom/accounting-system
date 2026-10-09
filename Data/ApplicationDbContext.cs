using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Data
{
	public class ApplicationDbContext : DbContext
	{
		public ApplicationDbContext(
			DbContextOptions<ApplicationDbContext> options)
			: base(options)
		{
		}

		// =========================================
		// المبيعات
		// =========================================

		public DbSet<SalesInvoice> SalesInvoices { get; set; }
		public DbSet<SalesInvoiceItem> SalesInvoiceItems { get; set; }

		// =========================================
		// المستخدمون
		// =========================================

		public DbSet<User> Users { get; set; }

		// =========================================
		// الأدوار والصلاحيات
		// =========================================

		public DbSet<Role> Roles { get; set; }
		public DbSet<Permission> Permissions { get; set; }
		public DbSet<RolePermission> RolePermissions { get; set; }
		public DbSet<UserPermission> UserPermissions { get; set; }

		// ✅ جديد: آخر تعبئة لكل مورد/صنف/وحدة
		public DbSet<SupplierProductPacking> SupplierProductPackings { get; set; }

		// =========================================
		// العملاء والموردون والموظفون
		// =========================================

		public DbSet<Customer> Customers { get; set; }
		public DbSet<Supplier> Suppliers { get; set; }
		public DbSet<Employee> Employees { get; set; }

		// =========================================
		// الأقسام والمسميات الوظيفية
		// =========================================

		public DbSet<Department> Departments { get; set; }
		public DbSet<Position> Positions { get; set; }

		// =========================================
		// ربط الموظفين بالفروع والمخازن
		// =========================================

		public DbSet<EmployeeBranch> EmployeeBranches { get; set; }
		public DbSet<EmployeeStore> EmployeeStores { get; set; }

		// =========================================
		// المناديب
		// =========================================

		public DbSet<SalesRepProfile> SalesRepProfiles { get; set; }

		// =========================================
		// الشركة والفروع والمخازن
		// =========================================

		public DbSet<Company> Companies { get; set; }
		public DbSet<Branch> Branches { get; set; }
		public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }
		public DbSet<Store> Stores { get; set; }
		public DbSet<Unit> Units { get; set; }
		public DbSet<Product> Products { get; set; }
		public DbSet<ProductUnit> ProductUnits { get; set; }
		public DbSet<StockLot> StockLots { get; set; }
		public DbSet<SequenceCounter> SequenceCounters { get; set; }
		public DbSet<Category> Categories { get; set; }
		public DbSet<ProductCategory> ProductCategories { get; set; }
		public DbSet<SalesInvoiceItemLot> SalesInvoiceItemLots { get; set; }
		public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
		public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; }
		public DbSet<StockTransaction> StockTransactions { get; set; }
		public DbSet<StockTransactionItem> StockTransactionItems { get; set; }
		public DbSet<CashAccount> CashAccounts { get; set; }
		public DbSet<TreasuryTransaction> TreasuryTransactions { get; set; }
		public DbSet<ChartAccount> ChartAccounts { get; set; }
		public DbSet<JournalEntry> JournalEntries { get; set; }
		public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
		public DbSet<SalesReturnInvoice> SalesReturnInvoices { get; set; }
		public DbSet<SalesReturnInvoiceItem> SalesReturnInvoiceItems { get; set; }
		public DbSet<PurchaseReturnInvoice> PurchaseReturnInvoices { get; set; }
		public DbSet<PurchaseReturnInvoiceItem> PurchaseReturnInvoiceItems { get; set; }
		public DbSet<AuditLog> AuditLogs { get; set; }
		public DbSet<AppNotification> AppNotifications { get; set; }
		public DbSet<SalesReturnItemLot> SalesReturnItemLots { get; set; }

		public DbSet<PaymentReminder> PaymentReminders { get; set; }

		public DbSet<FiscalPeriod> FiscalPeriods { get; set; }

		public DbSet<ShippingCompany> ShippingCompanies { get; set; }
		public DbSet<ShippingBill> ShippingBills { get; set; }

		// =========================================
		// الموارد البشرية — الرواتب والسلف
		// =========================================

		public DbSet<PayrollRun> PayrollRuns { get; set; }
		public DbSet<PayrollItem> PayrollItems { get; set; }
		public DbSet<EmployeeAdvance> EmployeeAdvances { get; set; }

		// =========================================
		// الحضور والإجازات والخصومات
		// =========================================

		public DbSet<Attendance> Attendances { get; set; }
		public DbSet<LeaveRequest> LeaveRequests { get; set; }
		public DbSet<EmployeeDeduction> EmployeeDeductions { get; set; }

		protected override void OnModelCreating(
			ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// =========================================
			// PasswordResetRequest
			// =========================================

			modelBuilder.Entity<PasswordResetRequest>()
				.HasIndex(x => x.Phone);

			modelBuilder.Entity<PasswordResetRequest>()
				.HasIndex(x => x.IsCompleted);

			// =========================================
			// سجل التدقيق
			// =========================================

			modelBuilder.Entity<AuditLog>()
				.HasIndex(x => x.CreatedAt);

			modelBuilder.Entity<AuditLog>()
				.HasIndex(x => x.EntityName);

			// =========================================
			// إشعارات التطبيق
			// =========================================

			modelBuilder.Entity<AppNotification>()
				.HasIndex(x => new
				{
					x.UserId,
					x.IsRead
				});

			modelBuilder.Entity<AppNotification>()
				.HasIndex(x => x.CreatedAt);

			// =========================================
			// User
			// =========================================

			modelBuilder.Entity<User>()
				.HasIndex(x => x.Phone)
				.IsUnique();

			// =========================================
			// Category
			// =========================================

			modelBuilder.Entity<Category>()
				.HasIndex(x => x.Name)
				.IsUnique();

			// =========================================
			// ProductCategory
			// =========================================

			modelBuilder.Entity<ProductCategory>()
				.HasKey(x => new
				{
					x.ProductId,
					x.CategoryId
				});

			modelBuilder.Entity<ProductCategory>()
				.HasOne(x => x.Product)
				.WithMany(x => x.ProductCategories)
				.HasForeignKey(x => x.ProductId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<ProductCategory>()
				.HasOne(x => x.Category)
				.WithMany(x => x.ProductCategories)
				.HasForeignKey(x => x.CategoryId)
				.OnDelete(DeleteBehavior.Cascade);

			// =========================================
			// User -> Role
			// =========================================

			modelBuilder.Entity<User>()
				.HasOne(x => x.Role)
				.WithMany()
				.HasForeignKey(x => x.RoleId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// Customer
			// =========================================

			modelBuilder.Entity<Customer>()
				.HasIndex(x => x.Phone)
				.IsUnique();

			// =========================================
			// User -> Customer
			// =========================================

			modelBuilder.Entity<User>()
				.HasOne(x => x.Customer)
				.WithMany(x => x.Users)
				.HasForeignKey(x => x.CustomerId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// User -> Supplier
			// =========================================

			modelBuilder.Entity<User>()
				.HasOne(x => x.Supplier)
				.WithMany(x => x.Users)
				.HasForeignKey(x => x.SupplierId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// User -> Employee
			// =========================================

			modelBuilder.Entity<User>()
				.HasOne(x => x.Employee)
				.WithMany(x => x.Users)
				.HasForeignKey(x => x.EmployeeId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// Role -> RolePermission
			// =========================================

			modelBuilder.Entity<RolePermission>()
				.HasOne(x => x.Role)
				.WithMany(x => x.RolePermissions)
				.HasForeignKey(x => x.RoleId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// Permission -> RolePermission
			// =========================================

			modelBuilder.Entity<RolePermission>()
				.HasOne(x => x.Permission)
				.WithMany(x => x.RolePermissions)
				.HasForeignKey(x => x.PermissionId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// User -> UserPermission
			// =========================================

			modelBuilder.Entity<UserPermission>()
				.HasOne(x => x.User)
				.WithMany()
				.HasForeignKey(x => x.UserId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// Permission -> UserPermission
			// =========================================

			modelBuilder.Entity<UserPermission>()
				.HasOne(x => x.Permission)
				.WithMany(x => x.UserPermissions)
				.HasForeignKey(x => x.PermissionId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// Unique Indexes — Permissions / Roles
			// =========================================

			modelBuilder.Entity<Permission>()
				.HasIndex(x => x.Code)
				.IsUnique();

			modelBuilder.Entity<Role>()
				.HasIndex(x => x.Code)
				.IsUnique();

			modelBuilder.Entity<RolePermission>()
				.HasIndex(x => new
				{
					x.RoleId,
					x.PermissionId
				})
				.IsUnique();

			modelBuilder.Entity<UserPermission>()
				.HasIndex(x => new
				{
					x.UserId,
					x.PermissionId
				})
				.IsUnique();

			// =========================================
			// ✅ Unit — إعدادات الوحدة
			// =========================================

			// الاسم فريد
			modelBuilder.Entity<Unit>()
				.HasIndex(x => x.Name)
				.IsUnique();

			// دقة عدد القطع
			modelBuilder.Entity<Unit>()
				.Property(x => x.PiecesPerUnit)
				.HasPrecision(18, 4);

			// =========================================
			// Product
			// =========================================

			modelBuilder.Entity<Product>()
				.HasIndex(x => x.Barcode)
				.IsUnique();

			modelBuilder.Entity<Product>()
				.HasIndex(x => x.Code)
				.IsUnique();

			// ✅ MinQuantity precision
			modelBuilder.Entity<Product>()
				.Property(x => x.MinQuantity)
				.HasPrecision(18, 3);

			// ✅ Product -> BaseUnit (مرة واحدة فقط)
			modelBuilder.Entity<Product>()
				.HasOne(x => x.BaseUnit)
				.WithMany()
				.HasForeignKey(x => x.BaseUnitId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// ProductUnit
			// =========================================

			modelBuilder.Entity<ProductUnit>()
				.Property(x => x.ConversionFactor)
				.HasPrecision(18, 6);

			modelBuilder.Entity<ProductUnit>()
				.Property(x => x.SalePrice)
				.HasPrecision(18, 2);

			modelBuilder.Entity<ProductUnit>()
				.HasOne(x => x.Product)
				.WithMany(x => x.ProductUnits)
				.HasForeignKey(x => x.ProductId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<ProductUnit>()
				.HasOne(x => x.Unit)
				.WithMany()
				.HasForeignKey(x => x.UnitId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<ProductUnit>()
				.HasIndex(x => new
				{
					x.ProductId,
					x.UnitId
				})
				.IsUnique();

			// =========================================
			// ✅ SupplierProductPacking — آخر تعبئة لكل مورد
			// =========================================

			modelBuilder.Entity<SupplierProductPacking>()
				.Property(x => x.ConversionFactor)
				.HasPrecision(18, 4);

			modelBuilder.Entity<SupplierProductPacking>()
				.HasIndex(x => new
				{
					x.SupplierId,
					x.ProductId,
					x.UnitId
				})
				.IsUnique()
				.HasDatabaseName("IX_SupplierProductPackings_Unique");

			modelBuilder.Entity<SupplierProductPacking>()
				.HasOne(x => x.Supplier)
				.WithMany()
				.HasForeignKey(x => x.SupplierId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<SupplierProductPacking>()
				.HasOne(x => x.Product)
				.WithMany()
				.HasForeignKey(x => x.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<SupplierProductPacking>()
				.HasOne(x => x.Unit)
				.WithMany()
				.HasForeignKey(x => x.UnitId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// StockLot
			// =========================================

			modelBuilder.Entity<StockLot>()
				.Property(x => x.QuantityReceived)
				.HasPrecision(18, 3);

			modelBuilder.Entity<StockLot>()
				.Property(x => x.QuantityRemaining)
				.HasPrecision(18, 3);

			modelBuilder.Entity<StockLot>()
				.Property(x => x.UnitCost)
				.HasPrecision(18, 4);

			modelBuilder.Entity<StockLot>()
				.HasOne(x => x.Product)
				.WithMany(x => x.StockLots)
				.HasForeignKey(x => x.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<StockLot>()
				.HasOne(x => x.Store)
				.WithMany()
				.HasForeignKey(x => x.StoreId)
				.OnDelete(DeleteBehavior.Restrict);

			// ✅ المورد اختياري (null = دفعة نظامية)
			modelBuilder.Entity<StockLot>()
				.HasOne(x => x.Supplier)
				.WithMany()
				.HasForeignKey(x => x.SupplierId)
				.IsRequired(false)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<StockLot>()
				.HasIndex(x => new
				{
					x.StoreId,
					x.ProductId,
					x.PurchaseDate,
					x.Id
				});

			// ✅ RowVersion — حماية من التزامن (منع Overselling)
			modelBuilder.Entity<StockLot>()
				.Property(x => x.RowVersion)
				.IsRowVersion();

			// =========================================
			// SalesInvoiceItemLot
			// =========================================

			modelBuilder.Entity<SalesInvoiceItemLot>()
				.Property(x => x.QuantityBaseUnit)
				.HasPrecision(18, 3);

			modelBuilder.Entity<SalesInvoiceItemLot>()
				.Property(x => x.UnitCost)
				.HasPrecision(18, 4);

			modelBuilder.Entity<SalesInvoiceItemLot>()
				.Property(x => x.TotalCost)
				.HasPrecision(18, 4);

			modelBuilder.Entity<SalesInvoiceItemLot>()
				.HasOne(x => x.SalesInvoiceItem)
				.WithMany(x => x.LotAllocations)
				.HasForeignKey(x => x.SalesInvoiceItemId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<SalesInvoiceItemLot>()
				.HasOne(x => x.StockLot)
				.WithMany()
				.HasForeignKey(x => x.StockLotId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<SalesInvoiceItemLot>()
				.HasIndex(x => x.StockLotId);

			// =========================================
			// Company -> Branch
			// =========================================

			modelBuilder.Entity<Branch>()
				.HasOne(x => x.Company)
				.WithMany(x => x.Branches)
				.HasForeignKey(x => x.CompanyId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<Branch>()
				.HasIndex(x => new
				{
					x.CompanyId,
					x.Name
				})
				.IsUnique();

			// =========================================
			// Branch -> Store
			// =========================================

			modelBuilder.Entity<Store>()
				.HasOne(x => x.Branch)
				.WithMany(x => x.Stores)
				.HasForeignKey(x => x.BranchId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<Store>()
				.HasIndex(x => new
				{
					x.BranchId,
					x.Name
				})
				.IsUnique();

			// =========================================
			// Employee <-> Branch
			// =========================================

			modelBuilder.Entity<EmployeeBranch>()
				.HasKey(x => new
				{
					x.EmployeeId,
					x.BranchId
				});

			modelBuilder.Entity<EmployeeBranch>()
				.HasOne(x => x.Employee)
				.WithMany(x => x.EmployeeBranches)
				.HasForeignKey(x => x.EmployeeId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<EmployeeBranch>()
				.HasOne(x => x.Branch)
				.WithMany(x => x.EmployeeBranches)
				.HasForeignKey(x => x.BranchId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// Employee <-> Store
			// =========================================

			modelBuilder.Entity<EmployeeStore>()
				.HasKey(x => new
				{
					x.EmployeeId,
					x.StoreId
				});

			modelBuilder.Entity<EmployeeStore>()
				.HasOne(x => x.Employee)
				.WithMany(x => x.EmployeeStores)
				.HasForeignKey(x => x.EmployeeId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<EmployeeStore>()
				.HasOne(x => x.Store)
				.WithMany(x => x.EmployeeStores)
				.HasForeignKey(x => x.StoreId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// الدقة العشرية — Customer / Supplier / Employee
			// =========================================

			modelBuilder.Entity<Customer>()
				.Property(x => x.OpeningBalance)
				.HasPrecision(18, 2);

			modelBuilder.Entity<Supplier>()
				.Property(x => x.OpeningBalance)
				.HasPrecision(18, 2);

			modelBuilder.Entity<Employee>()
				.Property(x => x.Salary)
				.HasPrecision(18, 2);

			// =========================================
			// Department / Position
			// =========================================

			modelBuilder.Entity<Department>()
				.HasIndex(x => x.Name)
				.IsUnique();

			modelBuilder.Entity<Position>()
				.HasIndex(x => x.Name)
				.IsUnique();

			modelBuilder.Entity<Employee>()
				.HasOne(x => x.Department)
				.WithMany(x => x.Employees)
				.HasForeignKey(x => x.DepartmentId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<Employee>()
				.HasOne(x => x.Position)
				.WithMany(x => x.Employees)
				.HasForeignKey(x => x.PositionId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// SalesRepProfile
			// =========================================

			modelBuilder.Entity<SalesRepProfile>(e =>
			{
				e.HasIndex(x => x.EmployeeId).IsUnique();
				e.Property(x => x.CommissionRate).HasPrecision(18, 4);
				e.Property(x => x.MonthlyTarget).HasPrecision(18, 2);

				e.HasOne(x => x.Employee)
					.WithMany()
					.HasForeignKey(x => x.EmployeeId)
					.OnDelete(DeleteBehavior.Restrict);

				e.HasOne(x => x.CustodyStore)
					.WithMany()
					.HasForeignKey(x => x.CustodyStoreId)
					.OnDelete(DeleteBehavior.Restrict);

				e.HasOne(x => x.CashAccount)
					.WithMany()
					.HasForeignKey(x => x.CashAccountId)
					.OnDelete(DeleteBehavior.Restrict);
			});

			// =========================================
			// Customer -> SalesRep
			// =========================================

			modelBuilder.Entity<Customer>()
				.HasOne(x => x.SalesRep)
				.WithMany(x => x.Customers)
				.HasForeignKey(x => x.SalesRepId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// SalesInvoice -> SalesRep
			// =========================================

			modelBuilder.Entity<SalesInvoice>()
				.HasOne(x => x.SalesRep)
				.WithMany(x => x.Invoices)
				.HasForeignKey(x => x.SalesRepId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<SalesInvoice>()
				.HasIndex(x => new { x.SalesRepId, x.InvoiceDate });

			// الرقم القومي فريد (لكن يسمح بـ NULL متعدد)
			modelBuilder.Entity<Employee>()
				.HasIndex(x => x.NationalId)
				.IsUnique()
				.HasFilter("[NationalId] IS NOT NULL");

			// =========================================
			// SalesInvoice
			// =========================================

			modelBuilder.Entity<SalesInvoice>()
				.HasIndex(x => x.InvoiceNumber)
				.IsUnique();

			// ✅ RowVersion — منع ترحيل الفاتورة مرتين
			modelBuilder.Entity<SalesInvoice>()
				.Property(x => x.RowVersion)
				.IsRowVersion();

			modelBuilder.Entity<SalesInvoice>()
				.Property(x => x.SubTotal)
				.HasPrecision(18, 2);

			modelBuilder.Entity<SalesInvoice>()
				.Property(x => x.DiscountAmount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<SalesInvoice>()
				.Property(x => x.TaxAmount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<SalesInvoice>()
				.Property(x => x.TotalAmount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<SalesInvoice>()
				.Property(x => x.SalesTaxAmount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<SalesInvoice>()
				.Property(x => x.SalesTaxRate)
				.HasPrecision(18, 4);

			modelBuilder.Entity<SalesInvoice>()
				.Property(x => x.TaxRate)
				.HasPrecision(18, 4);

			modelBuilder.Entity<SalesInvoice>()
				.HasOne(x => x.Branch)
				.WithMany()
				.HasForeignKey(x => x.BranchId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<SalesInvoice>()
				.HasOne(x => x.Store)
				.WithMany()
				.HasForeignKey(x => x.StoreId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<SalesInvoice>()
				.HasOne(x => x.Customer)
				.WithMany()
				.HasForeignKey(x => x.CustomerId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<SalesInvoice>()
				.HasOne(x => x.CreatedByUser)
				.WithMany()
				.HasForeignKey(x => x.CreatedByUserId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// SalesInvoiceItem
			// =========================================

			modelBuilder.Entity<SalesInvoiceItem>()
				.Property(x => x.Quantity)
				.HasPrecision(18, 3);

			modelBuilder.Entity<SalesInvoiceItem>()
				.Property(x => x.UnitPrice)
				.HasPrecision(18, 2);

			modelBuilder.Entity<SalesInvoiceItem>()
				.Property(x => x.TotalPrice)
				.HasPrecision(18, 2);

			modelBuilder.Entity<SalesInvoiceItem>()
				.Property(x => x.ConversionFactor)
				.HasPrecision(18, 6);

			modelBuilder.Entity<SalesInvoiceItem>()
				.Property(x => x.QuantityInBaseUnit)
				.HasPrecision(18, 3);

			modelBuilder.Entity<SalesInvoiceItem>()
				.HasOne(x => x.SalesInvoice)
				.WithMany(x => x.Items)
				.HasForeignKey(x => x.SalesInvoiceId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<SalesInvoiceItem>()
				.HasIndex(x => x.SalesInvoiceId);

			// =========================================
			// PurchaseInvoice
			// =========================================

			modelBuilder.Entity<PurchaseInvoice>()
				.HasIndex(x => x.InvoiceNumber)
				.IsUnique();

			// ✅ RowVersion — منع ترحيل الفاتورة مرتين
			modelBuilder.Entity<PurchaseInvoice>()
				.Property(x => x.RowVersion)
				.IsRowVersion();

			modelBuilder.Entity<PurchaseInvoice>()
				.Property(x => x.SubTotal)
				.HasPrecision(18, 2);

			modelBuilder.Entity<PurchaseInvoice>()
				.Property(x => x.DiscountAmount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<PurchaseInvoice>()
				.Property(x => x.TaxAmount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<PurchaseInvoice>()
				.Property(x => x.TotalAmount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<PurchaseInvoice>()
				.Property(x => x.SalesTaxAmount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<PurchaseInvoice>()
				.Property(x => x.SalesTaxRate)
				.HasPrecision(18, 4);

			modelBuilder.Entity<PurchaseInvoice>()
				.Property(x => x.TaxRate)
				.HasPrecision(18, 4);

			modelBuilder.Entity<PurchaseInvoice>()
				.HasOne(x => x.Supplier)
				.WithMany()
				.HasForeignKey(x => x.SupplierId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<PurchaseInvoice>()
				.HasOne(x => x.Store)
				.WithMany()
				.HasForeignKey(x => x.StoreId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// PurchaseInvoiceItem
			// =========================================

			modelBuilder.Entity<PurchaseInvoiceItem>()
				.Property(x => x.ConversionFactor)
				.HasPrecision(18, 6);

			modelBuilder.Entity<PurchaseInvoiceItem>()
				.Property(x => x.Quantity)
				.HasPrecision(18, 3);

			modelBuilder.Entity<PurchaseInvoiceItem>()
				.Property(x => x.QuantityInBaseUnit)
				.HasPrecision(18, 3);

			modelBuilder.Entity<PurchaseInvoiceItem>()
				.Property(x => x.UnitPrice)
				.HasPrecision(18, 4);

			modelBuilder.Entity<PurchaseInvoiceItem>()
				.Property(x => x.UnitCostInBaseUnit)
				.HasPrecision(18, 4);

			modelBuilder.Entity<PurchaseInvoiceItem>()
				.Property(x => x.LineTotal)
				.HasPrecision(18, 4);

			modelBuilder.Entity<PurchaseInvoiceItem>()
				.HasOne(x => x.PurchaseInvoice)
				.WithMany(x => x.Items)
				.HasForeignKey(x => x.PurchaseInvoiceId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<PurchaseInvoiceItem>()
				.HasIndex(x => x.PurchaseInvoiceId);

			modelBuilder.Entity<PurchaseInvoiceItem>()
				.HasOne(x => x.Product)
				.WithMany()
				.HasForeignKey(x => x.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<PurchaseInvoiceItem>()
				.HasOne(x => x.Unit)
				.WithMany()
				.HasForeignKey(x => x.UnitId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<PurchaseInvoiceItem>()
				.HasOne(x => x.StockLot)
				.WithOne(x => x.PurchaseInvoiceItem!)
				.HasForeignKey<StockLot>(x => x.PurchaseInvoiceItemId)
				.OnDelete(DeleteBehavior.SetNull);

			// =========================================
			// StockTransaction
			// =========================================

			modelBuilder.Entity<StockTransaction>()
				.HasIndex(x => x.TransactionNumber)
				.IsUnique();

			modelBuilder.Entity<StockTransaction>()
				.HasIndex(x => x.Type);

			modelBuilder.Entity<StockTransaction>()
				.HasIndex(x => x.CreatedAt);

			modelBuilder.Entity<StockTransaction>()
				.HasOne(x => x.FromStore)
				.WithMany()
				.HasForeignKey(x => x.FromStoreId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<StockTransaction>()
				.HasOne(x => x.ToStore)
				.WithMany()
				.HasForeignKey(x => x.ToStoreId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<StockTransaction>()
				.HasOne(x => x.CreatedByUser)
				.WithMany()
				.HasForeignKey(x => x.CreatedByUserId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<StockTransaction>()
				.HasMany(x => x.Items)
				.WithOne(x => x.StockTransaction)
				.HasForeignKey(x => x.StockTransactionId)
				.OnDelete(DeleteBehavior.Cascade);

			// =========================================
			// StockTransactionItem
			// =========================================

			modelBuilder.Entity<StockTransactionItem>()
				.Property(x => x.Quantity)
				.HasPrecision(18, 3);

			modelBuilder.Entity<StockTransactionItem>()
				.Property(x => x.UnitCost)
				.HasPrecision(18, 4);

			modelBuilder.Entity<StockTransactionItem>()
				.HasOne(x => x.Product)
				.WithMany()
				.HasForeignKey(x => x.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<StockTransactionItem>()
				.HasOne(x => x.StockLot)
				.WithMany()
				.HasForeignKey(x => x.StockLotId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<StockTransactionItem>()
				.HasIndex(x => x.ProductId);

			modelBuilder.Entity<StockTransactionItem>()
				.HasIndex(x => x.StockTransactionId);

			// =========================================
			// CashAccount
			// =========================================

			modelBuilder.Entity<CashAccount>()
				.HasMany(x => x.Transactions)
				.WithOne(x => x.CashAccount)
				.HasForeignKey(x => x.CashAccountId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<CashAccount>()
				.HasOne(x => x.ChartAccount)
				.WithMany()
				.HasForeignKey(x => x.ChartAccountId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<CashAccount>()
				.Property(x => x.DailyLimit)
				.HasPrecision(18, 2);

			modelBuilder.Entity<CashAccount>()
				.Property(x => x.MonthlyLimit)
				.HasPrecision(18, 2);

			// =========================================
			// TreasuryTransaction
			// =========================================

			modelBuilder.Entity<TreasuryTransaction>()
				.HasIndex(x => x.TransactionNumber)
				.IsUnique();

			modelBuilder.Entity<TreasuryTransaction>()
				.HasIndex(x => x.Type);

			modelBuilder.Entity<TreasuryTransaction>()
				.HasIndex(x => x.CreatedAt);

			modelBuilder.Entity<TreasuryTransaction>()
				.HasIndex(x => x.CashAccountId);

			modelBuilder.Entity<TreasuryTransaction>()
				.Property(x => x.Amount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<TreasuryTransaction>()
				.Property(x => x.WalletCommissionAmount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<TreasuryTransaction>()
				.Property(x => x.WalletFee)
				.HasPrecision(18, 2);

			modelBuilder.Entity<TreasuryTransaction>()
				.HasOne(x => x.TransferToCashAccount)
				.WithMany()
				.HasForeignKey(x => x.TransferToCashAccountId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<TreasuryTransaction>()
				.HasOne(x => x.Customer)
				.WithMany()
				.HasForeignKey(x => x.CustomerId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<TreasuryTransaction>()
				.HasOne(x => x.Supplier)
				.WithMany()
				.HasForeignKey(x => x.SupplierId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<TreasuryTransaction>()
				.HasOne(x => x.Employee)
				.WithMany()
				.HasForeignKey(x => x.EmployeeId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<TreasuryTransaction>()
				.HasOne(x => x.CreatedByUser)
				.WithMany()
				.HasForeignKey(x => x.CreatedByUserId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// SequenceCounter
			// =========================================

			modelBuilder.Entity<SequenceCounter>()
				.HasIndex(x => x.Key)
				.IsUnique();

			// =========================================
			// ChartAccount
			// =========================================

			modelBuilder.Entity<ChartAccount>()
				.HasIndex(x => x.Code)
				.IsUnique();

			modelBuilder.Entity<ChartAccount>()
				.HasIndex(x => x.Name)
				.IsUnique();

			modelBuilder.Entity<ChartAccount>()
				.HasOne(x => x.Parent)
				.WithMany(x => x.Children)
				.HasForeignKey(x => x.ParentId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// JournalEntry
			// =========================================

			modelBuilder.Entity<JournalEntry>()
				.HasIndex(x => x.EntryNumber)
				.IsUnique();

			modelBuilder.Entity<JournalEntry>()
				.HasIndex(x => x.EntryDate);

			modelBuilder.Entity<JournalEntry>()
				.HasIndex(x => x.IsPosted);

			modelBuilder.Entity<JournalEntry>()
				.HasMany(x => x.Lines)
				.WithOne(x => x.JournalEntry)
				.HasForeignKey(x => x.JournalEntryId)
				.OnDelete(DeleteBehavior.Cascade);

			// =========================================
			// ✅ Idempotency — منع تكرار القيود
			// =========================================

			modelBuilder.Entity<JournalEntry>()
				.HasIndex(x => new
				{
					x.SourceType,
					x.SourceId,
					x.EntryKind
				})
				.IsUnique()
				.HasDatabaseName("IX_JournalEntries_Source_Kind_Unique");

			// =========================================
			// JournalEntryLine
			// =========================================

			modelBuilder.Entity<JournalEntryLine>()
				.Property(x => x.Debit)
				.HasPrecision(18, 2);

			modelBuilder.Entity<JournalEntryLine>()
				.Property(x => x.Credit)
				.HasPrecision(18, 2);

			modelBuilder.Entity<JournalEntryLine>()
				.HasOne(x => x.ChartAccount)
				.WithMany()
				.HasForeignKey(x => x.ChartAccountId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<JournalEntryLine>()
				.HasOne(x => x.Customer)
				.WithMany()
				.HasForeignKey(x => x.CustomerId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<JournalEntryLine>()
				.HasOne(x => x.Supplier)
				.WithMany()
				.HasForeignKey(x => x.SupplierId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<JournalEntryLine>()
				.HasOne(x => x.Store)
				.WithMany()
				.HasForeignKey(x => x.StoreId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<JournalEntryLine>()
				.HasIndex(x => x.ChartAccountId);

			// =========================================
			// SalesReturnInvoice
			// =========================================

			modelBuilder.Entity<SalesReturnInvoice>()
				.HasIndex(x => x.InvoiceNumber)
				.IsUnique();

			// ✅ RowVersion — منع ترحيل المرتجع مرتين
			modelBuilder.Entity<SalesReturnInvoice>()
				.Property(x => x.RowVersion)
				.IsRowVersion();

			// ✅ TotalAmount precision
			modelBuilder.Entity<SalesReturnInvoice>()
				.Property(x => x.TotalAmount)
				.HasPrecision(18, 2);

			// ✅ RefundAmount precision
			modelBuilder.Entity<SalesReturnInvoice>()
				.Property(x => x.RefundAmount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<SalesReturnInvoice>()
				.HasMany(x => x.Items)
				.WithOne(x => x.SalesReturnInvoice)
				.HasForeignKey(x => x.SalesReturnInvoiceId)
				.OnDelete(DeleteBehavior.Cascade);

			// =========================================
			// SalesReturnInvoiceItem
			// =========================================

			modelBuilder.Entity<SalesReturnInvoiceItem>()
				.Property(x => x.Quantity)
				.HasPrecision(18, 3);

			modelBuilder.Entity<SalesReturnInvoiceItem>()
				.Property(x => x.UnitCost)
				.HasPrecision(18, 4);

			modelBuilder.Entity<SalesReturnInvoiceItem>()
				.Property(x => x.Total)
				.HasPrecision(18, 4);

			// ✅ ConversionFactor precision
			modelBuilder.Entity<SalesReturnInvoiceItem>()
				.Property(x => x.ConversionFactor)
				.HasPrecision(18, 6);

			// ✅ QuantityInBaseUnit precision
			modelBuilder.Entity<SalesReturnInvoiceItem>()
				.Property(x => x.QuantityInBaseUnit)
				.HasPrecision(18, 3);

			// ✅ UnitPrice precision
			modelBuilder.Entity<SalesReturnInvoiceItem>()
				.Property(x => x.UnitPrice)
				.HasPrecision(18, 4);

			modelBuilder.Entity<SalesReturnInvoiceItem>()
				.HasOne(x => x.Product)
				.WithMany()
				.HasForeignKey(x => x.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// PurchaseReturnInvoice
			// =========================================

			modelBuilder.Entity<PurchaseReturnInvoice>()
				.HasIndex(x => x.InvoiceNumber)
				.IsUnique();

			// ✅ TotalAmount precision
			modelBuilder.Entity<PurchaseReturnInvoice>()
				.Property(x => x.TotalAmount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<PurchaseReturnInvoice>()
				.HasMany(x => x.Items)
				.WithOne(x => x.PurchaseReturnInvoice)
				.HasForeignKey(x => x.PurchaseReturnInvoiceId)
				.OnDelete(DeleteBehavior.Cascade);

			// =========================================
			// PurchaseReturnInvoiceItem
			// =========================================

			modelBuilder.Entity<PurchaseReturnInvoiceItem>()
				.Property(x => x.Quantity)
				.HasPrecision(18, 3);

			modelBuilder.Entity<PurchaseReturnInvoiceItem>()
				.Property(x => x.UnitCost)
				.HasPrecision(18, 4);

			modelBuilder.Entity<PurchaseReturnInvoiceItem>()
				.Property(x => x.Total)
				.HasPrecision(18, 4);

			modelBuilder.Entity<PurchaseReturnInvoiceItem>()
				.HasOne(x => x.Product)
				.WithMany()
				.HasForeignKey(x => x.ProductId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// SalesReturnItemLot
			// =========================================

			// ✅ QuantityBaseUnit precision
			modelBuilder.Entity<SalesReturnItemLot>()
				.Property(x => x.QuantityBaseUnit)
				.HasPrecision(18, 3);

			// ✅ UnitCost precision
			modelBuilder.Entity<SalesReturnItemLot>()
				.Property(x => x.UnitCost)
				.HasPrecision(18, 4);

			// ✅ TotalCost precision
			modelBuilder.Entity<SalesReturnItemLot>()
				.Property(x => x.TotalCost)
				.HasPrecision(18, 4);

			// =========================================
			// ShippingCompany
			// =========================================

			modelBuilder.Entity<ShippingCompany>()
				.HasIndex(x => x.Name);

			modelBuilder.Entity<ShippingCompany>()
				.Property(x => x.ShippingRate)
				.HasPrecision(18, 2);

			// =========================================
			// ShippingBill
			// =========================================

			modelBuilder.Entity<ShippingBill>()
				.HasIndex(x => x.BillNumber);

			modelBuilder.Entity<ShippingBill>()
				.HasOne(x => x.SalesInvoice)
				.WithOne(x => x.ShippingBill)
				.HasForeignKey<ShippingBill>(x => x.SalesInvoiceId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<ShippingBill>()
				.HasOne(x => x.ShippingCompany)
				.WithMany()
				.HasForeignKey(x => x.ShippingCompanyId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<ShippingBill>()
				.HasOne(x => x.Driver)
				.WithMany()
				.HasForeignKey(x => x.DriverId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<ShippingBill>()
				.HasOne(x => x.CreatedByUser)
				.WithMany()
				.HasForeignKey(x => x.CreatedByUserId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// PayrollRun
			// =========================================

			modelBuilder.Entity<PayrollRun>()
				.HasIndex(x => x.PeriodName)
				.IsUnique();

			modelBuilder.Entity<PayrollRun>()
				.Property(x => x.TotalGross).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollRun>()
				.Property(x => x.TotalAbsenceDeduction).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollRun>()
				.Property(x => x.TotalLateDeduction).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollRun>()
				.Property(x => x.TotalManualDeductions).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollRun>()
				.Property(x => x.TotalAdvances).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollRun>()
				.Property(x => x.TotalCommissions).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollRun>()
				.Property(x => x.TotalBonuses).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollRun>()
				.Property(x => x.TotalNet).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollRun>()
				.HasOne(x => x.JournalEntry)
				.WithMany()
				.HasForeignKey(x => x.JournalEntryId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<PayrollRun>()
				.HasOne(x => x.PaymentJournalEntry)
				.WithMany()
				.HasForeignKey(x => x.PaymentJournalEntryId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<PayrollRun>()
				.HasOne(x => x.PaymentCashAccount)
				.WithMany()
				.HasForeignKey(x => x.PaymentCashAccountId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<PayrollRun>()
				.HasOne(x => x.CreatedByUser)
				.WithMany()
				.HasForeignKey(x => x.CreatedByUserId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<PayrollRun>()
				.HasMany(x => x.Items)
				.WithOne(x => x.PayrollRun)
				.HasForeignKey(x => x.PayrollRunId)
				.OnDelete(DeleteBehavior.Cascade);

			// =========================================
			// PayrollItem
			// =========================================

			modelBuilder.Entity<PayrollItem>()
				.Property(x => x.BaseSalary).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollItem>()
				.Property(x => x.AbsenceDeduction).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollItem>()
				.Property(x => x.LateDeduction).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollItem>()
				.Property(x => x.ManualDeductions).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollItem>()
				.Property(x => x.AdvancesDeduction).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollItem>()
				.Property(x => x.Commissions).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollItem>()
				.Property(x => x.Bonuses).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollItem>()
				.Property(x => x.NetSalary).HasPrecision(18, 2);

			modelBuilder.Entity<PayrollItem>()
				.HasOne(x => x.Employee)
				.WithMany()
				.HasForeignKey(x => x.EmployeeId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<PayrollItem>()
				.HasIndex(x => new { x.PayrollRunId, x.EmployeeId })
				.IsUnique();

			// =========================================
			// EmployeeAdvance
			// =========================================

			modelBuilder.Entity<EmployeeAdvance>()
				.Property(x => x.Amount).HasPrecision(18, 2);

			modelBuilder.Entity<EmployeeAdvance>()
				.HasOne(x => x.Employee)
				.WithMany()
				.HasForeignKey(x => x.EmployeeId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<EmployeeAdvance>()
				.HasOne(x => x.CashAccount)
				.WithMany()
				.HasForeignKey(x => x.CashAccountId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<EmployeeAdvance>()
				.HasOne(x => x.PayrollItem)
				.WithMany()
				.HasForeignKey(x => x.PayrollItemId)
				.IsRequired(false)
				.OnDelete(DeleteBehavior.SetNull);

			modelBuilder.Entity<EmployeeAdvance>()
				.HasOne(x => x.JournalEntry)
				.WithMany()
				.HasForeignKey(x => x.JournalEntryId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<EmployeeAdvance>()
				.HasOne(x => x.CreatedByUser)
				.WithMany()
				.HasForeignKey(x => x.CreatedByUserId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<EmployeeAdvance>()
				.HasIndex(x => new { x.EmployeeId, x.Status });

			// =========================================
			// Attendance
			// =========================================

			modelBuilder.Entity<Attendance>()
				.HasIndex(x => new { x.EmployeeId, x.Date })
				.IsUnique();

			modelBuilder.Entity<Attendance>()
				.HasIndex(x => x.Date);

			modelBuilder.Entity<Attendance>()
				.HasOne(x => x.Employee)
				.WithMany()
				.HasForeignKey(x => x.EmployeeId)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// LeaveRequest
			// =========================================

			modelBuilder.Entity<LeaveRequest>()
				.HasIndex(x => new { x.EmployeeId, x.Status });

			modelBuilder.Entity<LeaveRequest>()
				.HasIndex(x => x.CreatedAt);

			modelBuilder.Entity<LeaveRequest>()
				.HasOne(x => x.Employee)
				.WithMany()
				.HasForeignKey(x => x.EmployeeId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<LeaveRequest>()
				.HasOne(x => x.ApprovedByUser)
				.WithMany()
				.HasForeignKey(x => x.ApprovedBy)
				.OnDelete(DeleteBehavior.Restrict);

			// =========================================
			// EmployeeDeduction
			// =========================================

			modelBuilder.Entity<EmployeeDeduction>()
				.Property(x => x.Amount)
				.HasPrecision(18, 2);

			modelBuilder.Entity<EmployeeDeduction>()
				.HasIndex(x => new { x.EmployeeId, x.Date });

			modelBuilder.Entity<EmployeeDeduction>()
				.HasOne(x => x.Employee)
				.WithMany()
				.HasForeignKey(x => x.EmployeeId)
				.OnDelete(DeleteBehavior.Restrict);
		}
	}
}