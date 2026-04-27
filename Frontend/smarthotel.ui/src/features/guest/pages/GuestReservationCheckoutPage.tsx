import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ApiError } from '../../../shared/api/httpClient';
import { getCurrentGuestProfile } from '../../auth/api/authApi';
import { useAuth } from '../../auth/hooks/useAuth';
import {
  createReservation,
  createReservationPayment,
  getReservationById,
  type CreateReservationRequest,
} from '../api/reservationsApi';
import {
  checkoutDraftStorageKey,
  checkoutSelectionStorageKey,
  clearReservationFlowStorage,
  reservationSuccessStorageKey,
} from '../constants/reservationStorage';
import type { ReservationRoomSelection } from '../types/reservationSelection';

const confirmationReturnTo = '/reservas/confirmar?resumeConfirm=1';

interface CheckoutDraft {
  documentType: string;
  firstName: string;
  lastName: string;
  documentNumber: string;
  birthDate: string;
  email: string;
  phone: string;
  cardHolderName: string;
  cardNumber: string;
  expiry: string;
  cvv: string;
  pendingConfirmation: boolean;
}

interface CheckoutNavigationState {
  selection?: ReservationRoomSelection;
}

export function GuestReservationCheckoutPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { session, logout } = useAuth();
  const isLoggedIn = Boolean(session);
  const backButtonLabel = isLoggedIn ? 'Volver a reservas' : 'Volver a Inicio';

  const navigationState = (location.state as CheckoutNavigationState | null) ?? null;
  const storedDraft = useMemo(() => readCheckoutDraft(), []);
  const [selection] = useState<ReservationRoomSelection | null>(() => {
    if (navigationState?.selection) {
      writeCheckoutSelection(navigationState.selection);
      return navigationState.selection;
    }

    return readCheckoutSelection();
  });

  const shouldResumeConfirmation = useMemo(
    () => new URLSearchParams(location.search).get('resumeConfirm') === '1',
    [location.search],
  );
  const loginPathWithReturnTo = useMemo(
    () => `/login?returnTo=${encodeURIComponent(confirmationReturnTo)}`,
    [],
  );

  const [documentType, setDocumentType] = useState(storedDraft?.documentType || 'DNI');
  const [firstName, setFirstName] = useState(storedDraft?.firstName || '');
  const [lastName, setLastName] = useState(storedDraft?.lastName || '');
  const [documentNumber, setDocumentNumber] = useState(storedDraft?.documentNumber || '');
  const [birthDate, setBirthDate] = useState(storedDraft?.birthDate || '1995-01-01');
  const [email, setEmail] = useState(storedDraft?.email || session?.email || '');
  const [phone, setPhone] = useState(storedDraft?.phone || '');

  const [cardHolderName, setCardHolderName] = useState(storedDraft?.cardHolderName || '');
  const [cardNumber, setCardNumber] = useState(storedDraft?.cardNumber || '');
  const [expiry, setExpiry] = useState(storedDraft?.expiry || '');
  const [cvv, setCvv] = useState(storedDraft?.cvv || '');

  const [pendingConfirmation, setPendingConfirmation] = useState(storedDraft?.pendingConfirmation ?? false);
  const [cardSide, setCardSide] = useState<'front' | 'back'>('front');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const autoRetryTriggeredRef = useRef(false);

  useEffect(() => {
    if (!email && session?.email) {
      setEmail(session.email);
    }
  }, [email, session?.email]);

  useEffect(() => {
    if (!session?.accessToken) {
      return;
    }
    const accessToken = session.accessToken;
    const sessionEmail = session.email;

    let isMounted = true;

    async function loadGuestProfile() {
      try {
        const profile = await getCurrentGuestProfile(accessToken);
        if (!isMounted || !profile) {
          return;
        }

        setDocumentType(profile.documentTypeName);
        setFirstName(profile.firstName);
        setLastName(profile.lastName);
        setDocumentNumber(profile.documentNumber);
        setBirthDate(profile.birthDate);
        setEmail(profile.email ?? sessionEmail ?? '');
        setPhone(profile.phone ?? '');
      } catch {
        // Si no hay perfil, mantenemos los datos manuales.
      }
    }

    void loadGuestProfile();

    return () => {
      isMounted = false;
    };
  }, [session?.accessToken, session?.email]);

  useEffect(() => {
    writeCheckoutDraft({
      documentType,
      firstName,
      lastName,
      documentNumber,
      birthDate,
      email,
      phone,
      cardHolderName,
      cardNumber,
      expiry,
      cvv,
      pendingConfirmation,
    });
  }, [
    birthDate,
    cardHolderName,
    cardNumber,
    cvv,
    documentNumber,
    documentType,
    email,
    expiry,
    firstName,
    lastName,
    pendingConfirmation,
    phone,
  ]);

  useEffect(() => {
    if (!session || !shouldResumeConfirmation || !pendingConfirmation || submitting || !selection) {
      return;
    }

    if (autoRetryTriggeredRef.current) {
      return;
    }

    autoRetryTriggeredRef.current = true;
    void submitConfirmation(session.accessToken, true);
  }, [pendingConfirmation, selection, session, shouldResumeConfirmation, submitting]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selection) {
      setError('No encontramos una habitacion seleccionada. Vuelve a consultar disponibilidad.');
      return;
    }

    if (!session) {
      setPendingConfirmation(true);
      setError('Inicia sesión para confirmar la reserva.');
      navigate(loginPathWithReturnTo);
      return;
    }

    const validationError = validateCheckoutInput({
      documentType,
      firstName,
      lastName,
      documentNumber,
      birthDate,
      cardHolderName,
      cardNumber,
      expiry,
      cvv,
    });

    if (validationError) {
      setError(validationError);
      return;
    }

    autoRetryTriggeredRef.current = true;
    await submitConfirmation(session.accessToken, false);
  }

  async function submitConfirmation(accessToken: string, isRetryAfterLogin: boolean) {
    if (!selection) {
      return;
    }

    setSubmitting(true);
    setError(null);
    setPendingConfirmation(false);

    try {
      const reservation = await createReservation(buildReservationPayload(selection), accessToken);
      await createReservationPayment(
        reservation.reservationId,
        {
          amount: reservation.totalPrice,
          cardHolderName,
        },
        accessToken,
      );

      const fullReservation = await getReservationById(reservation.reservationId, accessToken);
      sessionStorage.setItem(reservationSuccessStorageKey, JSON.stringify(fullReservation));

      writeCheckoutDraft({
        documentType,
        firstName,
        lastName,
        documentNumber,
        birthDate,
        email,
        phone,
        cardHolderName,
        cardNumber,
        expiry,
        cvv,
        pendingConfirmation: false,
      });

      navigate('/reservas/resultado', {
        replace: true,
        state: {
          reservation: fullReservation,
        },
      });
    } catch (unknownError) {
      if (unknownError instanceof ApiError && unknownError.status === 401) {
        setPendingConfirmation(true);
        logout();
        navigate(loginPathWithReturnTo, { replace: true });
        return;
      }

      const fallbackMessage = isRetryAfterLogin
        ? 'No pudimos retomar automaticamente la confirmación. Revisa los datos e intenta nuevamente.'
        : 'No pudimos confirmar la reserva.';
      const message = unknownError instanceof ApiError ? unknownError.message : fallbackMessage;
      setError(message);
    } finally {
      setSubmitting(false);
    }
  }

  function buildReservationPayload(currentSelection: ReservationRoomSelection): CreateReservationRequest {
    return {
      passenger: {
        documentType,
        firstName,
        lastName,
        documentNumber,
        birthDate,
        email,
        phone,
      },
      checkIn: currentSelection.checkIn,
      checkOut: currentSelection.checkOut,
      guests: currentSelection.guests,
      roomId: currentSelection.room.roomId,
      roomTypeId: currentSelection.room.roomTypeId,
    };
  }

  function handleCardNumberChange(value: string) {
    const digitsOnly = value.replace(/\D/g, '').slice(0, 16);
    const grouped = digitsOnly.replace(/(\d{4})(?=\d)/g, '$1 ');
    setCardNumber(grouped);
  }

  function handleExpiryChange(value: string) {
    const digitsOnly = value.replace(/\D/g, '').slice(0, 4);
    if (digitsOnly.length <= 2) {
      setExpiry(digitsOnly);
      return;
    }

    setExpiry(`${digitsOnly.slice(0, 2)}/${digitsOnly.slice(2)}`);
  }

  function handleBackNavigation() {
    clearReservationFlowStorage();
    navigate(isLoggedIn ? '/reservas' : '/', { replace: true });
  }

  if (!selection) {
    return (
      <main className="page guest-reservations-page">
        <div className="app-popup-backdrop" role="dialog" aria-modal="true" aria-labelledby="missing-reservation-title">
          <section className="app-popup">
            <h2 id="missing-reservation-title">No hay reservas cargadas</h2>
            <p>Para continuar con el pago primero debes cargar una reserva.</p>
            <button className="btn btn-primary" type="button" onClick={() => navigate('/guest/panel', { replace: true })}>
              Ir al panel de control
            </button>
          </section>
        </div>
      </main>
    );
  }

  return (
    <main className="page guest-reservations-page">
      <header className="section-header">
        <p className="eyebrow">Cliente</p>
        <h1>Confirmación de reserva</h1>
        <p className="subtle">Revisa tus datos y completa el pago con tarjeta para confirmar la reserva.</p>
        <div className="button-row">
          <button className="btn btn-ghost" type="button" onClick={handleBackNavigation}>
            {backButtonLabel}
          </button>
        </div>
      </header>

      <form className="grid-cards checkout-grid" onSubmit={handleSubmit}>
        <section className="card form-card">
          <h2>Datos del cliente</h2>

          <label className="field">
            Nombre
            <input value={firstName} readOnly required />
          </label>

          <label className="field">
            Apellido
            <input value={lastName} readOnly required />
          </label>

          <label className="field">
            Tipo de documento
            <input value={documentType} readOnly required />
          </label>

          <label className="field">
            Número de documento
            <input
              inputMode="numeric"
              minLength={7}
              maxLength={8}
              value={documentNumber}
              readOnly
              required
            />
          </label>

          <label className="field">
            Fecha de nacimiento
            <input type="date" value={birthDate} readOnly required />
          </label>

          <label className="field">
            Email
            <input type="email" value={email} readOnly />
          </label>

          <label className="field">
            Telefono
            <input value={phone} readOnly />
          </label>
        </section>

        <section className="card form-card">
          <h2>Reserva y pago</h2>

          <div className="checkout-summary">
            <p>
              Habitacion {selection.room.roomNumber} ({selection.room.roomTypeName})
            </p>
            <p>
              Check in: {selection.checkIn} | Check out: {selection.checkOut}
            </p>
            <p>huéspedes: {selection.guests}</p>
            <p>Precio por noche: {formatCurrency(selection.room.pricePerNight)} USD</p>
            <p>Total estimado: {formatCurrency(selection.room.estimatedTotalPrice)} USD</p>
          </div>

          <div className="credit-card-demo">
            <div className={`credit-card ${cardSide === 'back' ? 'is-back' : ''}`}>
              <div className="credit-card-front">
                <p className="credit-card-brand">SMART HOTEL</p>
                <p className="credit-card-number">{formatCardPreview(cardNumber)}</p>
                <div className="credit-card-meta">
                  <span>{cardHolderName || 'NOMBRE TITULAR'}</span>
                  <span>{expiry || 'MM/YY'}</span>
                </div>
              </div>

              <div className="credit-card-back">
                <div className="credit-card-strip" />
                <div className="credit-card-cvv">
                  <span>CVV</span>
                  <strong>{cvv || '***'}</strong>
                </div>
              </div>
            </div>
          </div>

          <div className="credit-card-fields">
            <label className="field">
              Titular de la tarjeta de credito
              <input
                value={cardHolderName}
                onFocus={() => setCardSide('front')}
                onChange={(event) => setCardHolderName(event.target.value)}
                required
              />
            </label>

            <label className="field">
              Número de la tarjeta de credito
              <input
                inputMode="numeric"
                value={cardNumber}
                onFocus={() => setCardSide('front')}
                onChange={(event) => handleCardNumberChange(event.target.value)}
                placeholder="1234 5678 9012 3456"
                required
              />
            </label>

            <label className="field">
              Vencimiento (MM/YY)
              <input
                inputMode="numeric"
                value={expiry}
                onFocus={() => setCardSide('front')}
                onChange={(event) => handleExpiryChange(event.target.value)}
                placeholder="MM/YY"
                required
              />
            </label>

            <label className="field">
              CVV
              <input
                inputMode="numeric"
                value={cvv}
                onFocus={() => setCardSide('back')}
                onChange={(event) => setCvv(event.target.value.replace(/\D/g, '').slice(0, 3))}
                placeholder="123"
                required
              />
            </label>
          </div>

          {error ? <p className="message error">{error}</p> : null}

          <button className="btn btn-primary" type="submit" disabled={submitting}>
            {submitting ? 'Confirmando...' : 'Confirmación'}
          </button>
        </section>
      </form>
    </main>
  );
}

