using ServiceCenter.Api.Contracts;
using ServiceCenter.Api.Services;
using Xunit;

namespace ServiceCenter.Tests;

public sealed class ServiceStoreTests
{
    private static CreateOrderRequest ValidOrderRequest(string serial = "SN-TEST-001") =>
        new(
            ClientFullName: "Лавренюк Серафим",
            ClientPhone: "+380991112233",
            DeviceBrand: "Samsung",
            DeviceModel: "Galaxy S24",
            SerialNumber: serial,
            IssueDescription: "Не заряджається",
            EngineerId: 1);

    //AC-01: Створення замовлення з валідними даними
    [Fact]
    public void CreateOrder_ValidData_CreatesAcceptedOrderWithZeroCost_AC01()
    {
        var store = new ServiceStore();

        var (success, code, _, order) = store.CreateOrder(ValidOrderRequest());

        Assert.True(success);
        Assert.Null(code);
        Assert.NotNull(order);
        Assert.Equal("SC-0001", order!.OrderNumber);
        Assert.Equal("Accepted", order.Status);
        Assert.Equal(0m, order.TotalCost);
        Assert.Single(order.History);
    }

    //AC-02: Блокування дубліката активного замовлення з тим самим серійним номером
    [Fact]
    public void CreateOrder_DuplicateActiveSerialNumber_ReturnsConflictAndKeepsSingleOrder_AC02()
    {
        var store = new ServiceStore();
        store.CreateOrder(ValidOrderRequest("SN-DUP-100"));

        var (success, code, detail, duplicate) = store.CreateOrder(ValidOrderRequest("SN-DUP-100"));

        Assert.False(success);
        Assert.Equal("duplicate_active_order", code);
        Assert.Null(duplicate);
        Assert.Contains("SC-0001", detail); // UC-01 A2: повідомлення містить номер активного замовлення
        var (_, _, allOrders) = store.GetOrders(null, null);
        Assert.Single(allOrders!);
    }
    //AC-07: Перевірка валідації порожніх обов'язкових полів
    [Fact]
    public void CreateOrder_EmptyRequiredFields_ReturnsInvalidRequest_AC07()
    {
        var store = new ServiceStore();
        var invalidReq = ValidOrderRequest() with { ClientFullName = "   " };

        var (success, code, detail, _) = store.CreateOrder(invalidReq);

        Assert.False(success);
        Assert.Equal("invalid_request", code);
        Assert.Contains("ПІБ клієнта", detail); // UC-01 A1: повідомлення називає пропущене поле
    }

    //AC-05: Пошук замовлення та фільтрація
    [Fact]
    public void GetOrders_FilterByStatusAndNumber_ReturnsMatchingOrders_AC05()
    {
        var store = new ServiceStore();
        var (_, _, _, order1) = store.CreateOrder(ValidOrderRequest("SN-001"));
        store.CreateOrder(ValidOrderRequest("SN-002")); 

        var (ok, _, acceptedOrders) = store.GetOrders("Accepted", "SC-0001");

        Assert.True(ok);
        Assert.Single(acceptedOrders!);
        Assert.Equal("SC-0001", acceptedOrders![0].OrderNumber);
    }

}