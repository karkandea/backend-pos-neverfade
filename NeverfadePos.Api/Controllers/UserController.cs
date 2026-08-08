using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.User;
using NeverfadePos.Api.Services.Users;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize(Roles = "owner,admin")]
[Route("api/users")]
public sealed class UserController(
    IUserService userService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await userService.GetAllAsync(
            cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(
        CreateUserDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await userService.CreateAsync(
            request,
            cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> Update(
        Guid id,
        UpdateUserDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await userService.UpdateAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await userService.DeleteAsync(
            id,
            cancellationToken);

        return Ok(new { ok = true });
    }
}
