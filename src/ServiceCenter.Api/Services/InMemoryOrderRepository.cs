using ServiceCenter.Api.Contracts;
using ServiceCenter.Api.Models;

namespace ServiceCenter.Api.Services;

// Реалізація контракту сховища в пам'яті серверного процесу (v1.0.0).
// Реєструється як Singleton, щоб дані були спільними для всіх запитів; 
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly object _gate = new();

    private readonly List<ServiceEngineer> _engineers = new()
    {
        new ServiceEngineer { Id = 1, FullName = "Коваленко Олександр", Specialization = "Смартфони та планшети" },
        new ServiceEngineer { Id = 2, FullName = "Мельник Ірина", Specialization = "Ноутбуки та материнські плати" }
    };

    private readonly List<Client> _clients = new();
    private readonly List<Device> _devices = new();
    private readonly List<RepairOrder> _orders = new();

    public IReadOnlyList<ServiceEngineer> GetEngineers()
    {
        lock (_gate) return _engineers.ToArray();
    }

    // UC-01: Реєстрація замовлення та призначення майстра (FR-01, FR-02, AC-01, AC-02)
    public (bool Success, string? ErrorCode, string? ErrorDetail, OrderReceiptDto? Order) CreateOrder(CreateOrderRequest req)
    {
        var missingFields = OrderRequestRules.FindMissingFields(req);
        if (missingFields is not null)
            return (false, "invalid_request", missingFields, null);

        var normalizedSerial = req.SerialNumber.Trim();

        lock (_gate)
        {
            var engineer = _engineers.SingleOrDefault(e => e.Id == req.EngineerId);
            if (engineer is null)
                return (false, "engineer_not_found", null, null);

            // Перевірка AC-02: чи є активне замовлення (не Issued) для цього серійного номера
            var existingDeviceIds = _devices
                .Where(d => string.Equals(d.SerialNumber, normalizedSerial, StringComparison.OrdinalIgnoreCase))
                .Select(d => d.Id)
                .ToHashSet();

            var activeOrder = _orders.FirstOrDefault(o =>
                existingDeviceIds.Contains(o.DeviceId) &&
                o.Status != OrderStatus.Issued);

            if (activeOrder is not null)
                return (false, "duplicate_active_order",
                    $"Активне замовлення: {activeOrder.OrderNumber} (статус {activeOrder.Status}).", null);

            var client = new Client
            {
                Id = _clients.Count + 1,
                FullName = req.ClientFullName.Trim(),
                Phone = req.ClientPhone.Trim()
            };
            _clients.Add(client);

            var device = new Device
            {
                Id = _devices.Count + 1,
                ClientId = client.Id,
                Brand = req.DeviceBrand.Trim(),
                Model = req.DeviceModel.Trim(),
                SerialNumber = normalizedSerial
            };
            _devices.Add(device);

            // Конструктор RepairOrder встановлює Accepted, TotalCost = 0 та перший запис StatusHistory
            var order = new RepairOrder(
                id: _orders.Count + 1,
                orderNumber: $"SC-{_orders.Count + 1:D4}",
                clientId: client.Id,
                deviceId: device.Id,
                engineerId: engineer.Id,
                issueDescription: req.IssueDescription.Trim(),
                acceptedAt: DateTimeOffset.UtcNow);

            _orders.Add(order);
            return (true, null, null, MapToReceiptDto(order));
        }
    }

    // UC-02: Фіксація діагностики та додавання позицій ремонту з перерахунком TotalCost (FR-03, AC-03).
    // Правила перевіряє RepairOrder.AddRepairItem.
    public (bool Success, string? ErrorCode, OrderReceiptDto? Order) AddRepairItem(int orderId, AddRepairItemRequest request)
    {
        if (request is null)
            return (false, "invalid_item_values", null);

        lock (_gate)
        {
            var order = _orders.FirstOrDefault(o => o.Id == orderId);
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

            return (true, null, MapToReceiptDto(order));
        }
    }

    // UC-02 / UC-03: Послідовна зміна статусу замовлення (FR-04, AC-04).
    // Правила переходу перевіряє RepairOrder.ChangeStatus.
    public (bool Success, string? ErrorCode, OrderReceiptDto? Order) ChangeStatus(int orderId, ChangeStatusRequest req)
    {
        if (!OrderRequestRules.TryParseStatus(req.NewStatus, out var targetStatus))
            return (false, "invalid_status_value", null);

        lock (_gate)
        {
            var order = _orders.SingleOrDefault(o => o.Id == orderId);
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

            return (true, null, MapToReceiptDto(order));
        }
    }

    // UC-04: Пошук за номером замовлення та фільтрація за статусом (FR-05, AC-05)
    public (bool Success, string? ErrorCode, IReadOnlyList<OrderReceiptDto>? Orders) GetOrders(string? status, string? orderNumber)
    {
        OrderStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!OrderRequestRules.TryParseStatus(status, out var st))
                return (false, "invalid_status_value", null);
            parsedStatus = st;
        }

        lock (_gate)
        {
            IEnumerable<RepairOrder> query = _orders;

            if (parsedStatus.HasValue)
                query = query.Where(o => o.Status == parsedStatus.Value);

            if (!string.IsNullOrWhiteSpace(orderNumber))
            {
                var term = orderNumber.Trim();
                query = query.Where(o => o.OrderNumber.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var result = query.OrderBy(o => o.Id).Select(MapToReceiptDto).ToArray();
            return (true, null, result);
        }
    }

    public OrderReceiptDto? GetOrderById(int orderId)
    {
        lock (_gate)
        {
            var order = _orders.SingleOrDefault(o => o.Id == orderId);
            return order is null ? null : MapToReceiptDto(order);
        }
    }

    // Спринт S1: історія ремонтів пристрою за серійним номером
    public (bool Success, string? ErrorCode, IReadOnlyList<OrderReceiptDto>? Orders) GetDeviceHistory(string serialNumber)
    {
        var serial = serialNumber?.Trim() ?? "";

        lock (_gate)
        {
            var deviceIds = _devices
                .Where(d => string.Equals(d.SerialNumber, serial, StringComparison.OrdinalIgnoreCase))
                .Select(d => d.Id)
                .ToHashSet();

            if (deviceIds.Count == 0)
                return (false, "device_not_found", null);

            var result = _orders
                .Where(o => deviceIds.Contains(o.DeviceId))
                .OrderBy(o => o.Id)
                .Select(MapToReceiptDto)
                .ToArray();
            return (true, null, result);
        }
    }

    private OrderReceiptDto MapToReceiptDto(RepairOrder order) =>
        OrderReceiptMapper.ToDto(
            order,
            _clients.Single(c => c.Id == order.ClientId),
            _devices.Single(d => d.Id == order.DeviceId),
            _engineers.Single(e => e.Id == order.EngineerId));
}
