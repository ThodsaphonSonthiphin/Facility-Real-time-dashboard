using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace FacilityRealtime.Api;

/// <summary>
/// facility-0067 amendment (2026-10-28): the frontend build writes <c>name.br</c> and <c>name.gz</c> beside each text
/// file at the highest levels. A request for <c>name</c> from a browser that accepts one gets that copy as it is, so
/// the main JS travels about a third smaller than with on-the-fly compression (which uses the fastest level) and the
/// server does no work per request. Files without a copy, and index.html behind page paths, keep the on-the-fly path.
/// </summary>
public static class PrecompressedFiles
{
    private const string EncodingKey = "facility.precompressed.encoding";
    private const string OriginalPathKey = "facility.precompressed.original";

    /// <summary>Brotli first: it is the smaller of the two.</summary>
    private static readonly (string Encoding, string Suffix)[] Copies = [("br", ".br"), ("gzip", ".gz")];

    private static readonly FileExtensionContentTypeProvider TypesOfOriginals = new();

    /// <summary>Knows <c>.br</c> as well, so the static file middleware serves a copy instead of answering 404.</summary>
    public static IContentTypeProvider ContentTypes()
    {
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".br"] = "application/octet-stream";
        return types;
    }

    /// <summary>Points the request at the best copy the browser accepts; runs before UseStaticFiles.</summary>
    public static IApplicationBuilder UsePrecompressedFiles(this IApplicationBuilder app, IWebHostEnvironment env) =>
        app.Use((context, next) =>
        {
            var request = context.Request;
            var path = request.Path.Value;
            if ((HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
                && !string.IsNullOrEmpty(path) && Path.HasExtension(path))
            {
                var accepted = request.GetTypedHeaders().AcceptEncoding;
                var anyCopy = false;
                foreach (var (encoding, suffix) in Copies)
                {
                    if (!env.WebRootFileProvider.GetFileInfo(path + suffix).Exists)
                    {
                        continue;
                    }
                    anyCopy = true;
                    if (Accepts(accepted, encoding) && !context.Items.ContainsKey(EncodingKey))
                    {
                        context.Items[EncodingKey] = encoding;
                        context.Items[OriginalPathKey] = path;
                        request.Path = path + suffix;
                    }
                }
                if (anyCopy)
                {
                    // A cache in between must keep the plain and the compressed answers apart
                    context.Response.Headers.Vary = HeaderNames.AcceptEncoding;
                }
            }
            return next(context);
        });

    /// <summary>Called from the static file options' OnPrepareResponse: the copy is sent as the original file.</summary>
    public static void LabelCopy(StaticFileResponseContext context)
    {
        var http = context.Context;
        if (http.Items[EncodingKey] is string encoding && http.Items[OriginalPathKey] is string original)
        {
            http.Response.Headers.ContentEncoding = encoding;
            http.Response.ContentType = TypesOfOriginals.TryGetContentType(original, out var type)
                ? type
                : "application/octet-stream";
        }
    }

    private static bool Accepts(IList<StringWithQualityHeaderValue> accepted, string encoding) =>
        accepted.Any(value =>
            string.Equals(value.Value.Value, encoding, StringComparison.OrdinalIgnoreCase)
            && (value.Quality is null || value.Quality > 0));
}
