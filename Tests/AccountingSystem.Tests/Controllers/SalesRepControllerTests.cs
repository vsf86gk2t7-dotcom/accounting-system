using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class SalesRepControllerTests : BaseTest
{
	private readonly Mock<ISalesRepService> _repServiceMock;
	private readonly SalesRepController _sut;

	public SalesRepControllerTests()
	{
		_repServiceMock = new Mock<ISalesRepService>();

		_repServiceMock
			.Setup(x => x.GetCurrentRepIdAsync())
			.ReturnsAsync((int?)null);

		_sut = new SalesRepController(Context, _repServiceMock.Object);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
	}

	private async Task<SalesRepProfile> SeedRepProfileAsync()
	{
		var employee = new Employee
		{
			Name = "Rep",
			Phone = $"0100000{Random.Shared.Next(1000, 9999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Employees.Add(employee);
		await Context.SaveChangesAsync();

		var profile = new SalesRepProfile
		{
			EmployeeId = employee.Id,
			IsActive = true,
			CommissionType = CommissionType.PercentOfSales,
			CommissionRate = 5m,
			MonthlyTarget = 10000m,
			CreatedAt = DateTime.UtcNow
		};
		Context.SalesRepProfiles.Add(profile);
		await Context.SaveChangesAsync();
		return profile;
	}

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_GET_ReturnsViewResult()
	{
		var result = await _sut.Create();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Edit_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Edit(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Edit_WhenExists_ReturnsView()
	{
		var profile = await SeedRepProfileAsync();

		var result = await _sut.Edit(profile.Id);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Details_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Details(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task ToggleActive_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.ToggleActive(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}
}