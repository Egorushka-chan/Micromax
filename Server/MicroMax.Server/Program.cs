using System.Text.Json.Serialization;
using MicroMax.Server.Data;
using MicroMax.Server.Services;
using MicroMax.Server.Services.Assistant.Configuration;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Execution;
using MicroMax.Server.Services.Assistant.Prompting;
using MicroMax.Server.Services.Assistant.Providers;
using MicroMax.Server.Services.Assistant.Recovery;
using MicroMax.Server.Services.Assistant.Registry;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
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
    await DemoDataSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapControllers();

app.Run();
