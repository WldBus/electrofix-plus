using System.Linq.Expressions;

using ElectroFix.Api.Common;
using ElectroFix.Api.Data;
using ElectroFix.Api.Entities;
using ElectroFix.Shared;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ElectroFix.Api.Services;

// Заявки с сайта без регистрации и проверка статуса ремонта по номеру квитанции
public class RequestsService(AppDbContext db, CurrentUser me, IMemoryCache cache)
{
    // сколько неверных попыток даю на один номер квитанции и на сколько закрываю проверку
    public const int MaxStatusAttempts = 5;
    private static readonly TimeSpan StatusLock = TimeSpan.FromMinutes(15);

    private static readonly Expression<Func<RepairRequest, RepairRequestDto>> ToDto = r => new RepairRequestDto(
        r.RequestId, r.DeviceType, r.DeviceModel, r.Problem, r.DeliveryMethod, r.Branch, r.Address,
        r.ServiceId, r.Service != null ? r.Service.Name : null, r.ClientName, r.Phone, r.Email,
        r.Status, r.Comment, r.CreatedAt, r.ProcessedAt, r.Processor != null ? r.Processor.FullName : null);

    private static readonly Dictionary<string, Expression<Func<RepairRequest, object?>>> Sort = new()
    {
        ["id"] = r => r.RequestId,
        ["created"] = r => r.CreatedAt,
        ["status"] = r => r.Status,
        ["name"] = r => r.ClientName,
    };

    public async Task<int> CreateAsync(RepairRequestSaveRequest r)
    {
        CheckDelivery(r.DeliveryMethod, r.Branch, r.Address);
        if (r.ServiceId is { } serviceId && !await db.Services.AnyAsync(s => s.ServiceId == serviceId))
        {
            throw ApiException.Field(nameof(r.ServiceId), "Такой работы нет в прайсе");
        }
        var request = new RepairRequest
        {
            DeviceType = r.DeviceType.Trim(),
            DeviceModel = r.DeviceModel.Trim(),
            Problem = r.Problem.Trim(),
            DeliveryMethod = r.DeliveryMethod,
            // лишнее поле не сохраняю: при «принесу сам» адрес не нужен, и наоборот
            Branch = r.DeliveryMethod == "self" ? r.Branch : null,
            Address = r.DeliveryMethod == "self" ? null : r.Address?.Trim(),
            ServiceId = r.ServiceId,
            ClientName = r.ClientName.Trim(),
            Phone = r.Phone,
            Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim().ToLowerInvariant(),
        };
        db.RepairRequests.Add(request);
        await db.SaveChangesAsync();
        return request.RequestId;
    }

    // принесёт сам — нужен один из наших филиалов; курьер или выезд мастера — нужен адрес
    public static void CheckDelivery(string method, string? branch, string? address)
    {
        if (method == "self" && !Branches.All.Contains(branch))
        {
            throw ApiException.Field(nameof(RepairRequestSaveRequest.Branch), "Выберите филиал, куда принесёте технику");
        }
        if (method != "self" && string.IsNullOrWhiteSpace(address))
        {
            throw ApiException.Field(nameof(RepairRequestSaveRequest.Address), "Укажите адрес, откуда забрать технику");
        }
    }

    public Task<PagedResult<RepairRequestDto>> ListAsync(StatusListQuery q)
    {
        var query = db.RepairRequests.AsQueryable();
        if (!string.IsNullOrEmpty(q.Status))
        {
            query = query.Where(r => r.Status == q.Status);
        }
        if (q.Search.Like() is { } like)
        {
            query = query.Where(r => EF.Functions.ILike(r.ClientName, like) || EF.Functions.ILike(r.Phone, like)
                                     || EF.Functions.ILike(r.DeviceModel, like));
        }
        return query.SortBy(q.Sort, Sort, "created,desc").ToPagedAsync(q, ToDto);
    }

    public async Task<RepairRequestDto> GetAsync(int id) =>
        await db.RepairRequests.Where(r => r.RequestId == id).Select(ToDto).SingleOrDefaultAsync()
        ?? throw ApiException.NotFound("Заявка");

    // приёмщик взял заявку в работу, оформил клиента и запись или отклонил
    public async Task<RepairRequestDto> SetStatusAsync(int id, RequestStatusChange r)
    {
        var request = await db.RepairRequests.FindAsync(id) ?? throw ApiException.NotFound("Заявка");
        if (request.Status is "done" or "rejected")
        {
            throw ApiException.Conflict("Заявка уже закрыта");
        }
        if (r.Status == "rejected" && string.IsNullOrWhiteSpace(r.Comment))
        {
            throw ApiException.Field(nameof(r.Comment), "Напишите, почему заявка отклонена");
        }
        request.Status = r.Status;
        request.Comment = string.IsNullOrWhiteSpace(r.Comment) ? request.Comment : r.Comment.Trim();
        if (r.Status is "done" or "rejected")
        {
            request.ProcessedBy = me.RequiredId;
            request.ProcessedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    // Статус ремонта без входа. На неверные номер и телефон ответ одинаковый,
    // чтобы нельзя было понять, есть ли такой номер. После 5 ошибок номер закрываю на 15 минут
    public async Task<RepairStatusDto> StatusAsync(RepairStatusQuery q)
    {
        var number = RepairStages.Normalize(q.Number)
                     ?? throw ApiException.Field(nameof(q.Number), "Номер квитанции выглядит так: ЗН-2026-00012");
        var key = "status-attempts:" + number;
        if (cache.TryGetValue(key, out int failed) && failed >= MaxStatusAttempts)
        {
            throw new ApiException(429, "Слишком много неверных попыток. Попробуйте через 15 минут или позвоните нам");
        }

        var found = await db.WorkOrders
            .Where(w => w.WorkOrderNumber == number && w.Checkin!.Device!.Client!.Phone.EndsWith(q.Phone))
            .Select(w => new RepairStatusDto(
                w.WorkOrderNumber, w.Status,
                w.Checkin!.Device!.Type + " " + w.Checkin.Device.Brand + " " + w.Checkin.Device.Model,
                w.OpenedAt, w.ClosedAt))
            .SingleOrDefaultAsync();
        if (found == null)
        {
            cache.Set(key, failed + 1, StatusLock);
            throw new ApiException(404, "Ремонт не найден. Проверьте номер квитанции и последние 4 цифры телефона");
        }
        cache.Remove(key);
        return found;
    }
}
