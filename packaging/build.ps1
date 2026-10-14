param(
    # Versión X.Y.Z del instalador. Si se omite, se usa <Version> de Directory.Build.props.
    [string]$Version = ''
)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    if (!$Version) { $Version = ([xml](Get-Content Directory.Build.props)).Project.PropertyGroup.Version }
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "La versión '$Version' debe tener el formato X.Y.Z." }
    Write-Host "Versión $Version"
    dotnet restore POS.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Falló la restauración de dependencias.' }
    dotnet test POS.Tests/POS.Tests.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas.' }
    dotnet publish POS.Desktop/POS.Desktop.csproj -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -p:Version=$Version -o artifacts/windows
    if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación.' }
    $compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
    if (!(Test-Path $compiler)) { throw 'Instalá Inno Setup 6 desde https://jrsoftware.org/isinfo.php.' }
    & $compiler "/DAppVersion=$Version" packaging/MiPOS.iss
    if ($LASTEXITCODE -ne 0) { throw 'Falló el instalador.' }
    # Checksum que el actualizador de MiPOS verifica antes de instalar (formato sha256sum).
    $installer = 'artifacts/installer/MiPOS-Setup-x64.exe'
    $hash = (Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -Path "$installer.sha256" -Value "$hash  MiPOS-Setup-x64.exe" -NoNewline -Encoding ascii
} finally { Pop-Location }
