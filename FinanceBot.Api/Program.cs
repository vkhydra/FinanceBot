using System.Text;
using FinanceBot.Application;
using FinanceBot.Application.Contracts;
using FinanceBot.Infrastructure;
using FinanceBot.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

ConfigureSharedSettings(builder.Configuration, builder.Environment.ContentRootPath, builder.Environment.EnvironmentName);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [securityScheme] = Array.Empty<string>()
    });
});
builder.Services.AddApplication();
builder.Services.Configure<BillingOptions>(builder.Configuration.GetSection(BillingOptions.SectionName));
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Key), "Jwt:Key deve ser configurado.")
    .ValidateOnStart();
builder.Services.AddInfrastructure();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
builder.Services.AddAuthorization();

var app = builder.Build();

await app.Services.ApplyMigrationsAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseAuthentication();
app.UseMiddleware<CurrentUserContextMiddleware>();
app.UseAuthorization();

app.MapGet("/", () => "API FinanceBot Rodando!");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();

static void ConfigureSharedSettings(IConfigurationBuilder configuration, string contentRootPath, string environmentName)
{
    var solutionRoot = TryFindSolutionRoot(contentRootPath);
    if (solutionRoot is null)
    {
        return;
    }

    configuration
        .AddJsonFile(Path.Combine(solutionRoot, "appsettings.json"), optional: true, reloadOnChange: false)
        .AddJsonFile(Path.Combine(solutionRoot, $"appsettings.{environmentName}.json"), optional: true, reloadOnChange: false)
        .AddUserSecrets<Program>(optional: true)
        .AddEnvironmentVariables();
}

static string? TryFindSolutionRoot(string startPath)
{
    var directory = new DirectoryInfo(startPath);

    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "FinanceBot.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return null;
}

public partial class Program;

file sealed class ConfigureJwtBearerOptions : IPostConfigureOptions<JwtBearerOptions>
{
    private readonly IOptions<JwtOptions> _jwtOptions;

    public ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions;
    }

    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        var jwtOptions = _jwtOptions.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }
}
