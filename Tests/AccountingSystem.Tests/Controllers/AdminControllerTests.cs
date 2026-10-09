using AccountingSystem.Controllers;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class AdminControllerTests : BaseTest
{
	private readonly Mock<IConfiguration> _configMock;
	private readonly AdminController _sut;

	public AdminControllerTests()
	{
		_configMock = new Mock<IConfiguration>();

		_sut = new AdminController(Context, _configMock.Object);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
	}

	[Fact]
	public void Backups_ReturnsViewResult()
	{
		var result = _sut.Backups();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
public async Task RegistrationRequests_ReturnsResult()
{
    var result = await _sut.RegistrationRequests(null);
    result.Should().NotBeNull();
}

[Fact]
public async Task PasswordResetRequests_ReturnsResult()
{
    var result = await _sut.PasswordResetRequests(null);
    result.Should().NotBeNull();
}
}