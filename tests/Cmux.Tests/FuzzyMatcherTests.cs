using Cmux.Core.Services;
using FluentAssertions;
using Xunit;

namespace Cmux.Tests;

public class FuzzyMatcherTests
{
    [Fact]
    public void Score_ExactMatch_ReturnsHighScore()
    {
        var result = FuzzyMatcher.Score("settings", "settings");
        result.Score.Should().BeGreaterThan(100);
        result.MatchedIndices.Should().HaveCount(8);
    }

    [Fact]
    public void Score_PrefixMatch_ScoresHigherThanMiddleMatch()
    {
        var prefix = FuzzyMatcher.Score("set", "settings");
        var middle = FuzzyMatcher.Score("set", "reset");
        prefix.Score.Should().BeGreaterThan(middle.Score);
    }

    [Fact]
    public void Score_SubsequenceMatch_ReturnsPositiveScore()
    {
        var result = FuzzyMatcher.Score("nw", "new workspace");
        result.Score.Should().BeGreaterThan(0);
        result.MatchedIndices.Should().Contain(0);
    }

    [Fact]
    public void Score_NoMatch_ReturnsZero()
    {
        var result = FuzzyMatcher.Score("xyz", "settings");
        result.Score.Should().Be(0);
        result.MatchedIndices.Should().BeEmpty();
    }

    [Fact]
    public void Score_WordBoundaryMatch_ScoresHigherThanMidWordMatch()
    {
        var boundary = FuzzyMatcher.Score("cs", "close surface");
        var midWord = FuzzyMatcher.Score("cs", "access");
        boundary.Score.Should().BeGreaterThan(midWord.Score);
    }

    [Fact]
    public void Score_ConsecutiveMatch_ScoresHigherThanScattered()
    {
        var consecutive = FuzzyMatcher.Score("new", "new workspace");
        var scattered = FuzzyMatcher.Score("new", "notification preview");
        consecutive.Score.Should().BeGreaterThan(scattered.Score);
    }

    [Fact]
    public void Score_CaseInsensitive()
    {
        var lower = FuzzyMatcher.Score("set", "Settings");
        lower.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Score_EmptyPattern_ReturnsZero()
    {
        var result = FuzzyMatcher.Score("", "settings");
        result.Score.Should().Be(0);
    }

    [Fact]
    public void ScoreMultiField_ReturnsbestScoreAcrossFields()
    {
        var result = FuzzyMatcher.ScoreMultiField("set", "actions", "settings", "general");
        result.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ScoreMultiField_HandlesNullFields()
    {
        var result = FuzzyMatcher.ScoreMultiField("set", null, "settings", null);
        result.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ScoreMultiField_NoMatchInAnyField_ReturnsZero()
    {
        var result = FuzzyMatcher.ScoreMultiField("xyz", "abc", "def", "ghi");
        result.Score.Should().Be(0);
    }

    [Fact]
    public void Score_CamelCaseBoundary_ScoresHigher()
    {
        var camelCase = FuzzyMatcher.Score("nw", "newWorkspace");
        var midWord = FuzzyMatcher.Score("nw", "snowball");
        camelCase.Score.Should().BeGreaterThan(midWord.Score);
    }
}
