using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace POS.Infrastructure;

/// <summary>Última versión publicada como Release de GitHub, con el instalador y su checksum SHA-256.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string Notes, Uri InstallerUrl, Uri ChecksumUrl, long Size, DateTimeOffset Published);

/// <summary>
/// Busca y descarga actualizaciones desde las Releases públicas de GitHub. No requiere credenciales.
/// Solo acepta releases con un instalador <see cref="InstallerName"/> y su archivo <c>.sha256</c>; la descarga se verifica antes de devolverla.
/// </summary>
public sealed class UpdateChecker(HttpClient http, string owner = "C1r0L", string repo = "GestionOffline")
{
    public const string InstallerName = "MiPOS-Setup-x64.exe";
    public const string ChecksumName = InstallerName + ".sha256";

    /// <summary>Devuelve la última release publicada, o null si no hay ninguna o no trae instalador y checksum.</summary>
    public async Task<ReleaseInfo?> GetLatestAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/releases/latest");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MiPOS-Updater", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean() || root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (ParseTag(tag) is not Version version) return null;
        Uri? installer = null, checksum = null; long size = 0;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
            if (url.Scheme != Uri.UriSchemeHttps) continue;
            if (name == InstallerName) { installer = url; size = asset.GetProperty("size").GetInt64(); }
            else if (name == ChecksumName) checksum = url;
        }
        if (installer == null || checksum == null) return null;
        var notes = root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String ? body.GetString()!.Trim() : "";
        var published = root.TryGetProperty("published_at", out var at) && at.ValueKind == JsonValueKind.String ? at.GetDateTimeOffset() : DateTimeOffset.MinValue;
        return new(version, tag, notes, installer, checksum, size, published);
    }

    /// <summary>Convierte "v1.2.3" o "1.2.3" en una versión de tres partes; devuelve null si el tag no es una versión.</summary>
    public static Version? ParseTag(string tag)
    {
        var text = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(text, out var v) && v.Build >= 0 && v.Revision < 0 ? v : null;
    }

    /// <summary>Compara solo mayor, menor y parche: la versión de ensamblado agrega un cuarto número que no se publica.</summary>
    public static bool IsNewer(Version current, ReleaseInfo release) =>
        release.Version > new Version(current.Major, current.Minor, Math.Max(current.Build, 0));

    /// <summary>Descarga el instalador a <paramref name="directory"/> y verifica su SHA-256 contra el checksum publicado. Si no coincide, lo borra y falla.</summary>
    public async Task<string> DownloadAsync(ReleaseInfo release, string directory, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(directory);
        var expected = ParseChecksum(await http.GetStringAsync(release.ChecksumUrl, ct));
        var path = Path.Combine(directory, $"MiPOS-Setup-{release.Version}.exe");
        try
        {
            using (var response = await http.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? release.Size;
                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var target = File.Create(path);
                var buffer = new byte[81920]; long read = 0; int n;
                while ((n = await source.ReadAsync(buffer, ct)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, n), ct); read += n;
                    if (total > 0) progress?.Report(Math.Min(1, (double)read / total));
                }
            }
            string actual;
            await using (var file = File.OpenRead(path)) actual = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("La descarga no coincide con la versión publicada. No se instaló nada.");
            return path;
        }
        catch { File.Delete(path); throw; }
    }

    /// <summary>Acepta el formato de sha256sum ("hash  archivo") o solo el hash.</summary>
    public static string ParseChecksum(string text)
    {
        var hash = text.Trim().Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new InvalidDataException("El checksum publicado no es válido. No se instaló nada.");
        return hash;
    }
}

/// <summary>Recuerda cuándo se buscaron actualizaciones para hacerlo como máximo una vez por día.</summary>
public sealed class UpdateSchedule(string path)
{
    /// <summary>True si todavía no se buscó en el día local de <paramref name="now"/>.</summary>
    public bool ShouldCheck(DateTime now) => LastCheck() is not DateTime last || last.Date != now.Date;
    public void MarkChecked(DateTime now)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, string> { ["lastCheck"] = now.ToString("O") }));
    }
    DateTime? LastCheck()
    {
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            return data != null && data.TryGetValue("lastCheck", out var text) && DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out var at) ? at : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }
}
