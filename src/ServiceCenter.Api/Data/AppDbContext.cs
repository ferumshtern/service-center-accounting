using Microsoft.EntityFrameworkCore;
using ServiceCenter.Api.Models;

namespace ServiceCenter.Api.Data;

// Відображення класів моделі на таблиці SQLite 
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<ServiceEngineer> Engineers => Set<ServiceEngineer>();
    public DbSet<RepairOrder> Orders => Set<RepairOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ідентифікатори призначає сховище (так само, як у реалізації для пам'яті)
        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("Clients");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("Devices");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).ValueGeneratedNever();

            // NOCASE: порівняння серійних номерів без урахування регістру, як у v1.0.
            // Індекс прискорює перевірку дубліката (AC-02) та історію ремонтів пристрою.
            entity.Property(d => d.SerialNumber).UseCollation("NOCASE");
            entity.HasIndex(d => d.SerialNumber);

            // Кожний пристрій належить одному клієнту (1 — 0..*).
            entity.HasOne<Client>()
                .WithMany()
                .HasForeignKey(d => d.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServiceEngineer>(entity =>
        {
            entity.ToTable("Engineers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();

            // Початковий довідник інженерів
            entity.HasData(
                new ServiceEngineer { Id = 1, FullName = "Коваленко Олександр", Specialization = "Смартфони та планшети" },
                new ServiceEngineer { Id = 2, FullName = "Мельник Ірина", Specialization = "Ноутбуки та материнські плати" });
        });

        modelBuilder.Entity<RepairOrder>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).ValueGeneratedNever();
            entity.HasIndex(o => o.OrderNumber).IsUnique();
            entity.Property(o => o.Status).HasConversion<string>();

            // Кожне замовлення має рівно одного клієнта, пристрій та інженера (1 — 0..*).
            entity.HasOne<Client>().WithMany().HasForeignKey(o => o.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Device>().WithMany().HasForeignKey(o => o.DeviceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ServiceEngineer>().WithMany().HasForeignKey(o => o.EngineerId).OnDelete(DeleteBehavior.Restrict);

            // Композиція: позиції та історія існують лише в складі замовлення.
            // Номер позиції / запису історії унікальний у межах замовлення, ключ складений
            entity.OwnsMany(o => o.Items, item =>
            {
                item.ToTable("RepairItems");
                item.WithOwner().HasForeignKey("RepairOrderId");
                item.HasKey("RepairOrderId", nameof(RepairItem.Id));
                item.Property(i => i.Id).ValueGeneratedNever();
            });

            entity.OwnsMany(o => o.History, history =>
            {
                history.ToTable("StatusHistory");
                history.WithOwner().HasForeignKey("RepairOrderId");
                history.HasKey("RepairOrderId", nameof(StatusHistory.Id));
                history.Property(h => h.Id).ValueGeneratedNever();
                history.Property(h => h.Status).HasConversion<string>();
            });

            // Колекції відкриті лише для читання, тому EF Core працює з полями _items та _history.
            entity.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(o => o.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
