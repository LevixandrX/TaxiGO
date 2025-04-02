using System;
using System.Collections.Generic;

namespace TaxiGO.Models;

public partial class DriverAvailability
{
    public int AvailabilityId { get; set; }

    public int DriverId { get; set; }

    public bool IsAvailable { get; set; }

    public DateTime? LastUpdate { get; set; }

    public virtual User Driver { get; set; } = null!;
}
