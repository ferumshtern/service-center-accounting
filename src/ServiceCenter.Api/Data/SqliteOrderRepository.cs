using Microsoft.EntityFrameworkCore;
using ServiceCenter.Api.Contracts;
using ServiceCenter.Api.Models;
using ServiceCenter.Api.Services;

namespace ServiceCenter.Api.Data;

// Реалізація контракту сховища для бази SQLite (ЛР №5). Контекст AppDbContext
// надає контейнер залежностей; сховище і контекст живуть у межах одного HTTP-запиту.
// Зміни потрапляють у базу лише після SaveChanges, тому записи доступні й після перезапуску сервера.
public sealed class SqliteOrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;

    public SqliteOrderRepository(AppDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<ServiceEngineer> GetEngineers() =>
        _db.Engineers.AsNoTracking().OrderBy(e => e.Id).ToList();

    // UC-01 (FR-01, FR-02, AC-01, AC-02)
    public (bool Success, string? ErrorCode, string? ErrorDetail, OrderReceiptDto? Order) CreateOrder(CreateOrderRequest req)
    {
        var missingFields = OrderRequestRules.FindMissingFields(req);
        if (missingFields is not null)
            return (false, "invalid_request", missingFields, null);

        var normalizedSerial = req.SerialNumber.Trim();

        // Транзакція SQLite блокує запис до її завершення: перевірка дубліката
        // і додавання замовлення виконуються як одна операція (як lock у v1.0).
        using var transaction = _db.Database.BeginTransaction();

        var engineer = _db.Engineers.Find(req.EngineerId);
        if (engineer is null)
            return (false, "engineer_not_found", null, null);

        // AC-02: чи є активне замовлення (не Issued) для цього серійного номера (без урахування регістру — NOCASE)
        var activeOrder = _db.Orders
            .Where(o => o.Status != OrderStatus.Issued &&
                        _db.Devices.Any(d => d.Id == o.DeviceId && d.SerialNumber == normalizedSerial))
            .Select(o => new { o.OrderNumber, o.Status })
            .FirstOrDefault();

        if (activeOrder is not null)
            return (false, "duplicate_active_order",
                $"Активне замовлення: {activeOrder.OrderNumber} (статус {activeOrder.Status}).", null);

        var client = new Client
        {
            Id = NextId(_db.Clients.Select(c => (int?)c.Id)),
            FullName = req.ClientFullName.Trim(),
            Phone = req.ClientPhone.Trim()
        };

        var device = new Device
        {
            Id = NextId(_db.Devices.Select(d => (int?)d.Id)),
            ClientId = client.Id,
            Brand = req.DeviceBrand.Trim(),
            Model = req.DeviceModel.Trim(),
            SerialNumber = normalizedSerial
        };

        var orderId = NextId(_db.Orders.Select(o => (int?)o.Id));
        var order = new RepairOrder(
            id: orderId,
            orderNumber: $"SC-{orderId:D4}",
            clientId: client.Id,
            deviceId: device.Id,
            engineerId: engineer.Id,
            issueDescription: req.IssueDescription.Trim(),
            acceptedAt: DateTimeOffset.UtcNow);

        _db.Clients.Add(client);
        _db.Devices.Add(device);
        _db.Orders.Add(order);
        _db.SaveChanges();
        transaction.Commit();

        return (true, null, null, OrderReceiptMapper.ToDto(order, client, device, engineer));
    }

    // UC-02 (FR-03, AC-03): правила перевіряє RepairOrder.AddRepairItem
    public (bool Success, string? ErrorCode, OrderReceiptDto? Order) AddRepairItem(int orderId, AddRepairItemRequest request)
    {
        if (request is null)
            return (false, "invalid_item_values", null);

        using var transaction = _db.Database.BeginTransaction();

        var order = _db.Orders.Find(orderId);
        if (order is null)
            return (false, "order_not_found", null);

        try
        {
            order.AddRepairItem(request.Name, request.Quantity, request.UnitPrice, request.DiagnosticNotes);
        }
        catch (DomainRuleException ex)
        {
            return (false, ex.Code, null);
        }

        _db.SaveChanges();
        transaction.Commit();
        return (true, null, MapToReceiptDto(order));
    }

    // UC-02 / UC-03 (FR-04, AC-04): правила переходу перевіряє RepairOrder.ChangeStatus
    public (bool Success, string? ErrorCode, OrderReceiptDto? Order) ChangeStatus(int orderId, ChangeStatusRequest req)
    {
        if (!OrderRequestRules.TryParseStatus(req.NewStatus, out var targetStatus))
            return (false, "invalid_status_value", null);

        using var transaction = _db.Database.BeginTransaction();

        var order = _db.Orders.Find(orderId);
        if (order is null)
            return (false, "order_not_found", null);

        try
        {
            order.ChangeStatus(targetStatus, DateTimeOffset.UtcNow);
        }
        catch (DomainRuleException ex)
        {
            return (false, ex.Code, null);
        }

        _db.SaveChanges();
        transaction.Commit();
        return (true, null, MapToReceiptDto(order));
    }

    // UC-04 (FR-05, AC-05)
    public (bool Success, string? ErrorCode, IReadOnlyList<OrderReceiptDto>? Orders) GetOrders(string? status, string? orderNumber)
    {
        IQueryable<RepairOrder> query = _db.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!OrderRequestRules.TryParseStatus(status, out var parsedStatus))
                return (false, "invalid_status_value", null);
            query = query.Where(o => o.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(orderNumber))
        {
            // LIKE у SQLite не враховує регістр латиниці — як OrdinalIgnoreCase у v1.0
            var pattern = $"%{orderNumber.Trim()}%";
            query = query.Where(o => EF.Functions.Like(o.OrderNumber, pattern));
        }

        var orders = query.OrderBy(o => o.Id).ToList();
        return (true, null, orders.Select(MapToReceiptDto).ToArray());
    }

    public OrderReceiptDto? GetOrderById(int orderId)
    {
        var order = _db.Orders.AsNoTracking().FirstOrDefault(o => o.Id == orderId);
        return order is null ? null : MapToReceiptDto(order);
    }

    // Спринт S1: історія ремонтів пристрою за серійним номером.
    // Кожне замовлення має власний запис Device, тому шукаються всі записи з цим номером (NOCASE).
    public (bool Success, string? ErrorCode, IReadOnlyList<OrderReceiptDto>? Orders) GetDeviceHistory(string serialNumber)
    {
        var serial = serialNumber?.Trim() ?? "";

        var deviceIds = _db.Devices.AsNoTracking()
            .Where(d => d.SerialNumber == serial)
            .Select(d => d.Id)
            .ToList();

        if (deviceIds.Count == 0)
            return (false, "device_not_found", null);

        var orders = _db.Orders.AsNoTracking()
            .Where(o => deviceIds.Contains(o.DeviceId))
            .OrderBy(o => o.Id)
            .ToList();
        return (true, null, orders.Select(MapToReceiptDto).ToArray());
    }

    private static int NextId(IQueryable<int?> ids) => (ids.Max() ?? 0) + 1;

    private OrderReceiptDto MapToReceiptDto(RepairOrder order) =>
        OrderReceiptMapper.ToDto(
            order,
            _db.Clients.AsNoTracking().Single(c => c.Id == order.ClientId),
            _db.Devices.AsNoTracking().Single(d => d.Id == order.DeviceId),
            _db.Engineers.AsNoTracking().Single(e => e.Id == order.EngineerId));
}
