export const checkoutSelectionStorageKey = 'smarthotel.reservations.checkoutSelection';
export const checkoutDraftStorageKey = 'smarthotel.reservations.checkoutDraft';
export const reservationSuccessStorageKey = 'smarthotel.reservations.result';

export function clearReservationFlowStorage(): void {
  sessionStorage.removeItem(checkoutSelectionStorageKey);
  sessionStorage.removeItem(checkoutDraftStorageKey);
  sessionStorage.removeItem(reservationSuccessStorageKey);
}
