using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class PayrollFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public PayrollFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"payroll.view",
			"payroll.manage",
			"payroll.approve",
			"payroll.pay")
			.GetAwaiter().GetResult();
	}

	// Helper: زرع PayrollRun
	private async Task<int> SeedPayrollRunAsync(
		string periodName = "2026-10",
		PayrollRunStatus status = PayrollRunStatus.Draft)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var run = new PayrollRun
		{
			PeriodName = periodName,
			PeriodStart = new DateTime(2026, 10, 1),
			PeriodEnd = new DateTime(2026, 10, 31),
			Status = status,
			TotalNet = 5000,
			CreatedAt = DateTime.UtcNow
		};

		db.PayrollRuns.Add(run);
		await db.SaveChangesAsync();

		return run.Id;
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Payroll/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	// 2. Create GET
	[Fact]
	public async Task Create_Get_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Payroll/Create");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 3. Details
	[Fact]
	public async Task Details_Returns200()
	{
		var runId = await SeedPayrollRunAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync($"/Payroll/Details/{runId}");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 4. Details — non-existing
	[Fact]
	public async Task Details_NonExisting_ReturnsNotFound()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Payroll/Details/999999");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	// 5. Delete — Draft only
	[Fact]
	public async Task Delete_DraftRun_RemovesRun()
	{
		var runId = await SeedPayrollRunAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, $"/Payroll/Details/{runId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync($"/Payroll/Delete/{runId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var run = await db.PayrollRuns.FindAsync(runId);
		run.Should().BeNull();
	}

	// 6. Pay GET — Wrong status (not Approved)
	[Fact]
	public async Task Pay_Get_NotApproved_RedirectsWithError()
	{
		var runId = await SeedPayrollRunAsync(status: PayrollRunStatus.Draft);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync($"/Payroll/Pay/{runId}");

		// الـ Controller بيعمل RedirectToAction لو الـ Status مش Approved
		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });
	}
}