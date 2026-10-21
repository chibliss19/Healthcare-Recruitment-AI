using HealthcareRecruitment.Api.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class AiAssistantServiceTests
{
    [Fact]
    public async Task Database_Context_Should_Initialize_Correctly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "TestHealthcareDb")
            .Options;

        using var context = new AppDbContext(options);

        // Act
        var count = await context.Vacancies.CountAsync();

        // Assert
        Assert.Equal(0, count);
    }
}