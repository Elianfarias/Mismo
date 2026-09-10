$ErrorActionPreference = 'Stop'

$outDir = Join-Path $PSScriptRoot '..\Assets\Art\Voxel'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sizeX = 32
$sizeY = 60
$sizeZ = 24
$voxels = @{}

function Rgba([int]$r, [int]$g, [int]$b, [int]$a = 255) {
    return [System.BitConverter]::ToUInt32([byte[]]@($r, $g, $b, $a), 0)
}

$palette = @{
    Navy       = Rgba 32 39 70
    NavyLight  = Rgba 52 63 106
    NavyDark   = Rgba 16 20 39
    Brown      = Rgba 92 62 43
    BrownLight = Rgba 153 108 72
    Gold       = Rgba 207 139 31
    GoldLight  = Rgba 255 194 64
    Steel      = Rgba 103 111 124
    SteelLight = Rgba 171 180 188
    SteelDark  = Rgba 45 51 64
    Wrap       = Rgba 169 130 91
    Glove      = Rgba 39 38 45
    Boot       = Rgba 61 49 44
    BootLight  = Rgba 98 70 52
    Eye        = Rgba 221 158 255
}

function Set-Voxel([int]$x, [int]$y, [int]$z, [string]$color) {
    if ($x -lt 0 -or $x -ge $sizeX -or $y -lt 0 -or $y -ge $sizeY -or $z -lt 0 -or $z -ge $sizeZ) { return }
    $voxels["$x,$y,$z"] = $palette[$color]
}

function Add-Box([int]$x0, [int]$x1, [int]$y0, [int]$y1, [int]$z0, [int]$z1, [string]$color) {
    for ($x = $x0; $x -le $x1; $x++) {
        for ($y = $y0; $y -le $y1; $y++) {
            for ($z = $z0; $z -le $z1; $z++) { Set-Voxel $x $y $z $color }
        }
    }
}

function Add-Ellipsoid([double]$cx, [double]$cy, [double]$cz, [double]$rx, [double]$ry, [double]$rz, [string]$color) {
    for ($x = [math]::Floor($cx - $rx); $x -le [math]::Ceiling($cx + $rx); $x++) {
        for ($y = [math]::Floor($cy - $ry); $y -le [math]::Ceiling($cy + $ry); $y++) {
            for ($z = [math]::Floor($cz - $rz); $z -le [math]::Ceiling($cz + $rz); $z++) {
                $dx = ($x - $cx) / $rx; $dy = ($y - $cy) / $ry; $dz = ($z - $cz) / $rz
                if (($dx * $dx) + ($dy * $dy) + ($dz * $dz) -le 1.0) { Set-Voxel $x $y $z $color }
            }
        }
    }
}

function Add-Triangle([int]$cx, [int]$baseY, [int]$height, [int]$halfBase, [int]$z, [string]$color) {
    for ($row = 0; $row -lt $height; $row++) {
        $ratio = $row / [math]::Max(1, ($height - 1))
        $half = [math]::Floor($halfBase * (1.0 - $ratio))
        for ($x = ($cx - $half); $x -le ($cx + $half); $x++) { Set-Voxel $x ($baseY + $row) $z $color }
    }
}

# Same body proportions as BaseHumanoidVoxel, now dressed as a mage.
Add-Box 8 14 0 5 4 13 'Boot'
Add-Box 18 24 0 5 4 13 'Boot'
Add-Box 9 14 1 4 13 16 'BootLight'
Add-Box 18 23 1 4 13 16 'BootLight'
Add-Box 10 13 5 7 6 12 'Wrap'
Add-Box 19 22 5 7 6 12 'Wrap'

# Baggy trousers: two deliberately wide readable volumes.
Add-Ellipsoid 11 18 10 6 8 5 'Navy'
Add-Ellipsoid 21 18 10 6 8 5 'Navy'
Add-Box 8 14 14 17 6 15 'NavyLight'
Add-Box 18 24 14 17 6 15 'NavyLight'
Add-Box 10 14 17 20 6 14 'NavyDark'
Add-Box 18 22 17 20 6 14 'NavyDark'

# Belt, buckle and two pouches; keep the belt thin so it does not become a slab.
Add-Box 9 23 20 22 7 15 'Brown'
Add-Box 10 22 21 22 14 17 'BrownLight'
Add-Box 4 8 19 25 12 17 'Brown'
Add-Box 5 7 24 26 13 17 'BrownLight'
Add-Box 24 28 19 25 12 17 'Brown'
Add-Box 25 27 24 26 13 17 'BrownLight'
Add-Box 15 17 21 22 17 18 'Gold'

# Deep torso volume and long blue tunic front.
Add-Ellipsoid 16 29 11 8 9 6 'Navy'
Add-Box 10 22 23 35 6 13 'NavyDark'
Add-Box 12 20 22 42 14 18 'NavyLight'
Add-Box 13 19 23 42 18 19 'Navy'
# One-voxel gold edging with angular hem points.
Add-Box 11 12 23 40 17 19 'Gold'
Add-Box 20 21 23 40 17 19 'Gold'
Add-Box 12 13 40 43 17 19 'Gold'
Add-Box 19 20 40 43 17 19 'Gold'
Add-Box 14 15 42 45 17 19 'Gold'
Add-Box 17 18 42 45 17 19 'Gold'
Add-Box 15 17 44 46 17 19 'GoldLight'

