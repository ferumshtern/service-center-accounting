using ServiceCenter.Api.Rules;
using Xunit;

namespace ServiceCenter.Tests;

public sealed class WarrantyRuleTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, true)]
    [InlineData(89, true)]
    [InlineData(90, true)]
    [InlineData(91, false)]
    public void IsWithinWarranty_ReturnsExpectedResultForDaysAfterIssue(int daysAfterIssue, bool expected)
    {
        // Підготовка
        var claimedAt = IssuedAt.AddDays(daysAfterIssue);

        // Дія
        var actual = WarrantyRule.IsWithinWarranty(IssuedAt, claimedAt);

        // Перевірка
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IsWithinWarranty_WhenOneMinuteAfterWarrantyEnds_ReturnsFalse()
    {
        var claimedAt = IssuedAt.AddDays(90).AddMinutes(1);

        Assert.False(WarrantyRule.IsWithinWarranty(IssuedAt, claimedAt));
    }

    [Fact]
    public void IsWithinWarranty_WhenClaimIsBeforeIssue_Throws()
    {
        Action action = () => WarrantyRule.IsWithinWarranty(IssuedAt, IssuedAt.AddMinutes(-1));

        Assert.Throws<ArgumentException>(action);
    }
}
