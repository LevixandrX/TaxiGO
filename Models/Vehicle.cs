using System;
using System.Collections.Generic;

namespace TaxiGO.Models;

public partial class Vehicle
{
    public int VehicleId { get; set; }

    public int? DriverId { get; set; }

    public string Model { get; set; } = null!;

    public string LicensePlate { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int? Year { get; set; }

    public virtual User? Driver { get; set; }
}
