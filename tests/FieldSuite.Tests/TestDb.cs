using FieldSuite.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Tests;

public static class TestDb
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"fieldsuite-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
