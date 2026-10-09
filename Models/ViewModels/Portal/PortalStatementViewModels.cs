using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels.Portal
{
	// =============================================
	// سطر حركة في كشف حساب البوابة
	// =============================================

	public class PortalStatementRow
	{
		public DateTime Date { get; set; }

		public string TypeLabel { get; set; } = string.Empty;

		public string BadgeClass { get; set; } = "bg-secondary";

		public string Reference { get; set; } = string.Empty;

		public decimal Debit { get; set; }

		public decimal Credit { get; set; }

		public decimal Balance { get; set; }
	}

	// =============================================
	// كشف حساب البوابة (عميل / مورد)
	// =============================================

	public class PortalStatementViewModel
	{
		public string PartyName { get; set; } = string.Empty;

		public decimal OpeningBalance { get; set; }

		public decimal TotalInvoices { get; set; }

		public decimal TotalReturns { get; set; }

		public decimal TotalPaid { get; set; }

		public decimal CurrentBalance { get; set; }

		public List<PortalStatementRow> Rows { get; set; }
			= new List<PortalStatementRow>();
	}
}
