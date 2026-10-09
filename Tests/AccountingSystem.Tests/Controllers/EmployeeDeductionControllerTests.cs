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

public class EmployeeDeductionControllerTests : BaseTest
{
	private readonly EmployeeDeductionController _sut;

	public EmployeeDeductionControllerTests()
	{
		_sut = new EmployeeDeductionController(Context);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(
			httpContext, Mock.Of<ITempDataProvider>());
	}

	private async Task<Employee> SeedEmployeeAsync()
	{
		var employee = new Employee
		{
			Name = "Test Employee",
			Phone = $"0100000{Random.Shared.Next(1000, 9999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Employees.Add(employee);
		await Context.SaveChangesAsync();
		return employee;
	}

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index(null, null);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithFilters_ReturnsView()
	{
		var result = await _sut.Index("test", 1);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_GET_ReturnsViewResult()
	{
		var result = await _sut.Create();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new EmployeeDeduction();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Create(model);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task MyDeductions_ReturnsResult()
	{
		var result = await _sut.MyDeductions();
		result.Should().NotBeNull();
	}
}