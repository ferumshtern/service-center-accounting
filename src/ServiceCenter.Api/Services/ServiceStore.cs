using ServiceCenter.Api.Contracts;
using ServiceCenter.Api.Models;

namespace ServiceCenter.Api.Services;

public sealed class ServiceStore
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

    // UC-01: Реєстрація замовлення та призначення майстра (Серафим: FR-01, FR-02, AC-01, AC-02)
    // ErrorDetail уточнює відмову: перелік пропущених полів (A1) або номер активного замовлення (A2).
    public (bool Success, string? ErrorCode, string? ErrorDetail, OrderReceiptDto? Order) CreateOrder(CreateOrderRequest req)
    {
        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(req.ClientFullName)) missingFields.Add("ПІБ клієнта");
        if (string.IsNullOrWhiteSpace(req.ClientPhone)) missingFields.Add("телефон клієнта");
        if (string.IsNullOrWhiteSpace(req.DeviceBrand)) missingFields.Add("бренд пристрою");
        if (string.IsNullOrWhiteSpace(req.DeviceModel)) missingFields.Add("модель пристрою");
        if (string.IsNullOrWhiteSpace(req.SerialNumber)) missingFields.Add("серійний номер");
        if (string.IsNullOrWhiteSpace(req.IssueDescription)) missingFields.Add("опис несправності");

        if (missingFields.Count > 0)
            return (false, "invalid_request", $"Пропущено: {string.Join(", ", missingFields)}.", null);

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
                createdAt: DateTimeOffset.UtcNow);

            _orders.Add(order);
            return (true, null, null, MapToReceiptDto(order));
        }
    }

    // UC-04: Пошук за номером замовлення та фільтрація за статусом (FR-05, AC-05)
    public (bool Success, string? ErrorCode, IReadOnlyList<OrderReceiptDto>? Orders) GetOrders(string? status, string? orderNumber)
    {
        OrderStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseStatus(status, out var st))
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

    // Приймається лише назва статусу (Accepted, InProgress, Ready, Issued) без урахування регістру.
    // Числові значення ("3", "99") Enum.TryParse теж розібрав би, тому їх відхиляємо явно.
    private static bool TryParseStatus(string? value, out OrderStatus status)
    {
        status = default;
        var name = value?.Trim();
        var match = Enum.GetNames<OrderStatus>()
            .FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        return match is not null && Enum.TryParse(match, out status);
    }

    private OrderReceiptDto MapToReceiptDto(RepairOrder order)
    {
        var client = _clients.Single(c => c.Id == order.ClientId);
        var device = _devices.Single(d => d.Id == order.DeviceId);
        var engineer = _engineers.Single(e => e.Id == order.EngineerId);

        return new OrderReceiptDto(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.CreatedAt,
            client.FullName,
            client.Phone,
            device.Brand,
            device.Model,
            device.SerialNumber,
            order.IssueDescription,
            order.DiagnosticNotes,
            engineer.FullName,
            order.TotalCost,
            order.Items.Select(i => new RepairItemDto(i.Id, i.Name, i.Quantity, i.UnitPrice, i.Subtotal)).ToArray(),
            order.History.Select(h => new StatusHistoryDto(h.Status.ToString(), h.ChangedAt)).ToArray()
        );
    }
}
