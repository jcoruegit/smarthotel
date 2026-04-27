using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Availability.Query;
using SmartHotel.API.Features.Availability.Validator;

namespace SmartHotel.API.UnitTests.Features.Availability;

public sealed class AvailabilityQueryValidatorTests
{
    private readonly AvailabilityQueryValidator _validator = new();

    public static IEnumerable<object[]> ValidateCases()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        yield return
        [
            "Caso exitoso",
            new GetAvailabilityQuery(today.AddDays(1), today.AddDays(3), 2, null),
            false,
            null
        ];

        yield return
        [
            "Error por guests en cero",
            new GetAvailabilityQuery(today.AddDays(1), today.AddDays(3), 0, null),
            true,
            "guests"
        ];

        yield return
        [
            "Error por roomTypeId invalido",
            new GetAvailabilityQuery(today.AddDays(1), today.AddDays(3), 2, 0),
            true,
            "roomTypeId"
        ];

        yield return
        [
            "Error por check-out menor o igual a check-in",
            new GetAvailabilityQuery(today.AddDays(2), today.AddDays(2), 2, null),
            true,
            "check-out"
        ];

        yield return
        [
            "Error por check-in en el pasado",
            new GetAvailabilityQuery(today.AddDays(-1), today.AddDays(1), 2, null),
            true,
            "pasado"
        ];
    }

    [Theory]
    [MemberData(nameof(ValidateCases))]
    public void Validate_ShouldHandleSuccessAndErrorCases(
        string _,
        GetAvailabilityQuery query,
        bool shouldThrow,
        string? expectedMessageFragment)
    {
        if (shouldThrow)
        {
            var exception = Assert.Throws<UserFriendlyException>(() => _validator.Validate(query));
            Assert.Contains(expectedMessageFragment!, exception.Message, StringComparison.OrdinalIgnoreCase);
            return;
        }

        var noException = Record.Exception(() => _validator.Validate(query));
        Assert.Null(noException);
    }
}
