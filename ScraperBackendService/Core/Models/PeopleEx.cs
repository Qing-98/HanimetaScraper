using ScraperBackendService.Models;

namespace ScraperBackendService.Core.Models;

public static class PeopleEx
{
    /// <summary>按 Name+Type 去重后添加。</summary>
    public static void AddPerson(HanimeMetadata meta, string name, string type, string? role = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (meta.People.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                               && p.Type.Equals(type, StringComparison.OrdinalIgnoreCase)))
            return;
        meta.People.Add(new PersonDto { Name = name, Type = type, Role = role });
    }
}
