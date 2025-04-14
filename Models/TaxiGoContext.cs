using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaxiGO;

namespace TaxiGO.Models
{
    public partial class TaxiGoContext : DbContext
    {
        public TaxiGoContext()
        {
        }

        public TaxiGoContext(DbContextOptions<TaxiGoContext> options)
            : base(options)
        {
        }

        public virtual DbSet<DriverAvailability>? DriverAvailabilities { get; set; }

        public virtual DbSet<Log>? Logs { get; set; }

        public virtual DbSet<Order>? Orders { get; set; }

        public virtual DbSet<OrderStatusHistory>? OrderStatusHistories { get; set; }

        public virtual DbSet<Payment>? Payments { get; set; }

        public virtual DbSet<PromoCode>? PromoCodes { get; set; }

        public virtual DbSet<Tariff>? Tariffs { get; set; }

        public virtual DbSet<User>? Users { get; set; }

        public virtual DbSet<Vehicle>? Vehicles { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(App.Configuration.GetConnectionString("TaxiGoContext"));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DriverAvailability>(entity =>
            {
                entity.HasKey(e => e.AvailabilityId).HasName("PK__DriverAv__DA397991FF57A47B");

                entity.ToTable("DriverAvailability");

                entity.Property(e => e.AvailabilityId).HasColumnName("AvailabilityID");
                entity.Property(e => e.DriverId).HasColumnName("DriverID");
                entity.Property(e => e.LastUpdate)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime");

                entity.HasOne(d => d.Driver).WithMany(p => p.DriverAvailabilities)
                    .HasForeignKey(d => d.DriverId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__DriverAva__Drive__151B244E");
            });

            modelBuilder.Entity<Log>(entity =>
            {
                entity.HasKey(e => e.LogId).HasName("PK__Logs__5E5499A8642637EA");

                entity.Property(e => e.LogId).HasColumnName("LogID");
                entity.Property(e => e.Action).HasMaxLength(255);
                entity.Property(e => e.Timestamp)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime");
                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.HasOne(d => d.User).WithMany(p => p.Logs)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Logs__UserID__1EA48E88");
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BAF374F4862");

                entity.Property(e => e.OrderId).HasColumnName("OrderID");
                entity.Property(e => e.ClientComment).HasMaxLength(255);
                entity.Property(e => e.ClientId).HasColumnName("ClientID");
                entity.Property(e => e.Cost).HasColumnType("decimal(10, 2)");
                entity.Property(e => e.DistanceKm).HasColumnType("decimal(10, 2)");
                entity.Property(e => e.DriverComment).HasMaxLength(255);
                entity.Property(e => e.DriverId).HasColumnName("DriverID");
                entity.Property(e => e.EndPoint).HasMaxLength(100);
                entity.Property(e => e.OrderCompletionTime).HasColumnType("datetime");
                entity.Property(e => e.OrderTime)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime");
                entity.Property(e => e.PromoCodeId).HasColumnName("PromoCodeID");
                entity.Property(e => e.StartPoint).HasMaxLength(100);
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.Property(e => e.TariffId).HasColumnName("TariffID");
                entity.Property(e => e.WaitingPenalty).HasColumnType("decimal(10, 2)");

                entity.HasOne(d => d.Client).WithMany(p => p.OrderClients)
                    .HasForeignKey(d => d.ClientId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Orders__ClientID__08B54D69");

                entity.HasOne(d => d.Driver).WithMany(p => p.OrderDrivers)
                    .HasForeignKey(d => d.DriverId)
                    .HasConstraintName("FK__Orders__DriverID__09A971A2");

                entity.HasOne(d => d.PromoCode).WithMany(p => p.Orders)
                    .HasForeignKey(d => d.PromoCodeId)
                    .HasConstraintName("FK__Orders__PromoCod__0B91BA14");

                entity.HasOne(d => d.Tariff).WithMany(p => p.Orders)
                    .HasForeignKey(d => d.TariffId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Orders__TariffID__0A9D95DB");
            });

            modelBuilder.Entity<OrderStatusHistory>(entity =>
            {
                entity.HasKey(e => e.StatusHistoryId).HasName("PK__OrderSta__DB9734B125C99F23");

                entity.ToTable("OrderStatusHistory");

                entity.Property(e => e.StatusHistoryId).HasColumnName("StatusHistoryID");
                entity.Property(e => e.ChangeTime)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime");
                entity.Property(e => e.ChangedByUserId).HasColumnName("ChangedByUserID");
                entity.Property(e => e.OrderId).HasColumnName("OrderID");
                entity.Property(e => e.Status).HasMaxLength(20);

                entity.HasOne(d => d.ChangedByUser).WithMany(p => p.OrderStatusHistories)
                    .HasForeignKey(d => d.ChangedByUserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__OrderStat__Chang__1AD3FDA4");

                entity.HasOne(d => d.Order).WithMany(p => p.OrderStatusHistories)
                    .HasForeignKey(d => d.OrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__OrderStat__Order__19DFD96B");
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A58558D2D3D");

                entity.Property(e => e.PaymentId).HasColumnName("PaymentID");
                entity.Property(e => e.Amount).HasColumnType("decimal(10, 2)");
                entity.Property(e => e.OrderId).HasColumnName("OrderID");
                entity.Property(e => e.PaymentMethod).HasMaxLength(20);
                entity.Property(e => e.PaymentTime)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime");

                entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                    .HasForeignKey(d => d.OrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Payments__OrderI__10566F31");
            });

            modelBuilder.Entity<PromoCode>(entity =>
            {
                entity.HasKey(e => e.PromoCodeId).HasName("PK__PromoCod__867BC566787E4455");

                entity.HasIndex(e => e.Code, "UQ__PromoCod__A25C5AA7F682CC32").IsUnique();

                entity.Property(e => e.PromoCodeId).HasColumnName("PromoCodeID");
                entity.Property(e => e.Code).HasMaxLength(20);
                entity.Property(e => e.ExpiryDate).HasColumnType("datetime");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<Tariff>(entity =>
            {
                entity.HasKey(e => e.TariffId).HasName("PK__Tariffs__EBAF9D934259EAC3");

                entity.HasIndex(e => e.Name, "UQ__Tariffs__737584F6B369F2B5").IsUnique();

                entity.Property(e => e.TariffId).HasColumnName("TariffID");
                entity.Property(e => e.BasePrice).HasColumnType("decimal(10, 2)");
                entity.Property(e => e.Description).HasMaxLength(255);
                entity.Property(e => e.Name).HasMaxLength(20);
                entity.Property(e => e.PricePerKm).HasColumnType("decimal(10, 2)");
                entity.Property(e => e.WaitingPenaltyPerMin)
                    .HasDefaultValueSql("((10.00))")
                    .HasColumnType("decimal(10, 2)");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCAC621DCCAD");

                entity.HasIndex(e => e.Login, "UQ__Users__5E55825BA76605C9").IsUnique();

                entity.Property(e => e.UserId).HasColumnName("UserID");
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.Login).HasMaxLength(50);
                entity.Property(e => e.Name).HasMaxLength(100);
                entity.Property(e => e.PasswordHash).HasMaxLength(64);
                entity.Property(e => e.Phone).HasMaxLength(15);
                entity.Property(e => e.RegistrationDate)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnType("datetime");
                entity.Property(e => e.Role).HasMaxLength(20);
                entity.Property(e => e.AvatarPath).HasMaxLength(255);
            });

            modelBuilder.Entity<Vehicle>(entity =>
            {
                entity.HasKey(e => e.VehicleId).HasName("PK__Vehicles__476B54B2DACAF891");

                entity.HasIndex(e => e.LicensePlate, "UQ__Vehicles__026BC15CF8FB82C6").IsUnique();

                entity.HasIndex(e => e.DriverId, "UQ__Vehicles__F1B1CD25ABC918EE").IsUnique();

                entity.Property(e => e.VehicleId).HasColumnName("VehicleID");
                entity.Property(e => e.DriverId).HasColumnName("DriverID");
                entity.Property(e => e.LicensePlate).HasMaxLength(20);
                entity.Property(e => e.Model).HasMaxLength(50);
                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .HasDefaultValue("Available");
                entity.Property(e => e.Type).HasMaxLength(20);

                entity.HasOne(d => d.Driver).WithOne(p => p.Vehicle)
                    .HasForeignKey<Vehicle>(d => d.DriverId)
                    .HasConstraintName("FK__Vehicles__Driver__7D439ABD");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}