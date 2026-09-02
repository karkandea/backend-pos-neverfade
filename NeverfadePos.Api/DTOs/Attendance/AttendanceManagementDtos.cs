using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Attendance;

public sealed class AttendancePolicyDto
{
    public int GraceMinutes { get; set; }
    public int AbsenceThresholdMinutes { get; set; }
}

public sealed class UpdateAttendancePolicyRequestDto
{
    [Range(0, 180)]
    public int GraceMinutes { get; set; }

    [Range(1, 720)]
    public int AbsenceThresholdMinutes { get; set; }
}

public sealed class WeeklyScheduleDayDto
{
    [Range(0, 6)]
    public int DayOfWeek { get; set; }

    public bool IsWorkingDay { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }
}

public sealed class ReplaceWeeklyScheduleRequestDto
{
    [Required]
    public List<WeeklyScheduleDayDto> Days { get; set; } = [];
}

public sealed class ScheduleExceptionDto
{
    public Guid Id { get; set; }
    public Guid KaryawanId { get; set; }
    public DateOnly Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Note { get; set; }
}

public sealed class UpsertScheduleExceptionRequestDto
{
    public Guid KaryawanId { get; set; }
    public DateOnly Date { get; set; }

    [Required, RegularExpression("^(leave|holiday|changed_shift|off)$")]
    public string Type { get; set; } = string.Empty;

    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public sealed class AttendanceDashboardSummaryDto
{
    public int Scheduled { get; set; }
    public int Present { get; set; }
    public int Late { get; set; }
    public int Absent { get; set; }
    public int Working { get; set; }
    public int MissingCheckout { get; set; }
}

public sealed class AttendanceDashboardRowDto
{
    public Guid KaryawanId { get; set; }
    public string KaryawanNama { get; set; } = string.Empty;
    public string Jabatan { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ScheduleStart { get; set; }
    public string? ScheduleEnd { get; set; }
    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }
    public bool OutsideSchedule { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionNote { get; set; }
}

public sealed class AttendanceDashboardDto
{
    public string Date { get; set; } = string.Empty;
    public AttendanceDashboardSummaryDto Summary { get; set; } = new();
    public List<AttendanceDashboardRowDto> Employees { get; set; } = [];
}

public sealed class CorrectAttendanceRequestDto
{
    public Guid KaryawanId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }

    [Required, MinLength(3), MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AttendanceCorrectionDto
{
    public Guid Id { get; set; }
    public Guid AbsensiId { get; set; }
    public Guid CorrectedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string BeforeData { get; set; } = string.Empty;
    public string AfterData { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
