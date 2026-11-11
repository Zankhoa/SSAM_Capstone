using System.Text.Json.Serialization;

namespace SSAM_Server.DTOs.Common;

public class ItemsDto<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    public ItemsDto() { }

    public ItemsDto(IEnumerable<T> items)
    {
        Items = items.ToList();
    }
}
