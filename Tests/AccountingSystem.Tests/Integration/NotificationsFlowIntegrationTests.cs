using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class NotificationsFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public NotificationsFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory)
			.GetAwaiter().GetResult();
	}

	// =========================================
	// Helper: زرع إشعار
	// =========================================

	private async Task<int> SeedNotificationAsync(
		int userId,
		string title = "اختبار",
		bool isRead = false)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var note = new AppNotification
		{
			UserId = userId,
			Title = title,
			Message = "رسالة اختبار",
			Icon = "bi-bell",
			Level = NotificationLevel.Normal,
			IsRead = isRead,
			CreatedAt = DateTime.UtcNow
		};

		db.AppNotifications.Add(note);
		await db.SaveChangesAsync();

		return note.Id;
	}

	private async Task<int> GetAdminIdAsync()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var user = await db.Users
			.OrderBy(x => x.Id)
			.FirstAsync();

		return user.Id;
	}

	// =========================================
	// 1. Index
	// =========================================

	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Notifications/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	// =========================================
	// 2. MarkRead
	// =========================================

	[Fact]
	public async Task MarkRead_ExistingNotification_MarksAsRead()
	{
		var adminId = await GetAdminIdAsync();
		var noteId = await SeedNotificationAsync(adminId, "قبض جديد");

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Notifications/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Notifications/MarkRead/{noteId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var note = await db.AppNotifications.FindAsync(noteId);
		note!.IsRead.Should().BeTrue();
	}

	// =========================================
	// 3. MarkAllRead
	// =========================================

	[Fact]
	public async Task MarkAllRead_ChangesAllToRead()
	{
		var adminId = await GetAdminIdAsync();

		await SeedNotificationAsync(adminId, "إشعار 1");
		await SeedNotificationAsync(adminId, "إشعار 2");
		await SeedNotificationAsync(adminId, "إشعار 3");

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Notifications/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			"/Notifications/MarkAllRead", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var unread = await db.AppNotifications
			.Where(x => x.UserId == adminId && !x.IsRead)
			.CountAsync();

		unread.Should().Be(0);
	}

	// =========================================
	// 4. DeleteRead
	// =========================================

	[Fact]
	public async Task DeleteRead_RemovesOnlyReadNotifications()
	{
		var adminId = await GetAdminIdAsync();

		await SeedNotificationAsync(adminId, "مقروء 1", isRead: true);
		await SeedNotificationAsync(adminId, "مقروء 2", isRead: true);
		await SeedNotificationAsync(adminId, "غير مقروء", isRead: false);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Notifications/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			"/Notifications/DeleteRead", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var remaining = await db.AppNotifications
			.Where(x => x.UserId == adminId)
			.ToListAsync();

		remaining.Should().HaveCount(1);
		remaining[0].IsRead.Should().BeFalse();
	}

	// =========================================
	// 5. MarkRead — مش موجود
	// =========================================

	[Fact]
	public async Task MarkRead_NonExisting_DoesNotThrow()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Notifications/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			"/Notifications/MarkRead/999999", form);

		// المفروض يعمل redirect بدون crash
		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });
	}

	// =========================================
	// 6. Index بعد MarkAllRead
	// =========================================

	[Fact]
	public async Task Index_AfterMarkAllRead_StillReturns200()
	{
		var adminId = await GetAdminIdAsync();
		await SeedNotificationAsync(adminId, "إشعار");

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Notifications/Index");

		var markAllForm = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		await client.PostAsync("/Notifications/MarkAllRead", markAllForm);

		// افتح Index تاني
		var response = await client.GetAsync("/Notifications/Index");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}
}