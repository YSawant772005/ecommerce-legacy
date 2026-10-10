# Downloads real product photographs for the NovaKart catalogue, one per product type.
#
# Every product type (e.g. "Wireless Mouse", "Running Shoes") maps deterministically to a
# single relevant photo at Content/images/products/<type-slug>.jpg. Books all share
# book.jpg. Photos come from Wikimedia Commons / Wikipedia article lead images, chosen from
# scripts\image-map.json (curated type -> article) with scripts\overrides.json pinning exact
# files where the auto-pick is not ideal. Everything is cached on disk so the app keeps
# working fully offline once generated.
#
# Category tiles (Content/images/categories/<slug>.jpg) are copied from a representative
# type photo so they stay on-brand instead of showing random stock pictures.
#
# Usage:
#   powershell -File scripts\generate-images.ps1                 # fetch missing images
#   powershell -File scripts\generate-images.ps1 -Force          # re-download everything
#   powershell -File scripts\generate-images.ps1 -ProposeOnly    # show picks, download nothing
#
# Requires internet access for the one-time download (uses curl.exe).

param(
    [switch]$Force,
    [switch]$ProposeOnly,
    [int]$Side = 800
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$imgRoot = Join-Path $root "src\LegacyEcommerce\Content\images"
$outProducts = Join-Path $imgRoot "products"
$outCategories = Join-Path $imgRoot "categories"
New-Item -ItemType Directory -Force -Path $outProducts, $outCategories | Out-Null

$apiUA = "NovaKartDemo/1.0 (educational sample; contact: dev@example.com)"
$dlUA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36"

$curlCmd = Get-Command curl.exe -ErrorAction SilentlyContinue
if (-not $curlCmd) { throw "curl.exe is required to download the photo pack." }
$curl = $curlCmd.Source

# A representative type photo per category, used for the category tiles.
$categoryTiles = [ordered]@{
    "electronics"    = "4k-ultra-hd-smart-tv"
    "phones-tablets" = "5g-smartphone"
    "computers"      = "business-laptop"
    "furniture"      = "3-seater-sofa"
    "kitchen"        = "mixer-grinder"
    "mens-fashion"   = "slim-fit-shirt"
    "womens-fashion" = "saree"
    "footwear"       = "running-shoes"
    "beauty"         = "face-serum"
    "sports"         = "yoga-mat"
    "toys"           = "building-blocks-set"
    "books"          = "book"
}

function Slug([string]$v) {
    $sb = New-Object System.Text.StringBuilder
    $last = $false
    foreach ($ch in $v.ToLowerInvariant().ToCharArray()) {
        if ([char]::IsLetterOrDigit($ch)) { [void]$sb.Append($ch); $last = $false }
        elseif (-not $last -and $sb.Length -gt 0) { [void]$sb.Append('-'); $last = $true }
    }
    $s = $sb.ToString().Trim('-')
    if ($s.Length -eq 0) { return "item" }
    return $s
}

function Get-Json([string]$url) {
    for ($i = 0; $i -lt 4; $i++) {
        try {
            $raw = & $curl -s -H "User-Agent: $apiUA" -H "Accept: application/json" $url
            if ([string]::IsNullOrWhiteSpace($raw)) { throw "empty" }
            return $raw | ConvertFrom-Json
        } catch { Start-Sleep -Milliseconds (700 * ($i + 1)) }
    }
    return $null
}

$badTokens = @('logo','icon','diagram','map','flag','coat','symbol','sign','chart','graph',
    'screenshot','seal','montage','collage','sprite','wiktionary','wikiquote','wikinews',
    'wikiversity','wikibooks','wikisource','commons-logo','nuvola','oojs','ambox','question',
    'edit-clear','padlock','protection','portal','category','disambiguation','wikimedia',
    'wikidata','wikilove','sketch','drawing','crest','emblem','banner','wordmark','template',
    'stub','arrow','bullet','barnstar','typography','font','graffiti','street_art','wallpaper',
    'poster','painting','portrait','coin','banknote','stamp','historical','museum')

function Score-File($title, $mime, $w, $h, [string[]]$keywords) {
    if ($mime -notin @('image/jpeg','image/png','image/webp')) { return -1 }
    if ($null -eq $w -or $null -eq $h -or $w -lt 400 -or $h -lt 400) { return -1 }
    $name = $title.ToLowerInvariant()
    foreach ($t in $badTokens) { if ($name.Contains($t)) { return -1 } }
    $score = 0.0
    foreach ($k in $keywords) { if ($k -and $name.Contains($k.ToLowerInvariant())) { $score += 120 } }
    if ($mime -eq 'image/jpeg') { $score += 8 }
    $score += [math]::Min(40, [math]::Log([double]($w * $h)) * 2)
    $score -= [math]::Abs([math]::Log([double]$w / [double]$h)) * 6
    return $score
}

function Get-ExactFile([string]$fileName) {
    if ($fileName.StartsWith("File:")) { $fileName = $fileName.Substring(5) }
    $t = [uri]::EscapeDataString("File:$fileName")
    $url = "https://commons.wikimedia.org/w/api.php?action=query&format=json&prop=imageinfo&iiprop=url%7Csize%7Cmime&iiurlwidth=1000&titles=$t"
    $r = Get-Json $url
    if ($r -and $r.query -and $r.query.pages) {
        foreach ($p in $r.query.pages.PSObject.Properties.Value) {
            $ii = $p.imageinfo
            if ($ii) { return [pscustomobject]@{ title = $p.title; mime = $ii.mime; w = $ii.width; h = $ii.height; thumb = $ii.thumburl; orig = $ii.url; page = $ii.descriptionurl } }
        }
    }
    return $null
}

function Get-Candidates([string]$article, [string[]]$keywords, [string]$commons) {
    $found = @()
    if ($article) {
        $t = [uri]::EscapeDataString($article)
        $url = "https://en.wikipedia.org/w/api.php?action=query&format=json&redirects=1&prop=imageinfo&iiprop=url%7Csize%7Cmime&iiurlwidth=1000&generator=images&gimlimit=100&titles=$t"
        $r = Get-Json $url
        if ($r -and $r.query -and $r.query.pages) {
            foreach ($p in $r.query.pages.PSObject.Properties.Value) {
                $ii = $p.imageinfo
                if ($ii) { $found += [pscustomobject]@{ title = $p.title; mime = $ii.mime; w = $ii.width; h = $ii.height; thumb = $ii.thumburl; orig = $ii.url; page = $ii.descriptionurl } }
            }
        }
    }
    if ($commons) {
        $q = [uri]::EscapeDataString($commons)
        $url = "https://commons.wikimedia.org/w/api.php?action=query&format=json&list=search&srnamespace=6&srlimit=30&srsearch=$q"
        $r = Get-Json $url
        $titles = @()
        if ($r -and $r.query -and $r.query.search) { $titles = @($r.query.search | ForEach-Object { $_.title }) }
        if ($titles.Count -gt 0) {
            $joined = [uri]::EscapeDataString(($titles -join '|'))
            $url2 = "https://commons.wikimedia.org/w/api.php?action=query&format=json&prop=imageinfo&iiprop=url%7Csize%7Cmime&iiurlwidth=1000&titles=$joined"
            $r2 = Get-Json $url2
            if ($r2 -and $r2.query -and $r2.query.pages) {
                foreach ($p in $r2.query.pages.PSObject.Properties.Value) {
                    $ii = $p.imageinfo
                    if ($ii) { $found += [pscustomobject]@{ title = $p.title; mime = $ii.mime; w = $ii.width; h = $ii.height; thumb = $ii.thumburl; orig = $ii.url; page = $ii.descriptionurl } }
                }
            }
        }
    }
    return $found
}

function Get-Image([string]$src, [string]$slug, [string]$outPath) {
    Add-Type -AssemblyName System.Drawing
    $tmp = Join-Path $env:TEMP ("nk_" + $slug + ".bin")
    for ($attempt = 1; $attempt -le 6; $attempt++) {
        & $curl -s -L -H "User-Agent: $dlUA" -H "Referer: https://commons.wikimedia.org/" -o $tmp $src
        if (Test-Path -LiteralPath $tmp) {
            $bytes = [System.IO.File]::ReadAllBytes($tmp)
            if ($bytes.Length -gt 1024) {
                $isImg = ($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xD8) -or ($bytes[0] -eq 0x89 -and $bytes[1] -eq 0x50) -or ($bytes[0] -eq 0x47 -and $bytes[1] -eq 0x49)
                if ($isImg) {
                    try {
                        $ms = New-Object System.IO.MemoryStream(,$bytes)
                        $img = [System.Drawing.Image]::FromStream($ms)
                        $scale = [math]::Min($Side / $img.Width, $Side / $img.Height)
                        $tw = [int][math]::Max(1, [math]::Round($img.Width * $scale))
                        $th = [int][math]::Max(1, [math]::Round($img.Height * $scale))
                        $bmp = New-Object System.Drawing.Bitmap($Side, $Side)
                        $g = [System.Drawing.Graphics]::FromImage($bmp)
                        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                        $g.Clear([System.Drawing.Color]::White)
                        $g.DrawImage($img, [int](($Side - $tw) / 2), [int](($Side - $th) / 2), $tw, $th)
                        $g.Dispose()
                        $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Jpeg)
                        $bmp.Dispose(); $img.Dispose(); $ms.Dispose()
                        Remove-Item -LiteralPath $tmp -Force
                        return $true
                    } catch { }
                }
            }
        }
        Start-Sleep -Seconds (3 * $attempt)
    }
    return $false
}

