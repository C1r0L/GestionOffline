$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet restore POS.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Falló la restauración de dependencias.' }
    dotnet test POS.Tests/POS.Tests.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas.' }
    dotnet publish POS.Desktop/POS.Desktop.csproj -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o artifacts/windows
    if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación.' }
    $compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
    if (!(Test-Path $compiler)) { throw 'Instalá Inno Setup 6 desde https://jrsoftware.org/isinfo.php.' }
    & $compiler packaging/MiPOS.iss
    if ($LASTEXITCODE -ne 0) { throw 'Falló el instalador.' }
} finally { Pop-Location }
