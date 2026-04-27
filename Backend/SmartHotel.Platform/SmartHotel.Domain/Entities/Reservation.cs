using SmartHotel.Domain.Enums;

namespace SmartHotel.Domain.Entities
{
    public class Reservation
    {
        public int Id { get; set; }

        public int GuestId { get; set; }
        public Guest Guest { get; set; }

        public int RoomId { get; set; }
        public Room Room { get; set; }

        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }

        public decimal TotalPrice { get; set; }

        public ReservationStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
