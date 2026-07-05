using Azure.Monitor.OpenTelemetry.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Register body logging. Defaults log POST/PUT/PATCH bodies on 4xx/5xx responses;
// the options below are just to make the demo easy to poke at.
builder.Services.AddHttpBodyLogging(o =>
{
    o.MaxBytes = 100;
    o.DisableIpMasking = true;
});

// Export telemetry to Application Insights when a connection string is available.
// Guarded so the sample also runs locally without an Azure resource.
if (!string.IsNullOrEmpty(builder.Configuration["AzureMonitor:ConnectionString"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

var app = builder.Build();

app.UseHttpsRedirection();

// Register early, before endpoints (and before response compression, if used)
app.UseHttpBodyLogging();

// Echoes the order back; returns 400 (which triggers body logging) when "fail" is true
app.MapPost("/orders", (Order order) =>
    order.Fail
        ? Results.BadRequest(new { error = "Order rejected", order })
        : Results.Ok(order))
    .WithName("CreateOrder");

app.Run();

internal record Order(string Item, int Quantity, bool Fail, string? Password);
