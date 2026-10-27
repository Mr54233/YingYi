using System;
using System.Collections.Generic;
using System.Text;
using TinyPinyin;

namespace StrmAssistant.Pinyin
{
    /// <summary>
    ///     Pinyin generation layer (docs/analysis/p1-1-p1-2-拼音层方案.md §2).
    ///     Token shape is frozen by sandbox experiments: original name kept first,
    ///     per-character tokens, character bigrams, syllable bigrams connected,
    ///     spaced full pinyin, connected full pinyin, initials string and initial
    ///     suffixes (length &gt;= 2) so mid-token FTS prefix queries hit.
    /// </summary>
    internal static class PinyinConverter
    {
        private const char PinyinSeparator = ' ';

        internal static bool ContainsChinese(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            foreach (var c in text)
            {
                if (IsHanCharacter(c)) return true;
            }

            return false;
        }

        /// <summary>
        ///     Builds the token string injected into the FTS Name column (c0).
        ///     Returns null when the name contains no Chinese characters.
        /// </summary>
        internal static string BuildSearchTokens(string name)
        {
            if (!ContainsChinese(name)) return null;

            var chars = new List<char>();
            foreach (var c in name)
            {
                if (IsHanCharacter(c)) chars.Add(c);
            }

            if (chars.Count == 0) return null;

            var nameTokens = new List<string>(chars.Count);
            var charBigrams = new List<string>(chars.Count);
            var syllables = new List<string>(chars.Count);

            foreach (var c in chars)
            {
                var syllable = GetSyllable(c);
                nameTokens.Add(c.ToString());
                if (!string.IsNullOrEmpty(syllable)) syllables.Add(syllable);
            }

            for (var i = 0; i + 1 < chars.Count; i++)
            {
                charBigrams.Add(chars[i].ToString() + chars[i + 1]);
            }

            var syllableBigrams = new List<string>(syllables.Count);
            for (var i = 0; i + 1 < syllables.Count; i++)
            {
                // §2.3: syllable bigrams are written CONNECTED (yongzheng zhengwang
                // wangchao), the FTS prefix index otherwise misses mid-string
                // syllable queries like "zhengwang".
                syllableBigrams.Add(syllables[i] + syllables[i + 1]);
            }

            var fullSpaced = string.Join(PinyinSeparator.ToString(), syllables);
            var fullConnected = string.Concat(syllables);

            var initialsBuilder = new StringBuilder(syllables.Count);
            foreach (var syllable in syllables)
            {
                initialsBuilder.Append(syllable[0]);
            }

            var initials = initialsBuilder.ToString();

            var initialSuffixes = new List<string>();
            for (var length = initials.Length - 1; length >= 2; length--)
            {
                for (var start = 0; start + length <= initials.Length; start++)
                {
                    initialSuffixes.Add(initials.Substring(start, length));
                }
            }

            var parts = new List<string>
            {
                name,
                string.Join(PinyinSeparator.ToString(), nameTokens),
                string.Join(PinyinSeparator.ToString(), charBigrams),
                string.Join(PinyinSeparator.ToString(), syllableBigrams),
                fullSpaced,
                fullConnected,
                initials
            };
            parts.AddRange(initialSuffixes);

            return string.Join(PinyinSeparator.ToString(), parts);
        }

        /// <summary>
        ///     P1-2 sort name: per-character pinyin initials, latin letters and digits kept,
        ///     everything else dropped. 雍正王朝 2008 → YZWC2008. Returns null when the
        ///     name has no Chinese characters (caller skips the row).
        /// </summary>
        internal static string BuildSortName(string name)
        {
            if (!ContainsChinese(name)) return null;

            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (IsHanCharacter(c))
                {
                    var syllable = GetSyllable(c);
                    if (string.IsNullOrEmpty(syllable)) continue;
                    builder.Append(char.ToUpperInvariant(syllable[0]));
                }
                else if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToUpperInvariant(c));
                }
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }

        /// <summary>
        ///     Reserved for the multi-tone word override file (pinyin-overrides.json).
        ///     Intentionally empty in this milestone; kept per plan §2.2.
        /// </summary>
        internal static void LoadOverrides(string jsonPath)
        {
        }

        private static string GetSyllable(char c)
        {
            var syllable = PinyinHelper.GetPinyin(c);
            return string.IsNullOrEmpty(syllable) ? null : syllable.ToLowerInvariant();
        }

        private static bool IsHanCharacter(char c)
        {
            return c >= 0x4E00 && c <= 0x9FFF;
        }
    }
}
