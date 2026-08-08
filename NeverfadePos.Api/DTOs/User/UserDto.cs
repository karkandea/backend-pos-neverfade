namespace NeverfadePos.Api.DTOs.User;

public sealed class UserDto
{
    public Guid Id { get; set; }

    public string Nama { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool Active { get; set; }

    public DateTime CreatedAt { get; set; }
}
