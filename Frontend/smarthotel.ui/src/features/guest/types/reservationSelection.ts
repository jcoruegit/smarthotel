import type { AvailableRoom } from '../api/availabilityApi';

export interface ReservationRoomSelection {
  checkIn: string;
  checkOut: string;
  guests: number;
  room: AvailableRoom;
}
