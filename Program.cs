using Microsoft.EntityFrameworkCore;
using FinanceBot.Data;
using FinanceBot.Models;
using System.Globalization;
using FinanceBot.Services;
using FinanceBot.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<FinanceService>();

var app = builder.Build();

app.MapGet("/", () => "API FinanceBot Rodando!");

app.MapFinanceEntries();

app.Run();