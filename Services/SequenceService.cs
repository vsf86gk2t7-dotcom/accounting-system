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
        //
        // ملاحظة مهمة:
        // - لو فيه معاملة محيطة (Ambient Transaction)، بنعمل
        //   جواها من غير ما نبدأ معاملة جديدة — عشان الكل
        //   يتزمّن مع بعضه أو يتراجع مع بعضه.
        // - لو مفيش، بنبدأ معاملة خاصة ملفوفة بالـ
        //   ExecutionStrategy (عشان نتوافق مع EnableRetryOnFailure).
        // - بنستخدم UPDLOCK, ROWLOCK على الصف عشان نمنع
        //   قراءتين متزامنتين لنفس العداد قبل التحديث.
        // =========================================

        public interface ISequenceService
        {
                Task<long> NextValueAsync(
                        string key,
                        DateTime? date = null,
                        CancellationToken ct = default);

                Task<string> NextFormattedAsync(
                        string prefix,
                        string suffix,
                        DateTime? date = null,
                        int digits = 6,
                        CancellationToken ct = default);
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
                        DateTime? date = null,
                        CancellationToken ct = default)
                {
                        var effectiveDate = (date ?? DateTime.Today).Date;
                        var dbKey = $"{key}:{effectiveDate:yyyyMMdd}";

                        // إن كانت هناك معاملة محيطة (Confirm مثلًا) نعمل
                        // داخلها دون بدء معاملة خاصة حتى يُلتزم الكل معًا.
                        var ambient =
                                context.Database.CurrentTransaction != null;

                        if (ambient)
                        {
                                return await NextCoreAsync(dbKey, ambient, ct);
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
                                                await context.Database
                                                        .BeginTransactionAsync(ct);

                                        result =
                                                await NextCoreAsync(
                                                        dbKey,
                                                        ambient,
                                                        ct);

                                        // التزام المعاملة — بدونه تُلغى كل
                                        // التغييرات عند Dispose للـ Transaction.
                                        await transaction.CommitAsync(ct);
                                });

                        return result;
                }

                private async Task<long> NextCoreAsync(
                        string dbKey,
                        bool ambient,
                        CancellationToken ct)
                {
                        try
                        {
                                var counter =
                                        await LoadWithLockAsync(dbKey, ct);

                                if (counter == null)
                                {
                                        counter = new SequenceCounter
                                        {
                                                Key = dbKey,
                                                NextValue = 2,
                                                UpdatedAt = DateTime.UtcNow
                                        };

                                        context.SequenceCounters.Add(counter);

                                        await context.SaveChangesAsync(ct);

                                        return 1;
                                }

                                var value = counter.NextValue;

                                counter.NextValue = value + 1;
                                counter.UpdatedAt = DateTime.UtcNow;

                                await context.SaveChangesAsync(ct);

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

                                // ✅ تنظيف الـ ChangeTracker — بدونه ممكن
                                //    الاستعلام الجاي يرجع الكيان الفاشل
                                //    من الكاش المحلي بدل القراءة من DB.
                                context.ChangeTracker.Clear();

                                var retry =
                                        await LoadWithLockAsync(dbKey, ct);

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

                                await context.SaveChangesAsync(ct);

                                return retryValue;
                        }
                }

                // =========================================
                // قراءة العداد بقفل صف (UPDLOCK + ROWLOCK)
                // لمنع قراءتين متزامنتين لنفس العداد قبل
                // التحديث، مما يمنع تكرار الأرقام.
                // =========================================

        private async Task<SequenceCounter?> LoadWithLockAsync(
        string dbKey,
        CancellationToken ct)
{
        // =========================================
        // SQL Server: قفل صف لمنع التزامن
        // InMemory: قراءة عادية (مفيش قفل صف)
        // =========================================

        if (context.Database.IsRelational())
        {
                return await context.SequenceCounters
                        .FromSqlInterpolated($@"
                                SELECT * FROM [SequenceCounters]
                                WITH (UPDLOCK, ROWLOCK)
                                WHERE [Key] = {dbKey}")
                        .FirstOrDefaultAsync(ct);
        }

        return await context.SequenceCounters
                .FirstOrDefaultAsync(x => x.Key == dbKey, ct);
}

                // =========================================
                // رقم مركّب مثل: JRN-20260923-0001
                // =========================================

                public async Task<string> NextFormattedAsync(
                        string prefix,
                        string suffix,
                        DateTime? date = null,
                        int digits = 6,
                        CancellationToken ct = default)
                {
                        var value = await NextValueAsync(
                                suffix,
                                date,
                                ct);

                        var datePart =
                                (date ?? DateTime.Today)
                                        .ToString("yyyyMMdd");

                        return
                                $"{prefix}-{datePart}-{value.ToString().PadLeft(digits, '0')}";
                }
        }
}