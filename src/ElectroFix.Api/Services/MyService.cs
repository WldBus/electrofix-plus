using ElectroFix.Api.Common;
using ElectroFix.Api.Data;
using ElectroFix.Shared;

using Microsoft.EntityFrameworkCore;

namespace ElectroFix.Api.Services;

// Личный кабинет клиента. Всё, что тут отдаётся, относится только к его карточке клиента:
// чужие устройства и заказ-наряды для него «не найдены» (404), а не «нет прав»,
// чтобы по номерам нельзя было угадать, что они существуют
public class MyService(AppDbContext db, CurrentUser current, RepairService repair, WorkflowService workflow, ClientsService clients)
{
    // Карточка клиента для текущей учётки. Первый раз ищу клиента с тем же email или
    // телефоном (его мог завести приёмщик, когда человек пришёл в сервис) и привязываю.
    // Не нашёл — завожу нового клиента-физлицо процедурой sp_client_add
    public async Task<int> ClientIdAsync()
    {
        var userId = current.RequiredId;
        var linked = await db.Clients.IgnoreQueryFilters().Where(c => c.UserId == userId)
            .Select(c => new { c.ClientId, c.DeletedAt }).SingleOrDefaultAsync();
        if (linked != null)
        {
            return linked.DeletedAt == null
                ? linked.ClientId
                : throw ApiException.Conflict("Карточка клиента отключена. Обратитесь в сервис");
        }

        var user = await db.Users.SingleAsync(u => u.UserId == userId);
        var match = await db.Clients
            .Where(c => c.UserId == null && ((c.Email != null && c.Email.ToLower() == user.Email) || c.Phone == user.Phone))
            .OrderBy(c => c.ClientId)
            .FirstOrDefaultAsync();

        if (match == null)
        {
            if (user.Phone == null)
            {
                throw ApiException.Conflict("В учётной записи нет телефона. Обратитесь в сервис, чтобы его добавили");
            }
            var id = await db.CallAsync<int>("CALL sp_client_add('individual', @name, @phone, NULL, NULL, @email, NULL)",
                ("name", user.FullName), ("phone", user.Phone), ("email", user.Email));
            match = await db.Clients.SingleAsync(c => c.ClientId == id);
        }

        match.UserId = userId;
        await db.SaveChangesAsync();
        return match.ClientId;
    }

    public async Task<ClientDto> ProfileAsync() => await clients.GetAsync(await ClientIdAsync());

    public async Task<PagedResult<DeviceDto>> DevicesAsync() =>
        await repair.ListDevicesAsync(new ListQuery { Size = 100 }, await ClientIdAsync());

    public async Task<DeviceDto> AddDeviceAsync(DeviceSaveRequest r) => await workflow.AddDeviceAsync(await ClientIdAsync(), r);

    public async Task<PagedResult<CheckinDto>> CheckinsAsync(StatusListQuery q) =>
        await repair.ListCheckinsAsync(q, await ClientIdAsync());

    public async Task<CheckinDto> BookAsync(CheckinSaveRequest r)
    {
        var clientId = await ClientIdAsync();
        if (!await db.Devices.AnyAsync(d => d.DeviceId == r.DeviceId && d.ClientId == clientId))
        {
            throw ApiException.Field(nameof(r.DeviceId), "Выберите своё устройство");
        }
        return await workflow.BookAsync(r, byClient: true);
    }

    public async Task CancelCheckinAsync(int id)
    {
        var clientId = await ClientIdAsync();
        var checkin = await db.Checkins.SingleOrDefaultAsync(c => c.CheckinId == id && c.Device!.ClientId == clientId)
                      ?? throw ApiException.NotFound("Запись на приём");
        await workflow.CancelCheckinAsync(checkin);
    }

    public async Task<PagedResult<WorkOrderDto>> WorkOrdersAsync(WorkOrderListQuery q)
    {
        q.ClientId = await ClientIdAsync();
        q.MasterId = null;
        return await repair.ListWorkOrdersAsync(q);
    }

    public async Task<WorkOrderDetailsDto> WorkOrderAsync(int id)
    {
        var details = await repair.GetWorkOrderAsync(id);
        return details.Header.ClientId == await ClientIdAsync() ? details : throw ApiException.NotFound("Заказ-наряд");
    }

    public async Task<WorkOrderDetailsDto> DecideEstimateAsync(int id, EstimateDecisionRequest r)
    {
        await WorkOrderAsync(id);
        return await workflow.DecideEstimateAsync(id, r);
    }
}