function validateCheckoutInput(input: {
  documentType: string;
  firstName: string;
  lastName: string;
  documentNumber: string;
  birthDate: string;
  cardHolderName: string;
  cardNumber: string;
  expiry: string;
  cvv: string;
}): string | null {
  if (
    !input.documentType.trim()
    || !input.firstName.trim()
    || !input.lastName.trim()
    || !input.documentNumber.trim()
    || !input.birthDate.trim()
  ) {
    return 'Completa todos los datos obligatorios del cliente.';
  }

  if (!/^\d{7,8}$/.test(input.documentNumber.trim())) {
    return 'El número de documento debe tener al menos 7 digitos y como maximo 8, usando solo numeros.';
  }

  if (!input.cardHolderName.trim()) {
    return 'El titular de la tarjeta es obligatorio.';
  }

  const normalizedCardHolderName = normalizePersonName(input.cardHolderName);
  const normalizedGuestFullName = normalizePersonName(`${input.firstName} ${input.lastName}`);
  if (normalizedCardHolderName !== normalizedGuestFullName) {
    return 'El titular de la tarjeta debe coincidir con el nombre y apellido del cliente.';
  }

  const cardDigits = input.cardNumber.replace(/\s/g, '');
  if (!/^\d{16}$/.test(cardDigits)) {
    return 'El número de tarjeta debe tener 16 digitos.';
  }

  if (!/^\d{2}\/\d{2}$/.test(input.expiry)) {
    return 'El vencimiento debe tener formato MM/YY.';
  }

  const [monthRaw, yearRaw] = input.expiry.split('/');
  const month = Number(monthRaw);
  const year = Number(yearRaw);
  if (month < 1 || month > 12) {
    return 'El mes de vencimiento no es valido.';
  }

  const currentDate = new Date();
  const expiryYear = 2000 + year;
  const expiryDate = new Date(expiryYear, month, 0, 23, 59, 59, 999);
  if (expiryDate < currentDate) {
    return 'La tarjeta se encuentra vencida.';
  }

  if (!/^\d{3}$/.test(input.cvv)) {
    return 'El CVV debe tener 3 digitos.';
  }

  return null;
}

