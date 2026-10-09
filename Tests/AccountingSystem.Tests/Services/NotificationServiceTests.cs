using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace AccountingSystem.Tests.Services;

public class NotificationServiceTests : BaseTest
{
	private readonly Mock<IHttpContextAccessor> _httpAccessorMock;
	private readonly IMemoryCache _cache;                              // ⬅️ جديد
	private readonly Mock<ILogger<NotificationService>> _loggerMock;   // ⬅️ جديد
	private readonly NotificationService _sut;

	public NotificationServiceTests()
	{
		_httpAccessorMock = new Mock<IHttpContextAccessor>();
		_httpAccessorMock
			.Setup(x => x.HttpContext)
			.Returns((HttpContext?)null);

		// ⬇️⬇️⬇️ جديد ⬇️⬇️⬇️
		_cache = new MemoryCache(new MemoryCacheOptions());
		_loggerMock = new Mock<ILogger<NotificationService>>();
		// ⬆️⬆️⬆️ جديد ⬆️⬆️⬆️

		_sut = new NotificationService(
			Context,
			_httpAccessorMock.Object,
			_cache,                             // ⬅️ جديد
			_loggerMock.Object);                // ⬅️ جديد
	}

	// =========================================
	// Helpers
	// =========================================

	private async Task<AppNotification> SeedNotificationAsync(
		int userId,
		string title = "Test",
		bool isRead = false,
		DateTime? createdAt = null)
	{
		var note = new AppNotification
		{
			UserId = userId,
			Title = title,
			Message = "Test message",
			Icon = "bi-bell",
			IsRead = isRead,
			CreatedAt = createdAt ?? DateTime.UtcNow
		};

		Context.AppNotifications.Add(note);
		await Context.SaveChangesAsync();

		return note;
	}

	private void SetHttpContext()
	{
		var context = new DefaultHttpContext();
		_httpAccessorMock
			.Setup(x => x.HttpContext)
			.Returns(context);
	}

	// =========================================
	// 1. CurrentUserId
	// =========================================

	[Fact]
	public void CurrentUserId_WhenUserIsNull_ReturnsZero()
	{
		var result = _sut.CurrentUserId(null);

		result.Should().Be(0);
	}

	[Fact]
	public void CurrentUserId_WhenClaimMissing_ReturnsZero()
	{
		var principal = new ClaimsPrincipal(new ClaimsIdentity());

		var result = _sut.CurrentUserId(principal);

		result.Should().Be(0);
	}

