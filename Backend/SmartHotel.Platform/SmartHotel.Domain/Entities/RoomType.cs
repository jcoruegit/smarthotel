namespace SmartHotel.Domain.Entities
{
    public class RoomType
    {
        public int Id { get; set; }
        public string Name { get; set; } // Standard, Deluxe, Suite
        public decimal BasePrice { get; set; }
    }
}
