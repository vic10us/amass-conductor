namespace AmassOrchestrator.Web.Data.Amass;

public class AmassEntity
{
    public long EntityId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public short EtypeId { get; set; }
    public string NaturalKey { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public long RowId { get; set; }
}
