namespace SmartHotel.Domain.Entities;

public class Room
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Features { get; set; } = string.Empty;
    public int RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;
}
