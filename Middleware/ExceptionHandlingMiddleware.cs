using System.Net;
using System.Text.Json;

namespace AccountingSystem.Middleware
{
	public class ExceptionHandlingMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<ExceptionHandlingMiddleware> _logger;

		public ExceptionHandlingMiddleware(
			RequestDelegate next,
			ILogger<ExceptionHandlingMiddleware> logger)
		{
			_next = next;
			_logger = logger;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			try
			{
				await _next(context);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex,
					"Unhandled exception: {Message}", ex.Message);
				await HandleExceptionAsync(context, ex);
			}
		}

		private static async Task HandleExceptionAsync(
			HttpContext context, Exception exception)
		{
			context.Response.ContentType = "application/json";

			var (statusCode, message) = exception switch
			{
				UnauthorizedAccessException =>
					(HttpStatusCode.Unauthorized, "غير مصرح لك بالوصول."),
				KeyNotFoundException =>
					(HttpStatusCode.NotFound, "العنصر المطلوب غير موجود."),
				InvalidOperationException =>
					(HttpStatusCode.BadRequest, exception.Message),
				ArgumentException =>
					(HttpStatusCode.BadRequest, exception.Message),
				_ =>
					(HttpStatusCode.InternalServerError,
						"حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى.")
			};

			context.Response.StatusCode = (int)statusCode;

			var response = new
			{
				statusCode = (int)statusCode,
				message,
				traceId = context.TraceIdentifier
			};

			await context.Response.WriteAsync(
				JsonSerializer.Serialize(response));
		}
	}

	public static class ExceptionHandlingMiddlewareExtensions
	{
		public static IApplicationBuilder UseExceptionHandling(
			this IApplicationBuilder builder)
		{
			return builder.UseMiddleware<ExceptionHandlingMiddleware>();
		}
	}
}