# Cloth sleeves, oversized metal shoulder pads, bracers and gloves.
Add-Box 6 10 24 32 6 14 'Wrap'
Add-Box 22 26 24 32 6 14 'Wrap'
Add-Ellipsoid 8 32 10 4 4 5 'Steel'
Add-Ellipsoid 24 32 10 4 4 5 'Steel'
Add-Box 6 9 30 33 12 16 'SteelLight'
Add-Box 23 26 30 33 12 16 'SteelLight'
Add-Box 5 9 16 23 6 14 'Wrap'
Add-Box 23 27 16 23 6 14 'Wrap'
Add-Box 4 9 12 16 5 14 'SteelDark'
Add-Box 23 28 12 16 5 14 'SteelDark'
Add-Box 4 8 12 14 14 16 'Glove'
Add-Box 24 28 12 14 14 16 'Glove'

# Scarf, fully covered face and glowing eyes.
Add-Box 13 19 33 35 7 14 'Brown'
Add-Ellipsoid 16 36 11 6 4 5 'BrownLight'
Add-Box 11 21 35 42 6 15 'NavyDark'
Add-Box 12 20 36 41 15 20 'NavyDark'
Add-Box 13 14 38 39 20 21 'Eye'
Add-Box 18 19 38 39 20 21 'Eye'

# Wide hat brim and stepped crooked cone.
Add-Ellipsoid 16 43 12 14 2 7 'NavyDark'
Add-Box 4 27 43 44 8 16 'Navy'
Add-Box 7 24 45 45 9 15 'NavyLight'
for ($y = 46; $y -le 54; $y++) {
    $t = ($y - 46) / 8.0
    $rx = [math]::Max(1, [math]::Floor(8 - (7 * $t)))
    $rz = [math]::Max(1, [math]::Floor(5 - (4 * $t)))
    $cx = [math]::Round(16 - (1.5 * $t))
    Add-Box ($cx - $rx) ($cx + $rx) $y $y (12 - $rz) (12 + $rz) 'Navy'
}
Add-Box 11 15 54 55 10 14 'NavyLight'
Add-Box 10 14 55 56 9 13 'Navy'
Add-Box 9 12 56 57 8 11 'NavyDark'
Add-Box 8 10 57 58 8 10 'NavyDark'

# Large readable gold triangles on hat and chest.
Add-Triangle 16 47 5 3 19 'Gold'
Add-Box 15 16 49 50 20 21 'GoldLight'
Add-Triangle 16 29 5 3 20 'Gold'
Add-Box 15 16 31 32 21 21 'GoldLight'

$outPath = Join-Path $outDir 'ArcanistMageVoxel.qb'
$stream = [System.IO.File]::Open($outPath, [System.IO.FileMode]::Create)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    # Qubicle Binary 257, RGBA, Y-up, uncompressed, one matrix.
    $writer.Write([uint32]257)
    $writer.Write([uint32]0)
    $writer.Write([uint32]0)
    $writer.Write([uint32]0)
    $writer.Write([uint32]0)
    $writer.Write([uint32]1)
    $name = [System.Text.Encoding]::ASCII.GetBytes('ArcanistMageVoxel')
    $writer.Write([byte]$name.Length)
    $writer.Write($name)
    $writer.Write([uint32]$sizeX)
    $writer.Write([uint32]$sizeY)
    $writer.Write([uint32]$sizeZ)
    $writer.Write([int32]0)
    $writer.Write([int32]0)
    $writer.Write([int32]0)
    for ($z = 0; $z -lt $sizeZ; $z++) {
        for ($y = 0; $y -lt $sizeY; $y++) {
            for ($x = 0; $x -lt $sizeX; $x++) {
                $key = "$x,$y,$z"
                if ($voxels.ContainsKey($key)) { $writer.Write([uint32]$voxels[$key]) }
                else { $writer.Write([uint32]0) }
            }
        }
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

$metadata = [ordered]@{
    asset = 'ArcanistMageVoxel.qb'
    dimensions = @($sizeX, $sizeY, $sizeZ)
    filledVoxels = $voxels.Count
    coordinateSystem = 'X width, Y height, Z depth; front is positive Z'
    basedOn = 'BaseHumanoidVoxel proportions'
    palette = [ordered]@{
        Navy = '#202746'; NavyLight = '#343F6A'; NavyDark = '#101427'; Brown = '#5C3E2B'
        Gold = '#CF8B1F'; Steel = '#676F7C'; Wrap = '#A9825B'; Glove = '#27262D'; Eye = '#DD9EFF'
    }
}
$metadata | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 (Join-Path $outDir 'ArcanistMageVoxel.json')
Write-Output "Created $outPath ($($voxels.Count) filled voxels)"
