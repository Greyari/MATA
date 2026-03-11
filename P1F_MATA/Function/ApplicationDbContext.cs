using Microsoft.EntityFrameworkCore;
using P1F_MATA.Models;

namespace P1F_MATA.Function
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
