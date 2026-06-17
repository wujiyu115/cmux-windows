namespace Cmux.Core.Terminal;

public static class UnicodeWidth
{
    public static bool AmbiguousIsWide { get; set; }

    public static bool IsWideChar(char c)
    {
        int cp = c;
        if (AmbiguousIsWide && cp >= 0x2500 && cp <= 0x25FF) return true;
        return cp >= 0x1100 &&
            (cp <= 0x115F                  // Hangul Jamo
            || cp == 0x2329 || cp == 0x232A // LEFT/RIGHT-POINTING ANGLE BRACKET
            || (cp >= 0x2E80 && cp <= 0x303E)  // CJK Radicals..CJK Symbols
            || (cp >= 0x3040 && cp <= 0x33BF)  // Hiragana..CJK Compatibility
            || (cp >= 0x3400 && cp <= 0x4DBF)  // CJK Unified Ext A
            || (cp >= 0x4E00 && cp <= 0xA4CF)  // CJK Unified..Yi Radicals
            || (cp >= 0xA960 && cp <= 0xA97C)  // Hangul Jamo Extended-A
            || (cp >= 0xAC00 && cp <= 0xD7A3)  // Hangul Syllables
            || (cp >= 0xF900 && cp <= 0xFAFF)  // CJK Compatibility Ideographs
            || (cp >= 0xFE10 && cp <= 0xFE19)  // Vertical Forms
            || (cp >= 0xFE30 && cp <= 0xFE6F)  // CJK Compatibility Forms..Small Forms
            || (cp >= 0xFF01 && cp <= 0xFF60)  // Fullwidth Forms
            || (cp >= 0xFFE0 && cp <= 0xFFE6)); // Fullwidth Signs
    }

    public static int GetCharWidth(char c) => IsWideChar(c) ? 2 : 1;
}