	[Fact]
	public void CurrentUserId_WhenClaimIsNotInteger_ReturnsZero()
	{
		var identity = new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.NameIdentifier, "not-a-number")
		}, "TestAuth");
		var principal = new ClaimsPrincipal(identity);

		var result = _sut.CurrentUserId(principal);

		result.Should().Be(0);
	}

	[Fact]
	public void CurrentUserId_WhenClaimIsValid_ReturnsParsedId()
	{
		var identity = new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.NameIdentifier, "42")
		}, "TestAuth");
		var principal = new ClaimsPrincipal(identity);

		var result = _sut.CurrentUserId(principal);

		result.Should().Be(42);
	}

	// =========================================
	// 2. GetUnreadCountAsync
	// =========================================

	[Fact]
	public async Task GetUnreadCountAsync_WhenNoNotifications_ReturnsZero()
	{
		var result = await _sut.GetUnreadCountAsync(userId: 1);

		result.Should().Be(0);
	}

	[Fact]
	public async Task GetUnreadCountAsync_CountsOnlyUnread()
	{
		await SeedNotificationAsync(userId: 1, isRead: false);
		await SeedNotificationAsync(userId: 1, isRead: false);
		await SeedNotificationAsync(userId: 1, isRead: true);
		await SeedNotificationAsync(userId: 1, isRead: true);

		var result = await _sut.GetUnreadCountAsync(userId: 1);

		result.Should().Be(2);
	}

	[Fact]
	public async Task GetUnreadCountAsync_IgnoresOtherUsers()
	{
		await SeedNotificationAsync(userId: 1, isRead: false);
		await SeedNotificationAsync(userId: 2, isRead: false);
		await SeedNotificationAsync(userId: 2, isRead: false);

		var result = await _sut.GetUnreadCountAsync(userId: 1);

		result.Should().Be(1);
	}

	[Fact]
	public async Task GetUnreadCountAsync_WithHttpContext_CachesResult()
	{
		SetHttpContext();
		await SeedNotificationAsync(userId: 1, isRead: false);

		var first = await _sut.GetUnreadCountAsync(userId: 1);

		// إضافة إشعار جديد — المفروض مش يتأثر بسبب الـ cache
		await SeedNotificationAsync(userId: 1, isRead: false);

		var second = await _sut.GetUnreadCountAsync(userId: 1);

		first.Should().Be(1);
		second.Should().Be(1); // من الـ cache
	}

	// =========================================
	// 3. GetRecentAsync
	// =========================================

	[Fact]
	public async Task GetRecentAsync_WhenNoNotifications_ReturnsEmpty()
	{
		var result = await _sut.GetRecentAsync(userId: 1);

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task GetRecentAsync_ReturnsRequestedCount()
	{
		for (int i = 0; i < 10; i++)
		{
			await SeedNotificationAsync(userId: 1);
		}

		var result = await _sut.GetRecentAsync(userId: 1, count: 3);

		result.Should().HaveCount(3);
	}

	[Fact]
	public async Task GetRecentAsync_OrdersByCreatedAtDescending()
	{
		var baseDate = DateTime.UtcNow;

		var old = await SeedNotificationAsync(
			userId: 1, createdAt: baseDate.AddDays(-3));
		var mid = await SeedNotificationAsync(
			userId: 1, createdAt: baseDate.AddDays(-2));
		var recent = await SeedNotificationAsync(
			userId: 1, createdAt: baseDate.AddDays(-1));

		var result = await _sut.GetRecentAsync(userId: 1);

		result[0].Id.Should().Be(recent.Id);
		result[1].Id.Should().Be(mid.Id);
		result[2].Id.Should().Be(old.Id);
	}

	[Fact]
	public async Task GetRecentAsync_IgnoresOtherUsers()
	{
		await SeedNotificationAsync(userId: 1);
		await SeedNotificationAsync(userId: 2);
		await SeedNotificationAsync(userId: 2);

		var result = await _sut.GetRecentAsync(userId: 1);

		result.Should().HaveCount(1);
		result.All(x => x.UserId == 1).Should().BeTrue();
	}

	// =========================================
	// 4. GetAllAsync
	// =========================================

	[Fact]
	public async Task GetAllAsync_ReturnsAllForUser()
	{
		for (int i = 0; i < 5; i++)
		{
			await SeedNotificationAsync(userId: 1);
		}
		await SeedNotificationAsync(userId: 2);

		var result = await _sut.GetAllAsync(userId: 1);

		result.Should().HaveCount(5);
	}

	[Fact]
	public async Task GetAllAsync_OrdersByCreatedAtDescending()
	{
		var baseDate = DateTime.UtcNow;

		await SeedNotificationAsync(
			userId: 1, createdAt: baseDate.AddDays(-1));
		await SeedNotificationAsync(
			userId: 1, createdAt: baseDate);

		var result = await _sut.GetAllAsync(userId: 1);

		result[0].CreatedAt.Should().BeAfter(result[1].CreatedAt);
	}

	// =========================================
	// 5. CreateAsync
	// =========================================

	[Fact]
	public async Task CreateAsync_AddsNotificationToDatabase()
	{
		var note = await _sut.CreateAsync(
			userId: 1,
			title: "Test Title",
			message: "Test Message");

		note.Id.Should().BeGreaterThan(0);

		var fromDb = await Context.AppNotifications
			.FirstOrDefaultAsync(x => x.Id == note.Id);

		fromDb.Should().NotBeNull();
		fromDb!.UserId.Should().Be(1);
		fromDb.Title.Should().Be("Test Title");
		fromDb.Message.Should().Be("Test Message");
	}

	[Fact]
	public async Task CreateAsync_SetsDefaultValues()
	{
		var note = await _sut.CreateAsync(
			userId: 1,
			title: "Test",
			message: "Message");

		note.IsRead.Should().BeFalse();
		note.Icon.Should().Be("bi-bell");
		note.Url.Should().BeNull();
		note.CreatedAt.Should().BeCloseTo(
			DateTime.UtcNow, TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task CreateAsync_WithCustomIconAndUrl_UsesProvidedValues()
	{
		var note = await _sut.CreateAsync(
			userId: 1,
			title: "Test",
			message: "Message",
			icon: "bi-check",
			url: "/invoices/5");

		note.Icon.Should().Be("bi-check");
		note.Url.Should().Be("/invoices/5");
	}

	[Fact]
	public async Task CreateAsync_WithNullMessage_IsAllowed()
	{
		var note = await _sut.CreateAsync(
			userId: 1,
			title: "Test",
			message: null);

		note.Message.Should().BeNull();
	}

	// =========================================
	// 6. MarkReadAsync
	// =========================================

	[Fact]
	public async Task MarkReadAsync_MarksNotificationAsRead()
	{
		var note = await SeedNotificationAsync(userId: 1, isRead: false);

		await _sut.MarkReadAsync(userId: 1, id: note.Id);

		var fromDb = await Context.AppNotifications
			.FirstAsync(x => x.Id == note.Id);

		fromDb.IsRead.Should().BeTrue();
	}

	[Fact]
	public async Task MarkReadAsync_IgnoresOtherUsersNotifications()
	{
		var note = await SeedNotificationAsync(userId: 2, isRead: false);

		await _sut.MarkReadAsync(userId: 1, id: note.Id);

		var fromDb = await Context.AppNotifications
			.FirstAsync(x => x.Id == note.Id);

		fromDb.IsRead.Should().BeFalse();
	}

	[Fact]
	public async Task MarkReadAsync_WhenAlreadyRead_DoesNotThrow()
	{
		var note = await SeedNotificationAsync(userId: 1, isRead: true);

		Func<Task> act = async () =>
			await _sut.MarkReadAsync(userId: 1, id: note.Id);

		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task MarkReadAsync_WhenNotificationNotFound_DoesNotThrow()
	{
		Func<Task> act = async () =>
			await _sut.MarkReadAsync(userId: 1, id: 99999);

		await act.Should().NotThrowAsync();
	}

	// =========================================
	// 7. MarkAllReadAsync
	// =========================================

	[Fact]
	public async Task MarkAllReadAsync_MarksAllUnreadForUser()
	{
		await SeedNotificationAsync(userId: 1, isRead: false);
		await SeedNotificationAsync(userId: 1, isRead: false);
		await SeedNotificationAsync(userId: 1, isRead: true);

		await _sut.MarkAllReadAsync(userId: 1);

		var unreadCount = await Context.AppNotifications
			.CountAsync(x => x.UserId == 1 && !x.IsRead);

		unreadCount.Should().Be(0);
	}

	[Fact]
	public async Task MarkAllReadAsync_DoesNotAffectOtherUsers()
	{
		await SeedNotificationAsync(userId: 1, isRead: false);
		await SeedNotificationAsync(userId: 2, isRead: false);

		await _sut.MarkAllReadAsync(userId: 1);

		var user2Unread = await Context.AppNotifications
			.CountAsync(x => x.UserId == 2 && !x.IsRead);

		user2Unread.Should().Be(1);
	}

	[Fact]
	public async Task MarkAllReadAsync_WhenNoUnread_DoesNotThrow()
	{
		await SeedNotificationAsync(userId: 1, isRead: true);

		Func<Task> act = async () =>
			await _sut.MarkAllReadAsync(userId: 1);

		await act.Should().NotThrowAsync();
	}

	// =========================================
	// 8. DeleteReadAsync
	// =========================================

	[Fact]
	public async Task DeleteReadAsync_DeletesOnlyReadForUser()
	{
		await SeedNotificationAsync(userId: 1, isRead: true);
		await SeedNotificationAsync(userId: 1, isRead: true);
		await SeedNotificationAsync(userId: 1, isRead: false);
		await SeedNotificationAsync(userId: 2, isRead: true);

		var deleted = await _sut.DeleteReadAsync(userId: 1);

		deleted.Should().Be(2);

		var remaining = await Context.AppNotifications
			.Where(x => x.UserId == 1)
			.ToListAsync();

		remaining.Should().HaveCount(1);
		remaining[0].IsRead.Should().BeFalse();
	}

	[Fact]
	public async Task DeleteReadAsync_WhenNoRead_ReturnsZero()
	{
		await SeedNotificationAsync(userId: 1, isRead: false);
		await SeedNotificationAsync(userId: 1, isRead: false);

		var deleted = await _sut.DeleteReadAsync(userId: 1);

		deleted.Should().Be(0);

		var count = await Context.AppNotifications
			.CountAsync(x => x.UserId == 1);

		count.Should().Be(2);
	}

	[Fact]
	public async Task DeleteReadAsync_DoesNotAffectOtherUsers()
	{
		await SeedNotificationAsync(userId: 1, isRead: true);
		await SeedNotificationAsync(userId: 2, isRead: true);

		var deleted = await _sut.DeleteReadAsync(userId: 1);

		deleted.Should().Be(1);

		var user2Count = await Context.AppNotifications
			.CountAsync(x => x.UserId == 2);

		user2Count.Should().Be(1);
	}
}