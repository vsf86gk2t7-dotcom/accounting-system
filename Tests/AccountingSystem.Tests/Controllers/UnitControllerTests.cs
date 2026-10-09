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

public class UnitControllerTests : BaseTest
{
	private readonly UnitController _sut;

	public UnitControllerTests()
	{
		_sut = new UnitController(Context);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(
			httpContext, Mock.Of<ITempDataProvider>());
	}

	private async Task<Unit> SeedUnitAsync(string name = "Test Unit")
	{
		var unit = new Unit
		{
			Name = name,
			ShortName = "TU",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Units.Add(unit);
		await Context.SaveChangesAsync();
		return unit;
	}

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithUnits_ReturnsView()
	{
		await SeedUnitAsync("U1");
		await SeedUnitAsync("U2");

		var result = await _sut.Index();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public void Create_GET_ReturnsViewResult()
	{
		var result = _sut.Create();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new Unit();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Create(model);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Edit_GET_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Edit(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Edit_GET_WhenExists_ReturnsView()
	{
		var unit = await SeedUnitAsync();

		var result = await _sut.Edit(unit.Id);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task ToggleActive_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.ToggleActive(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}
}