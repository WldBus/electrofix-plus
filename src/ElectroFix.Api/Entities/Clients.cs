namespace ElectroFix.Api.Entities;

public class Client : ISoftDeletable
{
    public int ClientId { get; set; }
    public string ClientType { get; set; } = "individual";
    public string DisplayName { get; set; } = "";
    public string? Inn { get; set; }
    public string? Address { get; set; }
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // учётка клиента на сайте (личный кабинет), может и не быть
    public int? UserId { get; set; }
    public List<Device> Devices { get; set; } = [];
}

public class Device
{
    public int DeviceId { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }
    public string Type { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public int? ManufactureYear { get; set; }
    public string SerialNumber { get; set; } = "";
    public string? InventoryNumber { get; set; }
    public int RuntimeHours { get; set; }
}
