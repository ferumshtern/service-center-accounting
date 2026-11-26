using ServiceCenter.Api.Models;

namespace ServiceCenter.Api.Data;

// Спільні запити до бази для сховища та служб.
public static class OrderQueries
{
    // AC-02: активне (не Issued) замовлення для серійного номера; порівняння без регістру (NOCASE).
    public static string? FindActiveOrder(AppDbContext db, string serialNumber)
    {
        var active = db.Orders
            .Where(o => o.Status != OrderStatus.Issued &&
                        db.Devices.Any(d => d.Id == o.DeviceId && d.SerialNumber == serialNumber))
            .Select(o => new { o.OrderNumber, o.Status })
            .FirstOrDefault();

        return active is null ? null : $"Активне замовлення: {active.OrderNumber} (статус {active.Status}).";
    }

    public static int NextId(IQueryable<int?> ids) => (ids.Max() ?? 0) + 1;
}
