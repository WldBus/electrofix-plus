namespace ElectroFix.Api.Entities;

// справочник работ
public class Service : ISoftDeletable
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal NormHours { get; set; }
    public decimal Rate { get; set; }
    public int WarrantyMonths { get; set; }
    public DateTime? DeletedAt { get; set; }
}

// номенклатура запчастей
public class Part : ISoftDeletable
{
    public int PartId { get; set; }
    public string Article { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Unit { get; set; } = "шт";
    public int MinStock { get; set; }
    public DateTime? DeletedAt { get; set; }
}
