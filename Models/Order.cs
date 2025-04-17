using System;
using System.Collections.Generic;
using System.ComponentModel; // Для INotifyPropertyChanged
using System.ComponentModel.DataAnnotations.Schema; // пространство имён для [NotMapped]

namespace TaxiGO.Models;

public partial class Order : INotifyPropertyChanged
{
    private string _timeRemaining = "20:00";
    private bool _timerExpired = false;

    // Делаем событие PropertyChanged допускающим null
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public int OrderId { get; set; }
    public int ClientId { get; set; }
    public int? DriverId { get; set; }
    public string StartPoint { get; set; } = null!;
    public string EndPoint { get; set; } = null!;
    public DateTime? OrderTime { get; set; }
    public DateTime? OrderCompletionTime { get; set; }

    private string _status = null!;
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

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
    public bool IsPaid { get; set; }

    [NotMapped]
    public bool CanRateClient { get; set; }

    [NotMapped]
    public string TimeRemaining
    {
        get => _timeRemaining;
        set
        {
            _timeRemaining = value;
            OnPropertyChanged(nameof(TimeRemaining));
        }
    }

    [NotMapped]
    public bool TimerExpired
    {
        get => _timerExpired;
        set
        {
            _timerExpired = value;
            OnPropertyChanged(nameof(TimerExpired));
        }
    }
}