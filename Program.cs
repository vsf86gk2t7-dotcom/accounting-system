using AccountingSystem.Data;
using AccountingSystem.Middleware;
using AccountingSystem.Services;
using AccountingSystem.Services.EmployeeScope;
using AccountingSystem.Services.Jwt;
using AccountingSystem.Services.Pdf;
using AccountingSystem.Services.Permissions;
using AccountingSystem.Services.QrCode;
using AccountingSystem.Services.WhatsApp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
	builder.Configuration.AddUserSecrets<Program>();
}

// =========================================
// MVC
// =========================================

builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(policy =>
	{
		// ✅ قيّد الأصل حسب بيئتك
		policy.WithOrigins(
				"https://your-production-domain.com")
			  .AllowAnyMethod()
			  .AllowAnyHeader();
	});
});

builder.Services.AddControllersWithViews(options =>
{
	options.Filters.Add(
		new AuthorizeFilter(
			new AuthorizationPolicyBuilder()
				.RequireAuthenticatedUser()
				.Build()));
});

builder.Services.AddHealthChecks()
	.AddDbContextCheck<ApplicationDbContext>(
		name: "database",
		failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
		tags: new[] { "database" });

builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "Accounting System API",
		Version = "v1",
		Description = "نظام محاسبة متكامل"
	});

	options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
	{
		Name = "Authorization",
		Type = SecuritySchemeType.Http,
		Scheme = "bearer",
		BearerFormat = "JWT",
		In = ParameterLocation.Header,
		Description = "أدخل JWT Token هنا"
	});

	options.AddSecurityRequirement(new OpenApiSecurityRequirement
	{
		{
			new OpenApiSecurityScheme
			{
				Reference = new OpenApiReference
				{
					Type = ReferenceType.SecurityScheme,
					Id = "Bearer"
				}
			},
			Array.Empty<string>()
		}
	});
});

// =========================================
// Memory Cache (للـ SecurityStamp)
// =========================================
builder.Services.AddScoped<AccountingSystem.Services.Admin.FactoryResetService>();
builder.Services.AddMemoryCache();

// =========================================
// Rate Limiting — معطّل في بيئة Testing
// =========================================

if (!builder.Environment.IsEnvironment("Testing"))
{
	builder.Services.AddRateLimiter(options =>
	{
		options.RejectionStatusCode = 429;

		// ✅ Global limiter — يُطبَّق على كل الطلبات افتراضيًا
		options.GlobalLimiter =
			PartitionedRateLimiter.Create<HttpContext, string>(context =>
			{
				var ip = context.Connection.RemoteIpAddress?.ToString()
					?? "unknown";

				return RateLimitPartition.GetFixedWindowLimiter(
					partitionKey: $"global_{ip}",
					factory: _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = 200,
						Window = TimeSpan.FromMinutes(1),
						QueueLimit = 0
					});
			});

		options.AddPolicy("LoginPolicy", context =>
		{
			var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
			return RateLimitPartition.GetFixedWindowLimiter(
				partitionKey: $"login_{ip}",
				factory: _ => new FixedWindowRateLimiterOptions
				{
					PermitLimit = 5,
					Window = TimeSpan.FromMinutes(1),
					QueueLimit = 0
				});
		});

		options.AddPolicy("ForgotPasswordPolicy", context =>
		{
			var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
			return RateLimitPartition.GetFixedWindowLimiter(
				partitionKey: $"forgot_{ip}",
				factory: _ => new FixedWindowRateLimiterOptions
				{
					PermitLimit = 3,
					Window = TimeSpan.FromMinutes(5),
					QueueLimit = 0
				});
		});

		options.AddPolicy("GeneralPolicy", context =>
		{
			var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
			return RateLimitPartition.GetFixedWindowLimiter(
				partitionKey: $"general_{ip}",
				factory: _ => new FixedWindowRateLimiterOptions
				{
					PermitLimit = 100,
					Window = TimeSpan.FromMinutes(1),
					QueueLimit = 0
				});
		});
	});
}

