using Microsoft.Extensions.Caching.Memory;

namespace AccountingSystem.Services;

/// <summary>
/// كاش مؤقت لقيم SecurityStamp لتقليل الاستعلامات على قاعدة البيانات
/// أثناء التحقق من صحة JWT على كل Request.
/// </summary>
public interface ISecurityStampCache
{
	Task<string?> GetOrLoadAsync(
		int userId,
		Func<Task<(string? Stamp, bool IsActive)>> loader,
		CancellationToken cancellationToken = default);

	void Invalidate(int userId);

	void InvalidateAll();
}

public class SecurityStampCache : ISecurityStampCache
{
	private readonly IMemoryCache _cache;
	private readonly ILogger<SecurityStampCache> _logger;

	// مدة الكاش — قصيرة كفاية للأمان، طويلة كفاية لتقليل الضغط
	private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

	private const string KeyPrefix = "secuser:";

	// ✅ Sentinel واضح لعمل Program.cs
	private const string DbUnreachable = "__db_unreachable__";

	// ✅ مدخل واحد بدل مفتاحين — يمنع الـ race condition
	private sealed record CachedUserState(string? Stamp, bool IsActive);

	// ✅ لتتبع كل المفاتيح المُخزَّنة — يمكّن InvalidateAll من العمل
	private static readonly HashSet<int> TrackedUserIds = new();
	private static readonly object TrackedLock = new();

	public SecurityStampCache(
		IMemoryCache cache,
		ILogger<SecurityStampCache> logger)
	{
		_cache = cache;
		_logger = logger;
	}

	public async Task<string?> GetOrLoadAsync(
		int userId,
		Func<Task<(string? Stamp, bool IsActive)>> loader,
		CancellationToken cancellationToken = default)
	{
		var key = $"{KeyPrefix}{userId}";

		// ✅ محاولة القراءة من الكاش
		if (_cache.TryGetValue(key, out CachedUserState? cached) &&
			cached is not null)
		{
			// لو المستخدم غير نشط، ارفض فورًا من الكاش
			if (!cached.IsActive)
				return null;

			return cached.Stamp;
		}

		try
		{
			// ✅ احترم الإلغاء قبل الذهاب لقاعدة البيانات
			cancellationToken.ThrowIfCancellationRequested();

			var (stamp, isActive) = await loader();

			// ✅ نخزّن المدخلين في عنصر واحد (atomic)
			_cache.Set(key, new CachedUserState(stamp, isActive), CacheDuration);

			// ✅ نتتبع المستخدم لإمكانية InvalidateAll
			lock (TrackedLock)
			{
				TrackedUserIds.Add(userId);
			}

			if (!isActive)
				return null;

			return stamp;
		}
		catch (OperationCanceledException)
		{
			// ✅ لا تبتلع الإلغاء — أعد رميه
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex,
				"فشل تحميل SecurityStamp للمستخدم {UserId} من قاعدة البيانات.",
				userId);

			// ✅ Fail-Close: لا نُعيد قيمة كاش قديمة
			// Program.cs يعرف هذا الـ sentinel ويرفض الطلب
			return DbUnreachable;
		}
	}

	public void Invalidate(int userId)
	{
		_cache.Remove($"{KeyPrefix}{userId}");

		lock (TrackedLock)
		{
			TrackedUserIds.Remove(userId);
		}
	}

	public void InvalidateAll()
	{
		// IMemoryCache لا يدعم Clear، لكننا نتتبع كل المفاتيح المُخزَّنة
		// ونزيلها يدويًا.
		List<int> ids;

		lock (TrackedLock)
		{
			ids = TrackedUserIds.ToList();
			TrackedUserIds.Clear();
		}

		foreach (var id in ids)
		{
			_cache.Remove($"{KeyPrefix}{id}");
		}
	}
}