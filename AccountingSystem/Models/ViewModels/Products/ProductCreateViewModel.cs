using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels.Products
{
	public class ProductCreateViewModel
	{
		// =========================================
		// بيانات المنتج
		// =========================================

		[Required(
			ErrorMessage = "اسم المنتج مطلوب.")]
		[MaxLength(
			200,
			ErrorMessage = "اسم المنتج لا يمكن أن يتجاوز 200 حرف.")]
		[Display(Name = "اسم المنتج")]
		public string Name { get; set; } = string.Empty;

		[MaxLength(
			100,
			ErrorMessage = "الباركود لا يمكن أن يتجاوز 100 حرف.")]
		[Display(Name = "الباركود")]
		public string? Barcode { get; set; }

		[MaxLength(
			100,
			ErrorMessage = "كود المنتج لا يمكن أن يتجاوز 100 حرف.")]
		[Display(Name = "كود المنتج")]
		public string? Code { get; set; }

		[MaxLength(
			1000,
			ErrorMessage = "الوصف لا يمكن أن يتجاوز 1000 حرف.")]
		[Display(Name = "الوصف")]
		public string? Description { get; set; }

		// =========================================
		// الحد الأدنى للمخزون
		// =========================================

		[Range(
			0,
			double.MaxValue,
			ErrorMessage = "الحد الأدنى يجب ألا يكون سالبًا.")]
		[Display(Name = "الحد الأدنى للمخزون")]
		public decimal MinQuantity { get; set; }

		// =========================================
		// الوحدة الأساسية
		// =========================================

		[Required(
			ErrorMessage = "يجب اختيار الوحدة الأساسية.")]
		[Display(Name = "الوحدة الأساسية")]
		public int BaseUnitId { get; set; }

		// =========================================
		// التصنيفات
		// المنتج يمكن أن ينتمي لأكثر من تصنيف
		// =========================================

		[Display(Name = "التصنيفات")]
		public List<int> CategoryIds { get; set; }
			= new List<int>();

		public List<CategoryOptionViewModel> AvailableCategories { get; set; }
			= new List<CategoryOptionViewModel>();

		// =========================================
		// وحدات المنتج
		// =========================================

		public List<ProductUnitCreateViewModel> Units { get; set; }
			= new List<ProductUnitCreateViewModel>();

		// =========================================
		// الوحدات المتاحة للاختيار
		// =========================================

		public List<UnitOptionViewModel> AvailableUnits { get; set; }
			= new List<UnitOptionViewModel>();
	}

	// =============================================
	// وحدة مرتبطة بالمنتج
	// =============================================

	public class ProductUnitCreateViewModel
	{
		public int UnitId { get; set; }

		[Range(
			0.000001,
			double.MaxValue,
			ErrorMessage = "معامل التحويل يجب أن يكون أكبر من صفر.")]
		[Display(Name = "معامل التحويل")]
		public decimal ConversionFactor { get; set; } = 1;

		[Range(
			0,
			double.MaxValue,
			ErrorMessage = "سعر البيع يجب ألا يكون سالبًا.")]
		[Display(Name = "سعر البيع")]
		public decimal SalePrice { get; set; }

		[Display(Name = "نشطة")]
		public bool IsActive { get; set; } = true;
	}

	// =============================================
	// اختيار الوحدة
	// =============================================

	public class UnitOptionViewModel
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public string? ShortName { get; set; }
	}

	// =============================================
	// اختيار التصنيف
	// =============================================

	public class CategoryOptionViewModel
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;
	}
}
