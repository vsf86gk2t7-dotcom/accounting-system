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
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// =========================================
// MVC
// =========================================

builder.Services.AddControllersWithViews(options =>
{
	options.Filters.Add(
		new Microsoft.AspNetCore.Mvc.RequireHttpsAttribute());
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
// Rate Limiting
// =========================================

builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = 429;

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

// =========================================
// Database
// =========================================

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient();

// =========================================
// DbContext — نسخة واحدة فقط
//
// ملاحظة: EnableRetryOnFailure معطّلة حاليًا
// لأن هناك 9 أماكن في المشروع تستخدم
// BeginTransactionAsync مباشرة، وهي لا تتوافق
// مع SqlServerRetryingExecutionStrategy.
//
// لتفعيلها لاحقًا: غلّف كل BeginTransactionAsync
// داخل Database.CreateExecutionStrategy().ExecuteAsync(...)
// =========================================

builder.Services.AddDbContext<ApplicationDbContext>(
	(sp, options) =>
		options.UseSqlServer(
			builder.Configuration.GetConnectionString("DefaultConnection"),
			sqlOptions =>
			{
				sqlOptions.CommandTimeout(300);

				sqlOptions.EnableRetryOnFailure(
					maxRetryCount: 5,
					maxRetryDelay: TimeSpan.FromSeconds(30),
					errorNumbersToAdd: null);
			})
			.AddInterceptors(
				sp.GetRequiredService<AuditLogInterceptor>()));

builder.Services.AddScoped<AuditLogInterceptor>();

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// خدمة إحماء القاعدة والحفاظ على دفء كاشها
// لتفادي مهلات قراءة باردة على الأقراص البطيئة
builder.Services.AddHostedService<DatabaseKeepAliveService>();

// خدمة التذكيرات اليومية للفواتير المستحقة
builder.Services.AddHostedService<DailyInvoiceReminderService>();

// In-app notification service (topbar bell).
builder.Services.AddScoped<INotificationService, NotificationService>();

// =========================================
// Sequence (أرقام تسلسلية آمنة ضد التزامن)
// =========================================

builder.Services.AddScoped<
	ISequenceService,
	SequenceService>();

// =========================================
// Posting (محرك القيود المحاسبية التلقائية)
// =========================================

builder.Services.AddScoped<
	IPostingService,
	PostingService>();

builder.Services.AddScoped<
	ISalesInvoiceService,
	SalesInvoiceService>();

builder.Services.AddScoped<
	ITreasuryService,
	TreasuryService>();

// =========================================
// WhatsApp
// =========================================

builder.Services.AddScoped<
	IWhatsAppService,
	WhatsAppService>();

// =========================================
// PDF
// =========================================

builder.Services.AddScoped<
	IPdfInvoiceService,
	PdfInvoiceService>();

PdfInvoiceService.Configure(
	builder.Environment.WebRootPath);

// =========================================
// QR Code
// =========================================

builder.Services.AddScoped<
	IQrCodeService,
	QrCodeService>();

// =========================================
// JWT
// =========================================

builder.Services.AddScoped<
	IJwtTokenService,
	JwtTokenService>();

// =========================================
// Permissions
// =========================================

builder.Services.AddScoped<
	IPermissionManagementService,
	PermissionManagementService>();

builder.Services.AddScoped<
	IPermissionService,
	PermissionService>();

// =========================================
// Employee Scope
// =========================================

builder.Services.AddScoped<
	IEmployeeScopeService,
	EmployeeScopeService>();
// =========================================
// Payroll + Employee Advances
// =========================================

builder.Services.AddScoped<
	AccountingSystem.Services.IPayrollService,
	AccountingSystem.Services.PayrollService>();

builder.Services.AddScoped<
	AccountingSystem.Services.IEmployeeAdvanceService,
	AccountingSystem.Services.EmployeeAdvanceService>();
// =========================================
// Payroll Service
// =========================================


// =========================================
// Sales Rep (المناديب)
// =========================================

builder.Services.AddScoped<
	AccountingSystem.Services.ISalesRepService,
	AccountingSystem.Services.SalesRepService>();
// Invoice Reminder Service
builder.Services.AddScoped<
	IInvoiceReminderService,
	InvoiceReminderService>();

builder.Services.AddScoped<
	IDocumentNumberService,
	DocumentNumberService>();

// =========================================
// JWT Settings
// =========================================

var jwtSection =
	builder.Configuration.GetSection("JwtSettings");

var jwtIssuer =
	jwtSection["Issuer"];

var jwtAudience =
	jwtSection["Audience"];

var jwtSecretKey =
	jwtSection["SecretKey"];

if (string.IsNullOrWhiteSpace(jwtIssuer))
{
	throw new InvalidOperationException(
		"JwtSettings:Issuer غير موجود.");
}

if (string.IsNullOrWhiteSpace(jwtAudience))
{
	throw new InvalidOperationException(
		"JwtSettings:Audience غير موجود.");
}

if (string.IsNullOrWhiteSpace(jwtSecretKey) ||
	jwtSecretKey.Length < 32)
{
	throw new InvalidOperationException(
		"JwtSettings:SecretKey يجب أن يكون 32 حرفًا على الأقل.");
}

var signingKey =
	new SymmetricSecurityKey(
		Encoding.UTF8.GetBytes(jwtSecretKey));

// =========================================
// Authentication
// =========================================

builder.Services
	.AddAuthentication(
		JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.RequireHttpsMetadata =
			!builder.Environment.IsDevelopment();

		options.SaveToken = false;

		options.TokenValidationParameters =
			new TokenValidationParameters
			{
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = signingKey,

				ValidateIssuer = true,
				ValidIssuer = jwtIssuer,

				ValidateAudience = true,
				ValidAudience = jwtAudience,

				ValidateLifetime = true,

				ClockSkew =
					TimeSpan.FromSeconds(30)
			};

		// =====================================
		// JWT للموبايل / API
		// Authorization: Bearer <token>
		//
		// JWT للـ MVC
		// يتم قراءته من Cookie آمن
		// =====================================

		options.Events =
			new JwtBearerEvents
			{
				OnMessageReceived = context =>
				{
					// الأولوية للـ Authorization Header
					var authorizationHeader =
						context.Request
							.Headers
							.Authorization
							.ToString();

					if (!string.IsNullOrWhiteSpace(
							authorizationHeader) &&
						authorizationHeader.StartsWith(
							"Bearer ",
							StringComparison.OrdinalIgnoreCase))
					{
						return Task.CompletedTask;
					}

					// لو لا يوجد Bearer Token
					// نبحث في الـ Cookie
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
					{
						return Task.CompletedTask;
					}

					context.HandleResponse();
					context.Response.Redirect(
						"/Account/Login");

					return Task.CompletedTask;
				},
				OnForbidden = context =>
				{
					if (WantsJsonResponse(context.Request))
					{
						return Task.CompletedTask;
					}

					context.Response.Redirect(
						"/Account/AccessDenied");

					return Task.CompletedTask;
				},
				OnTokenValidated = async context =>
				{
					var userId =
						context.Principal
							?.FindFirst(
								ClaimTypes.NameIdentifier)
							?.Value;

					if (string.IsNullOrWhiteSpace(userId))
						return;

					var tokenStamp =
						context.Principal
							?.FindFirst("SecurityStamp")
							?.Value;

					if (string.IsNullOrWhiteSpace(tokenStamp))
						return;

					await using var scope =
						context.HttpContext
							.RequestServices
							.CreateAsyncScope();

					var db = scope.ServiceProvider
						.GetRequiredService<
							ApplicationDbContext>();

					var userIdInt = int.Parse(userId);
					var user = await db.Users
						.AsNoTracking()
						.FirstOrDefaultAsync(
							x => x.Id == userIdInt);

					if (user == null ||
						user.SecurityStamp != tokenStamp)
					{
						context.Fail(
							"SecurityStamp mismatch — " +
							"token revoked.");
					}
				}
			};
	});

// =========================================
// Authorization
// =========================================

builder.Services.AddAuthorization(options =>
{
	options.FallbackPolicy =
		new AuthorizationPolicyBuilder(
				JwtBearerDefaults.AuthenticationScheme)
			.RequireAuthenticatedUser()
			.Build();
});

var app = builder.Build();

// =========================================
// Database / Seeder
// =========================================

await DbSeeder.SeedAsync(
	app.Services,
	app.Configuration);

// =========================================
// HTTP Pipeline
// =========================================

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();

app.UseExceptionHandling();

app.Use(async (context, next) =>
{
	context.Response.Headers.Append(
		"X-Content-Type-Options", "nosniff");
	context.Response.Headers.Append(
		"X-Frame-Options", "DENY");
	context.Response.Headers.Append(
		"Referrer-Policy", "strict-origin-when-cross-origin");
	context.Response.Headers.Append(
		"Permissions-Policy",
		"camera=(), microphone=(), geolocation=()");

	await next();
});

app.UseStaticFiles();

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

app.UseRateLimiter();

// مهم جدًا:
// Authentication قبل Authorization
app.UseAuthentication();

app.UseAuthorization();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
	ResponseWriter = async (context, report) =>
	{
		context.Response.ContentType = "application/json";
		var result = System.Text.Json.JsonSerializer.Serialize(new
		{
			status = report.Status.ToString(),
			checks = report.Entries.Select(e => new
			{
				name = e.Key,
				status = e.Value.Status.ToString(),
				description = e.Value.Description
			})
		});
		await context.Response.WriteAsync(result);
	}
});

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();

static bool WantsJsonResponse(HttpRequest request)
{
	var accept = request.Headers.Accept.ToString();

	return accept.Contains(
			"application/json",
			StringComparison.OrdinalIgnoreCase) ||
		request.Path.StartsWithSegments(
			"/Account/Me",
			StringComparison.OrdinalIgnoreCase);
}