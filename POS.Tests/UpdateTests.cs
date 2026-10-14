using System.Net;
using System.Security.Cryptography;
using System.Text;
using POS.Infrastructure;
using Xunit;

namespace POS.Tests;
public sealed class UpdateTests : IDisposable
{
    readonly string directory=Path.Combine(Path.GetTempPath(),"pos-update-tests-"+Guid.NewGuid());
    static readonly byte[] Installer=Encoding.UTF8.GetBytes("instalador de prueba");
    static string Hash(byte[] data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    const string Base="https://github.com/C1r0L/GestionOffline/releases/download/v0.3.0/";

    /// <summary>Responde como la API de GitHub y la descarga de assets, sin red.</summary>
    sealed class FakeGitHub(string release,string checksum,byte[] installer) : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Requests++;var url=request.RequestUri!.ToString();
            HttpResponseMessage Ok(HttpContent c)=>new(HttpStatusCode.OK){Content=c};
            return Task.FromResult(url.EndsWith("/releases/latest")?(release.Length==0?new HttpResponseMessage(HttpStatusCode.NotFound):Ok(new StringContent(release)))
                :url.EndsWith(".sha256")?Ok(new StringContent(checksum))
                :url.EndsWith(".exe")?Ok(new ByteArrayContent(installer))
                :new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
    static string Release(string tag="v0.3.0",bool checksum=true,bool prerelease=false)=>$$"""
        {"tag_name":"{{tag}}","draft":false,"prerelease":{{(prerelease?"true":"false")}},"body":"Novedades de prueba","published_at":"2026-10-20T12:00:00Z","assets":[
          {"name":"MiPOS-Setup-x64.exe","size":{{Installer.Length}},"browser_download_url":"{{Base}}MiPOS-Setup-x64.exe"}
          {{(checksum?$",{{\"name\":\"MiPOS-Setup-x64.exe.sha256\",\"size\":80,\"browser_download_url\":\"{Base}MiPOS-Setup-x64.exe.sha256\"}}":"")}}]}
        """;
    static UpdateChecker Checker(string release,string? checksum=null,byte[]? installer=null)=>new(new HttpClient(new FakeGitHub(release,checksum??$"{Hash(Installer)}  MiPOS-Setup-x64.exe",installer??Installer)));

    [Fact] public async Task ReadsLatestReleaseAndDetectsNewerVersion()
    {
        var release=await Checker(Release()).GetLatestAsync();
        Assert.NotNull(release);Assert.Equal(new Version(0,3,0),release.Version);Assert.Equal("Novedades de prueba",release.Notes);
        Assert.True(UpdateChecker.IsNewer(new Version(0,2,0,0),release));
        Assert.False(UpdateChecker.IsNewer(new Version(0,3,0,0),release));
        Assert.False(UpdateChecker.IsNewer(new Version(0,4,0),release));
    }
    [Fact] public async Task IgnoresReleasesWithoutChecksumPrereleasesAndMissingReleases()
    {
        Assert.Null(await Checker(Release(checksum:false)).GetLatestAsync());
        Assert.Null(await Checker(Release(prerelease:true)).GetLatestAsync());
        Assert.Null(await Checker(Release(tag:"ultima")).GetLatestAsync());
        Assert.Null(await Checker("").GetLatestAsync());
    }
    [Theory][InlineData("v1.2.3",true)][InlineData("1.2.3",true)][InlineData("v1.2",false)][InlineData("v1.2.3.4",false)][InlineData("beta",false)]
    public void ParsesOnlyThreePartTags(string tag,bool valid)=>Assert.Equal(valid,UpdateChecker.ParseTag(tag)!=null);
    [Fact] public async Task DownloadsAndVerifiesInstaller()
    {
        var checker=Checker(Release());var release=(await checker.GetLatestAsync())!;
        var path=await checker.DownloadAsync(release,directory);
        Assert.Equal(Installer,await File.ReadAllBytesAsync(path));
    }
    [Fact] public async Task RejectsInstallerThatDoesNotMatchChecksum()
    {
        var checker=Checker(Release(),installer:Encoding.UTF8.GetBytes("instalador alterado"));var release=(await checker.GetLatestAsync())!;
        await Assert.ThrowsAsync<InvalidDataException>(()=>checker.DownloadAsync(release,directory));
        Assert.Empty(Directory.GetFiles(directory));
    }
    [Fact] public async Task RejectsInvalidChecksumFile()
    {
        var checker=Checker(Release(),checksum:"no es un hash");var release=(await checker.GetLatestAsync())!;
        await Assert.ThrowsAsync<InvalidDataException>(()=>checker.DownloadAsync(release,directory));
    }
    [Fact] public void ChecksAtMostOncePerDay()
    {
        var schedule=new UpdateSchedule(Path.Combine(directory,"update.json"));var morning=new DateTime(2026,10,20,9,0,0);
        Assert.True(schedule.ShouldCheck(morning));
        schedule.MarkChecked(morning);
        Assert.False(schedule.ShouldCheck(morning.AddHours(10)));
        Assert.True(schedule.ShouldCheck(morning.AddDays(1)));
    }
    public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
}
