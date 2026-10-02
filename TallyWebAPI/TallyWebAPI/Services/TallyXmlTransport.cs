using System.Net.Http.Headers;
using System.Text;

namespace TallyWebAPI.Services
{
    /// <summary>
    /// Sends XML to Tally using UTF-16 (which preserves Tamil input) and
    /// decodes the XML response from its BOM, byte pattern, or declared charset.
    /// </summary>
    internal static class TallyXmlTransport
    {
        private static readonly Uri TallyEndpoint = new("http://127.0.0.1:9000");

        public static async Task<string> PostAsync(
            HttpClient httpClient,
            string xmlRequest,
            CancellationToken cancellationToken = default)
        {
            var requestBytes = Encoding.Unicode.GetBytes(xmlRequest);
            using var content = new ByteArrayContent(requestBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
            content.Headers.ContentType.CharSet = "utf-16";

            using var request = new HttpRequestMessage(HttpMethod.Post, TallyEndpoint)
            {
                Content = content
            };
            request.Headers.TryAddWithoutValidation("Accept", "application/xml");
            request.Headers.TryAddWithoutValidation("Accept-Charset", "utf-16, utf-8");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            return DecodeXml(responseBytes, response.Content.Headers.ContentType?.CharSet);
        }

        private static string DecodeXml(byte[] bytes, string? declaredCharset)
        {
            if (bytes.Length >= 2)
            {
                if (bytes[0] == 0xFF && bytes[1] == 0xFE)
                    return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

                if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                    return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

                // Tally may return UTF-16 without a BOM or a charset header.
                if (bytes[1] == 0 && bytes[0] == (byte)'<')
                    return Encoding.Unicode.GetString(bytes);

                if (bytes[0] == 0 && bytes[1] == (byte)'<')
                    return Encoding.BigEndianUnicode.GetString(bytes);
            }

            if (string.Equals(declaredCharset?.Trim(' ', '"', '\''), "utf-16", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(declaredCharset?.Trim(' ', '"', '\''), "utf-16le", StringComparison.OrdinalIgnoreCase))
                return Encoding.Unicode.GetString(bytes);

            if (string.Equals(declaredCharset?.Trim(' ', '"', '\''), "utf-16be", StringComparison.OrdinalIgnoreCase))
                return Encoding.BigEndianUnicode.GetString(bytes);

            return Encoding.UTF8.GetString(bytes);
        }
    }
}
