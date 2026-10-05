using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Learning.Infrastructure.Data;

public class LearningDbContextFactory : IDesignTimeDbContextFactory<LearningDbContext>
{
    public LearningDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LearningDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=LearningDb;User Id=sa;Password=YourStrong@Passw0rd!2026;TrustServerCertificate=True;");

        return new LearningDbContext(optionsBuilder.Options);
    }
}
