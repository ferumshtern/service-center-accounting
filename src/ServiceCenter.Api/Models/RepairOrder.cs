namespace ServiceCenter.Api.Models;


// Агрегат замовлення. Усі правила життєвого циклу (FR-04) та додавання позицій
// з перерахунком TotalCost (FR-03) перевіряються тут; порушення правила —
// DomainRuleException із кодом помилки API.

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

    public static bool CanTransition(OrderStatus current, OrderStatus next) =>
        (current, next) switch
        {
            (OrderStatus.Accepted, OrderStatus.InProgress) => true,
            (OrderStatus.InProgress, OrderStatus.Ready) => true,
            (OrderStatus.Ready, OrderStatus.InProgress) => true,
            (OrderStatus.Ready, OrderStatus.Issued) => true,
            _ => false
        };

    // UC-02 / UC-03 (FR-04, AC-04)
    public void ChangeStatus(OrderStatus newStatus, DateTimeOffset changedAt)
    {
        if (Status == OrderStatus.Issued)
            throw new DomainRuleException("order_already_issued");

        if (!CanTransition(Status, newStatus))
            throw new DomainRuleException("invalid_status_transition");

        Status = newStatus;
        AddHistory(newStatus, changedAt);
    }

    // UC-02 (FR-03, AC-03, AC-04): спочатку перевіряються всі правила, лише потім
    // змінюється стан — при відмові замовлення залишається незмінним.
    public void AddRepairItem(string name, int quantity, decimal unitPrice, string? diagnosticNotes = null)
    {
        if (string.IsNullOrWhiteSpace(name) || quantity <= 0 || unitPrice < 0)
            throw new DomainRuleException("invalid_item_values");

        if (Status == OrderStatus.Issued)
            throw new DomainRuleException("order_already_issued");

        if (Status != OrderStatus.InProgress)
            throw new DomainRuleException("invalid_status_warning");

        if (!string.IsNullOrWhiteSpace(diagnosticNotes))
            DiagnosticNotes = diagnosticNotes.Trim();

        _items.Add(new RepairItem
        {
            Id = _items.Count + 1,
            Name = name.Trim(),
            Quantity = quantity,
            UnitPrice = unitPrice
        });
        CalculateTotalCost();
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
