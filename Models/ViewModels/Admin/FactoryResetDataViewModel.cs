namespace AccountingSystem.Models.ViewModels.Admin
{
	// =========================================
	// ÈíÇäÇÊ «ÅÚÇÏÉ ÖÈØ ÇáãÕäÚ» — ÞÑÇÁÉ ÝÞØ
	// ÊÚÑÖ ãÇ ÓíÊã ÍÐÝå ÍÊì íÚÑÝ ÇáÃÏãä ÇáÍÌã
	// ÞÈá ÊäÝíÐ ÇáÖÈØ
	// =========================================

	public class FactoryResetDataViewModel
	{
		// =========================================
		// ÃØÑÇÝ ÇáÊÚÇãá
		// =========================================

		public int Customers { get; set; }
		public int Suppliers { get; set; }
		public int Employees { get; set; }
		public int Users { get; set; }

		// =========================================
		// Çáåíßá ÇáÊäÙíãí
		// =========================================

		public int Branches { get; set; }
		public int Stores { get; set; }
		public int CashAccounts { get; set; }
		public int Departments { get; set; }
		public int Positions { get; set; }

		// =========================================
		// ÇáÍÓÇÈÇÊ (ÔÌÑÉ ÇáÍÓÇÈÇÊ)
		// — ÊÔãá ÍÓÇÈÇÊ ÇáÚãáÇÁ æÇáãæÑÏíä æÇáÈäæß
		// =========================================

		public int ChartAccounts { get; set; }
		public int JournalEntries { get; set; }
		public int JournalEntryLines { get; set; }

		// =========================================
		// ÇáÃÕäÇÝ æÇáãÎÒæä
		// =========================================

		public int Products { get; set; }
		public int Categories { get; set; }
		public int Units { get; set; }
		public int StockLots { get; set; }
		public int StockTransactions { get; set; }
		public int StockTransactionItems { get; set; }
		public int SupplierProductPackings { get; set; }

		// =========================================
		// ÇáÝæÇÊíÑ æÇáÍÑßÇÊ
		// =========================================

		public int SalesInvoices { get; set; }
		public int SalesInvoiceItems { get; set; }
		public int SalesReturnInvoices { get; set; }
		public int SalesReturnInvoiceItems { get; set; }
		public int PurchaseInvoices { get; set; }
		public int PurchaseInvoiceItems { get; set; }
		public int PurchaseReturnInvoices { get; set; }
		public int PurchaseReturnInvoiceItems { get; set; }
		public int TreasuryTransactions { get; set; }

		// =========================================
		// ÇáãäÇÏíÈ æÇáÔÍä
		// =========================================

		public int SalesRepProfiles { get; set; }
		public int ShippingBills { get; set; }
		public int ShippingCompanies { get; set; }

		// =========================================
		// ÇáãæÇÑÏ ÇáÈÔÑíÉ
		// =========================================

		public int Attendances { get; set; }
		public int LeaveRequests { get; set; }
		public int EmployeeDeductions { get; set; }
		public int EmployeeAdvances { get; set; }
		public int PayrollRuns { get; set; }
		public int PayrollItems { get; set; }

		// =========================================
		// ÇáäÙÇã
		// =========================================

		public int Companies { get; set; }
		public int AuditLogs { get; set; }
		public int AppNotifications { get; set; }

		// =========================================
		// ÇáãÌãæÚ Çáßáí
		// =========================================

		public int TotalRows
		{
			get
			{
				return
					// ÃØÑÇÝ ÇáÊÚÇãá
					Customers + Suppliers + Employees + Users +
					// Çáåíßá ÇáÊäÙíãí
					Branches + Stores + CashAccounts +
					Departments + Positions +
					// ÇáÍÓÇÈÇÊ
					ChartAccounts + JournalEntries + JournalEntryLines +
					// ÇáÃÕäÇÝ æÇáãÎÒæä
					Products + Categories + Units + StockLots +
					StockTransactions + StockTransactionItems +
					SupplierProductPackings +
					// ÇáÝæÇÊíÑ æÇáÍÑßÇÊ
					SalesInvoices + SalesInvoiceItems +
					SalesReturnInvoices + SalesReturnInvoiceItems +
					PurchaseInvoices + PurchaseInvoiceItems +
					PurchaseReturnInvoices + PurchaseReturnInvoiceItems +
					TreasuryTransactions +
					// ÇáãäÇÏíÈ æÇáÔÍä
					SalesRepProfiles + ShippingBills + ShippingCompanies +
					// ÇáãæÇÑÏ ÇáÈÔÑíÉ
					Attendances + LeaveRequests + EmployeeDeductions +
					EmployeeAdvances + PayrollRuns + PayrollItems +
					// ÇáäÙÇã
					Companies + AuditLogs + AppNotifications;
			}
		}

		public int TotalCount => TotalRows;
	}
}