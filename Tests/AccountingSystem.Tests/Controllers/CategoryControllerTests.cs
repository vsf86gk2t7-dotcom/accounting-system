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

public class CategoryControllerTests : BaseTest
{
	private readonly CategoryController _sut;

	public CategoryControllerTests()
	{
		_sut = new CategoryController(Context);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(
			httpContext, Mock.Of<ITempDataProvider>());
	}

	private async Task<Category> SeedCategoryAsync(string name = "Test Category")
	{
		var category = new Category
		{
			Name = name,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Categories.Add(category);
		await Context.SaveChangesAsync();
		return category;
	}

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithCategories_ReturnsView()
	{
		await SeedCategoryAsync("C1");

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
		var model = new Category();
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
		var category = await SeedCategoryAsync();

		var result = await _sut.Edit(category.Id);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task ToggleActive_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.ToggleActive(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}
}