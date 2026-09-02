using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.SharedPos;

public sealed class RegisterSharedPosDeviceRequestDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

public sealed class SharedPosDeviceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class RegisteredSharedPosDeviceDto
{
    public SharedPosDeviceDto Device { get; set; } = new();
    public string DeviceToken { get; set; } = string.Empty;
}

public sealed class UpdateEmployeeSharedAccessRequestDto
{
    public Guid? UserId { get; set; }

    [RegularExpression("^[0-9]{4,6}$", ErrorMessage = "PIN harus 4-6 digit angka.")]
    public string? Pin { get; set; }

    public bool ClearUserLink { get; set; }
    public bool ClearPin { get; set; }
}

public sealed class EmployeeSharedAccessDto
{
    public Guid KaryawanId { get; set; }
    public Guid? UserId { get; set; }
    public string? LinkedUsername { get; set; }
    public bool HasPin { get; set; }
    public DateTime? PinUpdatedAt { get; set; }
}

public sealed class SharedPosUnlockRequestDto
{
    [Required, RegularExpression("^[0-9]{4,6}$")]
    public string Pin { get; set; } = string.Empty;
}

public sealed class SharedEmployeeDto
{
    public Guid Id { get; set; }
    public string Nama { get; set; } = string.Empty;
    public string Jabatan { get; set; } = string.Empty;
    public string? Role { get; set; }
    public bool CanAccessPos { get; set; }
}

public sealed class SharedAttendanceStateDto
{
    public string Date { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }
    public string? ScheduleStart { get; set; }
    public string? ScheduleEnd { get; set; }
    public string? ExceptionType { get; set; }
    public bool OutsideSchedule { get; set; }
    public string? NextAction { get; set; }
}

public sealed class SharedPosUnlockResponseDto
{
    public string SessionToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public SharedEmployeeDto Employee { get; set; } = new();
    public SharedAttendanceStateDto Attendance { get; set; } = new();
    public string? PosToken { get; set; }
    public DateTime? PosExpiresAtUtc { get; set; }
}

public sealed class SharedPosSessionDto
{
    public DateTime ExpiresAtUtc { get; set; }
    public SharedEmployeeDto Employee { get; set; } = new();
    public SharedAttendanceStateDto Attendance { get; set; } = new();
}

public sealed class SharedAttendanceResultDto
{
    public bool Ok { get; set; }
    public string RecordedAt { get; set; } = string.Empty;
    public SharedAttendanceStateDto Attendance { get; set; } = new();
}
