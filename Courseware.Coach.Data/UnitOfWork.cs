using Courseware.Coach.Core;
using Courseware.Coach.Data.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CH = Courseware.Coach.Core.Coach;

namespace Courseware.Coach.Data
{
    public sealed class UnitOfWork : UnitOfWorkBase
    {
        internal CoursewareContext Context { get; } 
        public UnitOfWork(IConfiguration config) : base(config)
        {
            Context = new CoursewareContext(config);
        }
        public override Task SaveChanges(CancellationToken token = default)
        {
            return Context.SaveChangesAsync(token);
        }
        public override async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await base.DisposeAsync();
        }
    }
    public class CoursewareContext : DbContext
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<CH> Coaches { get; set; } = null!;
        public DbSet<Course> Courses { get; set; } = null!;
        protected IConfiguration Configuration { get; }
        public CoursewareContext(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCosmos(Configuration.GetConnectionString("CousewareDB") ?? throw new InvalidDataException(), databaseName: Configuration["CousewareDBName"] ?? throw new InvalidDataException());
        }
        override protected void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().ToContainer("User").HasPartitionKey(p => p.SourceId);
            modelBuilder.Entity<CH>().ToContainer("Coach").HasPartitionKey(p => p.SourceId);
            modelBuilder.Entity<Course>().ToContainer("Course").HasPartitionKey(p => p.SourceId);
        }
    }
}
