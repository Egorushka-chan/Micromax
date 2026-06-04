using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using MicroMax.Server.Configuration;
using MicroMax.Server.Data;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Services;
using MicroMax.Server.Services.Api;
using MicroMax.Server.Services.Assistant.Configuration;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Execution;
using MicroMax.Server.Services.Assistant.Prompting;
using MicroMax.Server.Services.Assistant.Providers;
using MicroMax.Server.Services.Assistant.Recovery;
using MicroMax.Server.Services.Assistant.Registry;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".data-protection-keys")));

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/", AdminPanelAuthenticationDefaults.PolicyName);
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MicroMax API",
        Version = "v1",
        Description = "REST API информационной системы управления микроскладом."
    });
    options.SupportNonNullableReferenceTypes();
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT access token"
    });
    options.OperationFilter<AuthorizeOperationFilter>();
    options.OperationFilter<ProblemDetailsOperationFilter>();

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=micromax;Username=micromax;Password=micromax";

builder.Services.AddDbContext<MicroMaxDbContext>(options => options.UseNpgsql(connectionString));
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtOptions>(jwtSection);
builder.Services.AddScoped<IPasswordHasher<MicroMax.Server.Models.AppUser>, PasswordHasher<MicroMax.Server.Models.AppUser>>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<WarehousePermissionService>();
builder.Services.AddScoped<AdminPanelSignInService>();
builder.Services.AddScoped<IAuthorizationHandler, AdminPanelAuthorizationHandler>();
builder.Services.AddScoped<WarehousesApiService>();
builder.Services.AddScoped<WarehouseUsersApiService>();
builder.Services.AddScoped<ZonesApiService>();
builder.Services.AddScoped<CellsApiService>();
builder.Services.AddScoped<ProductsApiService>();
builder.Services.AddScoped<StocksApiService>();
builder.Services.AddScoped<OperationsApiService>();
builder.Services.AddScoped<RolesApiService>();
builder.Services.AddScoped<UsersApiService>();
builder.Services.AddScoped<AssistantCommandExecutionService>();
builder.Services.AddScoped<AssistantApiService>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddCookie(AdminPanelAuthenticationDefaults.Scheme, options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    })
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || jwtOptions.SecretKey.Length < 32)
        {
            jwtOptions.SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                {
                    context.Fail("Неверный токен пользователя.");
                    return;
                }

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<MicroMaxDbContext>();
                var isActiveUser = await dbContext.AppUsers.AnyAsync(x => x.Id == userId && x.IsActive);
                if (!isActiveUser)
                {
                    context.Fail("Пользователь отключен или не найден.");
                }
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminPanelAuthenticationDefaults.PolicyName, policy =>
    {
        policy.AuthenticationSchemes.Add(AdminPanelAuthenticationDefaults.Scheme);
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new AdminPanelRequirement());
    });
});
builder.Services.AddScoped<WarehouseOperationService>();
builder.Services.Configure<AiAssistantOptions>(builder.Configuration.GetSection("Assistant"));
builder.Services.AddSingleton<AiCommandRegistry>();
builder.Services.AddSingleton<AiProviderAvailability>();
builder.Services.AddSingleton<AiCommandPromptBuilder>();
builder.Services.AddSingleton<AiCommandRules>();
builder.Services.AddSingleton<AiCommandNormalizer>();
builder.Services.AddScoped<IAiCommandProvider, OpenAiAiCommandProvider>();
builder.Services.AddScoped<IAiCommandProvider, OllamaAiCommandProvider>();
builder.Services.AddScoped<IAiCommandProvider, MockAiCommandProvider>();
builder.Services.AddScoped<AiProviderSelector>();
builder.Services.AddScoped<AssistantService>();
builder.Services.AddHostedService<AiProviderRecoveryService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MicroMaxDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<MicroMax.Server.Models.AppUser>>();
    await DatabaseBootstrapper.InitializeAsync(db, passwordHasher);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase),
    api =>
    {
        api.UseMiddleware<ApiExceptionHandlingMiddleware>();
    });

app.UseRouting();
app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;
    if (!httpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    if (httpContext.Response.HasStarted ||
        httpContext.Response.ContentLength is > 0 ||
        !string.IsNullOrWhiteSpace(httpContext.Response.ContentType))
    {
        return;
    }

    var problemDetails = CreateProblemDetailsForStatusCode(httpContext.Response.StatusCode, httpContext.Request.Path);
    if (problemDetails is null)
    {
        return;
    }

    httpContext.Response.ContentType = "application/problem+json";
    var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
    await problemDetailsService.WriteAsync(new ProblemDetailsContext
    {
        HttpContext = httpContext,
        ProblemDetails = problemDetails
    });
});
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapControllers();

app.Run();

static ProblemDetails? CreateProblemDetailsForStatusCode(int statusCode, PathString path)
{
    return statusCode switch
    {
        StatusCodes.Status401Unauthorized => new ProblemDetails
        {
            Status = statusCode,
            Title = "Требуется аутентификация",
            Detail = "Для доступа к ресурсу требуется действующий токен.",
            Instance = path
        },
        StatusCodes.Status403Forbidden => new ProblemDetails
        {
            Status = statusCode,
            Title = "Доступ запрещен",
            Detail = "У пользователя недостаточно прав для выполнения запроса.",
            Instance = path
        },
        StatusCodes.Status404NotFound => new ProblemDetails
        {
            Status = statusCode,
            Title = "Ресурс не найден",
            Detail = "Запрошенный маршрут или ресурс не найден.",
            Instance = path
        },
        _ => null
    };
}

public partial class Program;
