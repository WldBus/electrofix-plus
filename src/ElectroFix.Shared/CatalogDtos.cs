using System.ComponentModel.DataAnnotations;

namespace ElectroFix.Shared;

// справочник работ
public record ServiceDto(
    int ServiceId,
    string Name,
    string Category,
    decimal NormHours,
    decimal Rate,
    int WarrantyMonths,
    DateTime? DeletedAt);

public class ServiceSaveRequest
{
    [Required(ErrorMessage = "Укажите наименование")]
    [StringLength(150, ErrorMessage = "Наименование: не больше 150 символов")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Укажите категорию")]
    [StringLength(50, ErrorMessage = "Категория: не больше 50 символов")]
    public string Category { get; set; } = "";

    [Range(0.01, 100, ErrorMessage = "Норма времени: от 0,01 до 100 ч")]
    public decimal NormHours { get; set; } = 1;

    [Range(0, 1_000_000, ErrorMessage = "Ставка: от 0 до 1 000 000")]
    public decimal Rate { get; set; }

    [Range(0, 36, ErrorMessage = "Гарантия: от 0 до 36 месяцев")]
    public int WarrantyMonths { get; set; } = 3;
}

// номенклатура запчастей
public record PartDto(
    int PartId,
    string Article,
    string Name,
    string Category,
    string Unit,
    int MinStock,
    DateTime? DeletedAt);

public class PartSaveRequest
{
    public static readonly string[] Units = ["шт", "компл", "м", "кг", "л", "упак"];

    [Required(ErrorMessage = "Укажите артикул")]
    [RegularExpression(@"^[A-Za-z0-9._-]{3,50}$", ErrorMessage = "Артикул: 3–50 символов, латиница, цифры, . _ -")]
    public string Article { get; set; } = "";

    [Required(ErrorMessage = "Укажите наименование")]
    [StringLength(150, ErrorMessage = "Наименование: не больше 150 символов")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Укажите категорию")]
    [StringLength(50, ErrorMessage = "Категория: не больше 50 символов")]
    public string Category { get; set; } = "";

    [Required(ErrorMessage = "Укажите единицу измерения")]
    [RegularExpression("^(шт|компл|м|кг|л|упак)$", ErrorMessage = "Недопустимая единица измерения")]
    public string Unit { get; set; } = "шт";

    [Range(0, 100_000, ErrorMessage = "Минимальный запас: от 0 до 100 000")]
    public int MinStock { get; set; }
}
