using System;
using System.Collections.Generic;

namespace TaxiGO.Models;

public partial class Tariff
{
    public int TariffId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal BasePrice { get; set; }

    public decimal PricePerKm { get; set; }

    public decimal WaitingPenaltyPerMin { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
