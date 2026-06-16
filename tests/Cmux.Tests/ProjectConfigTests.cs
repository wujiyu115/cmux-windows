using Cmux.Core.Models;
using Cmux.Core.Services;
using FluentAssertions;
using Xunit;

namespace Cmux.Tests;

public class ProjectConfigTests
{
    [Fact]
    public void Parse_ValidJson_ReturnsConfig()
    {
        var json = """
        {
            "name": "My Project",
            "color": "#FF5733",
            "env": { "NODE_ENV": "development", "PORT": "3000" }
        }
        """;
        var config = ProjectConfigService.Parse(json);
        config.Should().NotBeNull();
        config!.Name.Should().Be("My Project");
        config.Color.Should().Be("#FF5733");
        config.Env.Should().ContainKey("NODE_ENV").WhoseValue.Should().Be("development");
    }

    [Fact]
    public void Parse_EmptyJson_ReturnsEmptyConfig()
    {
        var config = ProjectConfigService.Parse("{}");
        config.Should().NotBeNull();
        config!.Name.Should().BeNull();
        config.Env.Should().BeEmpty();
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        var config = ProjectConfigService.Parse("not json");
        config.Should().BeNull();
    }

    [Fact]
    public void Parse_JsonWithComments_Works()
    {
        var json = """
        {
            // this is a comment
            "name": "Test"
        }
        """;
        var config = ProjectConfigService.Parse(json);
        config.Should().NotBeNull();
        config!.Name.Should().Be("Test");
    }

    [Fact]
    public void FindConfigPath_ReturnsNullForNonExistentDir()
    {
        var path = ProjectConfigService.FindConfigPath(@"C:\nonexistent\path\that\does\not\exist");
        path.Should().BeNull();
    }
}
