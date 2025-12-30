using Microsoft.EntityFrameworkCore;
using P1F_TPM360_HUB.Models;

namespace P1F_TPM360_HUB.Function
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }
        //public DbSet<MasterJob> mst_Bom { get; set; }
        //public DbSet<EmployeeModel> mst_employee { get; set; }

    }
}
