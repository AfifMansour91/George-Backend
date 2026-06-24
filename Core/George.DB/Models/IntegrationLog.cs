using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// System-wide integration log: one row per external sync call between George and a store (WooCommerce),
/// across all entity kinds. <see cref="EntityType"/> (order / product / category / …) classifies the row
/// so a single admin "sync logs" screen can filter by type, entity, site, date and severity. Captures the
/// full request body + response so support can see exactly what was sent.
/// </summary>
[Index("SiteId", "CreatedAtUtc", Name = "IX_IntegrationLog_Site_Created")]
[Index("SiteId", "EntityType", "CreatedAtUtc", Name = "IX_IntegrationLog_Site_Type_Created")]
[Index("EntityType", "EntityId", Name = "IX_IntegrationLog_Entity")]
[Index("ExternalId", Name = "IX_IntegrationLog_ExternalId")]
public partial class IntegrationLog
{
    [Key]
    public int Id { get; set; }

    public int SiteId { get; set; }

    /// <summary>Entity kind this log concerns: <c>order</c> | <c>product</c> | <c>category</c> | … (free-form, lowercase).</summary>
    [StringLength(30)]
    public string EntityType { get; set; } = null!;

    /// <summary>George id of the entity (orderId / productId / categoryId) when applicable.</summary>
    public int? EntityId { get; set; }

    /// <summary>Store-side id (e.g. WooCommerce order/product/category id) for cross-referencing.</summary>
    [StringLength(100)]
    public string? ExternalId { get; set; }

    /// <summary><c>outbound</c> (George → store) or <c>inbound</c> (store → George).</summary>
    [StringLength(20)]
    public string Direction { get; set; } = "outbound";

    /// <summary>Logical operation, e.g. <c>oc-storeos/orders</c>, <c>wc/v3 product upsert</c>.</summary>
    [StringLength(80)]
    public string Operation { get; set; } = null!;

    /// <summary>Severity for screen filtering: <c>info</c> | <c>warning</c> | <c>error</c>.</summary>
    [StringLength(10)]
    public string Level { get; set; } = "info";

    [StringLength(1000)]
    public string? Url { get; set; }

    public int? HttpStatus { get; set; }

    public bool Success { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? RequestJson { get; set; }

    /// <summary>Response body (truncated for safety).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ResponseBody { get; set; }

    public int? DurationMs { get; set; }

    [StringLength(1000)]
    public string? Error { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }
}