$map = Get-Content (Join-Path $PSScriptRoot "image-map.json") -Raw | ConvertFrom-Json
$overrides = @{}
$ovPath = Join-Path $PSScriptRoot "overrides.json"
if (Test-Path -LiteralPath $ovPath) {
    $ov = Get-Content $ovPath -Raw | ConvertFrom-Json
    foreach ($p in $ov.PSObject.Properties) { $overrides[$p.Name] = $p.Value }
}
$entries = New-Object System.Collections.ArrayList
foreach ($m in $map) { [void]$entries.Add($m) }
[void]$entries.Add([pscustomobject]@{ cat = "books"; type = "Fiction"; article = "Book"; keyword = "book"; commons = $null })
$bookGenreTypes = @("Mystery","Science Fiction","Romance","Thriller","Fantasy","Self-Help","Business","Young Adult","Children's Picture Book","Cookbook","Programming")

$chosen = @()
$i = 0
foreach ($e in $entries) {
    $i++
    $isBook = ($e.cat -eq "books")
    $slug = if ($isBook) { "book" } else { Slug $e.type }
    $keywords = if ($isBook) { @("book") } else { @($e.keyword -split '\s+') }
    $article = if ($isBook) { "Book" } else { $e.article }
    $commonsTerm = if ($isBook) { $null } else { $e.commons }

    $best = $null; $bestScore = 0
    if ($overrides.ContainsKey($e.type)) {
        $best = Get-ExactFile $overrides[$e.type]
        if (-not $best) { Write-Warning ("override not found: " + $overrides[$e.type]) }
    }
    if (-not $best) {
        $cands = Get-Candidates $article $keywords $commonsTerm
        foreach ($c in $cands) {
            $s = Score-File $c.title $c.mime $c.w $c.h $keywords
            if ($s -gt $bestScore) { $bestScore = $s; $best = $c }
        }
    }
    $src = if ($best) { if ($best.thumb) { $best.thumb } else { $best.orig } } else { $null }
    $chosen += [pscustomobject]@{ cat = $e.cat; type = $e.type; slug = $slug; article = $article; title = if ($best) { $best.title } else { $null }; src = $src; page = if ($best) { $best.page } else { $null } }
    Write-Host ("[{0,3}] {1,-22} {2,-24} -> {3}" -f $i, $e.cat, $e.type, $(if ($best) { $best.title } else { "*** NONE ***" }))
    Start-Sleep -Milliseconds 180
}

