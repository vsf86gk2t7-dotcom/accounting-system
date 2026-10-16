using AccountingSystem.Data;

namespace AccountingSystem.Services
{
	public interface IDocumentNumberService
	{
		Task<string> GenerateNumberAsync(string prefix, string tableName, string datePart);
	}

	/// <summary>
	/// ✅ Wrapper حول SequenceService — يضمن أرقاماً تسلسلية
	/// آمنة ضد التزامن (UPDLOCK + HOLDLOCK + ROWLOCK)
	/// بدل القراءة من جدول الفواتير مباشرة (MAX + 1).
	/// </summary>
	public class DocumentNumberService : IDocumentNumberService
	{
		private readonly ISequenceService _sequence;

		public DocumentNumberService(ISequenceService sequence)
		{
			_sequence = sequence;
		}

		public async Task<string> GenerateNumberAsync(
			string prefix,
			string tableName,
			string datePart)
		{
			if (!DateTime.TryParseExact(
				datePart,
				"yyyyMMdd",
				null,
				System.Globalization.DateTimeStyles.None,
				out var date))
			{
				throw new ArgumentException(
					$"Invalid datePart: '{datePart}'. Expected yyyyMMdd.",
					nameof(datePart));
			}

			// نستخدم اسم الجدول كمفتاح عدّاد مستقل
			return await _sequence.NextFormattedAsync(
				prefix,
				tableName,
				date,
				digits: 4);
		}
	}
}