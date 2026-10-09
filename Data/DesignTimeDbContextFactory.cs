using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AccountingSystem.Data
{
	public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
	{
		public ApplicationDbContext CreateDbContext(string[] args)
		{
			var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
			optionsBuilder.UseSqlServer(
				"Server=.\\SQLEXPRESS;Database=AccountingSystemDb;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60",
				sqlOptions => sqlOptions.CommandTimeout(300))
				.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

			return new ApplicationDbContext(optionsBuilder.Options);
		}
	}
}
