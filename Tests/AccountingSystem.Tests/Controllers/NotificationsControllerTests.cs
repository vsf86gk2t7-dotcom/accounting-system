using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class NotificationsControllerTests : BaseTest
{
	private readonly Mock<INotificationService> _serviceMock;
	private readonly NotificationsController _sut;

	public NotificationsControllerTests()
	{
		_serviceMock = new Mock<INotificationService>();

		_serviceMock
			.Setup(x => x.GetAllAsync(It.IsAny<int>()))
			.ReturnsAsync(new List<AppNotification>());

		_serviceMock
			.Setup(x => x.GetUnreadCountAsync(It.IsAny<int>()))
			.ReturnsAsync(0);

		_sut = new NotificationsController(_serviceMock.Object);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
	}

	[Fact]
	public async Task Index_ReturnsResult()
	{
		var result = await _sut.Index();
		result.Should().NotBeNull();
	}

	[Fact]
	public async Task MarkRead_ReturnsResult()
	{
		var result = await _sut.MarkRead(id: 1);
		result.Should().NotBeNull();
	}

	[Fact]
	public async Task MarkAllRead_ReturnsResult()
	{
		var result = await _sut.MarkAllRead();
		result.Should().NotBeNull();
	}

	[Fact]
	public async Task DeleteRead_ReturnsResult()
	{
		var result = await _sut.DeleteRead();
		result.Should().NotBeNull();
	}
}