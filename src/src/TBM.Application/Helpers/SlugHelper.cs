using System.Text;
using System.Text.RegularExpressions;

namespace TBM.Application.Helpers;

public static class SlugHelper
{
    public static string GenerateSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        
        // Convert to lowercase
        text = text.ToLowerInvariant();
        
        // Remove accents
        text = RemoveAccents(text);
        
        // Replace spaces with hyphens
        text = text.Replace(" ", "-");
        
        // Remove invalid characters
        text = Regex.Replace(text, @"[^a-z0-9\-]", "");
        
        // Replace multiple hyphens with single hyphen
        text = Regex.Replace(text, @"-+", "-");
        
        // Trim hyphens from start and end
        text = text.Trim('-');
        
        return text;
    }
    
    private static string RemoveAccents(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();
        
        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }
        
        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}