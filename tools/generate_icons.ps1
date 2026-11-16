# Regenerate the PNG assets from icons/icon.svg using an installed Chrome or Edge.
# Run from any directory: pwsh -File tools/generate_icons.ps1
param([string]$BrowserPath)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$iconsPath = Join-Path $projectRoot 'icons'
$assetsPath = Join-Path $projectRoot 'Sources/Assets'
$renderPath = Join-Path $projectRoot 'Sources/obj/icon-renderer'

if (-not $BrowserPath) {
    $browsers = foreach ($installRoot in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA)) {
        if ($installRoot) {
            foreach ($relativePath in @('Google/Chrome/Application/chrome.exe', 'Microsoft/Edge/Application/msedge.exe')) {
                $candidate = Join-Path $installRoot $relativePath
                if (Test-Path -LiteralPath $candidate) { $candidate }
            }
        }
    }
    $BrowserPath = $browsers | Select-Object -First 1
}
if (-not $BrowserPath -or -not (Test-Path -LiteralPath $BrowserPath)) {
    throw 'Install Chrome or Edge, or specify its executable with -BrowserPath.'
}

New-Item -ItemType Directory -Path $renderPath -Force | Out-Null
$svg = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $iconsPath 'icon.svg')))
$htmlPath = Join-Path $renderPath 'render.html'
@"
<!doctype html><meta charset="utf-8"><body><script>
const image = new Image();
image.onload = () => {
  for (const size of [128, 256, 512]) {
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = size;
    canvas.getContext('2d').drawImage(image, 0, 0, size, size);
    const output = document.createElement('pre');
    output.id = 'icon-' + size;
    output.textContent = canvas.toDataURL('image/png').split(',')[1];
    document.body.append(output);
  }
};
image.src = 'data:image/svg+xml;base64,$svg';
</script></body>
"@ | Set-Content -LiteralPath $htmlPath -Encoding UTF8

$outputPath = Join-Path $renderPath 'output.html'
$arguments = @('--headless', '--disable-gpu', '--disable-extensions', '--disable-sync',
    '--disable-background-networking', '--no-first-run', '--no-default-browser-check',
    '--dump-dom', '--virtual-time-budget=3000',
    ('--user-data-dir="{0}"' -f (Join-Path $renderPath 'profile')), ([Uri]$htmlPath).AbsoluteUri)
$process = Start-Process -FilePath $BrowserPath -ArgumentList $arguments -WindowStyle Hidden `
    -RedirectStandardOutput $outputPath -RedirectStandardError (Join-Path $renderPath 'browser.log') -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "SVG renderer failed; see $renderPath/browser.log." }

$rendered = Get-Content -LiteralPath $outputPath -Raw
$images = @{}
foreach ($size in @(128, 256, 512)) {
    $match = [regex]::Match($rendered, ('<pre id="icon-{0}">([A-Za-z0-9+/=]+)</pre>' -f $size))
    if (-not $match.Success) { throw "SVG renderer did not produce the $size pixel icon." }
    $images[$size] = [Convert]::FromBase64String($match.Groups[1].Value)
}
foreach ($size in @(128, 256, 512)) {
    $name = if ($size -eq 128) { 'icon.png' } else { "icon-$size.png" }
    [IO.File]::WriteAllBytes((Join-Path $iconsPath $name), $images[$size])
    Write-Output "Generated $name ($size x $size) in icons from icons/icon.svg"
}
[IO.File]::WriteAllBytes((Join-Path $assetsPath 'icon.png'), $images[128])
Write-Output 'Updated Sources/Assets/icon.png for the NuGet package'
