using RoomManagement.Application.Bookings;

namespace RoomManagement.Application.Tests.Bookings;

public class BookingValidatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(1);

    private readonly CreateBookingRequestValidator _create = new();
    private readonly ListBookingsQueryValidator _list = new();
    private readonly AvailabilityQueryValidator _availability = new();

    [Fact]
    public void Create_ValidRequest_IsValid()
    {
        Assert.True(_create.Validate(new CreateBookingRequest(Guid.NewGuid(), Start, End)).IsValid);
    }

    [Fact]
    public void Create_EmptyUserId_IsInvalid()
    {
        var result = _create.Validate(new CreateBookingRequest(Guid.Empty, Start, End));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateBookingRequest.UserId));
    }

    [Fact]
    public void Create_MissingTimes_AreReportedOncePerField()
    {
        var result = _create.Validate(new CreateBookingRequest(Guid.NewGuid(), default, default));

        Assert.Single(result.Errors, e => e.PropertyName == nameof(CreateBookingRequest.Start));
        Assert.Single(result.Errors, e => e.PropertyName == nameof(CreateBookingRequest.End));
    }

    [Theory]
    [InlineData(0)] // end equals start
    [InlineData(-30)] // end before start
    public void Create_EndNotAfterStart_IsInvalid(int endOffsetMinutes)
    {
        var result = _create.Validate(new CreateBookingRequest(Guid.NewGuid(), Start, Start.AddMinutes(endOffsetMinutes)));

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(CreateBookingRequest.End), error.PropertyName);
        Assert.Equal("'End' must be after 'Start'.", error.ErrorMessage);
    }

    [Fact]
    public void List_NoBoundsOrSingleBound_IsValid()
    {
        Assert.True(_list.Validate(new ListBookingsQuery()).IsValid);
        Assert.True(_list.Validate(new ListBookingsQuery(From: Start)).IsValid);
        Assert.True(_list.Validate(new ListBookingsQuery(To: End)).IsValid);
        Assert.True(_list.Validate(new ListBookingsQuery(Start, End)).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void List_ToNotAfterFrom_IsInvalid(int toOffsetMinutes)
    {
        var result = _list.Validate(new ListBookingsQuery(Start, Start.AddMinutes(toOffsetMinutes)));

        Assert.Equal(nameof(ListBookingsQuery.To), Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Availability_ValidRange_IsValid()
    {
        Assert.True(_availability.Validate(new AvailabilityQuery(Start, End)).IsValid);
    }

    [Fact]
    public void Availability_MissingOrInvertedRange_IsInvalid()
    {
        var missing = _availability.Validate(new AvailabilityQuery(default, default));
        var inverted = _availability.Validate(new AvailabilityQuery(End, Start));

        Assert.Contains(missing.Errors, e => e.PropertyName == nameof(AvailabilityQuery.From));
        Assert.Contains(missing.Errors, e => e.PropertyName == nameof(AvailabilityQuery.To));
        Assert.Equal(nameof(AvailabilityQuery.To), Assert.Single(inverted.Errors).PropertyName);
    }
}
