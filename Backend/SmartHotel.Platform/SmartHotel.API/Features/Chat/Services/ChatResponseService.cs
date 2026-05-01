using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartHotel.API.Features.Availability.Handler;
using SmartHotel.API.Features.Availability.Query;
using SmartHotel.API.Features.Chat.Dto;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Persistence;

namespace SmartHotel.API.Features.Chat.Services;

public sealed class ChatResponseService(
    AppDbContext dbContext,
    GetAvailabilityQueryHandler availabilityHandler)
{
    private const string IntentAvailability = "consultar_disponibilidad";
    private const string IntentAmenities = "consultar_servicios";
    private const string IntentSchedulesPolicies = "consultar_horarios_politicas";
    private const string IntentMixed = "consulta_mixta";
    private const string IntentFallback = "fallback";
    private const string EveryDayCode = "Mon,Tue,Wed,Thu,Fri,Sat,Sun";

    private static readonly Regex IsoDateRegex = new(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex SlashOrDashDateRegex = new(@"\b\d{1,2}[/-]\d{1,2}[/-]\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex GuestsRegex = new(
        @"\b(?<count>\d{1,2})\s*(huesped(?:es)?|persona(?:s)?|guest(?:s)?|adult(?:s)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ForRegex = new(@"\b(para|for)\s+(?<count>\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<ChatMessageResponseDto> BuildResponseAsync(string userMessage, CancellationToken cancellationToken)
    {
        var normalizedMessage = userMessage.Trim();
        var language = DetectLanguage(normalizedMessage);
        var detection = DetectIntent(normalizedMessage, language);

        var reply = detection.Intent switch
        {
            IntentAvailability => await BuildAvailabilityReplyAsync(normalizedMessage, language, cancellationToken),
            IntentAmenities => await BuildAmenitiesReplyAsync(normalizedMessage, language, false, cancellationToken),
            IntentSchedulesPolicies => await BuildSchedulesPoliciesReplyAsync(normalizedMessage, language, false, cancellationToken),
            IntentMixed => await BuildMixedReplyAsync(normalizedMessage, detection, language, cancellationToken),
            _ => BuildFallbackReply(language)
        };

        return new ChatMessageResponseDto(
            Reply: reply,
            DetectedLanguage: language,
            DetectedIntent: detection.Intent);
    }

    private async Task<string> BuildMixedReplyAsync(
        string userMessage,
        IntentDetection detection,
        string language,
        CancellationToken cancellationToken)
    {
        var sections = new List<string>();

        if (detection.AskAvailability)
        {
            var title = language == "es" ? "Disponibilidad" : "Availability";
            var content = await BuildAvailabilityReplyAsync(userMessage, language, cancellationToken);
            sections.Add($"{title}:\n{content}");
        }

        if (detection.AskAmenities)
        {
            sections.Add(await BuildAmenitiesReplyAsync(userMessage, language, true, cancellationToken));
        }

        if (detection.AskSchedulesOrPolicies)
        {
            sections.Add(await BuildSchedulesPoliciesReplyAsync(userMessage, language, true, cancellationToken));
        }

        return string.Join("\n\n", sections);
    }

    private async Task<string> BuildAvailabilityReplyAsync(
        string userMessage,
        string language,
        CancellationToken cancellationToken)
    {
        var dates = ParseDates(userMessage, language);
        if (dates.Count < 2)
        {
            return language == "es"
                ? "Para consultar disponibilidad necesito check-in y check-out. Ejemplo: 2026-05-10 a 2026-05-12 para 2 huespedes."
                : "To check availability I need check-in and check-out dates. Example: 2026-05-10 to 2026-05-12 for 2 guests.";
        }

        var checkIn = dates[0];
        var checkOut = dates[1];
        if (checkOut <= checkIn)
        {
            return language == "es"
                ? "La fecha de check-out debe ser posterior al check-in."
                : "Check-out must be later than check-in.";
        }

        var parsedGuests = TryParseGuests(userMessage);
        var guests = parsedGuests ?? 2;
        var roomTypeId = await TryParseRoomTypeIdAsync(userMessage, cancellationToken);

        var availability = await availabilityHandler.HandleAsync(
            new GetAvailabilityQuery(checkIn, checkOut, guests, roomTypeId),
            cancellationToken);

        if (availability.Rooms.Count == 0)
        {
            return language == "es"
                ? $"No encontramos habitaciones disponibles del {FormatDate(checkIn)} al {FormatDate(checkOut)} para {guests} huespedes."
                : $"We could not find available rooms from {FormatDate(checkIn)} to {FormatDate(checkOut)} for {guests} guests.";
        }

        var header = language == "es"
            ? $"Encontramos disponibilidad del {FormatDate(checkIn)} al {FormatDate(checkOut)} para {guests} huespedes."
            : $"We found availability from {FormatDate(checkIn)} to {FormatDate(checkOut)} for {guests} guests.";

        var topRooms = availability.Rooms.Take(3).ToList();
        var roomLines = topRooms
            .Select(room => language == "es"
                ? $"- Hab. {room.RoomNumber} ({room.RoomTypeName}), capacidad {room.MaxCapacity}: {FormatMoney(room.PricePerNight)} por noche, total estimado {FormatMoney(room.EstimatedTotalPrice)}."
                : $"- Room {room.RoomNumber} ({room.RoomTypeName}), capacity {room.MaxCapacity}: {FormatMoney(room.PricePerNight)} per night, estimated total {FormatMoney(room.EstimatedTotalPrice)}.")
            .ToList();

        if (availability.Rooms.Count > topRooms.Count)
        {
            roomLines.Add(language == "es"
                ? $"Mostrando {topRooms.Count} de {availability.Rooms.Count} opciones."
                : $"Showing {topRooms.Count} of {availability.Rooms.Count} options.");
        }

        if (!parsedGuests.HasValue)
        {
            roomLines.Add(language == "es"
                ? "Asumi 2 huespedes porque no se indico cantidad."
                : "I assumed 2 guests because no guest count was provided.");
        }

        return $"{header}\n{string.Join("\n", roomLines)}";
    }

    private async Task<string> BuildAmenitiesReplyAsync(
        string userMessage,
        string language,
        bool includeTitle,
        CancellationToken cancellationToken)
    {
        var amenities = await dbContext.HotelAmenities
            .AsNoTracking()
            .Where(amenity => amenity.IsActive)
            .OrderBy(amenity => amenity.DisplayOrder)
            .ThenBy(amenity => amenity.Name)
            .ToListAsync(cancellationToken);

        if (amenities.Count == 0)
        {
            return language == "es"
                ? "No hay servicios cargados en este momento."
                : "There are no amenities configured right now.";
        }

        var (filteredAmenities, isSpecificAmenityRequest) = FilterAmenitiesByRequest(amenities, userMessage);
        if (isSpecificAmenityRequest && filteredAmenities.Count == 0)
        {
            return language == "es"
                ? "No encontramos informacion para ese servicio."
                : "We could not find information for that amenity.";
        }

        var lines = filteredAmenities.Select(amenity =>
        {
            var schedule = BuildScheduleText(amenity.AvailableFrom, amenity.AvailableTo, amenity.DaysOfWeek, language);
            var price = amenity.IsComplimentary
                ? (language == "es" ? "sin costo" : "complimentary")
                : (language == "es"
                    ? $"costo {FormatMoney(amenity.Price)}{FormatCurrency(amenity.Currency)}"
                    : $"cost {FormatMoney(amenity.Price)}{FormatCurrency(amenity.Currency)}");

            var reservation = amenity.RequiresReservation
                ? (language == "es" ? ", requiere reserva" : ", reservation required")
                : string.Empty;

            return $"- {amenity.Name}: {amenity.Description} ({schedule}, {price}{reservation}).";
        });

        var body = string.Join("\n", lines);
        if (!includeTitle)
        {
            return body;
        }

        var title = language == "es" ? "Servicios" : "Amenities";
        return $"{title}:\n{body}";
    }

    private async Task<string> BuildSchedulesPoliciesReplyAsync(
        string userMessage,
        string language,
        bool includeTitle,
        CancellationToken cancellationToken)
    {
        var schedules = await dbContext.HotelSchedules
            .AsNoTracking()
            .Where(schedule => schedule.IsActive)
            .OrderBy(schedule => schedule.DisplayOrder)
            .ThenBy(schedule => schedule.Title)
            .ToListAsync(cancellationToken);

        var policies = await dbContext.HotelPolicies
            .AsNoTracking()
            .Where(policy => policy.IsActive)
            .OrderBy(policy => policy.DisplayOrder)
            .ThenBy(policy => policy.Title)
            .ToListAsync(cancellationToken);

        var (filteredSchedules, filteredPolicies, isSpecificTopicRequest) = FilterSchedulesAndPoliciesByRequest(
            schedules,
            policies,
            userMessage);

        if (isSpecificTopicRequest && filteredSchedules.Count == 0 && filteredPolicies.Count == 0)
        {
            return language == "es"
                ? "No encontramos informacion para ese horario o politica."
                : "We could not find information for that schedule or policy.";
        }

        var sections = new List<string>();

        if (filteredSchedules.Count > 0)
        {
            var scheduleLines = filteredSchedules.Select(schedule =>
            {
                var scheduleText = BuildScheduleText(schedule.StartTime, schedule.EndTime, schedule.DaysOfWeek, language);
                var notes = string.IsNullOrWhiteSpace(schedule.Notes)
                    ? string.Empty
                    : (language == "es" ? $" Nota: {schedule.Notes}" : $" Note: {schedule.Notes}");

                return $"- {schedule.Title}: {scheduleText}.{notes}";
            });

            var heading = language == "es" ? "Horarios" : "Schedules";
            sections.Add($"{heading}:\n{string.Join("\n", scheduleLines)}");
        }

        if (filteredPolicies.Count > 0)
        {
            var policyLines = filteredPolicies.Select(policy => $"- {policy.Title}: {policy.Description}");
            var heading = language == "es" ? "Politicas" : "Policies";
            sections.Add($"{heading}:\n{string.Join("\n", policyLines)}");
        }

        if (sections.Count == 0)
        {
            return language == "es"
                ? "No hay horarios ni politicas cargadas en este momento."
                : "There are no schedules or policies configured right now.";
        }

        var content = string.Join("\n\n", sections);
        if (!includeTitle)
        {
            return content;
        }

        var title = language == "es" ? "Horarios y politicas" : "Schedules and policies";
        return $"{title}:\n{content}";
    }

    private static IntentDetection DetectIntent(string message, string language)
    {
        var normalized = message.ToLowerInvariant();
        var hasTwoDates = ParseDates(message, language).Count >= 2;

        var askAvailability = hasTwoDates || ContainsAny(normalized,
            "disponibilidad",
            "habitacion",
            "habitaciones",
            "reserva",
            "reservar",
            "availability",
            "room",
            "rooms",
            "booking");

        var askAmenities = ContainsAny(normalized,
            "servicio",
            "servicios",
            "gimnasio",
            "gym",
            "sauna",
            "lavanderia",
            "laundry",
            "pileta",
            "piscina",
            "pool",
            "wifi",
            "wi-fi",
            "internet",
            "amenities",
            "amenity");

        var askSchedulesPolicies = ContainsAny(normalized,
            "check-in",
            "checkin",
            "check-out",
            "checkout",
            "desayuno",
            "breakfast",
            "horario",
            "horarios",
            "schedule",
            "schedules",
            "politica",
            "politicas",
            "policy",
            "policies",
            "cancelacion",
            "cancellation",
            "mascotas",
            "pets");

        var activeIntentCount = new[] { askAvailability, askAmenities, askSchedulesPolicies }.Count(value => value);

        var intent = activeIntentCount switch
        {
            0 => IntentFallback,
            1 when askAvailability => IntentAvailability,
            1 when askAmenities => IntentAmenities,
            1 => IntentSchedulesPolicies,
            _ => IntentMixed
        };

        return new IntentDetection(intent, askAvailability, askAmenities, askSchedulesPolicies);
    }

    private async Task<int?> TryParseRoomTypeIdAsync(string message, CancellationToken cancellationToken)
    {
        var normalized = message.ToLowerInvariant();
        var roomTypes = await dbContext.RoomTypes
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var roomType in roomTypes)
        {
            if (normalized.Contains(roomType.Name.ToLowerInvariant()))
            {
                return roomType.Id;
            }
        }

        return null;
    }

    private static int? TryParseGuests(string message)
    {
        var guestMatch = GuestsRegex.Match(message);
        if (guestMatch.Success
            && int.TryParse(guestMatch.Groups["count"].Value, out var guests)
            && guests > 0)
        {
            return guests;
        }

        var forMatch = ForRegex.Match(message);
        if (forMatch.Success
            && int.TryParse(forMatch.Groups["count"].Value, out guests)
            && guests > 0)
        {
            return guests;
        }

        return null;
    }

    private static List<DateOnly> ParseDates(string message, string language)
    {
        var matches = IsoDateRegex.Matches(message)
            .Cast<Match>()
            .Concat(SlashOrDashDateRegex.Matches(message).Cast<Match>())
            .OrderBy(match => match.Index)
            .Select(match => match.Value)
            .ToList();

        var formats = language == "es"
            ? new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "MM/dd/yyyy", "M/d/yyyy" }
            : new[] { "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy", "MM-dd-yyyy", "M-d-yyyy", "dd/MM/yyyy", "d/M/yyyy" };

        var result = new List<DateOnly>();
        foreach (var token in matches)
        {
            if (!DateOnly.TryParseExact(token, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            if (!result.Contains(date))
            {
                result.Add(date);
            }
        }

        return result;
    }

    private static (IReadOnlyList<HotelAmenity> Amenities, bool IsSpecificRequest) FilterAmenitiesByRequest(
        IReadOnlyList<HotelAmenity> amenities,
        string userMessage)
    {
        var normalizedMessage = NormalizeText(userMessage);
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return (amenities.ToList(), false);
        }

        var amenityConceptTokens = new Dictionary<string, string[]>
        {
            ["wifi"] = ["wifi", "wi fi", "internet"],
            ["gym"] = ["gimnasio", "gym", "fitness"],
            ["sauna"] = ["sauna"],
            ["laundry"] = ["lavanderia", "laundry", "lavado", "planchado"],
            ["spa"] = ["spa"],
            ["pool"] = ["pileta", "piscina", "pool"],
            ["parking"] = ["estacionamiento", "parking", "cochera"]
        };

        var requestedConcepts = amenityConceptTokens
            .Where(entry => entry.Value.Any(token => ContainsToken(normalizedMessage, token)))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (requestedConcepts.Count > 0)
        {
            var filteredByConcept = amenities
                .Where(amenity => AmenityMatchesConcepts(amenity, requestedConcepts, amenityConceptTokens))
                .ToList();

            return (filteredByConcept, true);
        }

        var filteredByName = amenities
            .Where(amenity => ContainsToken(normalizedMessage, NormalizeText(amenity.Name)))
            .ToList();

        if (filteredByName.Count > 0)
        {
            return (filteredByName, true);
        }

        return (amenities.ToList(), false);
    }

    private static bool AmenityMatchesConcepts(
        HotelAmenity amenity,
        IReadOnlyCollection<string> requestedConcepts,
        IReadOnlyDictionary<string, string[]> amenityConceptTokens)
    {
        var searchableText = NormalizeText($"{amenity.Name} {amenity.Description}");

        return requestedConcepts.Any(concept =>
            amenityConceptTokens.TryGetValue(concept, out var tokens)
            && tokens.Any(token => ContainsToken(searchableText, token)));
    }

    private static (IReadOnlyList<HotelSchedule> Schedules, IReadOnlyList<HotelPolicy> Policies, bool IsSpecificRequest)
        FilterSchedulesAndPoliciesByRequest(
            IReadOnlyList<HotelSchedule> schedules,
            IReadOnlyList<HotelPolicy> policies,
            string userMessage)
    {
        var normalizedMessage = NormalizeText(userMessage);
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return (schedules.ToList(), policies.ToList(), false);
        }

        var schedulePolicyConceptTokens = new Dictionary<string, string[]>
        {
            ["breakfast"] = ["desayuno", "breakfast"],
            ["checkin"] = ["check in", "checkin", "ingreso"],
            ["checkout"] = ["check out", "checkout", "salida", "egreso"],
            ["frontdesk"] = ["recepcion", "front desk", "frontdesk"],
            ["cancellation"] = ["cancelacion", "cancellation", "cancelar"],
            ["pets"] = ["mascota", "mascotas", "pets", "pet"],
            ["children"] = ["nino", "ninos", "menor", "menores", "children", "child", "kids"]
        };

        var requestedConcepts = schedulePolicyConceptTokens
            .Where(entry => entry.Value.Any(token => ContainsToken(normalizedMessage, token)))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (requestedConcepts.Count > 0)
        {
            var filteredSchedules = schedules
                .Where(schedule => ScheduleMatchesConcepts(schedule, requestedConcepts, schedulePolicyConceptTokens))
                .ToList();

            var filteredPolicies = policies
                .Where(policy => PolicyMatchesConcepts(policy, requestedConcepts, schedulePolicyConceptTokens))
                .ToList();

            return (filteredSchedules, filteredPolicies, true);
        }

        return (schedules.ToList(), policies.ToList(), false);
    }

    private static bool ScheduleMatchesConcepts(
        HotelSchedule schedule,
        IReadOnlyCollection<string> requestedConcepts,
        IReadOnlyDictionary<string, string[]> conceptTokens)
    {
        var searchableText = NormalizeText($"{schedule.Code} {schedule.Title} {schedule.Notes}");

        return requestedConcepts.Any(concept =>
            conceptTokens.TryGetValue(concept, out var tokens)
            && tokens.Any(token => ContainsToken(searchableText, token)));
    }

    private static bool PolicyMatchesConcepts(
        HotelPolicy policy,
        IReadOnlyCollection<string> requestedConcepts,
        IReadOnlyDictionary<string, string[]> conceptTokens)
    {
        var searchableText = NormalizeText($"{policy.Code} {policy.Title} {policy.Description} {policy.Category}");

        return requestedConcepts.Any(concept =>
            conceptTokens.TryGetValue(concept, out var tokens)
            && tokens.Any(token => ContainsToken(searchableText, token)));
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    private static bool ContainsToken(string haystack, string token)
    {
        if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var normalizedToken = NormalizeText(token);
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return false;
        }

        return $" {haystack} ".Contains($" {normalizedToken} ", StringComparison.Ordinal);
    }

    private static string BuildFallbackReply(string language)
    {
        return language == "es"
            ? "Te puedo ayudar con disponibilidad, servicios y horarios/politicas del hotel. Ejemplo: 'Hay disponibilidad del 2026-05-10 al 2026-05-12 para 2 huespedes?'"
            : "I can help with availability, amenities, and hotel schedules/policies. Example: 'Do you have availability from 2026-05-10 to 2026-05-12 for 2 guests?'";
    }

    private static string BuildScheduleText(TimeOnly? from, TimeOnly? to, string? daysOfWeek, string language)
    {
        var dayText = string.Equals(daysOfWeek, EveryDayCode, StringComparison.OrdinalIgnoreCase)
            ? (language == "es" ? "todos los dias" : "every day")
            : (!string.IsNullOrWhiteSpace(daysOfWeek) ? daysOfWeek : (language == "es" ? "sin dias especificados" : "days not specified"));

        if (from.HasValue && to.HasValue)
        {
            return $"{FormatTime(from)}-{FormatTime(to)}, {dayText}";
        }

        if (from.HasValue)
        {
            return language == "es"
                ? $"desde {FormatTime(from)}, {dayText}"
                : $"from {FormatTime(from)}, {dayText}";
        }

        if (to.HasValue)
        {
            return language == "es"
                ? $"hasta {FormatTime(to)}, {dayText}"
                : $"until {FormatTime(to)}, {dayText}";
        }

        return language == "es"
            ? $"disponible, {dayText}"
            : $"available, {dayText}";
    }

    private static string FormatMoney(decimal? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.00", CultureInfo.InvariantCulture)
            : "0.00";
    }

    private static string FormatCurrency(string? currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? string.Empty
            : $" {currency.Trim().ToUpperInvariant()}";
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatTime(TimeOnly? value)
    {
        return value.HasValue
            ? value.Value.ToString("HH:mm", CultureInfo.InvariantCulture)
            : "--:--";
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(text.Contains);
    }

    private static string DetectLanguage(string text)
    {
        var normalized = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "en";
        }

        var spanishHints = new[]
        {
            "hola",
            "buenas",
            "hotel",
            "tiene",
            "tienen",
            "hay",
            "habitacion",
            "habitaciones",
            "disponibilidad",
            "reserva",
            "reservar",
            "servicio",
            "servicios",
            "desayuno",
            "horario",
            "horarios",
            "politica",
            "politicas",
            "huesped",
            "huespedes",
            "lavanderia",
            "gimnasio",
            "pileta",
            "piscina",
            "recepcion",
            "mascotas",
            "cancelacion",
            "precio",
            "gracias"
        };

        var englishHints = new[]
        {
            "hello",
            "hi",
            "hotel",
            "availability",
            "room",
            "rooms",
            "reservation",
            "reserve",
            "booking",
            "amenity",
            "amenities",
            "service",
            "services",
            "breakfast",
            "schedule",
            "schedules",
            "policy",
            "policies",
            "guest",
            "guests",
            "laundry",
            "gym",
            "pool",
            "front desk",
            "pets",
            "cancellation",
            "price",
            "thanks"
        };

        var spanishScore = spanishHints.Count(token => ContainsToken(normalized, token));
        var englishScore = englishHints.Count(token => ContainsToken(normalized, token));

        return spanishScore > englishScore ? "es" : "en";
    }

    private sealed record IntentDetection(
        string Intent,
        bool AskAvailability,
        bool AskAmenities,
        bool AskSchedulesOrPolicies);
}
