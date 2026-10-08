using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HireYar.Api.Errors;
using HireYar.Api.OpenApi;
using HireYar.Api.Security;
using HireYar.Application.Auth;
using HireYar.Application.Common;
using HireYar.Application.Dashboard;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.JobPositions;
using HireYar.Application.Organizations;
using HireYar.Infrastructure;
using HireYar.Infrastructure.Auth;
using HireYar.Application.Interviews;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
var builder = WebApplication.CreateBuilder(args);
// Developer-specific settings stay outside Git; environment variables retain final precedence.
if (!builder.Configuration.GetValue<bool>("SkipLocalSettings"))
    builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
var security = builder.Configuration.GetSection("Security").Get<SecurityOptions>() ?? new();
if (Encoding.UTF8.GetByteCount(security.SigningKey) < 32 || string.IsNullOrWhiteSpace(security.Issuer) || string.IsNullOrWhiteSpace(security.Audience)
    || security.AccessMinutes is < 1 or > 60 || security.RefreshDays is < 1 or > 30)
    throw new InvalidOperationException("Security requires a signing key of at least 32 bytes, issuer, audience and valid token lifetimes.");
static bool IsOrigin(string value, bool development) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || development && uri.Scheme == "http") && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
if (!IsOrigin(security.FrontendUrl, builder.Environment.IsDevelopment()) || security.AllowedOrigins.Length == 0 || security.AllowedOrigins.Any(x => !IsOrigin(x, builder.Environment.IsDevelopment()) || x.EndsWith('/')))
    throw new InvalidOperationException("Security requires explicit frontend and CORS origins; HTTPS is mandatory outside Development and CORS origins must omit trailing slash.");
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Default"))) throw new InvalidOperationException("ConnectionStrings:Default is required.");
if (string.IsNullOrWhiteSpace(security.DataProtectionPath)) throw new InvalidOperationException("Security:DataProtectionPath is required.");
builder.Services.AddDataProtection().SetApplicationName("HireYar").PersistKeysToFileSystem(new DirectoryInfo(security.DataProtectionPath));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, CurrentUser>(); builder.Services.AddScoped<CurrentAccountValidation>();
builder.Services.AddScoped<ICurrentInterviewAccess, CurrentInterviewAccess>();
builder.Services.AddScoped<PanelWizardService>();
builder.Services.AddScoped<AuthService>(); builder.Services.AddScoped<JobService>(); builder.Services.AddScoped<TemplateService>(); builder.Services.AddScoped<OrganizationService>(); builder.Services.AddScoped<DashboardService>(); builder.Services.AddScoped<CsrfFilter>();
builder.Services.AddControllers().AddJsonOptions(o => { o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)); o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow; });
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = c =>
{
    var problem = new ValidationProblemDetails(c.ModelState) { Status = 400, Title = "Request validation failed." };
    problem.Extensions["code"] = "validation_failed"; problem.Extensions["traceId"] = c.HttpContext.TraceIdentifier;
    return new BadRequestObjectResult(problem);
});
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
{
    c.ProblemDetails.Extensions.TryAdd("traceId", c.HttpContext.TraceIdentifier);
    c.ProblemDetails.Extensions.TryAdd("code", c.ProblemDetails.Status switch { 401 => "unauthorized", 403 => "forbidden", 404 => "not_found", 429 => "rate_limited", _ => "request_failed" });
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false; o.EventsType = typeof(CurrentAccountValidation);
    o.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidIssuer = security.Issuer,
        ValidAudience = security.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(security.SigningKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(5),
        NameClaimType = "sub",
        RoleClaimType = "role"
    };
});
builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, InterviewAuthenticationHandler>(InterviewAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(o => {
    o.FallbackPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build();
    o.AddPolicy("SystemAdmin", p => p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireClaim("system_admin", "true"));
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(security.AllowedOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials()));
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "hireyar.csrf"; o.Cookie.HttpOnly = true; o.Cookie.Path = "/api/auth"; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always; });
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("candidate", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue<int?>("CandidateRateLimit:PermitLimit") ?? 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue<int?>("AuthRateLimit:PermitLimit") ?? 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddOpenApi(o => o.AddDocumentTransformer((doc, context, ct) =>
{
    doc.Info.Title = "HireYar API"; doc.Info.Version = "v1"; doc.Info.Description = OpenApiText.Introduction;
    doc.Components ??= new(); doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" };
    doc.Components.SecuritySchemes["InterviewToken"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", Description = "Six ASCII digit access code scoped to the interview ID in the route. Legacy opaque invitation tokens remain supported. Cannot access HR endpoints." };
    return Task.CompletedTask;
}).AddOperationTransformer(async (operation, context, ct) =>
{
    var routeValues = context.Description.ActionDescriptor.RouteValues;
    routeValues.TryGetValue("controller", out var controller);
    routeValues.TryGetValue("action", out var action);
    operation.Summary = OpenApiText.Summary(controller, action);
    if (controller == "CandidateInterviews" && action is "Answer" or "Introduction")
    {
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object, Required = action == "Introduction" ? new HashSet<string> { "audio", "clientTurnId" } : new HashSet<string> { "questionId", "audio" },
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["questionId"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                            ["audio"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary", Description = "Required audio file; default maximum 20 MiB. See mediaCapabilities for configured limits." },
                            ["durationSeconds"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Description = "Client-observed duration, default maximum 600 seconds; does not determine billing." },
                            ["lastSpeechEndUtc"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time", Description = "UTC timestamp of the last detected speech, excluding the silence wait." },
                            ["clientTurnId"] = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 128, Description = "Stable identifier for one turn, unique within the interview. Reuse for transport retries; another question with the same identifier returns 409." },
                            ["video"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary", Description = "Optional WebM/MP4 video, default maximum 40 MiB; only record when mediaCapabilities.videoAnswers is true." }
                        }
                    }
                }
            }
        };
        if (action == "Introduction") operation.RequestBody.Content!["multipart/form-data"].Schema!.Properties!.Remove("questionId");
    }
    if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(controller == "CandidateInterviews" ? "InterviewToken" : "Bearer", context.Document)] = [] }];
    if (context.Description.RelativePath?.StartsWith("api/auth", StringComparison.Ordinal) == true && context.Description.HttpMethod != "GET")
    {
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter { Name = "X-CSRF-TOKEN", In = ParameterLocation.Header, Required = true, Description = "Obtain from GET /api/auth/csrf using the same bearer identity and cookie jar.", Schema = new OpenApiSchema { Type = JsonSchemaType.String } });
    }
    var schema = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, ct);
    operation.Responses ??= new();
    foreach (var status in new[] { "400", "401", "403", "404", "409", "413", "429", "500", "503" })
        operation.Responses.TryAdd(status, new OpenApiResponse { Description = "ProblemDetails with code and traceId; validation errors include errors.", Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() { Schema = schema } } });
}));
var app = builder.Build();
if (!app.Environment.IsDevelopment())
    app.Use((context, next) =>
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) && proto.Count == 1 && string.Equals(proto[0], "https", StringComparison.OrdinalIgnoreCase))
            context.Request.Scheme = Uri.UriSchemeHttps;
        return next(context);
    });
app.UseExceptionHandler(); app.UseStatusCodePages();
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.Use(async (context, next) => { context.Response.Headers.CacheControl = "no-store"; context.Response.Headers.XContentTypeOptions = "nosniff"; await next(); });
// Swagger UI is middleware, so serve it before fallback authorization in every environment.
app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "HireYar v1"));
// IP limits also cover failed candidate authentication attempts.
app.UseRouting(); app.UseCors(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllers(); app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapOpenApi().AllowAnonymous();
app.Run();
public partial class Program;
