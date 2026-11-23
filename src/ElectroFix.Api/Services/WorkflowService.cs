using ElectroFix.Api.Common;
using ElectroFix.Api.Data;
using ElectroFix.Api.Entities;
using ElectroFix.Shared;

using Microsoft.EntityFrameworkCore;

namespace ElectroFix.Api.Services;

// Рабочие операции сотрудников и клиента: запись, приёмка, смета, ремонт, оплата, склад.
// Главные проверки (переходы статусов, остатки, переплата) уже сидят в триггерах и
// процедурах БД. Тут я добавляю то, что база не знает: кто какую кнопку может нажать
public class WorkflowService(AppDbContext db, CurrentUser current, RepairService repair)
{
    private static readonly TimeZoneInfo Moscow = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");

    // какие статусы заказ-наряда каждая роль меняет обычной кнопкой. Остальные переходы
    // идут через отдельные действия: смета, согласование, назначение мастера, закрытие
    // ---------- устройства и записи ----------

    public async Task<DeviceDto> AddDeviceAsync(int clientId, DeviceSaveRequest r)
    {
        if (!await db.Clients.AnyAsync(c => c.ClientId == clientId))
        {
            throw ApiException.Field(nameof(r.ClientId), "Такого клиента нет");
        }
        var device = new Device
        {
            ClientId = clientId,
            Type = r.Type.Trim(),
            Brand = r.Brand.Trim(),
            Model = r.Model.Trim(),
            ManufactureYear = r.ManufactureYear,
            SerialNumber = r.SerialNumber.Trim(),
            InventoryNumber = string.IsNullOrWhiteSpace(r.InventoryNumber) ? null : r.InventoryNumber.Trim(),
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return await repair.GetDeviceAsync(device.DeviceId);
    }

    // клиент записывается сам (статус «записан»), приёмщик — сразу «подтверждён» на себя
    public async Task<CheckinDto> BookAsync(CheckinSaveRequest r, bool byClient)
    {
        var start = MoscowToUtc(r.SlotStart!.Value);
        if (start < DateTime.UtcNow.AddMinutes(-5))
        {
            throw ApiException.Field(nameof(r.SlotStart), "Это время уже прошло");
        }
        if (start > DateTime.UtcNow.AddDays(90))
        {
            throw ApiException.Field(nameof(r.SlotStart), "Записаться можно не дальше чем на 90 дней вперёд");
        }
        if (!Branches.All.Contains(r.Branch))
        {
            throw ApiException.Field(nameof(r.Branch), "Такого филиала нет");
        }
        if (!await db.Devices.AnyAsync(d => d.DeviceId == r.DeviceId))
        {
            throw ApiException.Field(nameof(r.DeviceId), "Такого устройства нет");
        }

        var checkin = new Checkin
        {
            DeviceId = r.DeviceId,
            Branch = r.Branch,
            SlotStart = start,
            SlotEnd = start.AddMinutes(byClient ? 60 : r.DurationMinutes),
            ServicesRequested = Clean(r.ServicesRequested),
            Defects = r.Defects.Trim(),
            Priority = byClient ? "normal" : r.Priority,
            Status = byClient ? "booked" : "confirmed",
            ReceptionistId = byClient ? null : current.RequiredId,
        };
        db.Checkins.Add(checkin);
        await db.SaveChangesAsync();
        return await repair.GetCheckinAsync(checkin.CheckinId);
    }

    // приёмщик подтверждает запись или отмечает отмену / неявку
    public async Task SetCheckinStatusAsync(int id, string status)
    {
        var c = await db.Checkins.SingleOrDefaultAsync(x => x.CheckinId == id) ?? throw ApiException.NotFound("Запись на приём");
        var allowed = c.Status switch
        {
            "booked" => new[] { "confirmed", "cancelled", "no_show" },
            "confirmed" => ["cancelled", "no_show"],
            _ => [],
        };
        if (!allowed.Contains(status))
        {
            throw ApiException.Conflict(
                $"Запись в статусе «{Labels.Of(Labels.CheckinStatuses, c.Status)}» нельзя перевести в «{Labels.Of(Labels.CheckinStatuses, status)}»");
        }
        c.Status = status;
        // в подтверждённой записи должен быть приёмщик (так требует ограничение в базе)
        c.ReceptionistId ??= current.RequiredId;
        await db.SaveChangesAsync();
    }

    // отмена своей записи клиентом
    public async Task CancelCheckinAsync(Checkin c)
    {
        if (c.Status is not ("booked" or "confirmed"))
        {
            throw ApiException.Conflict("Эту запись уже нельзя отменить");
        }
        c.Status = "cancelled";
        await db.SaveChangesAsync();
    }

    // приёмка: записываю результат осмотра и открываю заказ-наряд процедурой БД
    public async Task<int> OpenWorkOrderAsync(int checkinId, OpenWorkOrderRequest r)
    {
        var c = await db.Checkins.SingleOrDefaultAsync(x => x.CheckinId == checkinId)
                ?? throw ApiException.NotFound("Запись на приём");
        c.ConditionState = Clean(r.ConditionState) ?? c.ConditionState;
        c.RuntimeAtCheckin = r.RuntimeAtCheckin ?? c.RuntimeAtCheckin;
        await db.SaveChangesAsync();

        return await db.CallAsync<int>("CALL sp_open_work_order(@checkin, @receptionist, NULL)",
            ("checkin", checkinId), ("receptionist", current.RequiredId));
    }

    // ---------- заказ-наряд ----------

    public async Task<WorkOrderDetailsDto> ChangeStatusAsync(int id, string status)
    {
        var w = await LoadAsync(id);
        if (!WorkOrderRules.MovesFor(current.Role!, w.Status).Contains(status))
        {
            throw ApiException.Conflict(
                $"Вы не можете перевести заказ-наряд из «{Labels.Of(Labels.WorkOrderStatuses, w.Status)}» " +
                $"в «{Labels.Of(Labels.WorkOrderStatuses, status)}»");
        }
        w.Status = status;
        await db.SaveChangesAsync();
        return await repair.GetWorkOrderAsync(id);
    }

    public async Task<WorkOrderDetailsDto> AssignMasterAsync(int id, int masterId)
    {
        await LoadAsync(id);
        await db.CallAsync<object>("CALL sp_assign_master(@wo, @master)", ("wo", id), ("master", masterId));
        return await repair.GetWorkOrderAsync(id);
    }

    public async Task<WorkOrderDetailsDto> AddServiceAsync(int id, OrderServiceAddRequest r)
    {
        var w = await LoadForEstimateAsync(id);
        if (!await db.Services.AnyAsync(s => s.ServiceId == r.ServiceId))
        {
            throw ApiException.Field(nameof(r.ServiceId), "Такой работы нет в справочнике");
        }
        db.OrderServices.Add(new OrderService
        {
            WorkOrderId = w.WorkOrderId,
            ServiceId = r.ServiceId,
            DiscountMarkup = r.DiscountMarkup,
            EstimateVersion = await NextVersionAsync(id),
        });
        await db.SaveChangesAsync();
        return await repair.GetWorkOrderAsync(id);
    }

    public async Task<WorkOrderDetailsDto> AddPartAsync(int id, OrderPartAddRequest r)
    {
        var w = await LoadForEstimateAsync(id);
        if (!await db.Parts.AnyAsync(p => p.PartId == r.PartId))
        {
            throw ApiException.Field(nameof(r.PartId), "Такой запчасти нет в справочнике");
        }
        db.OrderParts.Add(new OrderPart
        {
            WorkOrderId = w.WorkOrderId,
            PartId = r.PartId,
            Quantity = r.Quantity,
            Price = r.Price,
        });
        await db.SaveChangesAsync();
        return await repair.GetWorkOrderAsync(id);
    }

    // убрать можно только черновик: то, что клиент уже видел, остаётся в истории
    public async Task<WorkOrderDetailsDto> RemoveLineAsync(int id, int lineId, bool isPart)
    {
        await LoadForEstimateAsync(id);
        var removed = isPart
            ? await db.OrderParts.Where(p => p.OrderPartId == lineId && p.WorkOrderId == id && p.ApprovalStatus == "draft").ExecuteDeleteAsync()
            : await db.OrderServices.Where(s => s.OrderServiceId == lineId && s.WorkOrderId == id && s.ApprovalStatus == "draft").ExecuteDeleteAsync();
        if (removed == 0)
        {
            throw ApiException.Conflict("Удалить можно только позицию-черновик, которую ещё не отправили клиенту");
        }
        return await repair.GetWorkOrderAsync(id);
    }

    // мастер отмечает, сколько часов реально ушло на работу
    public async Task<WorkOrderDetailsDto> SetActualTimeAsync(int id, int lineId, decimal hours)
    {
        var w = await LoadAsync(id);
        if (w.Status is not ("in_progress" or "assigned"))
        {
            throw ApiException.Conflict("Время отмечается, пока заказ-наряд в работе");
        }
        var line = await db.OrderServices.SingleOrDefaultAsync(s => s.OrderServiceId == lineId && s.WorkOrderId == id)
                   ?? throw ApiException.NotFound("Работа в смете");
        if (line.ApprovalStatus != "approved")
        {
            throw ApiException.Conflict("Время отмечается только по согласованным работам");
        }
        line.ActualTime = hours;
        await db.SaveChangesAsync();
        return await repair.GetWorkOrderAsync(id);
    }

    // черновики уходят клиенту на согласование, заказ-наряд — в статус «Смета»
    public async Task<WorkOrderDetailsDto> SendEstimateAsync(int id)
    {
        var w = await LoadForEstimateAsync(id);
        var services = await db.OrderServices.Where(s => s.WorkOrderId == id && s.ApprovalStatus == "draft").ToListAsync();
        var parts = await db.OrderParts.Where(p => p.WorkOrderId == id && p.ApprovalStatus == "draft").ToListAsync();
        if (services.Count + parts.Count == 0)
        {
            throw ApiException.Conflict("В смете нет новых позиций — сначала добавьте работы или запчасти");
        }
        services.ForEach(s => s.ApprovalStatus = "pending");
        parts.ForEach(p => p.ApprovalStatus = "pending");

        // из «Нового» в «Смету» напрямую нельзя (так в триггере), поэтому через диагностику
        if (w.Status == "new")
        {
            w.Status = "diagnostics";
            await db.SaveChangesAsync();
        }
        w.Status = "estimate";
        await db.SaveChangesAsync();
        return await repair.GetWorkOrderAsync(id);
    }

    // согласование сметы: клиент в кабинете или приёмщик со слов клиента по телефону
    public async Task<WorkOrderDetailsDto> DecideEstimateAsync(int id, EstimateDecisionRequest r)
    {
        var w = await db.WorkOrders.SingleOrDefaultAsync(x => x.WorkOrderId == id) ?? throw ApiException.NotFound("Заказ-наряд");
        if (w.Status != "estimate")
        {
            throw ApiException.Conflict("Сейчас по заказ-наряду нечего согласовывать");
        }
        var services = await db.OrderServices.Where(s => s.WorkOrderId == id && s.ApprovalStatus == "pending").ToListAsync();
        var parts = await db.OrderParts.Where(p => p.WorkOrderId == id && p.ApprovalStatus == "pending").ToListAsync();
        if (services.Count + parts.Count == 0)
        {
            throw ApiException.Conflict("Позиций на согласовании нет");
        }
        services.ForEach(s => s.ApprovalStatus = r.ApprovedServiceIds.Contains(s.OrderServiceId) ? "approved" : "rejected");
        parts.ForEach(p => p.ApprovalStatus = r.ApprovedPartIds.Contains(p.OrderPartId) ? "approved" : "rejected");
        await db.SaveChangesAsync();

        // есть хоть одна согласованная работа — смета согласована. Если мастер уже был
        // (доп. работы во время ремонта), сразу возвращаю наряд ему
        if (await db.OrderServices.AnyAsync(s => s.WorkOrderId == id && s.ApprovalStatus == "approved"))
        {
            w.Status = "approved";
            await db.SaveChangesAsync();
            if (w.MasterId is { } master)
            {
                await db.CallAsync<object>("CALL sp_assign_master(@wo, @master)", ("wo", id), ("master", master));
            }
        }
        return await repair.GetWorkOrderAsync(id);
    }

    // закрытие: процедура сама проверит смету и выставит счёт
    public async Task<WorkOrderDetailsDto> CloseAsync(int id)
    {
        await LoadAsync(id);
        await db.CallAsync<object>("CALL sp_close_work_order(@wo)", ("wo", id));
        return await repair.GetWorkOrderAsync(id);
    }

    public async Task RegisterPaymentAsync(PaymentSaveRequest r) =>
        await db.CallAsync<object>("CALL sp_register_payment(@invoice, @amount, @method, @refund)",
            ("invoice", r.InvoiceId), ("amount", r.Amount), ("method", r.Method), ("refund", r.IsRefund));

    public Task<List<MasterDto>> MastersAsync() =>
        db.Users.Where(u => u.Role!.Code == RoleCodes.Master && !u.IsBlocked)
            .OrderBy(u => u.FullName)
            .Select(u => new MasterDto(u.UserId, u.FullName,
                db.WorkOrders.Count(w => w.MasterId == u.UserId && w.Status != "closed" && w.Status != "cancelled")))
            .ToListAsync();

    // ---------- склад ----------

    // приход партии на склад (роль закупщика проверит и триггер)
    public async Task ReceiveAsync(StockReceiptRequest r)
    {
        if (!await db.Parts.AnyAsync(p => p.PartId == r.PartId))
        {
            throw ApiException.Field(nameof(r.PartId), "Такой запчасти нет в справочнике");
        }
        db.StockMoves.Add(new StockMove
        {
            PartId = r.PartId,
            MoveType = "receipt",
            Quantity = r.Quantity,
            Batch = r.Batch.Trim(),
            StorageCell = r.StorageCell.Trim(),
            PerformedBy = current.RequiredId,
        });
        await db.SaveChangesAsync();
    }

    // выдача в ремонт: остаток и то, что деталь есть в смете, проверяет триггер склада
    public async Task IssueAsync(StockIssueRequest r) =>
        await db.CallAsync<object>("CALL sp_issue_part(@wo, @part, @batch, @qty, @user)",
            ("wo", r.WorkOrderId), ("part", r.PartId), ("batch", r.Batch), ("qty", r.Quantity), ("user", current.RequiredId));

    // что мастерам ещё нужно выдать: согласованные запчасти минус уже выданное
    public async Task<List<PartToIssueDto>> PartsToIssueAsync()
    {
        string[] active = ["approved", "assigned", "in_progress"];
        var needed = await db.OrderParts
            .Where(p => p.ApprovalStatus == "approved" && active.Contains(p.WorkOrder!.Status))
            .GroupBy(p => new { p.WorkOrderId, p.WorkOrder!.WorkOrderNumber, p.WorkOrder.Status, p.PartId, p.Part!.Article, p.Part.Name, p.Part.Unit })
            .Select(g => new { g.Key, Needed = g.Sum(x => x.Quantity) })
            .ToListAsync();

        var ids = needed.Select(n => n.Key.WorkOrderId).Distinct().ToList();
        var issued = await db.StockMoves
            .Where(m => m.WorkOrderId != null && ids.Contains(m.WorkOrderId.Value) && (m.MoveType == "issue" || m.MoveType == "return"))
            .GroupBy(m => new { m.WorkOrderId, m.PartId })
            .Select(g => new { g.Key.WorkOrderId, g.Key.PartId, Qty = g.Sum(m => m.MoveType == "issue" ? m.Quantity : -m.Quantity) })
            .ToListAsync();

        return needed
            .Select(n => new PartToIssueDto(n.Key.WorkOrderId, n.Key.WorkOrderNumber, n.Key.Status, n.Key.PartId,
                n.Key.Article, n.Key.Name, n.Key.Unit, n.Needed,
                issued.Where(i => i.WorkOrderId == n.Key.WorkOrderId && i.PartId == n.Key.PartId).Sum(i => i.Qty)))
            .Where(x => x.Remaining > 0)
            .OrderBy(x => x.WorkOrderNumber)
            .ToList();
    }

    // партии детали с остатком — для выпадающего списка при выдаче
    public Task<List<StockBalanceDto>> BatchesAsync(int partId) =>
        db.StockBalance.Where(b => b.PartId == partId && b.OnHand > 0)
            .OrderBy(b => b.Batch)
            .Select(b => new StockBalanceDto(b.PartId, b.Article, b.Name, b.Batch, b.StorageCell, b.OnHand, b.Reserved, b.Available))
            .ToListAsync();

    // ---------- помощники ----------

    // мастер работает только со своими заказ-нарядами
    private async Task<WorkOrder> LoadAsync(int id)
    {
        var w = await db.WorkOrders.SingleOrDefaultAsync(x => x.WorkOrderId == id) ?? throw ApiException.NotFound("Заказ-наряд");
        if (current.Role == RoleCodes.Master && w.MasterId != current.Id)
        {
            throw new ApiException(403, "Это заказ-наряд другого мастера");
        }
        return w;
    }

    private async Task<WorkOrder> LoadForEstimateAsync(int id)
    {
        var w = await LoadAsync(id);
        if (!WorkOrderRules.EditableEstimate.Contains(w.Status))
        {
            throw ApiException.Conflict($"В статусе «{Labels.Of(Labels.WorkOrderStatuses, w.Status)}» смету уже не меняют");
        }
        return w;
    }

    // новые позиции идут следующей версией сметы, если прошлую уже отправляли клиенту
    private async Task<int> NextVersionAsync(int id)
    {
        var sent = await db.OrderServices.Where(s => s.WorkOrderId == id && s.ApprovalStatus != "draft")
            .MaxAsync(s => (int?)s.EstimateVersion);
        var draft = await db.OrderServices.Where(s => s.WorkOrderId == id && s.ApprovalStatus == "draft")
            .MaxAsync(s => (int?)s.EstimateVersion);
        return draft ?? (sent ?? 0) + 1;
    }

    // время из формы — московское, в базу кладу UTC
    // (если время пришло с часовым поясом, он уже учтён)
    private static DateTime MoscowToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => TimeZoneInfo.ConvertTimeToUtc(value, Moscow),
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
