using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Services.QrCode;
using AccountingSystem.Services.WhatsApp;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class EmployeeControllerTests : BaseTest
{
	private readonly Mock<IWhatsAppService> _whatsAppMock;
	private readonly Mock<IQrCodeService> _qrCodeMock;
	private readonly EmployeeController _sut;

	public EmployeeControllerTests()
	{
		_whatsAppMock = new Mock<IWhatsAppService>();
		_qrCodeMock = new Mock<IQrCodeService>();

		_sut = new EmployeeController(Context, _whatsAppMock.Object, _qrCodeMock.Object);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
	}

	private async Task<Employee> SeedEmployeeAsync(string name = "Test Employee")
	{
		var employee = new Employee
		{
			Name = name,
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
    var result = await _sut.Index(null, null, null, null);
    result.Should().BeOfType<ViewResult>();
}

[Fact]
public async Task Index_WithEmployees_ReturnsView()
{
    await SeedEmployeeAsync("E1");
    await SeedEmployeeAsync("E2");

    var result = await _sut.Index(null, null, null, null);
    result.Should().BeOfType<ViewResult>();
}

	[Fact]
	public async Task Create_GET_ReturnsViewResult()
	{
		var result = await _sut.Create();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Details_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Details(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Details_WhenExists_ReturnsView()
	{
		var employee = await SeedEmployeeAsync();

		var result = await _sut.Details(employee.Id);
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
		var employee = await SeedEmployeeAsync();

		var result = await _sut.Edit(employee.Id);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task ToggleActive_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.ToggleActive(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task RequestPortal_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.RequestPortal(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}
}