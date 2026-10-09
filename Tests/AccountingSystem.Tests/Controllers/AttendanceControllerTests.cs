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

public class AttendanceControllerTests : BaseTest
{
	private readonly AttendanceController _sut;

	public AttendanceControllerTests()
	{
		_sut = new AttendanceController(Context);

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
		var result = await _sut.Index(null, null, null);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithFilters_ReturnsView()
	{
		var result = await _sut.Index("test", 1, "present");
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task CheckIn_WithValidEmployee_ReturnsResult()
	{
		var employee = await SeedEmployeeAsync();

		var result = await _sut.CheckIn(employee.Id);
		result.Should().NotBeNull();
	}

	[Fact]
	public async Task CheckOut_WithValidEmployee_ReturnsResult()
	{
		var employee = await SeedEmployeeAsync();

		var result = await _sut.CheckOut(employee.Id);
		result.Should().NotBeNull();
	}

	[Fact]
	public async Task MyAttendance_ReturnsResult()
	{
		var result = await _sut.MyAttendance();
		result.Should().NotBeNull();
	}
}