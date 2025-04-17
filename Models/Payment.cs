using System;
using System.Collections.Generic;

namespace TaxiGO.Models;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public decimal Amount { get; set; }

    public DateTime? PaymentTime { get; set; }

    public int PaymentMethodId { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual PaymentMethod PaymentMethod { get; set; } = null!;
}