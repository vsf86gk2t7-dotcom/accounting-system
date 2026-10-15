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
		// ÃØÑÇÝ ÇáÊÚÇãá (ÊõãÍì ßãíÊß)
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

		// =========================================
		// ÇáÍÓÇÈÇÊ (ÔÌÑÉ ÇáÍÓÇÈÇÊ)
		// — ÊÔãá ÍÓÇÈÇÊ ÇáÚãáÇÁ æÇáãæÑÏíä æÇáÈäæß
		// =========================================

		public int ChartAccounts { get; set; }

		// =========================================
		// ÇáÃÕäÇÝ æÇáãÎÒæä
		// =========================================

		public int Products { get; set; }
		public int Categories { get; set; }
		public int Units { get; set; }
		public int StockLots { get; set; }

		// =========================================
		// ÇáÝæÇÊíÑ æÇáÍÑßÇÊ
		// =========================================

		public int SalesInvoices { get; set; }
		public int PurchaseInvoices { get; set; }
		public int TreasuryTransactions { get; set; }
		public int JournalEntries { get; set; }
		public int StockTransactions { get; set; }

		// =========================================
		// ÇáäÙÇã
		// =========================================

		public int Companies { get; set; }
		public int AuditLogs { get; set; }
		public int AppNotifications { get; set; }

		public int TotalRows
		{
			get
			{
				return
					Customers + Suppliers + Employees + Users +
					Branches + Stores + CashAccounts +
					ChartAccounts +
					Products + Categories + Units + StockLots +
					SalesInvoices + PurchaseInvoices +
					TreasuryTransactions + JournalEntries +
					StockTransactions +
					Companies + AuditLogs + AppNotifications;
			}
		}
	public int TotalCount => TotalRows;
	}
}
