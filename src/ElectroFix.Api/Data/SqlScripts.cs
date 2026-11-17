namespace ElectroFix.Api.Data;

// SQL-файлы из папки Data/Sql лежат внутри сборки (см. csproj), тут я их достаю по имени
public static class SqlScripts
{
    public static string Read(string fileName)
    {
        var assembly = typeof(SqlScripts).Assembly;
        using var stream = assembly.GetManifestResourceStream("Sql." + fileName)
            ?? throw new InvalidOperationException($"Нет SQL-скрипта {fileName} в сборке");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
