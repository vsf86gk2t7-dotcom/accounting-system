using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace AccountingSystem.Data
{
	/// <summary>
	/// يلتقط كل عملية إنشاء / تعديل / حذف على جداول الأعمال
	/// ويكتبها تلقائيًا في جدول سجلات التدقيق.
	/// </summary>
	public class AuditLogInterceptor : SaveChangesInterceptor
	{
		// =========================================
		// الخصائص الحساسة التي لا يجب تسجيلها
		// =========================================

		private static readonly HashSet<string> SensitiveProperties =
			new(StringComparer.OrdinalIgnoreCase)
			{
				"PasswordHash",
				"ActivationCodeHash",
				"ActivationCodeExpiresAt",
				"SecurityStamp",
				"ConcurrencyStamp",
				"ResetTokenHash",
				"PasswordResetToken",
				"RowVersion"
			};

		private readonly IHttpContextAccessor _httpContextAccessor;

		public AuditLogInterceptor(
			IHttpContextAccessor httpContextAccessor)
		{
			_httpContextAccessor = httpContextAccessor;
		}

		public override InterceptionResult<int> SavingChanges(
			DbContextEventData eventData,
			InterceptionResult<int> result)
		{
			// ✅ null check
			if (eventData.Context != null)
			{
				WriteAuditLogs(eventData.Context);
			}

			return base.SavingChanges(eventData, result);
		}

		public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
			DbContextEventData eventData,
			InterceptionResult<int> result,
			CancellationToken cancellationToken = default)
		{
			// ✅ null check
			if (eventData.Context != null)
			{
				WriteAuditLogs(eventData.Context);
			}

			return base.SavingChangesAsync(
				eventData,
				result,
				cancellationToken);
		}

		private void WriteAuditLogs(DbContext context)
		{
			var entries = context.ChangeTracker
				.Entries<object>()
				.ToList();

			if (entries.Count == 0)
			{
				return;
			}

			var userId = GetCurrentUserId();
			var userName = GetCurrentUserName();
			var ipAddress = GetIpAddress();

			foreach (var entry in entries)
			{
				if (entry.State == EntityState.Detached ||
					entry.State == EntityState.Unchanged)
				{
					continue;
				}

				var entity = entry.Entity;

				// لا نسجل على سجلات التدقيق نفسها
				if (entity is AuditLog)
				{
					continue;
				}

				var action = entry.State switch
				{
					EntityState.Added => "create",
					EntityState.Modified => "update",
					EntityState.Deleted => "delete",
					_ => "change"
				};

				var entityId = GetEntityId(entry)?.ToString()
					?? string.Empty;

				var propertyDetails = CollectDetails(entry, action);

				context.Add(
					new AuditLog
					{
						UserId = userId,
						UserName = userName,
						IpAddress = ipAddress,
						Action = action,
						EntityName = entity.GetType().Name,
						EntityId = entityId,
						Summary = GetSummary(entity),
						Details = propertyDetails == null
							? null
							: JsonSerializer.Serialize(propertyDetails)
					});
			}
		}

		// =========================================
		// ✅ EntityId يدعم Composite Keys
		// =========================================

		private string? GetEntityId(EntityEntry entry)
		{
			var key = entry.Metadata.FindPrimaryKey();

			if (key == null || key.Properties.Count == 0)
			{
				return null;
			}

			// لو Primary Key بسيط → رجّع القيمة مباشرة
			if (key.Properties.Count == 1)
			{
				var value = entry
					.Property(key.Properties[0].Name)
					.CurrentValue;

				return value?.ToString();
			}

			// لو Composite Key → نجمع كل القيم
			var parts = new List<string>();

			foreach (var keyProp in key.Properties)
			{
				var value = entry
					.Property(keyProp.Name)
					.CurrentValue;

				parts.Add($"{keyProp.Name}={value}");
			}

			return string.Join(", ", parts);
		}

		private string? GetSummary(object entity)
		{
			var name = entity
				.GetType()
				.GetProperty("Name")
				?.GetValue(entity)
				?.ToString();

			if (!string.IsNullOrWhiteSpace(name))
			{
				return name;
			}

			var number = entity
				.GetType()
				.GetProperty("InvoiceNumber")
				?.GetValue(entity)
				?.ToString();

			if (!string.IsNullOrWhiteSpace(number))
			{
				return number;
			}

			var code = entity
				.GetType()
				.GetProperty("Code")
				?.GetValue(entity)
				?.ToString();

			if (!string.IsNullOrWhiteSpace(code))
			{
				return code;
			}

			var phone = entity
				.GetType()
				.GetProperty("Phone")
				?.GetValue(entity)
				?.ToString();

			if (!string.IsNullOrWhiteSpace(phone))
			{
				return phone;
			}

			return null;
		}

		private Dictionary<string, string>? CollectDetails(
			EntityEntry entry,
			string action)
		{
			var details = new Dictionary<string, string>();

			if (action == "delete")
			{
				foreach (var property in entry.Properties)
				{
					var name = property.Metadata.Name;

					// ✅ تخطي الحقول الحساسة
					if (SensitiveProperties.Contains(name))
					{
						continue;
					}

					var value = property.OriginalValue?.ToString();

					if (!string.IsNullOrWhiteSpace(value))
					{
						details[name] = value;
					}
				}

				return details.Count == 0 ? null : details;
			}

			if (action == "create")
			{
				foreach (var property in entry.Properties)
				{
					var name = property.Metadata.Name;

					// ✅ تخطي الحقول الحساسة
					if (SensitiveProperties.Contains(name))
					{
						continue;
					}

					var value = property.CurrentValue?.ToString();

					if (!string.IsNullOrWhiteSpace(value) &&
						name != "Id")
					{
						details[name] = value;
					}
				}

				return details.Count == 0 ? null : details;
			}

			// update — نسجل الحقول المتغيرة فقط
			foreach (var property in entry.Properties)
			{
				if (!property.IsModified)
				{
					continue;
				}

				var name = property.Metadata.Name;

				// ✅ تخطي الحقول الحساسة
				if (SensitiveProperties.Contains(name))
				{
					continue;
				}

				var oldValue = property.OriginalValue?.ToString() ?? "null";
				var newValue = property.CurrentValue?.ToString() ?? "null";

				// لو القيمة نفسها ما اتغيرتش فعلاً → تخطي
				if (oldValue == newValue)
				{
					continue;
				}

				details[$"{name}.old"] = oldValue;
				details[$"{name}.new"] = newValue;
			}

			return details.Count == 0 ? null : details;
		}

		private int? GetCurrentUserId()
		{
			var user = _httpContextAccessor.HttpContext?.User;

			var value = user?.FindFirstValue(ClaimTypes.NameIdentifier);

			if (int.TryParse(value, out var id))
			{
				return id;
			}

			return null;
		}

		private string? GetCurrentUserName()
		{
			return _httpContextAccessor
				.HttpContext?
				.User?
				.Identity?
				.Name;
		}

		private string? GetIpAddress()
		{
			return _httpContextAccessor
				.HttpContext?
				.Connection?
				.RemoteIpAddress?
				.ToString();
		}
	}
}