using System;
using System.Collections.Generic;

namespace TaxiGO.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Login { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public DateTime? RegistrationDate { get; set; }

    public string? Address { get; set; }

    public string? AvatarPath { get; set; }

    public virtual ICollection<DriverAvailability> DriverAvailabilities { get; set; } = new List<DriverAvailability>();

    public virtual ICollection<Log> Logs { get; set; } = new List<Log>();

    public virtual ICollection<Order> OrderClients { get; set; } = new List<Order>();

    public virtual ICollection<Order> OrderDrivers { get; set; } = new List<Order>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();

    public virtual Vehicle? Vehicle { get; set; }
}