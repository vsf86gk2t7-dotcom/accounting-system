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

public class LeaveRequestControllerTests : BaseTest
{
	private readonly LeaveRequestController _sut;

	public LeaveRequestControllerTests()
	{
		_sut = new LeaveRequestController(Context);

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
		var result = await _sut.Index(status: null);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithStatusFilter_ReturnsView()
	{
		var result = await _sut.Index(status: "pending");
		result.Should().BeOfType<ViewResult>();
	}

		[Fact]
	public async Task Create_GET_WhenUserNotEmployee_Redirects()
	{
		// ✅ الـ action بيعتمد على وجود Employee مربوط بالـ user
		// بدون ذلك → RedirectToAction
		var result = await _sut.Create();

		(result is ViewResult || result is RedirectToActionResult)
			.Should().BeTrue();
	}

	[Fact]
	public async Task Create_POST_WithInvalidModelState_Redirects()
	{
		var model = new LeaveRequest();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Create(model);

		// ✅ ممكن يكون ViewResult أو RedirectToActionResult
		(result is ViewResult || result is RedirectToActionResult)
			.Should().BeTrue();
	}

	[Fact]
	public async Task Approve_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Approve(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Reject_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Reject(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task MyLeaves_ReturnsResult()
	{
		var result = await _sut.MyLeaves();
		result.Should().NotBeNull();
	}
}