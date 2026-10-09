using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class AttendanceFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public AttendanceFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"employee.view",
			"employee.edit")
			.GetAwaiter().GetResult();
	}

	// =========================================
	// 1. Index
	// =========================================

	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Attendance/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	[Fact]
	public async Task Index_WithFilters_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Attendance/Index?search=test&status=Present");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// =========================================
	// 2. CheckIn
	// =========================================

	[Fact]
	public async Task CheckIn_NewEmployee_PersistsAttendance()
	{
		var employeeId = await AttendanceTestHelpers
			.SeedEmployeeAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await AttendanceTestHelpers
			.CheckInViaHttpAsync(_factory, client, employeeId);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther },
			$"Status={response.StatusCode}, Location={response.Headers.Location}");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var record = await db.Attendances
			.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == DateTime.Today);

		record.Should().NotBeNull();
		record!.CheckIn.Should().NotBeNull();
		record.Status.Should().Be(AttendanceStatus.Present);
	}

	[Fact]
	public async Task CheckIn_NonExistingEmployee_ReturnsNotFound()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Attendance/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["employeeId"] = "999999",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Attendance/CheckIn/999999", form);

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task CheckIn_Twice_DoesNotDuplicate()
	{
		var employeeId = await AttendanceTestHelpers
			.SeedEmployeeAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		// أول مرة
		await AttendanceTestHelpers.CheckInViaHttpAsync(_factory, client, employeeId);

		// تاني مرة
		var response2 = await AttendanceTestHelpers
			.CheckInViaHttpAsync(_factory, client, employeeId);

		// redirect (بس مع Error TempData)
		response2.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		// سجل واحد فقط
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var count = await db.Attendances
			.CountAsync(x => x.EmployeeId == employeeId && x.Date == DateTime.Today);

		count.Should().Be(1);
	}

	// =========================================
	// 3. CheckOut
	// =========================================

	[Fact]
	public async Task CheckOut_AfterCheckIn_SetsCheckOutTime()
	{
		var employeeId = await AttendanceTestHelpers
			.SeedEmployeeAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		// CheckIn الأول
		await AttendanceTestHelpers.CheckInViaHttpAsync(_factory, client, employeeId);

		// CheckOut
		var response = await AttendanceTestHelpers
			.CheckOutViaHttpAsync(_factory, client, employeeId);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var record = await db.Attendances
			.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == DateTime.Today);

		record.Should().NotBeNull();
		record!.CheckOut.Should().NotBeNull();
	}

	[Fact]
	public async Task CheckOut_WithoutCheckIn_RedirectsWithError()
	{
		var employeeId = await AttendanceTestHelpers
			.SeedEmployeeAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		// CheckOut بدون CheckIn
		var response = await AttendanceTestHelpers
			.CheckOutViaHttpAsync(_factory, client, employeeId);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		// مفيش سجل فيه CheckOut
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var anyWithCheckOut = await db.Attendances
			.AnyAsync(x =>
				x.EmployeeId == employeeId &&
				x.CheckOut != null);

		anyWithCheckOut.Should().BeFalse();
	}

	[Fact]
	public async Task CheckOut_NonExistingEmployee_ReturnsNotFound()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Attendance/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["employeeId"] = "999999",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Attendance/CheckOut/999999", form);

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}
}