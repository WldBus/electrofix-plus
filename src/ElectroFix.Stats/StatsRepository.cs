using System.Data.Common;

using ElectroFix.Shared;

using Npgsql;

namespace ElectroFix.Stats;

// Запросы к представлениям статистики. EF тут не нужен: пять простых SELECT
public class StatsRepository(NpgsqlDataSource db)
{
    public async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var command = db.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync();
            return true;
        }
        catch (NpgsqlException)
        {
            return false;
        }
    }

    public async Task<DashboardDto> DashboardAsync(int months)
    {
        var summary = (await QueryAsync(
            "SELECT clients, users, active_orders, total_debt FROM v_stats_summary",
            r => new StatsSummaryDto(Int(r, 0), Int(r, 1), Int(r, 2), r.GetDecimal(3))))[0];

        var statuses = await QueryAsync(
            "SELECT status, orders FROM v_work_order_status_counts ORDER BY orders DESC",
            r => new StatusCountDto(r.GetString(0), Int(r, 1)));

        var revenue = await QueryAsync(
            ByMonth("v_revenue_by_month v", "coalesce(v.income, 0), coalesce(v.refunds, 0)"),
            r => new RevenueMonthDto(Month(r), r.GetDecimal(1), r.GetDecimal(2)), ("months", months));

        // сервис статистики видит только админ, поэтому ФИО мастеров скрываю всегда
        var workload = await QueryAsync(
            "SELECT master_id, master, active_orders, norm_hours, actual_hours FROM v_master_workload ORDER BY active_orders DESC, master",
            r => new MasterWorkloadDto(r.GetInt32(0), PersonalData.MaskName(r.GetString(1))!, Int(r, 2), r.GetDecimal(3), r.GetDecimal(4)));

        var parts = await QueryAsync(
            "SELECT part_id, article, name, min_stock, on_hand FROM v_stats_parts_below_min ORDER BY min_stock - on_hand DESC",
            r => new PartBelowMinDto(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetDecimal(4)));

        var users = (await QueryAsync(
            "SELECT total, active, blocked, deleted FROM v_stats_users",
            r => new UserStatsDto(Int(r, 0), Int(r, 1), Int(r, 2), Int(r, 3))))[0];

        var byRole = await QueryAsync(
            "SELECT role, users FROM v_stats_users_by_role ORDER BY role_id",
            r => new RoleCountDto(r.GetString(0), Int(r, 1)));

        var registrations = await QueryAsync(
            ByMonth("v_stats_registrations_by_month v", "coalesce(v.users, 0)"),
            r => new MonthCountDto(Month(r), Int(r, 1)), ("months", months));

        // безопасность — по дням за последние 30 дней (по Москве)
        var security = await QueryAsync(
            """
            SELECT d::date, coalesce(v.logins, 0), coalesce(v.failed, 0), coalesce(v.denied, 0), coalesce(v.not_found, 0)
            FROM generate_series((now() AT TIME ZONE 'Europe/Moscow')::date - 29,
                                 (now() AT TIME ZONE 'Europe/Moscow')::date, interval '1 day') AS d
            LEFT JOIN v_stats_security_by_day v ON v.day = d::date
            ORDER BY 1
            """,
            r => new SecurityDayDto(Month(r), Int(r, 1), Int(r, 2), Int(r, 3), Int(r, 4)));

        var ips = await QueryAsync(
            "SELECT ip, attempts, last_at FROM v_stats_suspicious_ips ORDER BY attempts DESC LIMIT 10",
            r => new SuspiciousIpDto(r.GetString(0), Int(r, 1), r.GetDateTime(2)));

        var newByMonth = await QueryAsync(
            ByMonth("v_stats_new_by_month v", "coalesce(v.clients, 0), coalesce(v.orders, 0)"),
            r => new NewByMonthDto(Month(r), Int(r, 1), Int(r, 2)), ("months", months));

        var repairTime = await QueryAsync(
            ByMonth("v_stats_repair_time_by_month v", "coalesce(v.closed, 0), v.avg_days"),
            r => new RepairTimeMonthDto(Month(r), Int(r, 1), r.IsDBNull(2) ? null : r.GetDecimal(2)), ("months", months));

        var topServices = await QueryAsync(
            "SELECT name, times, revenue FROM v_stats_top_services ORDER BY revenue DESC, times DESC LIMIT 10",
            r => new TopServiceDto(r.GetString(0), Int(r, 1), r.GetDecimal(2)));

        var topParts = await QueryAsync(
            "SELECT article, name, unit, used FROM v_stats_top_parts WHERE used > 0 ORDER BY used DESC LIMIT 10",
            r => new TopPartDto(r.GetString(0), r.GetString(1), r.GetString(2), r.GetDecimal(3)));

        return new DashboardDto(summary, statuses, revenue, workload, parts, users, byRole, registrations,
            security, ips, newByMonth, repairTime, topServices, topParts);
    }

    // последние N месяцев, даже если в каком-то месяце ничего не было (generate_series даёт нули)
    private static string ByMonth(string view, string columns) => $"""
        SELECT m::date, {columns}
        FROM generate_series(date_trunc('month', now()) - make_interval(months => @months - 1),
                             date_trunc('month', now()), interval '1 month') AS m
        LEFT JOIN {view} ON v.month = m::date
        ORDER BY 1
        """;

    private static int Int(DbDataReader r, int i) => Convert.ToInt32(r[i]);

    private static DateOnly Month(DbDataReader r) => DateOnly.FromDateTime(r.GetDateTime(0));

    private async Task<List<T>> QueryAsync<T>(string sql, Func<DbDataReader, T> map, params (string Name, object Value)[] args)
    {
        await using var command = db.CreateCommand(sql);
        foreach (var (name, value) in args)
        {
            command.Parameters.AddWithValue(name, value);
        }
        await using var reader = await command.ExecuteReaderAsync();
        var list = new List<T>();
        while (await reader.ReadAsync())
        {
            list.Add(map(reader));
        }
        return list;
    }
}
