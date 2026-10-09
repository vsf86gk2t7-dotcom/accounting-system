using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class LeaveRequestFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public LeaveRequestFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"employee.view",
			"employee.edit")
			.GetAwaiter().GetResult();
	}

	// Helper: زرع موظف + ربط الـ Admin بيه
	private async Task<int> SeedEmployeeAndLinkToAdminAsync()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var employee = new Employee
		{
			Name = $"موظف {Guid.NewGuid():N}".Substring(0, 20),
			Phone = $"0100{Random.Shared.Next(1000000, 9999999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Employees.Add(employee);
		await db.SaveChangesAsync();

		// ربط الـ Admin employee
		var admin = await db.Users
			.OrderBy(x => x.Id)
			.FirstAsync();
		admin.EmployeeId = employee.Id;
		await db.SaveChangesAsync();

		return employee.Id;
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/LeaveRequest/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	// 2. Create GET
	[Fact]
	public async Task Create_Get_Returns200()
	{
		await SeedEmployeeAndLinkToAdminAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/LeaveRequest/Create");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 3. Create POST — valid
	[Fact]
	public async Task Create_Post_PersistsLeaveRequest()
	{
		var employeeId = await SeedEmployeeAndLinkToAdminAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/LeaveRequest/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["StartDate"] = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"),
			["EndDate"] = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd"),
			["Reason"] = "إجازة سنوية",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/LeaveRequest/Create", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther },
			$"Status={response.StatusCode}, Location={response.Headers.Location}");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var req = await db.LeaveRequests
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync();

		req.Should().NotBeNull();
		req!.EmployeeId.Should().Be(employeeId);
		req.Reason.Should().Be("إجازة سنوية");
		req.Status.Should().Be(LeaveStatus.Pending);
	}

	// 4. Create POST — EndDate before StartDate
	[Fact]
	public async Task Create_Post_EndBeforeStart_ReturnsViewWithError()
	{
		await SeedEmployeeAndLinkToAdminAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/LeaveRequest/Create");

		var beforeCount = 0;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			beforeCount = await db.LeaveRequests.CountAsync();
		}

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["StartDate"] = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd"),
			["EndDate"] = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"),
			["Reason"] = "تواريخ غلط",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/LeaveRequest/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var afterCount = await db2.LeaveRequests.CountAsync();
		afterCount.Should().Be(beforeCount);
	}

	// 5. Approve
	[Fact]
	public async Task Approve_ChangesStatusToApproved()
	{
		var employeeId = await SeedEmployeeAndLinkToAdminAsync();

		int requestId;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var req = new LeaveRequest
			{
				EmployeeId = employeeId,
				StartDate = DateTime.Today.AddDays(1),
				EndDate = DateTime.Today.AddDays(3),
				Reason = "اختبار",
				Status = LeaveStatus.Pending,
				CreatedAt = DateTime.UtcNow
			};
			db.LeaveRequests.Add(req);
			await db.SaveChangesAsync();
			requestId = req.Id;
		}

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/LeaveRequest/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/LeaveRequest/Approve/{requestId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var updated = await db2.LeaveRequests.FindAsync(requestId);
		updated!.Status.Should().Be(LeaveStatus.Approved);
	}

	// 6. Reject
	[Fact]
	public async Task Reject_ChangesStatusToRejected()
	{
		var employeeId = await SeedEmployeeAndLinkToAdminAsync();

		int requestId;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var req = new LeaveRequest
			{
				EmployeeId = employeeId,
				StartDate = DateTime.Today.AddDays(1),
				EndDate = DateTime.Today.AddDays(3),
				Reason = "للرفض",
				Status = LeaveStatus.Pending,
				CreatedAt = DateTime.UtcNow
			};
			db.LeaveRequests.Add(req);
			await db.SaveChangesAsync();
			requestId = req.Id;
		}

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/LeaveRequest/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/LeaveRequest/Reject/{requestId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var updated = await db2.LeaveRequests.FindAsync(requestId);
		updated!.Status.Should().Be(LeaveStatus.Rejected);
	}
}