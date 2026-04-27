namespace SmartHotel.Domain.Entities;

public class HotelAmenity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeOnly? AvailableFrom { get; set; }
    public TimeOnly? AvailableTo { get; set; }
    public string? DaysOfWeek { get; set; }
    public bool IsComplimentary { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public bool RequiresReservation { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
