# SmartHotel Platform

Aplicacion web para gestion hotelera con foco en reservas, autenticacion por roles, reglas de pricing y asistencia por chat.

## Que hace la aplicacion

SmartHotel Platform cubre dos grandes perfiles de uso:

1. Cliente (Guest):
- Consulta disponibilidad por fechas y cantidad de huespedes.
- Reserva habitacion y confirma pago.
- Consulta sus reservas.
- Edita su perfil y cambia su clave.

2. Personal interno (Staff/Admin):
- Acceso autenticado para operacion interna.
- Gestion de empleados y roles (Admin).
- Base API lista para gestion de reservas internas y pricing rules.

Tambien incluye un chat de atencion que responde consultas sobre:
- Disponibilidad.
- Servicios del hotel.
- Horarios y politicas.

Nota: el chat actual es un asistente basado en reglas + datos del sistema (no un LLM externo).

## Tecnologias usadas

### Backend
- .NET 9
- ASP.NET Core Minimal APIs
- Entity Framework Core 9
- SQL Server
- ASP.NET Core Identity
- JWT Bearer Authentication
- Swagger / OpenAPI

### Frontend
- React 19
- TypeScript
- Vite
- React Router

### Testing
- xUnit (backend)
- Integration tests con `WebApplicationFactory`
- Vitest + Testing Library (frontend)

## Arquitectura usada

El backend usa una arquitectura **Vertical Slice** con enfoque **Clean Architecture pragmatica**:

- Organizacion por feature (no por capas tecnicas puras).
- Endpoints por modulo funcional (`Auth`, `Availability`, `Reservations`, `PricingRules`, etc.).
- Uso de `Command`, `Query`, `Handler`, `Validator` donde agrega valor (CQRS selectivo).
- `DbContext` directo (sin repository pattern artificial).

Estructura general:

1. `SmartHotel.API`
- Capa de exposicion HTTP (Minimal APIs, auth, validaciones, handlers de entrada).

2. `SmartHotel.Domain`
- Entidades y enums del negocio (Guest, Room, Reservation, Payment, PricingRule, etc.).

3. `SmartHotel.Infrastructure`
- Persistencia EF Core, `AppDbContext`, migraciones y seeders.
- Integracion con Identity.

4. `Frontend/smarthotel.ui`
- UI de cliente y staff, consumo de APIs REST, manejo de sesion y rutas protegidas por rol.

## Estructura del repositorio

```text
Backend/SmartHotel.Platform/
  SmartHotel.API/
  SmartHotel.Domain/
  SmartHotel.Infrastructure/
  SmartHotel.API.UnitTests/
  SmartHotel.API.IntegrationTests/

Frontend/smarthotel.ui/
docs/ai/
```

## Como levantar el proyecto localmente

## Requisitos
- .NET SDK 9
- SQL Server (local o accesible)
- Node.js 20+ y npm

## Backend
1. Ir a:
```bash
cd Backend/SmartHotel.Platform/SmartHotel.API
```
2. Revisar configuracion en `appsettings.json`:
- `ConnectionStrings:DBConnection`
- `Jwt` (`Issuer`, `Audience`, `Key`, `ExpiresMinutes`)
- `Cors:AllowedOrigins`
3. Ejecutar:
```bash
dotnet run
```

API por defecto en desarrollo:
- `https://localhost:7087`
- Swagger: `https://localhost:7087/swagger`

## Frontend
1. Ir a:
```bash
cd Frontend/smarthotel.ui
```
2. Definir `VITE_API_BASE_URL` si corresponde (por defecto usa `https://localhost:7087`).
3. Ejecutar:
```bash
npm install
npm run dev
```

UI por defecto:
- `http://localhost:5173`

## Datos semilla

En entorno `Development`, el backend aplica migraciones y carga datos base:
- Tipos de documento.
- Tipos de habitacion y habitaciones.
- Servicios, horarios y politicas del hotel.
- Reglas de pricing iniciales.
- Usuario admin segun `IdentitySeed` de `appsettings.json`.

## Testing

### Backend
```bash
cd Backend/SmartHotel.Platform
dotnet test
```

### Frontend
```bash
cd Frontend/smarthotel.ui
npm test
```

## Nuevos puntos a agregar (roadmap recomendado)

1. Completar UI de staff para reservas y pricing rules
- Hoy existen endpoints listos, pero varias pantallas son base/placeholder.

2. Flujo completo de cancelaciones y reembolsos
- Incluir devoluciones parciales/totales, politicas y trazabilidad de cambios.

3. Integracion real de pagos
- Reemplazar simulacion por gateway real (tokenizacion, 3DS, webhooks, conciliacion).

4. Chat con IA generativa (opcional)
- Integrar proveedor LLM + guardrails + observabilidad.
- Mantener fallback actual basado en reglas.

5. Observabilidad y operacion
- Logs estructurados, metricas, trazas distribuidas, tableros y alertas.

6. Seguridad avanzada
- Refresh tokens, rotacion de claves, rate limiting, auditoria y hardening de CORS/cabeceras.

7. Testing end-to-end
- Casos E2E para login, reserva, pago y panel staff.

8. CI/CD
- Pipeline de build, test, calidad, migraciones y despliegue por ambientes.

