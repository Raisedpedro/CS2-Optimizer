using CS2Optimizer.Core.Updates;

namespace CS2Optimizer.Tests;

public sealed class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.0.4", "1.0.4")]
    [InlineData("1.0.4", "1.0.4")]
    [InlineData("V2.3", "2.3")]
    [InlineData("v1.0.4-beta.1", "1.0.4")]
    [InlineData("  v1.2.3.4  ", "1.2.3.4")]
    public void ParseTag_aceita_os_formatos_usados_no_release(string tag, string esperado)
    {
        Assert.Equal(Version.Parse(esperado), UpdateChecker.ParseTag(tag));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v")]
    [InlineData("abc")]
    [InlineData("release-1")]
    public void ParseTag_devolve_null_quando_nao_da_para_ler(string? tag)
    {
        Assert.Null(UpdateChecker.ParseTag(tag));
    }

    [Fact]
    public void IsNewer_compara_versoes_e_ignora_nulos()
    {
        Assert.True(UpdateChecker.IsNewer(new Version(1, 0, 4), new Version(1, 0, 3)));
        Assert.True(UpdateChecker.IsNewer(new Version(2, 0), new Version(1, 9, 9)));
        Assert.True(UpdateChecker.IsNewer(new Version(1, 0, 3, 1), new Version(1, 0, 3)));

        Assert.False(UpdateChecker.IsNewer(new Version(1, 0, 3), new Version(1, 0, 3)));
        Assert.False(UpdateChecker.IsNewer(new Version(1, 0, 2), new Version(1, 0, 3)));
        Assert.False(UpdateChecker.IsNewer(null, new Version(1, 0, 3)));
        Assert.False(UpdateChecker.IsNewer(new Version(1, 0, 4), null));
    }

    [Fact]
    public void ParseRelease_devolve_os_dados_do_release_mais_novo()
    {
        const string json = """
        {
          "tag_name": "v1.0.4",
          "name": "CS2 / Windows Optimizer 1.0.4",
          "html_url": "https://github.com/Raisedpedro/CS2-Optimizer/releases/tag/v1.0.4",
          "body": "correções",
          "published_at": "2026-11-10T18:00:00Z",
          "draft": false,
          "prerelease": false
        }
        """;

        var info = UpdateChecker.ParseRelease(json, new Version(1, 0, 3));

        Assert.NotNull(info);
        Assert.Equal("v1.0.4", info!.Tag);
        Assert.Equal(new Version(1, 0, 4), info.Version);
        Assert.Equal("CS2 / Windows Optimizer 1.0.4", info.Name);
        Assert.Contains("v1.0.4", info.ReleaseUrl);
        Assert.Equal("correções", info.Notes);
        Assert.NotNull(info.PublishedAt);
    }

    [Fact]
    public void ParseRelease_ignora_versao_igual_ou_mais_antiga()
    {
        const string json = """{ "tag_name": "v1.0.3", "name": "atual" }""";

        Assert.Null(UpdateChecker.ParseRelease(json, new Version(1, 0, 3)));
        Assert.Null(UpdateChecker.ParseRelease(json, new Version(1, 1, 0)));
    }

    [Theory]
    [InlineData("""{ "tag_name": "v9.9.9", "draft": true }""")]
    [InlineData("""{ "tag_name": "v9.9.9", "prerelease": true }""")]
    [InlineData("""{ "tag_name": "sem-versao" }""")]
    [InlineData("""{ "tag_name": "" }""")]
    public void ParseRelease_ignora_rascunho_pre_release_e_tag_invalida(string json)
    {
        Assert.Null(UpdateChecker.ParseRelease(json, new Version(1, 0, 0)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ nao e json }")]
    [InlineData("[1, 2, 3]")]
    [InlineData("null")]
    public void ParseRelease_nao_quebra_com_resposta_estranha(string json)
    {
        Assert.Null(UpdateChecker.ParseRelease(json, new Version(1, 0, 0)));
    }

    [Fact]
    public void ParseRelease_preenche_url_de_release_quando_o_json_nao_traz()
    {
        const string json = """{ "tag_name": "v1.1.0" }""";

        var info = UpdateChecker.ParseRelease(json, new Version(1, 0, 0));

        Assert.NotNull(info);
        Assert.Equal("v1.1.0", info!.Name);
        Assert.Equal(UpdateChecker.ReleasesPageUrl(UpdateChecker.DefaultRepository), info.ReleaseUrl);
    }
}
