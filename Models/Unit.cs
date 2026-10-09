using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	/// <summary>
	/// نوع الوحدة:
	/// - Standard: وحدة ثابتة معروفة القيمة دائماً (قطعة = 1، دستة = 12).
	/// - Variable: وحدة متغيرة تعتمد على المورد/الصنف (كرتونة، طقم، ...).
	/// </summary>
	public enum UnitKind
	{
		Standard = 0,
		Variable = 1
	}

	public class Unit
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(100)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(20)]
		public string? ShortName { get; set; }

		// ═══════════════════════════════════════════════════════
		// ✅ جديد: نوع الوحدة
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// نوع الوحدة. الافتراضي: Variable (لأن معظم الوحدات المضافة سريعاً تكون متغيرة).
		/// </summary>
		public UnitKind Kind { get; set; } = UnitKind.Variable;

		// ═══════════════════════════════════════════════════════
		// ✅ جديد: عدد القطع داخل الوحدة (للوحدات الثابتة فقط)
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// عدد القطع داخل الوحدة — يُستخدم فقط عند Kind = Standard.
		/// أمثلة: قطعة = 1، دستة = 12.
		/// يبقى null للوحدات المتغيرة (Variable).
		/// </summary>
		[Range(0.0001, 100000,
			ErrorMessage = "عدد القطع يجب أن يكون أكبر من صفر.")]
		public decimal? PiecesPerUnit { get; set; }

		// ═══════════════════════════════════════════════════════

		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}