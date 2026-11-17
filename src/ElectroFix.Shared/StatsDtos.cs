namespace ElectroFix.Shared;

// всё, что нужно графикам админки, одним ответом сервиса статистики
public record DashboardDto(
    StatsSummaryDto Summary,
    IReadOnlyList<StatusCountDto> WorkOrdersByStatus,
    IReadOnlyList<RevenueMonthDto> RevenueByMonth,
    IReadOnlyList<MasterWorkloadDto> MasterWorkload,
    IReadOnlyList<PartBelowMinDto> PartsBelowMin,
    UserStatsDto Users,
    IReadOnlyList<RoleCountDto> UsersByRole,
    IReadOnlyList<MonthCountDto> RegistrationsByMonth,
    IReadOnlyList<SecurityDayDto> SecurityByDay,
    IReadOnlyList<SuspiciousIpDto> SuspiciousIps,
    IReadOnlyList<NewByMonthDto> NewByMonth,
    IReadOnlyList<RepairTimeMonthDto> RepairTimeByMonth,
    IReadOnlyList<TopServiceDto> TopServices,
    IReadOnlyList<TopPartDto> TopParts);

public record StatsSummaryDto(int Clients, int Users, int ActiveOrders, decimal TotalDebt);

public record StatusCountDto(string Status, int Orders);

public record RevenueMonthDto(DateOnly Month, decimal Income, decimal Refunds);

public record MasterWorkloadDto(int MasterId, string Master, int ActiveOrders, decimal NormHours, decimal ActualHours);

public record PartBelowMinDto(int PartId, string Article, string Name, int MinStock, decimal OnHand)
{
    public decimal ToOrder => MinStock - OnHand;
}

// активные — входили за последние 30 дней и не заблокированы
public record UserStatsDto(int Total, int Active, int Blocked, int Deleted);

public record RoleCountDto(string Role, int Users);

public record MonthCountDto(DateOnly Month, int Count);

public record SecurityDayDto(DateOnly Day, int Logins, int Failed, int Denied, int NotFound);

public record SuspiciousIpDto(string Ip, int Attempts, DateTime LastAt);

public record NewByMonthDto(DateOnly Month, int Clients, int Orders);

public record RepairTimeMonthDto(DateOnly Month, int Closed, decimal? AvgDays);

public record TopServiceDto(string Name, int Times, decimal Revenue);

public record TopPartDto(string Article, string Name, string Unit, decimal Used);
