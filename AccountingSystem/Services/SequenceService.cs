using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services
{
	// =========================================
	// توليد أرقام تتابعية آمنة ضد التزامن
	//
	// يعتمد على جدول SequenceCounters بفهرس
	// فريد على Key. كل عملية تسحب معرف الترتيب
	// من آخر قيمة مطابقة للفاصل الزمني الحالي.
	// =========================================

	public interface ISequenceService
	{
		Task<long> NextValueAsync(
			string key,
			DateTime? date = null);

		Task<string> NextFormattedAsync(
			string prefix,
			string suffix,
			DateTime? date = null,
			int digits = 6);
	}

	public sealed class SequenceService(
		ApplicationDbContext context) : ISequenceService
	{
		// =========================================
		// القيمة التالية المسلسلة بفاصل زمني
		// (اليوم افتراضيًا) — تتحمل التزامن
		// =========================================

		public async Task<long> NextValueAsync(
			string key,
			DateTime? date = null)
		{
			var effectiveDate = (date ?? DateTime.Today).Date;
			var dbKey = $"{key}:{effectiveDate:yyyyMMdd}";

			// إن كانت هناك معاملة محيطة (Confirm مثلًا) نعمل
			// داخلها دون بدء معاملة خاصة حتى يُلتزم الكل معًا.
			var ambient =
				context.Database.CurrentTransaction != null;

			if (ambient)
			{
				return await NextCoreAsync(dbKey, ambient);
			}

			// بدون معاملة محيطة نبدأ معاملة خاصة، ويجب أن تُلف
			// بالاستراتيجية (EnableRetryOnFailure لا يدعم معاملة
			// يدوية خارج CreateExecutionStrategy).
			var strategy =
				context.Database.CreateExecutionStrategy();

			long result = 0;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var transaction =
						await context.Database.BeginTransactionAsync();

					result =
						await NextCoreAsync(dbKey, ambient);

					// =========================================
					// [إصلاح] التزام المعاملة — بدونه تُلغى كل
					// التغييرات عند Dispose للـ Transaction
					// =========================================
					await transaction.CommitAsync();
				});

			return result;
		}

		private async Task<long> NextCoreAsync(
			string dbKey,
			bool ambient)
		{
			try
			{
				// =========================================
				// [إصلاح] قفل الصف (UPDLOCK + ROWLOCK)
				// لمنع قراءتين متزامنتين لنفس العداد قبل
				// التحديث، مما يمنع تكرار الأرقام.
				// =========================================
				var counter = await context.SequenceCounters
					.FromSqlInterpolated($@"
						SELECT * FROM [SequenceCounters]
						WITH (UPDLOCK, ROWLOCK)
						WHERE [Key] = {dbKey}")
					.FirstOrDefaultAsync();

				if (counter == null)
				{
					counter = new SequenceCounter
					{
						Key = dbKey,
						NextValue = 2,
						UpdatedAt = DateTime.UtcNow
					};

					context.SequenceCounters.Add(counter);

					await context.SaveChangesAsync();

					return 1;
				}

				var value = counter.NextValue;

				counter.NextValue = value + 1;
				counter.UpdatedAt = DateTime.UtcNow;

				await context.SaveChangesAsync();

				return value;
			}
			catch (DbUpdateException)
			{
				// تعارض إدراج متزامن على نفس المفتاح —
				// معاملة خاصة: أعد المحاولة مرة واحدة
				// معاملة محيطة: دعها تفشل فتتراجع كلها
				if (ambient)
				{
					throw;
				}

				// =========================================
				// [إصلاح] نفس قفل الصف في إعادة المحاولة
				// =========================================
				var retry = await context.SequenceCounters
					.FromSqlInterpolated($@"
						SELECT * FROM [SequenceCounters]
						WITH (UPDLOCK, ROWLOCK)
						WHERE [Key] = {dbKey}")
					.FirstOrDefaultAsync();

				if (retry == null)
				{
					throw;
				}

				var retryValue = retry.NextValue;

				retry.NextValue = retryValue + 1;
				retry.UpdatedAt = DateTime.UtcNow;

				context.SequenceCounters.Update(retry);

				await context.SaveChangesAsync();

				return retryValue;
			}
		}

		// =========================================
		// رقم مركّب مثل: JRN-20260923-0001
		// =========================================

		public async Task<string> NextFormattedAsync(
			string prefix,
			string suffix,
			DateTime? date = null,
			int digits = 6)
		{
			var value = await NextValueAsync(
				suffix,
				date);

			var datePart =
				(date ?? DateTime.Today)
					.ToString("yyyyMMdd");

			return
				$"{prefix}-{datePart}-{value.ToString().PadLeft(digits, '0')}";
		}
	}
}