// =========================================
// Database
// =========================================

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services.AddDbContext<ApplicationDbContext>(
	(sp, options) =>
		options.UseSqlServer(
			builder.Configuration.GetConnectionString("DefaultConnection"),
			sqlOptions =>
			{
				sqlOptions.CommandTimeout(300);

				sqlOptions.EnableRetryOnFailure(
					maxRetryCount: 5,
					maxRetryDelay: TimeSpan.FromSeconds(10),
					errorNumbersToAdd: null);
			})
			.AddInterceptors(
				sp.GetRequiredService<AuditLogInterceptor>())
			.ConfigureWarnings(w =>
			{
				if (!builder.Environment.IsDevelopment())
				{
					w.Ignore(
						Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
				}
			}));

builder.Services.AddScoped<AuditLogInterceptor>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// =========================================
// Background Services
// =========================================

builder.Services.AddHostedService<DatabaseKeepAliveService>();
builder.Services.AddHostedService<DailyInvoiceReminderService>();

// =========================================
// Application Services
// =========================================

builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISequenceService, SequenceService>();
builder.Services.AddScoped<IPostingService, PostingService>();
builder.Services.AddScoped<ISalesInvoiceService, SalesInvoiceService>();
builder.Services.AddScoped<ITreasuryService, TreasuryService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<IPdfInvoiceService, PdfInvoiceService>();

PdfInvoiceService.Configure(builder.Environment.WebRootPath);

builder.Services.AddScoped<IQrCodeService, QrCodeService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPermissionManagementService, PermissionManagementService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IEmployeeScopeService, EmployeeScopeService>();

builder.Services.AddScoped<
	AccountingSystem.Services.IPayrollService,
	AccountingSystem.Services.PayrollService>();

builder.Services.AddScoped<
	AccountingSystem.Services.IEmployeeAdvanceService,
	AccountingSystem.Services.EmployeeAdvanceService>();

builder.Services.AddScoped<
	AccountingSystem.Services.ISalesRepService,
	AccountingSystem.Services.SalesRepService>();

builder.Services.AddScoped<IInvoiceReminderService, InvoiceReminderService>();
builder.Services.AddScoped<IDocumentNumberService, DocumentNumberService>();

builder.Services.AddSingleton<ISecurityStampCache, SecurityStampCache>();

// =========================================
// JWT Settings
// =========================================

var jwtSection = builder.Configuration.GetSection("JwtSettings");

var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];
var jwtSecretKey = jwtSection["SecretKey"];

if (string.IsNullOrWhiteSpace(jwtIssuer))
	throw new InvalidOperationException("JwtSettings:Issuer غير موجود.");

if (string.IsNullOrWhiteSpace(jwtAudience))
	throw new InvalidOperationException("JwtSettings:Audience غير موجود.");

