using System;
using System.Collections.Generic;

namespace TaxiGO.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public int ClientId { get; set; }

    public int? DriverId { get; set; }

    public string StartPoint { get; set; } = null!;

    public string EndPoint { get; set; } = null!;

    public DateTime? OrderTime { get; set; }

    public DateTime? OrderCompletionTime { get; set; }

    public string Status { get; set; } = null!;

    public int TariffId { get; set; }

    public int? PromoCodeId { get; set; }

    public decimal? DistanceKm { get; set; }

    public int? WaitingTimeMin { get; set; }

    public decimal? WaitingPenalty { get; set; }

    public decimal? Cost { get; set; }

    public int? ClientRating { get; set; }

    public int? DriverRating { get; set; }

    public string? ClientComment { get; set; }

    public string? DriverComment { get; set; }

    public virtual User Client { get; set; } = null!;

    public virtual User? Driver { get; set; }

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual PromoCode? PromoCode { get; set; }

    public virtual Tariff Tariff { get; set; } = null!;
}
