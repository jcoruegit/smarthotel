namespace SmartHotel.API.Common.Errors;

public sealed class UserFriendlyException : Exception
{
    public UserFriendlyException(string message, int statusCode = StatusCodes.Status400BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