# Collapse the twelve book genres to the single shared book image.
$bookRow = $chosen | Where-Object { $_.slug -eq 'book' } | Select-Object -First 1
foreach ($g in $bookGenreTypes) {
    $chosen += [pscustomobject]@{ cat = "books"; type = $g; slug = "book"; article = "Book"; title = $bookRow.title; src = $bookRow.src; page = $bookRow.page }
}

$chosen | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $PSScriptRoot "chosen.json") -Encoding UTF8
Write-Host ("Resolved {0} type entries ({1} with no source)." -f $chosen.Count, (($chosen | Where-Object { -not $_.src }).Count))

if ($ProposeOnly) { return }

foreach ($c in $chosen) {
    if (-not $c.src) { continue }
    $out = Join-Path $outProducts ($c.slug + ".jpg")
    if ((Test-Path -LiteralPath $out) -and -not $Force) { continue }
    if (Get-Image $c.src $c.slug $out) { Write-Host ("  saved " + $c.slug + ".jpg") }
    else { Write-Warning ("failed: " + $c.slug) }
}

foreach ($cat in $categoryTiles.Keys) {
    $src = Join-Path $outProducts ($categoryTiles[$cat] + ".jpg")
    if (Test-Path -LiteralPath $src) { Copy-Item -LiteralPath $src -Destination (Join-Path $outCategories ($cat + ".jpg")) -Force }
}

Write-Host "Photo pack ready. Category tiles refreshed from representative type images."
