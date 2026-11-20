using System.Text.Json;
using ServiceCenter.Api.Contracts;
using ServiceCenter.Api.Infrastructure;
using ServiceCenter.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// Реєструємо наше сховище в пам'яті як Singleton (існує протягом роботи серверного процесу)
builder.Services.AddSingleton<ServiceStore>();

builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

var app = builder.Build();

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
app.MapGet("/api/engineers", (ServiceStore store) => Results.Ok(store.GetEngineers()));

// UC-04: Перелік замовлень з фільтрацією за статусом та пошуком за номером (FR-05, AC-05)
app.MapGet("/api/orders", (string? status, string? orderNumber, ServiceStore store) =>
{
    var (success, errorCode, orders) = store.GetOrders(status, orderNumber);
    return success ? Results.Ok(orders) : ApiErrors.Result(errorCode!);
});

// Отримання картки замовлення / електронної квитанції за ID (FR-05, AC-05)
app.MapGet("/api/orders/{id:int}", (int id, ServiceStore store) =>
{
    var order = store.GetOrderById(id);
    return order is not null ? Results.Ok(order) : ApiErrors.Result("order_not_found");
});

// UC-01: Реєстрація нового замовлення та призначення майстра (FR-01, FR-02, AC-01, AC-02)
app.MapPost("/api/orders", (CreateOrderRequest request, ServiceStore store) =>
{
    var (success, errorCode, errorDetail, order) = store.CreateOrder(request);
    return success
        ? Results.Created($"/api/orders/{order!.Id}", order)
        : ApiErrors.Result(errorCode!, errorDetail);
});


app.Run();

public partial class Program { }