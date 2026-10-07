# Generates deterministic SVG artwork for NovaKart catalogue
# Usage: powershell -File scripts\generate-images.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outProducts = Join-Path $root "src\LegacyEcommerce\Content\images\products"
$outCategories = Join-Path $root "src\LegacyEcommerce\Content\images\categories"
New-Item -ItemType Directory -Force -Path $outProducts, $outCategories | Out-Null

$hues = @{
    "electronics"    = 220
    "phones-tablets" = 268
    "computers"      = 198
    "furniture"      = 30
    "kitchen"        = 12
    "mens-fashion"   = 212
    "womens-fashion" = 330
    "footwear"       = 158
    "beauty"         = 284
    "sports"         = 18
    "toys"           = 48
    "books"          = 355
}

function Get-Art([string]$cat, [string]$deep) {
    switch ($cat) {
        "electronics" {
            @"
<rect x="128" y="150" width="344" height="222" rx="18" fill="#ffffff"/>
<rect x="148" y="170" width="304" height="168" rx="10" fill="$deep"/>
<circle cx="230" cy="235" r="34" fill="#ffffff" opacity="0.30"/>
<rect x="288" y="212" width="120" height="14" rx="7" fill="#ffffff" opacity="0.55"/>
<rect x="288" y="240" width="84" height="14" rx="7" fill="#ffffff" opacity="0.35"/>
<rect x="200" y="292" width="200" height="14" rx="7" fill="#ffffff" opacity="0.30"/>
<rect x="272" y="372" width="56" height="42" fill="#ffffff"/>
<rect x="212" y="410" width="176" height="16" rx="8" fill="#ffffff"/>
"@
        }
        "phones-tablets" {
            @"
<rect x="212" y="118" width="176" height="344" rx="32" fill="#ffffff"/>
<rect x="228" y="146" width="144" height="288" rx="16" fill="$deep"/>
<rect x="268" y="130" width="64" height="10" rx="5" fill="$deep"/>
<circle cx="344" cy="135" r="7" fill="#ffffff"/>
<circle cx="300" cy="240" r="46" fill="#ffffff" opacity="0.28"/>
<rect x="256" y="320" width="88" height="12" rx="6" fill="#ffffff" opacity="0.55"/>
<rect x="276" y="348" width="48" height="12" rx="6" fill="#ffffff" opacity="0.35"/>
<rect x="288" y="446" width="24" height="6" rx="3" fill="$deep" opacity="0.5"/>
"@
        }
        "computers" {
            @"
<rect x="152" y="146" width="296" height="196" rx="16" fill="#ffffff"/>
<rect x="170" y="164" width="260" height="150" rx="8" fill="$deep"/>
<rect x="212" y="200" width="176" height="12" rx="6" fill="#ffffff" opacity="0.55"/>
<rect x="212" y="226" width="120" height="12" rx="6" fill="#ffffff" opacity="0.35"/>
<rect x="212" y="252" width="150" height="12" rx="6" fill="#ffffff" opacity="0.35"/>
<path d="M118 344 L482 344 L512 390 L88 390 Z" fill="#ffffff"/>
<rect x="252" y="358" width="96" height="10" rx="5" fill="$deep" opacity="0.45"/>
"@
        }
        "furniture" {
            @"
<rect x="146" y="186" width="308" height="118" rx="26" fill="#ffffff"/>
<rect x="122" y="272" width="356" height="90" rx="24" fill="#ffffff"/>
<rect x="96" y="224" width="64" height="146" rx="30" fill="$deep"/>
<rect x="440" y="224" width="64" height="146" rx="30" fill="$deep"/>
<rect x="126" y="216" width="118" height="86" rx="16" fill="$deep" opacity="0.35"/>
<rect x="356" y="216" width="118" height="86" rx="16" fill="$deep" opacity="0.35"/>
<rect x="138" y="362" width="18" height="44" rx="6" fill="$deep"/>
<rect x="444" y="362" width="18" height="44" rx="6" fill="$deep"/>
<rect x="122" y="356" width="356" height="18" rx="9" fill="$deep"/>
"@
        }
        "kitchen" {
            @"
<rect x="150" y="236" width="300" height="30" rx="15" fill="#ffffff"/>
<path d="M172 266 H428 V344 A64 64 0 0 1 364 408 H236 A64 64 0 0 1 172 344 Z" fill="#ffffff"/>
<rect x="140" y="300" width="46" height="24" rx="12" fill="$deep"/>
<rect x="414" y="300" width="46" height="24" rx="12" fill="$deep"/>
<circle cx="300" cy="216" r="22" fill="$deep"/>
<rect x="286" y="196" width="28" height="24" fill="#ffffff"/>
<rect x="232" y="300" width="136" height="14" rx="7" fill="$deep" opacity="0.4"/>
<rect x="256" y="336" width="88" height="14" rx="7" fill="$deep" opacity="0.25"/>
"@
        }
        "mens-fashion" {
            @"
<path d="M218 156 L266 130 A46 46 0 0 0 334 130 L382 156 L452 214 L404 276 L366 246 V418 A24 24 0 0 1 342 442 H258 A24 24 0 0 1 234 418 V246 L196 276 L148 214 Z" fill="#ffffff"/>
<path d="M266 130 A46 46 0 0 0 334 130 L320 168 A26 26 0 0 1 280 168 Z" fill="$deep"/>
<rect x="234" y="300" width="132" height="12" rx="6" fill="$deep" opacity="0.35"/>
<rect x="248" y="332" width="104" height="12" rx="6" fill="$deep" opacity="0.25"/>
"@
        }
        "womens-fashion" {
            @"
<path d="M244 140 H356 L374 186 L342 214 H258 L226 186 Z" fill="#ffffff"/>
<path d="M258 214 H342 L412 430 A16 16 0 0 1 397 452 H203 A16 16 0 0 1 188 430 Z" fill="#ffffff"/>
<path d="M272 240 H328 L392 424 H208 Z" fill="$deep" opacity="0.30"/>
<rect x="244" y="196" width="112" height="16" rx="8" fill="$deep"/>
<circle cx="300" cy="176" r="14" fill="$deep"/>
"@
        }
        "footwear" {
            @"
<path d="M116 344 C130 272 176 250 234 244 L300 236 L362 286 C412 300 464 314 484 334 C494 344 494 362 482 368 H126 C116 364 112 354 116 344 Z" fill="#ffffff"/>
<path d="M116 368 H484 V390 A14 14 0 0 1 470 404 H130 A14 14 0 0 1 116 390 Z" fill="$deep"/>
<path d="M234 244 L262 300 M262 250 L288 306 M292 254 L316 310" stroke="$deep" stroke-width="10" stroke-linecap="round" opacity="0.5"/>
<circle cx="410" cy="330" r="16" fill="$deep" opacity="0.45"/>
"@
        }
        "beauty" {
            @"
<rect x="234" y="188" width="132" height="248" rx="34" fill="#ffffff"/>
<rect x="274" y="146" width="52" height="52" fill="#ffffff"/>
<rect x="262" y="112" width="76" height="44" rx="12" fill="$deep"/>
<rect x="250" y="252" width="100" height="98" rx="12" fill="$deep" opacity="0.75"/>
<rect x="266" y="276" width="68" height="12" rx="6" fill="#ffffff" opacity="0.85"/>
<rect x="278" y="302" width="44" height="12" rx="6" fill="#ffffff" opacity="0.6"/>
<circle cx="300" cy="392" r="20" fill="$deep" opacity="0.45"/>
"@
        }
        "sports" {
            @"
<rect x="168" y="284" width="264" height="32" rx="16" fill="#ffffff"/>
<rect x="126" y="222" width="58" height="156" rx="18" fill="$deep"/>
<rect x="196" y="246" width="44" height="108" rx="14" fill="#ffffff"/>
<rect x="416" y="222" width="58" height="156" rx="18" fill="$deep"/>
<rect x="360" y="246" width="44" height="108" rx="14" fill="#ffffff"/>
<rect x="240" y="292" width="120" height="16" rx="8" fill="$deep" opacity="0.55"/>
"@
        }
        "toys" {
            @"
<rect x="176" y="316" width="120" height="106" rx="14" fill="#ffffff"/>
<rect x="304" y="316" width="120" height="106" rx="14" fill="$deep"/>
<rect x="240" y="204" width="120" height="106" rx="14" fill="$deep" opacity="0.75"/>
<circle cx="236" cy="368" r="24" fill="$deep" opacity="0.45"/>
<circle cx="364" cy="368" r="24" fill="#ffffff" opacity="0.75"/>
<circle cx="300" cy="256" r="24" fill="#ffffff" opacity="0.85"/>
<rect x="196" y="300" width="80" height="14" rx="7" fill="$deep" opacity="0.35"/>
"@
        }
        "books" {
            @"
<rect x="176" y="132" width="248" height="330" rx="16" fill="#ffffff"/>
<rect x="176" y="132" width="42" height="330" rx="16" fill="$deep"/>
<rect x="244" y="196" width="140" height="16" rx="8" fill="$deep" opacity="0.65"/>
<rect x="244" y="232" width="104" height="14" rx="7" fill="$deep" opacity="0.4"/>
<circle cx="314" cy="330" r="46" fill="$deep" opacity="0.3"/>
<rect x="244" y="404" width="140" height="14" rx="7" fill="$deep" opacity="0.4"/>
"@
        }
        default {
            @"
<circle cx="300" cy="290" r="120" fill="#ffffff"/>
"@
        }
    }
}

