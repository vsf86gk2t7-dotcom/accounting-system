using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class ShippingBill
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(50)]
		public string BillNumber { get; set; } = string.Empty;

		public DateTime BillDate { get; set; } = DateTime.UtcNow;

		// =========================================
		// الفاتورة المرتبطة
		// =========================================

		[Required]
		public int SalesInvoiceId { get; set; }

		public SalesInvoice? SalesInvoice { get; set; }

		// =========================================
		// طريقة التوصيل
		// =========================================

		[Required]
		public DeliveryMethod DeliveryMethod { get; set; }
			= DeliveryMethod.InternalDriver;

		// =========================================
		// شركة الشحن الخارجية (لو ExternalCompany)
		// =========================================

		public int? ShippingCompanyId { get; set; }

		public ShippingCompany? ShippingCompany { get; set; }

		// =========================================
		// المندوب الداخلي (لو InternalDriver)
		// =========================================

		public int? DriverId { get; set; }

		public Employee? Driver { get; set; }

		// =========================================
		// حالة التوصيل
		// =========================================

		public ShippingBillStatus Status { get; set; }
			= ShippingBillStatus.Pending;

		public DateTime? ScheduledDate { get; set; }

		public DateTime? DeliveredDate { get; set; }

		// =========================================
		// الملاحظات
		// =========================================

		[MaxLength(1000)]
		public string? Notes { get; set; }

		[MaxLength(1000)]
		public string? DeliveryNotes { get; set; }

		// =========================================
		// التواريخ
		// =========================================

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public int? CreatedByUserId { get; set; }

		public User? CreatedByUser { get; set; }
	}
}
