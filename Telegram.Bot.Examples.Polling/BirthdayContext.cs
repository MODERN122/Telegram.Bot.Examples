using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Bot.Examples.Polling
{
    public class BirthdayContext:DbContext
    {
        public DbSet<Birthday>? Birthdays { get; set; }

        public string DbPath { get; private set; }

        public BirthdayContext()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = @"C:\Users\Mikhail\Documents\GitHub\Telegram.Bot.Examples\Telegram.Bot.Examples.Polling\birtdays.db";
        }
        // The following configures EF to create a Sqlite database file in the
        // special "local" folder for your platform.
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        { options.UseSqlite($"Data Source={DbPath}"); }
    }
}
