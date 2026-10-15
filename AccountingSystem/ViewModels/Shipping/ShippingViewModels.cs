namespace AccountingSystem.Models.ViewModels.Shipping
{
	public class ShippingBillIndexViewModel
	{
		public List<ShippingBill> ShippingBills { get; set; } = new();
		public string Search { get; set; } = "";
		public ShippingBillStatus? StatusFilter { get; set; }
	}



	public class ShippingBillDetailsViewModel
	{
		public ShippingBill ShippingBill { get; set; } = null!;
		public string InvoiceNumber { get; set; } = "";
		public string CustomerName { get; set; } = "";
		public string BranchName { get; set; } = "";
		public string StoreName { get; set; } = "";
		public decimal InvoiceTotal { get; set; }
		public int TotalItems { get; set; }
		public string DeliveryMethodLabel { get; set; } = "";
		public string StatusLabel { get; set; } = "";
	}
}