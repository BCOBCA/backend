using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using BCOBCA.API.Middlewares;
using BCOBCA.API.Services;
using BCOBCA.API.Extensions;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.Interfaces;
using BCOBCA.BLL.Services;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Interfaces;
using BCOBCA.DAL.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

EnvironmentBootstrap.Load();
var builder = WebApplication.CreateBuilder(args);
builder.ConfigureEnvironmentHosting();
var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32 || signingKey.Contains("REPLACE", StringComparison.OrdinalIgnoreCase) || signingKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Configure Jwt__SigningKey with at least 32 random bytes.");
var connection = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Configure ConnectionStrings__Default.");
foreach (var key in new[] { "Jwt:Issuer", "Jwt:Audience" })
    if (string.IsNullOrWhiteSpace(builder.Configuration[key])) throw new InvalidOperationException($"Configure {key.Replace(":", "__")}.");
foreach (var key in new[] { "Jwt:AccessTokenMinutes", "Jwt:RefreshTokenDays", "Auth:RateLimitPermit", "Auth:RateLimitWindowSeconds", "Checkout:ReservationHours", "Inventory:SweepIntervalSeconds" })
    if (builder.Configuration.GetValue<int>(key) <= 0) throw new InvalidOperationException($"Configure positive {key.Replace(":", "__")}.");
foreach (var key in new[] { "Membership:SpendPerPoint", "Affiliate:ApprovalHoldDays", "Settlement:PlatformFeeRate", "Settlement:HoldDays" })
    if (builder.Configuration[key] is null || builder.Configuration.GetValue<decimal>(key) < 0) throw new InvalidOperationException($"Configure nonnegative {key.Replace(":", "__")}.");
var platformFeeRate = builder.Configuration.GetValue<decimal>("Settlement:PlatformFeeRate");
if (platformFeeRate > 1 || decimal.Round(platformFeeRate, 4) != platformFeeRate) throw new InvalidOperationException("Settlement__PlatformFeeRate must be a fraction between 0 and 1 with at most four decimal places.");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddScoped<ICommerceStore, CommerceStore>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddScoped<ILoyaltyConfigurationService, LoyaltyConfigurationService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAccountSecurityService, AccountSecurityService>();
builder.Services.AddScoped<IAdminCatalogService, AdminCatalogService>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IRefundSettlementService, RefundSettlementService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IAdminOperationsService, AdminOperationsService>();
builder.Services.AddHostedService<ReservationExpiryWorker>();
builder.Services.AddHttpClient<CloudinaryImageService>(client => client.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddValidatorsFromAssemblyContaining<RegisterValidator>();
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
    new BadRequestObjectResult(new ApiResponse<object>(false, "Dữ liệu không hợp lệ", null,
        context.ModelState.ToDictionary(x => x.Key, x => x.Value!.Errors.Select(_ => "Giá trị không hợp lệ").ToArray()))));
builder.Services.AddBCOBCAOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("Jwt:ClockSkewSeconds")), NameClaimType = JwtRegisteredClaimNames.Sub
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var accountId) || !Guid.TryParse(context.Principal?.FindFirst("sid")?.Value, out var familyId)) { context.Fail("Invalid session"); return; }
            var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var now = DateTimeOffset.UtcNow;
            var valid = await db.Accounts.AnyAsync(x => x.Id == accountId && x.Status == 1, context.HttpContext.RequestAborted)
                && await db.RefreshTokens.AnyAsync(x => x.AccountId == accountId && x.FamilyId == familyId && x.RevokedAt == null && x.ExpiresAt > now, context.HttpContext.RequestAborted);
            if (!valid) context.Fail("Session revoked");
        },
        OnChallenge = async context => { context.HandleResponse(); context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new ApiResponse<object>(false, "Vui lòng đăng nhập", null)); },
        OnForbidden = async context => { context.Response.StatusCode = 403; await context.Response.WriteAsJsonAsync(new ApiResponse<object>(false, "Không có quyền truy cập", null)); }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("image-upload", context => RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue<int>("Auth:RateLimitPermit"), Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("Auth:RateLimitWindowSeconds")), QueueLimit = 0 }));
    options.OnRejected = async (context, ct) => await context.HttpContext.Response.WriteAsJsonAsync(new ApiResponse<object>(false, "Quá nhiều yêu cầu. Vui lòng thử lại sau", null), ct);
});
var app = builder.Build();
if (args.Contains("--provision-superadmin", StringComparer.Ordinal))
{
    await app.ProvisionFirstSuperAdminAsync(CancellationToken.None);
    return;
}
app.UseMiddleware<ExceptionMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/api/health", async (ApplicationDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(ApiResponse<object>.Ok(new { status = "healthy" })) : Results.Json(new ApiResponse<object>(false, "Database không khả dụng", null), statusCode: 503));
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "BCOBCA API"));
}
app.Run();
public partial class Program;
