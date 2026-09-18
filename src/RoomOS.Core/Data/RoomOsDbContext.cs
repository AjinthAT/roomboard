using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Data.Entities;

namespace RoomOS.Core.Data;

public sealed class RoomOsDbContext(DbContextOptions<RoomOsDbContext> options) : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<AudioOutput> AudioOutputs => Set<AudioOutput>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Room>(room =>
        {
            room.ToTable("rooms");
            room.HasKey(r => r.Id);
            room.Property(r => r.Id).HasColumnName("id");
            room.Property(r => r.Name).HasColumnName("name").IsRequired();
            room.Property(r => r.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<Device>(device =>
        {
            device.ToTable("devices");
            device.HasKey(d => d.Id);
            device.Property(d => d.Id).HasColumnName("id");
            device.Property(d => d.RoomId).HasColumnName("room_id").IsRequired();
            device.Property(d => d.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
            device.Property(d => d.Name).HasColumnName("name").IsRequired();
            device.Property(d => d.Enabled).HasColumnName("enabled");
            device.Property(d => d.ConfigJson).HasColumnName("config_json").IsRequired();

            device.HasOne(d => d.Room)
                .WithMany(r => r.Devices)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AudioOutput>(output =>
        {
            output.ToTable("audio_outputs");
            output.HasKey(o => o.Id);
            output.Property(o => o.Id).HasColumnName("id");
            output.Property(o => o.PcDeviceId).HasColumnName("pc_device_id").IsRequired();
            output.Property(o => o.WindowsDeviceId).HasColumnName("windows_device_id");
            output.Property(o => o.MatchHint).HasColumnName("match_hint").IsRequired();
            output.Property(o => o.FriendlyName).HasColumnName("friendly_name").IsRequired();
            output.Property(o => o.SortOrder).HasColumnName("sort_order");
        });
    }
}
