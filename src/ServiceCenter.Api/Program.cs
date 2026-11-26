using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ServiceCenter.Api.Contracts;
using ServiceCenter.Api.Data;
using ServiceCenter.Api.Infrastructure;
using ServiceCenter.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// Постійне збереження: контекст бази та сховище SQLite створюються контейнером залежностей окремо для кожного HTTP-запиту
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(DatabaseConnection.Resolve(builder.Configuration)));
builder.Services.AddScoped<IOrderRepository, SqliteOrderRepository>();
builder.Services.AddScoped<IWarrantyService, WarrantyService>();

builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

var app = builder.Build();

// Сервер не стартує з базою, до якої не застосовано міграції.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.GetPendingMigrations().Any())
        throw new InvalidOperationException(
            "База даних не створена або застаріла. Виконайте: dotnet ef database update --project src/ServiceCenter.Api");
}

app.UseDefaultFiles();
app.UseStaticFiles();
// Глобальне перехоплення некоректного JSON (NFR-02, AC-07)
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (BadHttpRequestException)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new ApiError("invalid_request", "Некоректний формат тіла запиту."));
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Отримання переліку сервісних інженерів
app.MapGet("/api/engineers", (IOrderRepository store) => Results.Ok(store.GetEngineers()));

// UC-04: Перелік замовлень з фільтрацією за статусом та пошуком за номером (FR-05, AC-05); S2: пошук за телефоном
app.MapGet("/api/orders", (string? status, string? orderNumber, string? phone, IOrderRepository store) =>
{
    var (success, errorCode, orders) = store.GetOrders(status, orderNumber, phone);
    return success ? Results.Ok(orders) : ApiErrors.Result(errorCode!);
});

// Отримання картки замовлення / електронної квитанції за ID (FR-05, AC-05)
app.MapGet("/api/orders/{id:int}", (int id, IOrderRepository store) =>
{
    var order = store.GetOrderById(id);
    return order is not null ? Results.Ok(order) : ApiErrors.Result("order_not_found");
});

// UC-01: Реєстрація нового замовлення та призначення майстра (FR-01, FR-02, AC-01, AC-02)
app.MapPost("/api/orders", (CreateOrderRequest request, IOrderRepository store) =>
{
    var (success, errorCode, errorDetail, order) = store.CreateOrder(request);
    return success
        ? Results.Created($"/api/orders/{order!.Id}", order)
        : ApiErrors.Result(errorCode!, errorDetail);
});

// UC-02: Додавання результатів діагностики та позицій ремонту (FR-03, AC-03)
app.MapPost("/api/orders/{id:int}/items", (int id, AddRepairItemRequest request, IOrderRepository store) =>
{
    var (success, errorCode, order) = store.AddRepairItem(id, request);
    return success ? Results.Ok(order) : ApiErrors.Result(errorCode!);
});

// UC-02 & UC-03: Переведення статусу замовлення (FR-04, AC-04)
app.MapPatch("/api/orders/{id:int}/status", (int id, ChangeStatusRequest request, IOrderRepository store) =>
{
    var (success, errorCode, order) = store.ChangeStatus(id, request);
    return success ? Results.Ok(order) : ApiErrors.Result(errorCode!);
});

// Спринт S1: історія ремонтів пристрою за серійним номером
app.MapGet("/api/devices/{serialNumber}/orders", (string serialNumber, IOrderRepository store) =>
{
    var (success, errorCode, orders) = store.GetDeviceHistory(serialNumber);
    return success ? Results.Ok(orders) : ApiErrors.Result(errorCode!);
});

// Спринт S2: гарантійне звернення за виданим замовленням
app.MapPost("/api/orders/{id:int}/warranty-claims", (int id, WarrantyClaimRequest request, IWarrantyService warranty) =>
{
    var (success, errorCode, errorDetail, order) = warranty.CreateClaim(id, request);
    return success
        ? Results.Created($"/api/orders/{order!.Id}", order)
        : ApiErrors.Result(errorCode!, errorDetail);
});

app.Run();

public partial class Program { }