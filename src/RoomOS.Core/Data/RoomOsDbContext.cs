using Microsoft.EntityFrameworkCore;
using RoomOS.Core.Data.Entities;

namespace RoomOS.Core.Data;

public sealed class RoomOsDbContext(DbContextOptions<RoomOsDbContext> options) : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<AudioOutput> AudioOutputs => Set<AudioOutput>();
    public DbSet<IntegrationToken> IntegrationTokens => Set<IntegrationToken>();
    public DbSet<Scene> Scenes => Set<Scene>();
    public DbSet<Routine> Routines => Set<Routine>();

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

            output.HasOne<Device>()
                .WithMany()
                .HasForeignKey(o => o.PcDeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Scene>(scene =>
        {
            scene.ToTable("scenes");
            scene.HasKey(s => s.Id);
            scene.Property(s => s.Id).HasColumnName("id");
            scene.Property(s => s.RoomId).HasColumnName("room_id").IsRequired();
            scene.Property(s => s.Name).HasColumnName("name").IsRequired();
            scene.Property(s => s.Icon).HasColumnName("icon").IsRequired();
            scene.Property(s => s.StepsJson).HasColumnName("steps_json").IsRequired();
            scene.Property(s => s.SortOrder).HasColumnName("sort_order");

            scene.HasOne<Room>()
                .WithMany()
                .HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Routine>(routine =>
        {
            routine.ToTable("routines");
            routine.HasKey(r => r.Id);
            routine.Property(r => r.Id).HasColumnName("id");
            routine.Property(r => r.Name).HasColumnName("name").IsRequired();
            routine.Property(r => r.SceneId).HasColumnName("scene_id").IsRequired();
            routine.Property(r => r.MinuteOfDay).HasColumnName("minute_of_day");
            routine.Property(r => r.Days).HasColumnName("days").IsRequired();
            routine.Property(r => r.Enabled).HasColumnName("enabled");
            routine.Property(r => r.SortOrder).HasColumnName("sort_order");

            routine.HasOne<Scene>()
                .WithMany()
                .HasForeignKey(r => r.SceneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IntegrationToken>(token =>
        {
            token.ToTable("integration_tokens");
            token.HasKey(t => t.Provider);
            token.Property(t => t.Provider).HasColumnName("provider");
            token.Property(t => t.AccessToken).HasColumnName("access_token").IsRequired();
            token.Property(t => t.RefreshToken).HasColumnName("refresh_token").IsRequired();
            token.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        });
    }
}
