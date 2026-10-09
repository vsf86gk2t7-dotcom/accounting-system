using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class SalesReturnItemLot
	{
		public int Id { get; set; }

		[Required]
		public int SalesReturnInvoiceItemId { get; set; }

		public SalesReturnInvoiceItem? SalesReturnInvoiceItem { get; set; }

		[Required]
		public int StockLotId { get; set; }

		public StockLot? StockLot { get; set; }

		public decimal QuantityBaseUnit { get; set; }

		public decimal UnitCost { get; set; }

		public decimal TotalCost { get; set; }
	}
}
