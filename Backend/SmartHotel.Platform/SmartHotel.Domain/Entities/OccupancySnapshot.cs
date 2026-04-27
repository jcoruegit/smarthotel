namespace SmartHotel.Domain.Entities
{
    public class OccupancySnapshot
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int TotalRooms { get; set; }
        public int OccupiedRooms { get; set; }
    }
}
