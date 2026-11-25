using ServiceCenter.Api.Contracts;
using ServiceCenter.Api.Models;

namespace ServiceCenter.Api.Services;

// Контракт сховища замовлень (ЛР №5). Обробники HTTP-запитів працюють лише з ним
// і не знають, де зберігаються дані: у пам'яті (InMemoryOrderRepository) чи в базі.
// Обидві реалізації повертають однакові результати й коди відмов.
public interface IOrderRepository
{
    IReadOnlyList<ServiceEngineer> GetEngineers();

    // UC-01 (FR-01, FR-02): ErrorDetail уточнює відмову (пропущені поля або номер активного замовлення).
    (bool Success, string? ErrorCode, string? ErrorDetail, OrderReceiptDto? Order) CreateOrder(CreateOrderRequest req);

    // UC-02 (FR-03)
    (bool Success, string? ErrorCode, OrderReceiptDto? Order) AddRepairItem(int orderId, AddRepairItemRequest request);

    // UC-02 / UC-03 (FR-04)
    (bool Success, string? ErrorCode, OrderReceiptDto? Order) ChangeStatus(int orderId, ChangeStatusRequest req);

    // UC-04 (FR-05)
    (bool Success, string? ErrorCode, IReadOnlyList<OrderReceiptDto>? Orders) GetOrders(string? status, string? orderNumber);

    OrderReceiptDto? GetOrderById(int orderId);

    // Спринт S1: історія ремонтів пристрою за серійним номером (від найстарішого замовлення).
    // device_not_found — пристрій з таким серійним номером ще не обслуговувався.
    (bool Success, string? ErrorCode, IReadOnlyList<OrderReceiptDto>? Orders) GetDeviceHistory(string serialNumber);
}
