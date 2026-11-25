using ServiceCenter.Api.Contracts;
using ServiceCenter.Api.Models;

namespace ServiceCenter.Api.Services;

// Перевірки вхідних даних, спільні для всіх реалізацій IOrderRepository.
public static class OrderRequestRules
{
    // UC-01 A1 (AC-07): null, якщо всі поля заповнені, інакше — перелік пропущених полів.
    public static string? FindMissingFields(CreateOrderRequest req)
    {
        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(req.ClientFullName)) missingFields.Add("ПІБ клієнта");
        if (string.IsNullOrWhiteSpace(req.ClientPhone)) missingFields.Add("телефон клієнта");
        if (string.IsNullOrWhiteSpace(req.DeviceBrand)) missingFields.Add("бренд пристрою");
        if (string.IsNullOrWhiteSpace(req.DeviceModel)) missingFields.Add("модель пристрою");
        if (string.IsNullOrWhiteSpace(req.SerialNumber)) missingFields.Add("серійний номер");
        if (string.IsNullOrWhiteSpace(req.IssueDescription)) missingFields.Add("опис несправності");

        return missingFields.Count > 0 ? $"Пропущено: {string.Join(", ", missingFields)}." : null;
    }

    // Приймається лише назва статусу (Accepted, InProgress, Ready, Issued) без урахування регістру.
    public static bool TryParseStatus(string? value, out OrderStatus status)
    {
        status = default;
        var name = value?.Trim();
        var match = Enum.GetNames<OrderStatus>()
            .FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        return match is not null && Enum.TryParse(match, out status);
    }
}
