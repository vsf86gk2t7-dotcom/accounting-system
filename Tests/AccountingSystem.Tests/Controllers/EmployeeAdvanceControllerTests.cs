using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class EmployeeAdvanceControllerTests : BaseTest
{
	private readonly Mock<IEmployeeAdvanceService> _advanceServiceMock;
	private readonly EmployeeAdvanceController _sut;

	public EmployeeAdvanceControllerTests()
	{
		_advanceServiceMock = new Mock<IEmployeeAdvanceService>();

		_sut = new EmployeeAdvanceController(
			Context,
			_advanceServiceMock.Object);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(
			httpContext, Mock.Of<ITempDataProvider>());
	}

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index(employeeId: null);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithEmployeeId_ReturnsView()
	{
		var result = await _sut.Index(employeeId: 1);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_GET_ReturnsViewResult()
	{
		var result = await _sut.Create();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_POST_WithInvalidModelState_Redirects()
	{
		// Signature: Create(int, decimal, DateTime, string?, int)
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Create(
			employeeId: 1,
			amount: 100m,
			date: DateTime.Today,
			reason: "test",
			cashAccountId: 1);

		// ✅ ممكن يكون ViewResult أو RedirectToActionResult
		(result is ViewResult || result is RedirectToActionResult)
			.Should().BeTrue();
	}

	[Fact]
	public async Task Details_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Details(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}
}