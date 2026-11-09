using Cosplabuilder.Models;
using Microsoft.EntityFrameworkCore;
using TaskModel = Cosplabuilder.Models.Task;

namespace Cosplabuilder.Views.Data
{
    public class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Image> Images => Set<Image>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<Part> Parts => Set<Part>();
        public DbSet<TaskModel> Tasks => Set<TaskModel>();
        public DbSet<Tool> Tools => Set<Tool>();
        public DbSet<Material> Materials => Set<Material>();
        public DbSet<MaterialUse> MaterialUses => Set<MaterialUse>();
        public DbSet<Reference> References => Set<Reference>();

        public DbSet<TaskTypeLookup> TaskTypes => Set<TaskTypeLookup>();
        public DbSet<ProjectStatusLookup> ProjectStatuses => Set<ProjectStatusLookup>();
        public DbSet<ToolTypeLookup> ToolTypes => Set<ToolTypeLookup>();
        public DbSet<ReferenceTypeLookup> ReferenceTypes => Set<ReferenceTypeLookup>();
        public DbSet<MaterialTypeLookup> MaterialTypes => Set<MaterialTypeLookup>();
        public DbSet<PartTypeLookup> PartTypes => Set<PartTypeLookup>();
        public DbSet<LanguageLookup> Languages => Set<LanguageLookup>();
        public DbSet<CurrencyLookup> Currencies => Set<CurrencyLookup>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.UseCollation("utf8mb4_lithuanian_ci");

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("User");
                entity.HasKey(u => u.Id);
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasOne(u => u.AvatarImage)
                      .WithMany()
                      .HasForeignKey(u => u.ImageId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Image>(entity =>
            {
                entity.ToTable("Image");
                entity.HasKey(i => i.Id);
            });

            modelBuilder.Entity<Project>(entity =>
            {
                entity.ToTable("Project");
                entity.HasKey(p => p.Id);
                entity.HasOne(p => p.CoverImage)
                      .WithMany()
                      .HasForeignKey(p => p.ImageId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(p => p.Owner)
                      .WithMany()
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(p => p.Parts)
                      .WithOne(pt => pt.Project)
                      .HasForeignKey(pt => pt.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(p => p.References)
                      .WithOne(r => r.Project)
                      .HasForeignKey(r => r.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Part>(entity =>
            {
                entity.ToTable("Part");
                entity.HasKey(pt => pt.Id);
                entity.HasOne(pt => pt.PartImage)
                      .WithMany()
                      .HasForeignKey(pt => pt.ImageId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(pt => pt.Tasks)
                      .WithOne(t => t.Part)
                      .HasForeignKey(t => t.PartId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(pt => pt.MaterialUses)
                      .WithOne(mu => mu.Part)
                      .HasForeignKey(mu => mu.PartId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TaskModel>(entity =>
            {
                entity.ToTable("Task");
                entity.HasKey(t => t.Id);
            });

            modelBuilder.Entity<Tool>(entity =>
            {
                entity.ToTable("Tool");
                entity.HasKey(tl => tl.Id);
                entity.HasOne(tl => tl.ToolImage)
                      .WithMany()
                      .HasForeignKey(tl => tl.ImageId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(tl => tl.Owner)
                      .WithMany()
                      .HasForeignKey(tl => tl.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Material>(entity =>
            {
                entity.ToTable("Material");
                entity.HasKey(m => m.Id);
                entity.HasOne(m => m.MaterialImage)
                      .WithMany()
                      .HasForeignKey(m => m.ImageId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<MaterialUse>(entity =>
            {
                entity.ToTable("MaterialUse");
                entity.HasKey(mu => mu.Id);
                entity.HasOne(mu => mu.Material)
                      .WithMany()
                      .HasForeignKey(mu => mu.MaterialId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Reference>(entity =>
            {
                entity.ToTable("Reference");
                entity.HasKey(r => r.Id);
                entity.HasMany(r => r.Images)
                      .WithOne()
                      .HasForeignKey(i => i.ReferenceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
