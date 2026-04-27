using SmartHotel.API.Common.Errors;
using SmartHotel.API.Features.Chat.Dto;
using SmartHotel.API.Features.Chat.Services;

namespace SmartHotel.API.Features.Chat.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/chat")
            .WithTags("Chat")
            .AllowAnonymous();

        group.MapPost("/message", HandleAsync)
            .WithName("SendChatMessage")
            .WithSummary("Enviar mensaje al chat")
            .WithDescription("Recibe un mensaje del usuario y devuelve una respuesta inicial del asistente del hotel.")
            .Accepts<ChatMessageRequestDto>("application/json")
            .Produces<ChatMessageResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static Task<IResult> HandleAsync(
        ChatMessageRequestDto? request,
        ChatResponseService chatResponseService,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new UserFriendlyException("El cuerpo de la solicitud es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new UserFriendlyException("El campo 'message' es obligatorio.");
        }

        return HandleCoreAsync(request, chatResponseService, cancellationToken);
    }

    private static async Task<IResult> HandleCoreAsync(
        ChatMessageRequestDto request,
        ChatResponseService chatResponseService,
        CancellationToken cancellationToken)
    {
        var response = await chatResponseService.BuildResponseAsync(request.Message, cancellationToken);
        return TypedResults.Ok(response);
    }
}