function New-Svg([string]$cat, [int]$base, [int]$variant, [string]$mode) {
    $h = ($base + ($variant * 11)) % 360
    $deep = "hsl($h,62%,42%)"
    $art = Get-Art $cat $deep

    if ($mode -eq "product") {
        $h2 = ($h + 22) % 360
        $c1 = "hsl($h,72%,86%)"
        $c2 = "hsl($h2,66%,72%)"
        $accent1 = "#ffffff"
        $accent2 = "#ffffff"
        $op1 = "0.14"
        $op2 = "0.10"
        $shadow = "#0f172a"
        $shadowOp = "0.22"
    } else {
        $c1 = "hsl($h,60%,96%)"
        $c2 = "hsl($h,55%,90%)"
        $accent1 = "hsl($h,70%,80%)"
        $accent2 = "hsl($h,65%,85%)"
        $op1 = "0.55"
        $op2 = "0.5"
        $shadow = "hsl($h,50%,35%)"
        $shadowOp = "0.18"
    }

    $svg = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 600" width="600" height="600" role="img">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="$c1"/>
      <stop offset="1" stop-color="$c2"/>
    </linearGradient>
    <filter id="sh" x="-40%" y="-40%" width="180%" height="180%">
      <feDropShadow dx="0" dy="16" stdDeviation="18" flood-color="$shadow" flood-opacity="$shadowOp"/>
    </filter>
  </defs>
  <rect width="600" height="600" fill="url(#bg)"/>
  <circle cx="472" cy="116" r="152" fill="$accent1" opacity="$op1"/>
  <circle cx="104" cy="506" r="118" fill="$accent2" opacity="$op2"/>
  <ellipse cx="300" cy="474" rx="176" ry="26" fill="$shadow" opacity="0.12"/>
  <g filter="url(#sh)">
$art
  </g>
</svg>
"@
    if ($mode -eq "product") {
        $file = Join-Path $outProducts ("{0}-v{1}.svg" -f $cat, $variant)
    } else {
        $file = Join-Path $outCategories "$cat.svg"
    }
    $svg | Set-Content -Path $file -Encoding UTF8
}

foreach ($cat in $hues.Keys) {
    $base = $hues[$cat]
    for ($v = 0; $v -lt 10; $v++) {
        New-Svg $cat $base $v "product"
    }
    New-Svg $cat $base 0 "category"
}

# favicon
$star = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">
  <rect width="64" height="64" rx="14" fill="#4f46e5"/>
  <path d="M32 12 L38.5 25.5 L53 27.5 L42.5 38 L45 52.5 L32 45.5 L19 52.5 L21.5 38 L11 27.5 L25.5 25.5 Z" fill="#fbbf24"/>
</svg>
"@
$star | Set-Content -Path (Join-Path $root "src\LegacyEcommerce\Content\favicon.svg") -Encoding UTF8

Write-Host "Generated SVG artwork."
