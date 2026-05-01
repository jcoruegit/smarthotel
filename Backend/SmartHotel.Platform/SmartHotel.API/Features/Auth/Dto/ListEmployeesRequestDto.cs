namespace SmartHotel.API.Features.Auth.Dto;

public sealed class ListEmployeesRequestDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int? DocumentTypeId { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Profile { get; set; }
}