if (string.IsNullOrWhiteSpace(jwtSecretKey) || jwtSecretKey.Length < 32)
	throw new InvalidOperationException("JwtSettings:SecretKey يجب أن يكون 32 حرفًا على الأقل.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey));

// =========================================
// Authentication
// =========================================

builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
		options.SaveToken = false;

		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = signingKey,

			ValidateIssuer = true,
			ValidIssuer = jwtIssuer,

			ValidateAudience = true,
			ValidAudience = jwtAudience,

			ValidateLifetime = true,

			// ✅ 30 ثانية بدل 5 دقائق
			ClockSkew = TimeSpan.FromSeconds(30)
		};

		options.Events = new JwtBearerEvents
		{
			OnMessageReceived = context =>
			{
				var authorizationHeader = context.Request.Headers.Authorization.ToString();

				if (!string.IsNullOrWhiteSpace(authorizationHeader) &&
					authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
				{
					return Task.CompletedTask;
				}

				if (context.Request.Cookies.TryGetValue(
					"AccountingSystem.AccessToken",
					out var token))
				{
					context.Token = token;
				}

				return Task.CompletedTask;
			},

			OnChallenge = context =>
			{
				if (WantsJsonResponse(context.Request))
					return Task.CompletedTask;

				context.HandleResponse();
				context.Response.Redirect("/Account/Login");
				return Task.CompletedTask;
			},

			OnForbidden = context =>
			{
				if (WantsJsonResponse(context.Request))
					return Task.CompletedTask;

				context.Response.Redirect("/Account/AccessDenied");
				return Task.CompletedTask;
			},

			// ✅ Fail-Close بدل Fail-Open
			OnTokenValidated = async context =>
			{
				var userIdClaim = context.Principal?
					.FindFirst(ClaimTypes.NameIdentifier)?.Value;

				if (string.IsNullOrWhiteSpace(userIdClaim))
					return;

				var tokenStamp = context.Principal?
					.FindFirst("SecurityStamp")?.Value;

				if (string.IsNullOrWhiteSpace(tokenStamp))
					return;

				if (!int.TryParse(userIdClaim, out var userIdInt) || userIdInt <= 0)
				{
					context.Fail("Invalid user id claim.");
					return;
				}

				var cache = context.HttpContext.RequestServices
					.GetRequiredService<ISecurityStampCache>();

				var logger = context.HttpContext.RequestServices
					.GetRequiredService<ILogger<Program>>();

				try
				{
					var dbStamp = await cache.GetOrLoadAsync(userIdInt, async () =>
					{
						await using var scope = context.HttpContext
							.RequestServices.CreateAsyncScope();

						var db = scope.ServiceProvider
							.GetRequiredService<ApplicationDbContext>();

						var user = await db.Users
							.AsNoTracking()
							.Where(x => x.Id == userIdInt)
							.Select(x => new
							{
								x.SecurityStamp,
								x.IsActive
							})
							.FirstOrDefaultAsync();

						if (user == null)
							return (null, false);

						return (user.SecurityStamp, user.IsActive);
					});

					// ✅ Fail-Close: DB غير متاحة → رفض
					if (dbStamp == "__db_unreachable__")
					{
						logger.LogWarning(
							"DB unreachable — رفض التوكن للمستخدم {UserId}",
							userIdInt);
						context.Fail("Service temporarily unavailable.");
						return;
					}

					if (string.IsNullOrEmpty(dbStamp))
					{
						context.Fail("User not found or inactive.");
						return;
					}

					if (dbStamp != tokenStamp)
					{
						context.Fail("SecurityStamp mismatch — token revoked.");
					}
				}
				catch (SqlException ex)
				{
					logger.LogError(ex,
						"SQL error — رفض التوكن للمستخدم {UserId}", userIdInt);
					context.Fail("Service temporarily unavailable.");
				}
				catch (Exception ex)
				{
					logger.LogError(ex,
						"Unexpected error — رفض التوكن للمستخدم {UserId}", userIdInt);
					context.Fail("Authentication failed.");
				}
			}
		};
	});

// =========================================
// Authorization
// =========================================

builder.Services.AddAuthorization();

var app = builder.Build();

// =========================================
// Database Seeder
// =========================================

if (!app.Configuration.GetValue("SkipDatabaseSeeding", false))
{
	await DbSeeder.SeedAsync(app.Services, app.Configuration);
}

// =========================================
// HTTP Pipeline
// =========================================

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	app.UseHsts();
}

if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();
}

app.UseExceptionHandling();

app.Use(async (context, next) =>
{
	context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
	context.Response.Headers.Append("X-Frame-Options", "DENY");
	context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
	context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

	await next();
});

app.UseStaticFiles();
app.UseCors();
app.UseRouting();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI(options =>
	{
		options.SwaggerEndpoint("/swagger/v1/swagger.json", "Accounting System API v1");
		options.RoutePrefix = "swagger";
	});
}

if (!app.Environment.IsEnvironment("Testing"))
{
	app.UseRateLimiter();
}

app.UseAuthentication();
app.UseAuthorization();

// ✅ Health endpoint بدون تفاصيل حساسة
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
	ResponseWriter = async (context, report) =>
	{
		context.Response.ContentType = "application/json";
		await context.Response.WriteAsync(
			System.Text.Json.JsonSerializer.Serialize(new
			{
				status = report.Status.ToString()
			}));
	}
}).AllowAnonymous();

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();

static bool WantsJsonResponse(HttpRequest request)
{
	var accept = request.Headers.Accept.ToString();

	return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
		request.Path.StartsWithSegments("/Account/Me", StringComparison.OrdinalIgnoreCase);
}

// مطلوب للـ Integration Tests
public partial class Program { }