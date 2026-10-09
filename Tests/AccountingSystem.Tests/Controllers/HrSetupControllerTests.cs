using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class HrSetupControllerTests : BaseTest
{
	private readonly HrSetupController _sut;

	public HrSetupControllerTests()
	{
		_sut = new HrSetupController(Context);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(
			httpContext, Mock.Of<ITempDataProvider>());
	}

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithDepartments_ReturnsView()
	{
		Context.Departments.Add(new Department
		{
			Name = "Test Dept",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		});
		Context.Positions.Add(new Position
		{
			Name = "Test Pos",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		});
		await Context.SaveChangesAsync();

		var result = await _sut.Index();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task AddDepartment_WithEmptyName_ReturnsResult()
	{
		var result = await _sut.AddDepartment(name: null);
		result.Should().NotBeNull();
	}

	[Fact]
	public async Task ToggleDepartment_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.ToggleDepartment(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task AddPosition_WithEmptyName_ReturnsResult()
	{
		var result = await _sut.AddPosition(name: null);
		result.Should().NotBeNull();
	}

	[Fact]
	public async Task TogglePosition_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.TogglePosition(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}
}