namespace ElectroFix.Api.Entities;

// заявка на ремонт с сайта (без регистрации). Приёмщик перезванивает, заводит клиента
// и запись на приём, а заявку отмечает «Оформлена» или «Отклонена»
public class RepairRequest
{
    public int RequestId { get; set; }
    public string DeviceType { get; set; } = "";
    public string DeviceModel { get; set; } = "";
    public string Problem { get; set; } = "";
    public string DeliveryMethod { get; set; } = "self";
    public string? Branch { get; set; }
    public string? Address { get; set; }
    public int? ServiceId { get; set; }
    public Service? Service { get; set; }
    public string ClientName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string Status { get; set; } = "new";
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ProcessedBy { get; set; }
    public User? Processor { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
