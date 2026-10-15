namespace AccountingSystem.Models.ViewModels.Shipping
{


	public class ShippingBillCreateViewModel
	{
		public int SalesInvoiceId { get; set; }   // خليها int مش int?
		public string SalesInvoiceNumber { get; set; } = "";
		public string CustomerName { get; set; } = "";
		public decimal InvoiceTotal { get; set; }

		public DeliveryMethod DeliveryMethod { get; set; }
		public int? ShippingCompanyId { get; set; }
		public int? DriverId { get; set; }
		public DateTime? ScheduledDate { get; set; }
		public string? Notes { get; set; }
		public string? DeliveryNotes { get; set; }

		public List<ShippingCompany> ShippingCompanies { get; set; } = new();
		public List<Employee> Drivers { get; set; } = new();
	}


}