using ServiceCenter.Api.Contracts;
using ServiceCenter.Api.Services;
using Xunit;

namespace ServiceCenter.Tests;

public sealed class InMemoryOrderRepositoryTests
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
        var store = new InMemoryOrderRepository();

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
        var store = new InMemoryOrderRepository();
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
        var store = new InMemoryOrderRepository();
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
        var store = new InMemoryOrderRepository();
        var (_, _, _, order1) = store.CreateOrder(ValidOrderRequest("SN-001"));
        store.CreateOrder(ValidOrderRequest("SN-002")); 

        var (ok, _, acceptedOrders) = store.GetOrders("Accepted", "SC-0001");

        Assert.True(ok);
        Assert.Single(acceptedOrders!);
        Assert.Equal("SC-0001", acceptedOrders![0].OrderNumber);
    }

    // 5. UC-02 (FR-03, AC-03, AC-07): Додавання позицій ремонту, автопідрахунок TotalCost та відмова при від'ємній ціні
    [Fact]
    public void AddRepairItem_ValidItems_CalculatesTotalCostAndRejectsNegativePrice_AC03()
    {
        var store = new InMemoryOrderRepository();
        var (_, _, _, created) = store.CreateOrder(ValidOrderRequest());
        store.ChangeStatus(created!.Id, new ChangeStatusRequest("InProgress"));

        // Додавання двох валідних позицій та перевірка автоматичного перерахунку суми (600 + 800 = 1400)
        store.AddRepairItem(created.Id, new AddRepairItemRequest("Заміна роз'єму", "Роз'єм Type-C", 1, 600m));
        var (ok2, _, updated) = store.AddRepairItem(created.Id, new AddRepairItemRequest(null, "Робота майстра", 1, 800m));

        Assert.True(ok2);
        Assert.Equal(1400m, updated!.TotalCost);

        // Спроба додати позицію з від'ємною ціною (відмова без зміни TotalCost)
        var (badOk, badCode, _) = store.AddRepairItem(created.Id, new AddRepairItemRequest(null, "Помилка", 1, -100m));
        Assert.False(badOk);
        Assert.Equal("invalid_item_values", badCode);
        Assert.Equal(1400m, store.GetOrderById(created.Id)!.TotalCost);
    }

    // 6. UC-02 & UC-03 (FR-04, AC-04, AC-07): Контроль послідовності статусів та блокування редагування після видачі (Issued)
    [Fact]
    public void ChangeStatus_InvalidTransitionOrEditingIssuedOrder_ReturnsConflict_AC04()
    {
        var store = new InMemoryOrderRepository();
        var (_, _, _, created) = store.CreateOrder(ValidOrderRequest());

        // Спроба перескочити з Accepted одразу в Issued (відмова без зміни статусу)
        var (skipOk, skipCode, _) = store.ChangeStatus(created!.Id, new ChangeStatusRequest("Issued"));
        Assert.False(skipOk);
        Assert.Equal("invalid_status_transition", skipCode);
        Assert.Equal("Accepted", store.GetOrderById(created.Id)!.Status);

        // Послідовний перехід за життєвим циклом до Issued
        store.ChangeStatus(created.Id, new ChangeStatusRequest("InProgress"));
        store.AddRepairItem(created.Id, new AddRepairItemRequest("Діагностика", "Чистка", 1, 500m));
        store.ChangeStatus(created.Id, new ChangeStatusRequest("Ready"));
        store.ChangeStatus(created.Id, new ChangeStatusRequest("Issued"));

        // Спроба додати роботу після видачі (Issued) — відмова без зміни TotalCost
        var (editIssuedOk, editCode, _) = store.AddRepairItem(created.Id, new AddRepairItemRequest(null, "Деталь", 1, 200m));
        Assert.False(editIssuedOk);
        Assert.Equal("order_already_issued", editCode);
        Assert.Equal(500m, store.GetOrderById(created.Id)!.TotalCost);
    }

    // Спринт S1: історія ремонтів пристрою за серійним номером
    [Fact]
    public void GetDeviceHistory_ReturnsAllOrdersOfDeviceAndRejectsUnknownSerial_S1()
    {
        var store = new InMemoryOrderRepository();
        var (_, _, _, first) = store.CreateOrder(ValidOrderRequest("SN-HIST-001"));
        store.ChangeStatus(first!.Id, new ChangeStatusRequest("InProgress"));
        store.ChangeStatus(first.Id, new ChangeStatusRequest("Ready"));
        store.ChangeStatus(first.Id, new ChangeStatusRequest("Issued"));
        var (_, _, _, second) = store.CreateOrder(ValidOrderRequest("sn-hist-001"));
        store.CreateOrder(ValidOrderRequest("SN-OTHER-002"));

        var (success, _, history) = store.GetDeviceHistory("SN-HIST-001");

        Assert.True(success);
        Assert.NotNull(history);
        Assert.Equal(2, history!.Count);
        Assert.Equal(first.OrderNumber, history[0].OrderNumber);
        Assert.Equal("Issued", history[0].Status);
        Assert.Equal(second!.OrderNumber, history[1].OrderNumber);

        var (unknownSuccess, unknownCode, unknownOrders) = store.GetDeviceHistory("SN-UNKNOWN-0000");
        Assert.False(unknownSuccess);
        Assert.Equal("device_not_found", unknownCode);
        Assert.Null(unknownOrders);
    }

    // Спринт S2 (#8): пошук замовлень за фрагментом телефону клієнта
    [Fact]
    public void GetOrders_FilterByPhoneFragment_ReturnsClientOrdersOrEmptyList_S2()
    {
        var store = new InMemoryOrderRepository();
        store.CreateOrder(ValidOrderRequest("SN-PHONE-001"));
        store.CreateOrder(ValidOrderRequest("SN-PHONE-002"));
        store.CreateOrder(ValidOrderRequest("SN-PHONE-003") with { ClientPhone = "+380670000000" });

        var (success, _, found) = store.GetOrders(null, null, "991112233");
        var (_, _, none) = store.GetOrders(null, null, "111111111");

        Assert.True(success);
        Assert.Equal(2, found!.Count);
        Assert.Empty(none!);
    }
}
