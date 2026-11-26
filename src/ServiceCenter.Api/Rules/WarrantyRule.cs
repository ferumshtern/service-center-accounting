namespace ServiceCenter.Api.Rules;

// Спринт S2: гарантійне звернення приймається, якщо від видачі минуло не більше 90 днів.
public static class WarrantyRule
{
    public const int WarrantyDays = 90;

    public static bool IsWithinWarranty(DateTimeOffset issuedAt, DateTimeOffset claimedAt)
    {
        if (claimedAt < issuedAt)
            throw new ArgumentException("Дата звернення не може бути раніше дати видачі.", nameof(claimedAt));

        return claimedAt <= issuedAt.AddDays(WarrantyDays);
    }
}