function normalizePersonName(value: string): string {
  return value.trim().replace(/\s+/g, ' ').toUpperCase();
}

function formatCardPreview(value: string): string {
  const normalized = value.replace(/\D/g, '');
  if (!normalized) {
    return '**** **** **** ****';
  }

  return normalized.replace(/(\d{4})(?=\d)/g, '$1 ');
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('es-AR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}

function readCheckoutSelection(): ReservationRoomSelection | null {
  const raw = sessionStorage.getItem(checkoutSelectionStorageKey);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as ReservationRoomSelection;
  } catch {
    return null;
  }
}

function writeCheckoutSelection(selection: ReservationRoomSelection): void {
  sessionStorage.setItem(checkoutSelectionStorageKey, JSON.stringify(selection));
}

function readCheckoutDraft(): CheckoutDraft | null {
  const raw = sessionStorage.getItem(checkoutDraftStorageKey);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as Partial<CheckoutDraft>;

    return {
      documentType: parsed.documentType ?? 'DNI',
      firstName: parsed.firstName ?? '',
      lastName: parsed.lastName ?? '',
      documentNumber: parsed.documentNumber ?? '',
      birthDate: parsed.birthDate ?? '1995-01-01',
      email: parsed.email ?? '',
      phone: parsed.phone ?? '',
      cardHolderName: parsed.cardHolderName ?? '',
      cardNumber: parsed.cardNumber ?? '',
      expiry: parsed.expiry ?? '',
      cvv: parsed.cvv ?? '',
      pendingConfirmation: Boolean(parsed.pendingConfirmation),
    };
  } catch {
    return null;
  }
}

function writeCheckoutDraft(draft: CheckoutDraft): void {
  sessionStorage.setItem(checkoutDraftStorageKey, JSON.stringify(draft));
}
