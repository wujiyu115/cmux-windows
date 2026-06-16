namespace Cmux.Core.Services;

/// <summary>
/// Provides fuzzy matching with scoring that rewards prefix matches, word boundary matches,
/// consecutive character matches, and camelCase boundary matches.
/// </summary>
public static class FuzzyMatcher
{
    public record MatchResult(int Score, List<int> MatchedIndices);

    private const int PrefixBonus = 15;
    private const int WordBoundaryBonus = 10;
    private const int CamelCaseBonus = 8;
    private const int ConsecutiveBonus = 8;
    private const int BaseCharScore = 10;
    private const int ExactMatchBonus = 30;

    /// <summary>
    /// Scores a pattern against a text using fuzzy subsequence matching.
    /// Returns a <see cref="MatchResult"/> with the score and matched character indices.
    /// A score of zero indicates no match.
    /// </summary>
    public static MatchResult Score(string pattern, string text)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(text))
            return new MatchResult(0, []);

        var patternLower = pattern.ToLowerInvariant();
        var textLower = text.ToLowerInvariant();

        // Quick check: can the pattern be a subsequence of the text at all?
        if (!IsSubsequence(patternLower, textLower))
            return new MatchResult(0, []);

        // Use dynamic programming to find the best scoring match.
        // We try all valid subsequence alignments and pick the highest-scoring one.
        var bestResult = FindBestMatch(patternLower, textLower, text);

        if (bestResult.MatchedIndices.Count == 0)
            return new MatchResult(0, []);

        return bestResult;
    }

    /// <summary>
    /// Scores a pattern against multiple fields and returns the best match result.
    /// </summary>
    public static MatchResult ScoreMultiField(string pattern, params string?[] fields)
    {
        if (string.IsNullOrEmpty(pattern))
            return new MatchResult(0, []);

        MatchResult best = new(0, []);

        foreach (var field in fields)
        {
            if (field is null)
                continue;

            var result = Score(pattern, field);
            if (result.Score > best.Score)
                best = result;
        }

        return best;
    }

    private static bool IsSubsequence(string pattern, string text)
    {
        int pi = 0;
        for (int ti = 0; ti < text.Length && pi < pattern.Length; ti++)
        {
            if (text[ti] == pattern[pi])
                pi++;
        }
        return pi == pattern.Length;
    }

    private static MatchResult FindBestMatch(string patternLower, string textLower, string originalText)
    {
        int pLen = patternLower.Length;
        int tLen = textLower.Length;

        // dp[pi, ti] = best score achievable matching patternLower[0..pi-1] using textLower[0..ti-1]
        // We also track the choice at each cell to reconstruct indices.
        var dp = new int[pLen + 1, tLen + 1];
        var matched = new bool[pLen + 1, tLen + 1]; // true = we matched pattern[pi-1] at text[ti-1]

        // Initialize: matching 0 pattern chars = score 0 (valid base case)
        // Matching >0 pattern chars with 0 text chars = impossible (use -infinity)
        for (int pi = 1; pi <= pLen; pi++)
            for (int ti = 0; ti <= tLen; ti++)
                dp[pi, ti] = -1; // sentinel for "impossible"

        for (int pi = 1; pi <= pLen; pi++)
        {
            for (int ti = 1; ti <= tLen; ti++)
            {
                // Option 1: skip text[ti-1], carry forward dp[pi, ti-1]
                int skipScore = dp[pi, ti - 1];

                // Option 2: match pattern[pi-1] with text[ti-1] if chars match
                int matchScore = -1;
                if (patternLower[pi - 1] == textLower[ti - 1] && dp[pi - 1, ti - 1] >= 0)
                {
                    matchScore = dp[pi - 1, ti - 1] + ComputeCharScore(pi - 1, ti - 1, patternLower, textLower, originalText, matched, pi - 1, ti - 1);
                }

                if (matchScore >= skipScore && matchScore >= 0)
                {
                    dp[pi, ti] = matchScore;
                    matched[pi, ti] = true;
                }
                else
                {
                    dp[pi, ti] = skipScore;
                    matched[pi, ti] = false;
                }
            }
        }

        int finalScore = dp[pLen, tLen];
        if (finalScore < 0)
            return new MatchResult(0, []);

        // Add exact match bonus
        if (pLen == tLen && patternLower == textLower)
            finalScore += ExactMatchBonus;

        // Reconstruct matched indices
        var indices = new List<int>();
        int p = pLen, t = tLen;
        while (p > 0 && t > 0)
        {
            if (matched[p, t])
            {
                indices.Add(t - 1);
                p--;
                t--;
            }
            else
            {
                t--;
            }
        }

        indices.Reverse();

        return new MatchResult(finalScore, indices);
    }

    private static int ComputeCharScore(int patternIdx, int textIdx, string patternLower, string textLower, string originalText, bool[,] matched, int pi, int ti)
    {
        int score = BaseCharScore;

        // Prefix bonus: matching at the start of the text
        if (textIdx == 0)
            score += PrefixBonus;

        // Word boundary bonus: character after space, underscore, dash
        if (textIdx > 0 && IsWordSeparator(originalText[textIdx - 1]))
            score += WordBoundaryBonus;

        // CamelCase bonus: uppercase char preceded by lowercase
        if (textIdx > 0 && char.IsUpper(originalText[textIdx]) && char.IsLower(originalText[textIdx - 1]))
            score += CamelCaseBonus;

        // Consecutive bonus: previous pattern char was matched at the immediately preceding text position.
        // matched[pi, ti] (1-based) tells us pattern[pi-1] matched text[ti-1], which is exactly
        // pattern[patternIdx-1] matched text[textIdx-1] — the directly preceding alignment.
        if (patternIdx > 0 && ti > 0 && matched[pi, ti])
            score += ConsecutiveBonus;

        return score;
    }

    private static bool IsWordSeparator(char c) =>
        c is ' ' or '_' or '-' or '/' or '\\' or '.';
}
