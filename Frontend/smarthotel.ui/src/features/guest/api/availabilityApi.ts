import { httpRequest } from '../../../shared/api/httpClient';

export interface RoomTypeOption {
  id: number;
  name: string;
  basePrice: number;
}

export interface AvailableRoom {
  roomId: number;
  roomNumber: string;
  roomTypeId: number;
  roomTypeName: string;
  maxCapacity: number;
  features: string;
  pricePerNight: number;
  estimatedTotalPrice: number;
}

export interface AvailabilityResponse {
  checkIn: string;
  checkOut: string;
  nights: number;
  guests: number;
  rooms: AvailableRoom[];
  message: string | null;
}

interface AvailabilityQuery {
  checkIn: string;
  checkOut: string;
  guests: number;
  roomTypeId?: number;
}

export async function getRoomTypes(): Promise<RoomTypeOption[]> {
  return httpRequest<RoomTypeOption[]>('/api/availability/room-types');
}

export async function getAvailability(query: AvailabilityQuery): Promise<AvailabilityResponse> {
  const params = new URLSearchParams({
    checkIn: query.checkIn,
    checkOut: query.checkOut,
    guests: String(query.guests),
  });

  if (query.roomTypeId) {
    params.set('roomTypeId', String(query.roomTypeId));
  }

  return httpRequest<AvailabilityResponse>(`/api/availability?${params.toString()}`);
}
