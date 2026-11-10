using System.Net.Http;
using System.Text.Json;

namespace CS2Optimizer.Core.Updates;

/// <summary>Uma versão mais nova publicada no GitHub.</summary>
public sealed record UpdateInfo(
    string Tag,
    Version Version,
    string Name,
    string ReleaseUrl,
    string Notes,
    DateTimeOffset? PublishedAt);

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Update, string? Error)
{
    public static UpdateCheckResult Current() => new(UpdateCheckStatus.UpToDate, null, null);

    public static UpdateCheckResult Newer(UpdateInfo info) =>
        new(UpdateCheckStatus.UpdateAvailable, info, null);

    public static UpdateCheckResult Failed(string error) =>
        new(UpdateCheckStatus.Failed, null, error);
}

/// <summary>
/// Consulta a última release publicada no GitHub e compara com a versão do programa.
/// Nunca lança: sem internet, com a API fora do ar ou resposta estranha o resultado é
/// <see cref="UpdateCheckStatus.Failed"/> (a tela só informa, não incomoda).
/// </summary>
public static class UpdateChecker
{
    public const string DefaultRepository = "Raisedpedro/CS2-Optimizer";

    public static string ReleasesPageUrl(string repository) =>
        $"https://github.com/{repository}/releases/latest";

    public static string LatestReleaseApi(string repository) =>
        $"https://api.github.com/repos/{repository}/releases/latest";

    /// <summary>Converte a tag do release ("v1.0.4", "1.0.4-beta.1") em <see cref="Version"/>.</summary>
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        var texto = tag.Trim();
        if (texto.StartsWith('v') || texto.StartsWith('V')) texto = texto[1..];

        // sufixo de pré-release ("-beta.1") não entra na comparação
        var hifen = texto.IndexOf('-');
        if (hifen >= 0) texto = texto[..hifen];

        return Version.TryParse(texto, out var versao) ? versao : null;
    }

    public static bool IsNewer(Version? latest, Version? current) =>
        latest is not null && current is not null && latest > current;

    /// <summary>
    /// Lê o JSON de <c>/releases/latest</c>. Devolve null quando o release é rascunho,
    /// pré-release, tem tag inválida ou não é mais novo que <paramref name="current"/>.
    /// </summary>
    public static UpdateInfo? ParseRelease(string? json, Version current)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;
            if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) return null;

            var tag = Text(root, "tag_name");
            var versao = ParseTag(tag);
            if (!IsNewer(versao, current)) return null;

            var nome = Text(root, "name");
            if (string.IsNullOrWhiteSpace(nome)) nome = tag;
            var url = Text(root, "html_url");
            if (string.IsNullOrWhiteSpace(url)) url = ReleasesPageUrl(DefaultRepository);
            var notas = Text(root, "body") ?? "";

            DateTimeOffset? publicado = null;
            var publicadoTexto = Text(root, "published_at");
            if (!string.IsNullOrWhiteSpace(publicadoTexto) &&
                DateTimeOffset.TryParse(publicadoTexto, out var data))
                publicado = data;

            return new UpdateInfo(tag!, versao!, nome!, url!, notas, publicado);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Faz a consulta. Qualquer falha vira <see cref="UpdateCheckStatus.Failed"/>.</summary>
    public static async Task<UpdateCheckResult> CheckAsync(
        Version current,
        TimeSpan? timeout = null,
        string repository = DefaultRepository,
        CancellationToken cancellationToken = default)
    {
        var limite = timeout ?? TimeSpan.FromSeconds(8);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(limite);

        try
        {
            using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"CS2Optimizer/{current}");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var json = await http.GetStringAsync(LatestReleaseApi(repository), cts.Token)
                                 .ConfigureAwait(false);

            var info = ParseRelease(json, current);
            return info is null ? UpdateCheckResult.Current() : UpdateCheckResult.Newer(info);
        }
        catch (OperationCanceledException)
        {
            return UpdateCheckResult.Failed("tempo esgotado");
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed(ex.Message);
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}
