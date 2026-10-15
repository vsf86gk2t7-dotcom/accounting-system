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

					// التزام المعاملة — بدونه تُلغى كل
					// التغييرات عند Dispose للـ Transaction
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
				var counter = await LoadWithLockAsync(dbKey);

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
				// تعارض إدراج متزامن على نفس المفتاح.
				// - معاملة محيطة: دعها تفشل فتتراجع كلها.
				// - معاملة خاصة: أعد المحاولة مرة واحدة.
				if (ambient)
				{
					throw;
				}

				// =========================================
				// [إصلاح] نفصل فقط كيانات SequenceCounter
				// اللي في حالة Added. استخدام Clear() بيفصل
				// كل كيانات الـ context، وده بيضيّع تعديلات
				// المستدعي بدون ما يعرف.
				// =========================================
				foreach (var entry in context.ChangeTracker
					.Entries<SequenceCounter>()
					.Where(e => e.State == EntityState.Added)
					.ToList())
				{
					entry.State = EntityState.Detached;
				}

				var retry = await LoadWithLockAsync(dbKey);

				if (retry == null)
				{
					// الصف اختفى (transaction تانية اترجعت)
					// — نعيد رمي الاستثناء الأصلي.
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
		// قراءة العداد بقفل صف.
		//
		// UPDLOCK: قفل تحديث — يمنع قراءتين متزامنتين
		//          لنفس الصف قبل التحديث.
		// HOLDLOCK: يوسّع القفل لنطاق المفتاح كله، فيمنع
		//           أي إدراج جديد لنفس المفتاح لحد ما
		//           المعاملة تخلص. ضروري لأول رقم في اليوم
		//           (لما الصف مش موجود أصلاً).
		// ROWLOCK:  قفل على مستوى الصف مش الصفحة.
		//
		// InMemory: قراءة عادية (مفيش قفل صف).
		// =========================================

		private async Task<SequenceCounter?> LoadWithLockAsync(
			string dbKey)
		{
			if (context.Database.IsRelational())
			{
				return await context.SequenceCounters
					.FromSqlInterpolated($@"
						SELECT * FROM [SequenceCounters]
						WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
						WHERE [Key] = {dbKey}")
					.FirstOrDefaultAsync();
			}

			return await context.SequenceCounters
				.FirstOrDefaultAsync(x => x.Key == dbKey);
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
			// [إصلاح] نحسب التاريخ مرة واحدة ونستخدمه
			// للعداد وللنص، حتى لا يختلفا لو عبرت الساعة
			// منتصف الليل بين السطرين.
			var effectiveDate = (date ?? DateTime.Today).Date;

			var value = await NextValueAsync(
				suffix,
				effectiveDate);

			var datePart = effectiveDate.ToString("yyyyMMdd");

			return
				$"{prefix}-{datePart}-{value.ToString().PadLeft(digits, '0')}";
		}
	}
}