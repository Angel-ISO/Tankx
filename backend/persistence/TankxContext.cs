using Microsoft.EntityFrameworkCore;
using backend.domain.entities;
using System.Reflection;


namespace backend.persistence;
public class TankxContext : DbContext
{
    public TankxContext(DbContextOptions<TankxContext> options) : base(options)
    {
        
    }
    
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();
    public DbSet<Match> Matches => Set<Match>();

     protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
