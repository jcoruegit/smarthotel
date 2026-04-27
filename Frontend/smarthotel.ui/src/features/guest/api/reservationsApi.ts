import { httpRequest } from '../../../shared/api/httpClient';

export interface PassengerInput {
  documentType: string;
  firstName: string;
  lastName: string;
  documentNumber: string;
  birthDate: string;
  email?: string;
  phone?: string;
}

export interface CreateReservationRequest {
  passenger: PassengerInput;
  checkIn: string;
  checkOut: string;
  guests: number;
  roomId?: number;
  roomTypeId?: number;
}

interface ReservationPassenger {
  guestId: number;
  documentTypeId: number;
  documentTypeName: string;
  firstName: string;
  lastName: string;
  documentNumber: string;
  birthDate: string;
  email: string | null;
  phone: string | null;
}

export interface ReservationDetails {
  reservationId: number;
  passenger: ReservationPassenger;
  roomId: number;
  roomNumber: string;
  roomTypeId: number;
  roomTypeName: string;
  checkIn: string;
  checkOut: string;
  nights: number;
  guests?: number;
  pricePerNight: number;
  totalPrice: number;
  totalPaid: number;
  remainingBalance: number;
  status: string;
}

export interface CreateReservationPaymentRequest {
  amount: number;
  cardHolderName: string;
}

export interface ReservationPaymentResult {
  paymentId: number;
  reservationId: number;
  amount: number;
  paymentStatus: string;
  reservationTotalPrice: number;
  totalPaid: number;
  remainingBalance: number;
  fullyPaid: boolean;
  reservationStatus: string;
  registeredAtUtc: string;
}

export interface GuestReservationListItem {
  reservationId: number;
  roomId: number;
  roomNumber: string;
  roomTypeName: string;
  checkIn: string;
  checkOut: string;
  nights: number;
  totalPrice: number;
  totalPaid: number;
  remainingBalance: number;
  status: string;
  createdAtUtc: string;
}

export async function createReservation(payload: CreateReservationRequest, accessToken: string): Promise<ReservationDetails> {
  return httpRequest<ReservationDetails>('/api/reservations', {
    method: 'POST',
    accessToken,
    body: payload,
  });
}

export async function getReservationById(id: number, accessToken: string): Promise<ReservationDetails> {
  return httpRequest<ReservationDetails>(`/api/reservations/${id}`, {
    accessToken,
  });
}

export async function createReservationPayment(
  reservationId: number,
  payload: CreateReservationPaymentRequest,
  accessToken: string,
): Promise<ReservationPaymentResult> {
  return httpRequest<ReservationPaymentResult>(`/api/reservations/${reservationId}/payments`, {
    method: 'POST',
    accessToken,
    body: payload,
  });
}

export async function listMyReservations(
  accessToken: string,
  filters?: { fromDate?: string; toDate?: string },
): Promise<GuestReservationListItem[]> {
  const query = new URLSearchParams();

  if (filters?.fromDate) {
    query.set('fromDate', filters.fromDate);
  }

  if (filters?.toDate) {
    query.set('toDate', filters.toDate);
  }

  const queryString = query.toString();
  const path = queryString ? `/api/reservations/mine?${queryString}` : '/api/reservations/mine';

  return httpRequest<GuestReservationListItem[]>(path, {
    accessToken,
  });
}
