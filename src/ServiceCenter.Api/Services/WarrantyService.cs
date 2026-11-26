using Microsoft.EntityFrameworkCore;
using ServiceCenter.Api.Contracts;
using ServiceCenter.Api.Data;
using ServiceCenter.Api.Models;
using ServiceCenter.Api.Rules;

namespace ServiceCenter.Api.Services;

// Спринт S2: координує гарантійне звернення. Контекст бази отримує через конструктор від контейнера.
// Нове замовлення і запис WarrantyClaims зберігаються одним SaveChanges у транзакції:
// або обидва записи, або жодного.
public sealed class WarrantyService : IWarrantyService
{
    private readonly AppDbContext _db;

    public WarrantyService(AppDbContext db)
    {
        _db = db;
    }

    public (bool Success, string? ErrorCode, string? ErrorDetail, OrderReceiptDto? Order) CreateClaim(int originalOrderId, WarrantyClaimRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.IssueDescription))
            return (false, "invalid_warranty_request", null, null);

        using var transaction = _db.Database.BeginTransaction();

        var original = _db.Orders.Find(originalOrderId);
        if (original is null)
            return (false, "order_not_found", null, null);

        if (original.Status != OrderStatus.Issued)
            return (false, "order_not_issued", null, null);

        if (_db.WarrantyClaims.Any(c => c.OriginalOrderId == originalOrderId))
            return (false, "warranty_claim_exists", null, null);

        var issuedAt = original.History.Last(h => h.Status == OrderStatus.Issued).ChangedAt;
        var claimedAt = DateTimeOffset.UtcNow;
        if (!WarrantyRule.IsWithinWarranty(issuedAt, claimedAt))
            return (false, "warranty_expired",
                $"Видано {issuedAt:dd.MM.yyyy}, гарантія діяла до {issuedAt.AddDays(WarrantyRule.WarrantyDays):dd.MM.yyyy}.", null);

        var device = _db.Devices.Single(d => d.Id == original.DeviceId);
        var activeOrder = OrderQueries.FindActiveOrder(_db, device.SerialNumber);
        if (activeOrder is not null)
            return (false, "duplicate_active_order", activeOrder, null);

        var orderId = OrderQueries.NextId(_db.Orders.Select(o => (int?)o.Id));
        var warrantyOrder = new RepairOrder(
            id: orderId,
            orderNumber: $"SC-{orderId:D4}",
            clientId: original.ClientId,
            deviceId: original.DeviceId,
            engineerId: original.EngineerId,
            issueDescription: $"Гарантійне звернення за {original.OrderNumber}: {request.IssueDescription.Trim()}",
            acceptedAt: claimedAt);

        var claim = new WarrantyClaim
        {
            Id = OrderQueries.NextId(_db.WarrantyClaims.Select(c => (int?)c.Id)),
            OriginalOrderId = original.Id,
            WarrantyOrderId = warrantyOrder.Id,
            ClaimedAt = claimedAt
        };

        _db.Orders.Add(warrantyOrder);
        _db.WarrantyClaims.Add(claim);

        try
        {
            _db.SaveChanges();
            transaction.Commit();
        }
        catch (DbUpdateException)
        {
            return (false, "save_failed", null, null);
        }

        return (true, null, null, OrderReceiptMapper.ToDto(
            warrantyOrder,
            _db.Clients.AsNoTracking().Single(c => c.Id == warrantyOrder.ClientId),
            device,
            _db.Engineers.AsNoTracking().Single(e => e.Id == warrantyOrder.EngineerId)));
    }
}
