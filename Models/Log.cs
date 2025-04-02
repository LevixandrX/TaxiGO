using System;
using System.Collections.Generic;

namespace TaxiGO.Models;

public partial class Log
{
    public int LogId { get; set; }

    public int UserId { get; set; }

    public string Action { get; set; } = null!;

    public DateTime? Timestamp { get; set; }

    public virtual User User { get; set; } = null!;
}
