using Dayleaf.Services;
using Microsoft.Net.Http.Headers;

namespace Dayleaf.Endpoints;

public static class MediaEndpoints
{
    public static void MapMediaEndpoints(this WebApplication app)
    {
        app.MapGet("/media/{id:guid}", async (Guid id, HttpContext http, IMediaService media, PasswordGate gate) =>
        {
            // With the password gate on, anonymous visitors only get images that appear on a shared page.
            var signedIn = http.User.Identity?.IsAuthenticated == true;
            if (gate.Enabled && !signedIn && !await media.IsOnSharedPageAsync(id)) return Results.NotFound();

            var file = await media.GetAsync(id);
            if (file is null) return Results.NotFound();
            var path = media.PathOf(file);
            if (!File.Exists(path)) return Results.NotFound();

            var etag = new EntityTagHeaderValue($"\"{file.Sha256}\"");
            http.Response.Headers.CacheControl = gate.Enabled ? "private, max-age=31536000, immutable" : "public, max-age=31536000, immutable";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(path, file.ContentType, entityTag: etag, enableRangeProcessing: true);
        }).AllowAnonymous();
    }
}
