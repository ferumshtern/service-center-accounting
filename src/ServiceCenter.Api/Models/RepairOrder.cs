namespace ServiceCenter.Api.Models;

// Агрегат замовлення (UC-01). Колекції Items і History змінюються лише методами класу.

public sealed class RepairOrder
{
    private readonly List<RepairItem> _items = new();
    private readonly List<StatusHistory> _history = new();

    public int Id { get; }
    public string OrderNumber { get; }
    public int ClientId { get; }
    public int DeviceId { get; }
    public int EngineerId { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public string IssueDescription { get; }
    public string DiagnosticNotes { get; private set; } = "";
    public decimal TotalCost { get; private set; }
    public OrderStatus Status { get; private set; }

    public IReadOnlyList<RepairItem> Items => _items;
    public IReadOnlyList<StatusHistory> History => _history;

    // UC-01 (FR-01, AC-01): нове замовлення завжди у статусі Accepted, TotalCost = 0,
    // з першим записом в історії статусів.
    public RepairOrder(
        int id,
        string orderNumber,
        int clientId,
        int deviceId,
        int engineerId,
        string issueDescription,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrderNumber = orderNumber;
        ClientId = clientId;
        DeviceId = deviceId;
        IssueDescription = issueDescription;
        CreatedAt = createdAt;
        AssignEngineer(engineerId);

        Status = OrderStatus.Accepted;
        AddHistory(OrderStatus.Accepted, createdAt);
    }

    public void AssignEngineer(int engineerId)
    {
        EngineerId = engineerId;
    }

    public void CalculateTotalCost()
    {
        TotalCost = _items.Sum(x => x.Subtotal);
    }

    private void AddHistory(OrderStatus status, DateTimeOffset changedAt)
    {
        _history.Add(new StatusHistory
        {
            Id = _history.Count + 1,
            Status = status,
            ChangedAt = changedAt
        });
    }
}
