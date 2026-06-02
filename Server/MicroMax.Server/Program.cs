using System.Text.Json.Serialization;
using MicroMax.Server.Api;
using MicroMax.Server.Data;
using MicroMax.Server.Services;
using MicroMax.Server.Services.Assistant;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
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
builder.Services.AddSingleton<AiProviderAvailability>();
builder.Services.AddSingleton<AiCommandPromptBuilder>();
builder.Services.AddSingleton<AiCommandNormalizer>();
builder.Services.AddScoped<IAiCommandProvider, OllamaAiCommandProvider>();
builder.Services.AddScoped<IAiCommandProvider, OpenAiAiCommandProvider>();
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
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapMicroMaxApi();

app.Run();
