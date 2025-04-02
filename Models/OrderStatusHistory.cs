using System;
using System.Collections.Generic;

namespace TaxiGO.Models;

public partial class OrderStatusHistory
{
    public int StatusHistoryId { get; set; }

    public int OrderId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? ChangeTime { get; set; }

    public int ChangedByUserId { get; set; }

    public virtual User ChangedByUser { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
