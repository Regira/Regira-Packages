using System.Text.RegularExpressions;
using System.Web;
using Regira.IO.Utilities;
using Regira.Utilities;

namespace Regira.Web.Utilities;

public static class UriUtility
{
    /// <summary>
    /// Converts a relative URI to an absolute URI based on the application's base URI.
    /// </summary>
    /// <param name="relativeUri">The relative URI to convert to an absolute URI.</param>
    /// <returns>A string representation of the absolute URI.</returns>
    /// <remarks>
    /// This method constructs an absolute URI by combining the application's base URI with the provided relative URI.
    /// </remarks>
    public static string ToAbsoluteUri(string relativeUri)
    {
        var baseUri = new UriBuilder().Uri;
        var uri = new Uri(baseUri, relativeUri);

        var urlBuilder = new UriBuilder
        {
            Path = uri.AbsolutePath,
            Query = uri.Query,
            Port = uri.Port
        };

        return urlBuilder.Uri.ToString();
    }
    /// <summary>
    /// Converts the specified input string into a URL-friendly "slug" format.
    /// </summary>
    /// <param name="input">The input string to be converted into a slug. Can be <c>null</c> or empty.</param>
    /// <returns>
    /// A slugified version of the input string, which is a lowercase, hyphen-separated string
    /// with diacritical marks and invalid characters removed. Returns an empty string if the input is <c>null</c> or whitespace.
    /// </returns>
    /// <remarks>
    /// This method removes diacritical marks, replaces invalid characters with valid ones, 
    /// collapses multiple spaces into a single space, and replaces spaces with hyphens.
    /// It ensures the resulting string is suitable for use in URLs.
    /// </remarks>
    public static string Slugify(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // https://stackoverflow.com/questions/2920744/url-slugify-algorithm-in-c#answer-2921135
        var str = input.RemoveDiacritics()!;
        // invalid chars           
        str = Regex.Replace(str, @"[^a-zA-Z0-9\s-]", string.Empty);
        // convert multiple spaces into one space   
        str = Regex.Replace(str, @"\s+", " ").Trim();
        // cut and trim 
        //str = str.Substring(0, str.Length <= 45 ? str.Length : 45).Trim();
        // hyphens
        str = Regex.Replace(str, @"\s", "-");

        str = Regex.Replace(str, "-+", "-");
        return str.Trim('-');
    }

    /// <summary>
    /// Converts a byte array into a Base64-encoded data URL for an image.
    /// </summary>
    /// <param name="bytes">The byte array representing the image data.</param>
    /// <param name="contentType">
    /// The MIME type of the image (e.g., "image/png"). Defaults to "image/png" if not specified.
    /// </param>
    /// <returns>
    /// A string containing the Base64-encoded data URL for the image, in the format:
    /// <c>data:[contentType];base64,[Base64String]</c>.
    /// </returns>
    /// <remarks>
    /// This method is useful for embedding images directly into HTML or other documents
    /// as Base64-encoded data URLs.
    /// </remarks>
    public static string ToBase64ImageUrl(byte[] bytes, string contentType = "image/png")
    {
        return $"data:{contentType};base64,{bytes.GetBase64String()}";
    }

    /// <summary>
    /// Converts a querystring into a lookup
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public static ILookup<string, string> ToQueryDictionary(string query)
    {
        return query
            .TrimStart('?')
            .Split('&')
            .Select(x =>
            {
                var segments = x.Split('=');
                return new { Key = segments.FirstOrDefault(), Value = segments.LastOrDefault() };
            })
            .ToLookup(x => HttpUtility.UrlDecode(x.Key)!, x => HttpUtility.UrlDecode(x.Value)!);
    }
}