using NeverfadePos.Api.DTOs.User;

namespace NeverfadePos.Api.Services.Users;

public interface IUserService
{
    Task<List<UserDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<UserDto> CreateAsync(
        CreateUserDto request,
        CancellationToken cancellationToken = default);

    Task<UserDto> UpdateAsync(
        Guid id,
        UpdateUserDto request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
