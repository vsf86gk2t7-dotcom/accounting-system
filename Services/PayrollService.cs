using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services
{
	public interface IPayrollService
	{
		Task<PayrollRun> CreateDraftAsync(
			string periodName,
			DateTime periodStart,
			DateTime periodEnd,
			int? userId,
			CancellationToken ct = default);

		Task RecalculateAsync(int runId, CancellationToken ct = default);

		Task<bool> DeleteDraftAsync(int runId);
	}

	        public class PayrollService(
                ApplicationDbContext context) : IPayrollService
        
	{
		// ساعة بداية الدوام الرسمي (تعدّل حسب سياسة الشركة)
		private const int WORK_START_HOUR = 9;
		private const int WORK_START_MINUTE = 0;

		// عدد أيام الشهر المعياري (لحساب خصم الغياب)
		private const decimal MONTH_DAYS = 30m;

		// عدد دقائق يوم العمل (8 ساعات)
		private const decimal WORK_MINUTES_PER_DAY = 480m;

		// =========================================
		// إنشاء شهر رواتب جديد
		// =========================================

		public async Task<PayrollRun> CreateDraftAsync(
			string periodName,
			DateTime periodStart,
			DateTime periodEnd,
			int? userId,
			CancellationToken ct = default)
		{
			periodName = periodName?.Trim() ?? string.Empty;

			if (string.IsNullOrEmpty(periodName))
				throw new InvalidOperationException("اسم الشهر مطلوب.");

			var exists = await context.PayrollRuns
				.AnyAsync(x => x.PeriodName == periodName, ct);

			if (exists)
				throw new InvalidOperationException(
					$"يوجد شهر رواتب باسم '{periodName}' بالفعل.");

			var run = new PayrollRun
			{
				PeriodName = periodName,
				PeriodStart = periodStart.Date,
				PeriodEnd = periodEnd.Date,
				Status = PayrollRunStatus.Draft,
				CreatedByUserId = userId,
				CreatedAt = DateTime.UtcNow
			};

			context.PayrollRuns.Add(run);
			await context.SaveChangesAsync(ct);

			await BuildItemsAsync(run, ct);

			return run;
		}

		// =========================================
		// إعادة حساب
		// =========================================

		public async Task RecalculateAsync(
			int runId,
			CancellationToken ct = default)
		{
			var run = await context.PayrollRuns
				.Include(x => x.Items)
				.FirstOrDefaultAsync(x => x.Id == runId, ct)
				?? throw new InvalidOperationException("شهر الرواتب غير موجود.");

			if (run.Status != PayrollRunStatus.Draft)
				throw new InvalidOperationException(
					"لا يمكن إعادة الحساب إلا للمسودة.");

			context.PayrollItems.RemoveRange(run.Items);
			run.Items.Clear();
			await context.SaveChangesAsync(ct);

			await BuildItemsAsync(run, ct);
		}

		// =========================================
		// حذف مسودة
		// =========================================

		public async Task<bool> DeleteDraftAsync(int runId)
		{
			var run = await context.PayrollRuns
				.FirstOrDefaultAsync(x => x.Id == runId);

			if (run == null) return false;

			if (run.Status != PayrollRunStatus.Draft)
				throw new InvalidOperationException(
					"لا يمكن حذف إلا المسودة.");

			context.PayrollRuns.Remove(run);
			await context.SaveChangesAsync();
			return true;
		}

		// =========================================
		// بناء البنود
		// =========================================

		private async Task BuildItemsAsync(
			PayrollRun run,
			CancellationToken ct)
		{
			var employees = await context.Employees
				.AsNoTracking()
				.Where(x => x.IsActive && x.Salary > 0)
				.OrderBy(x => x.Name)
				.ToListAsync(ct);

			var start = run.PeriodStart.Date;
			var end = run.PeriodEnd.Date;

			// ===== 1) الحضور =====
			var attendance = await context.Attendances
				.AsNoTracking()
				.Where(x => x.Date >= start && x.Date <= end)
				.ToListAsync(ct);

			// ===== 2) الخصومات اليدوية =====
			var manualDeductions = await context.EmployeeDeductions
				.AsNoTracking()
				.Where(x => x.Date >= start && x.Date <= end)
				.ToListAsync(ct);

			// ===== 3) السلف غير المخصومة =====
			var advances = await context.EmployeeAdvances
				.AsNoTracking()
				.Where(x => x.Status == AdvanceStatus.Paid)
				.ToListAsync(ct);

			// ===== 4) المناديب =====
			var reps = await context.SalesRepProfiles
				.AsNoTracking()
				.Include(p => p.Employee)
				.Where(p => p.IsActive && p.Employee != null)
				.ToListAsync(ct);

			var repIds = reps.Select(p => p.Id).ToList();

			var repSales = await context.SalesInvoices
				.AsNoTracking()
				.Where(x => x.Status == SalesInvoiceStatus.Confirmed
					&& x.SalesRepId != null
					&& repIds.Contains(x.SalesRepId.Value)
					&& x.InvoiceDate >= start
					&& x.InvoiceDate <= end)
				.GroupBy(x => x.SalesRepId!.Value)
				.Select(g => new
				{
					RepId = g.Key,
					Total = g.Sum(x => x.TotalAmount)
				})
				.ToDictionaryAsync(x => x.RepId, x => x.Total, ct);

			var repCashAccountIds = reps
				.Where(p => p.CashAccountId.HasValue)
				.Select(p => p.CashAccountId!.Value)
				.ToList();

			var repCollections = repCashAccountIds.Any()
				? await context.TreasuryTransactions
					.AsNoTracking()
					.Where(x => x.Type == TreasuryTransactionType.Receive
						&& x.CreatedAt >= start
						&& x.CreatedAt <= end
						&& repCashAccountIds.Contains(x.CashAccountId))
					.GroupBy(x => x.CashAccountId)
					.Select(g => new
					{
						CashAccountId = g.Key,
						Total = g.Sum(x => x.Amount)
					})
					.ToDictionaryAsync(x => x.CashAccountId, x => x.Total, ct)
				: new Dictionary<int, decimal>();

			// عدد الفواتير لكل مندوب (لعمولة FixedPerInvoice)
			var repInvoiceCounts = await context.SalesInvoices
				.AsNoTracking()
				.Where(x => x.Status == SalesInvoiceStatus.Confirmed
					&& x.SalesRepId != null
					&& repIds.Contains(x.SalesRepId.Value)
					&& x.InvoiceDate >= start
					&& x.InvoiceDate <= end)
				.GroupBy(x => x.SalesRepId!.Value)
				.Select(g => new { RepId = g.Key, Count = g.Count() })
				.ToDictionaryAsync(x => x.RepId, x => x.Count, ct);

			// ===== بناء البنود =====
			decimal totalGross = 0;
			decimal totalAbsence = 0;
			decimal totalLate = 0;
			decimal totalManual = 0;
			decimal totalAdvances = 0;
			decimal totalCommissions = 0;
			decimal totalNet = 0;

			foreach (var emp in employees)
			{
				var item = new PayrollItem
				{
					PayrollRunId = run.Id,
					EmployeeId = emp.Id,
					BaseSalary = emp.Salary
				};

				// ===== الحضور =====
				var empAtt = attendance
					.Where(a => a.EmployeeId == emp.Id)
					.ToList();

				// خصم الغياب
				item.AbsenceDays = empAtt.Count(a =>
					a.Status == AttendanceStatus.Absent);

				if (item.AbsenceDays > 0)
				{
					item.AbsenceDeduction = Math.Round(
						emp.Salary / MONTH_DAYS * item.AbsenceDays, 2);
				}

				// ✅ خصم التأخير — محسوب من CheckIn
				var totalLateMinutes = 0;

				foreach (var att in empAtt.Where(a => a.CheckIn.HasValue))
				{
					var workStart = att.Date.Date.AddHours(WORK_START_HOUR)
						.AddMinutes(WORK_START_MINUTE);

					if (att.CheckIn!.Value > workStart)
					{
						var minutes = (int)(att.CheckIn.Value - workStart).TotalMinutes;
						totalLateMinutes += minutes;
					}
				}

				item.LateMinutes = totalLateMinutes;

				if (item.LateMinutes > 0)
				{
					var minuteRate = emp.Salary / MONTH_DAYS / WORK_MINUTES_PER_DAY;
					item.LateDeduction = Math.Round(
						minuteRate * item.LateMinutes, 2);
				}

				// ===== الخصومات اليدوية =====
				item.ManualDeductions = manualDeductions
					.Where(d => d.EmployeeId == emp.Id)
					.Sum(d => d.Amount);

				// ===== السلف =====
				item.AdvancesDeduction = advances
					.Where(a => a.EmployeeId == emp.Id)
					.Sum(a => a.Amount);

				// ===== العمولات =====
				var rep = reps.FirstOrDefault(p => p.EmployeeId == emp.Id);
				if (rep != null)
				{
					decimal commissionAmount = 0;

					switch (rep.CommissionType)
					{
						case CommissionType.PercentOfSales:
							var sales = repSales.TryGetValue(rep.Id, out var s) ? s : 0;
							commissionAmount = Math.Round(
								sales * rep.CommissionRate / 100m, 2);
							break;

						case CommissionType.PercentOfCollections:
							var collections = 0m;
							if (rep.CashAccountId.HasValue &&
								repCollections.TryGetValue(
									rep.CashAccountId.Value, out var c))
								collections = c;
							commissionAmount = Math.Round(
								collections * rep.CommissionRate / 100m, 2);
							break;

						case CommissionType.FixedPerInvoice:
							var count = repInvoiceCounts.TryGetValue(
								rep.Id, out var cnt) ? cnt : 0;
							commissionAmount = Math.Round(
								count * rep.CommissionRate, 2);
							break;
					}

					item.Commissions = commissionAmount;
				}

				// ===== الصافي =====
				item.NetSalary =
					item.BaseSalary
					+ item.Commissions
					+ item.Bonuses
					- item.AbsenceDeduction
					- item.LateDeduction
					- item.ManualDeductions
					- item.AdvancesDeduction;

				if (item.NetSalary < 0) item.NetSalary = 0;

				run.Items.Add(item);

				totalGross += item.BaseSalary;
				totalAbsence += item.AbsenceDeduction;
				totalLate += item.LateDeduction;
				totalManual += item.ManualDeductions;
				totalAdvances += item.AdvancesDeduction;
				totalCommissions += item.Commissions;
				totalNet += item.NetSalary;
			}

			run.TotalGross = totalGross;
			run.TotalAbsenceDeduction = totalAbsence;
			run.TotalLateDeduction = totalLate;
			run.TotalManualDeductions = totalManual;
			run.TotalAdvances = totalAdvances;
			run.TotalCommissions = totalCommissions;
			run.TotalNet = totalNet;

			await context.SaveChangesAsync(ct);
		}
	}
}