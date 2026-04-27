namespace SmartHotel.API.Features.Chat.Dto;

public sealed record ChatMessageResponseDto(
    string Reply,
    string DetectedLanguage,
    string DetectedIntent);
