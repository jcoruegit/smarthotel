namespace SmartHotel.Domain.Entities
{
    public class PricingRule
    {
        public int Id { get; set; }
        public int RoomTypeId { get; set; }
        public RoomType RoomType { get; set; }

        public DateTime Date { get; set; }

        public decimal Price { get; set; }

        public string Reason { get; set; } // "High demand", "Weekend", etc.
    }
}
