using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SSAM_Server.Common;

public static partial class SlugHelper
{
    public const int MaxSlugLength = 50;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    public static partial Regex WorkspaceCodeRegex();

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$")]
    public static partial Regex RoleCodeRegex();

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}$")]
    public static partial Regex ProjectKeyRegex();

    public static string StripVietnamese(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(ch switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => ch
            });
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string Slugify(string? input)
    {
        var stripped = StripVietnamese(input).ToLowerInvariant();
        var replaced = Regex.Replace(stripped, "[^a-z0-9]+", "-").Trim('-');
        if (replaced.Length > MaxSlugLength)
        {
            replaced = replaced[..MaxSlugLength].TrimEnd('-');
        }
        return replaced;
    }

    public static string ToRoleCode(string? input)
    {
        var stripped = StripVietnamese(input).ToUpperInvariant();
        var code = Regex.Replace(stripped, "[^A-Z0-9]+", "_").Trim('_');
        if (code.Length > 50)
        {
            code = code[..50].TrimEnd('_');
        }
        if (string.IsNullOrEmpty(code))
            return string.Empty;

        if (!char.IsAsciiLetterUpper(code[0]))
        {
            code = $"R_{code}";
            if (code.Length > 50)
                code = code[..50].TrimEnd('_');
        }
        return code;
    }

    public static string ToProjectKey(string? name)
    {
        var stripped = StripVietnamese(name);
        var words = Regex.Split(stripped, "[^A-Za-z0-9]+").Where(w => !string.IsNullOrEmpty(w)).ToArray();
        var acronym = words.FirstOrDefault(w => w.Length >= 2 && ProjectKeyRegex().IsMatch(w));
        if (acronym != null)
            return acronym;

        var initials = string.Concat(words.Select(w => w[0])).ToUpperInvariant();
        var key = Regex.Replace(initials, "^[0-9]+", "");
        if (key.Length < 2 && words.Length > 0)
        {
            key = Regex.Replace(words[0].ToUpperInvariant(), "[^A-Z0-9]", "");
            key = Regex.Replace(key, "^[0-9]+", "");
        }
        return key.Length > 10 ? key[..10] : key;
    }
}
