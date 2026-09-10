$ErrorActionPreference = 'Stop'

$outDir = Join-Path $PSScriptRoot '..\Assets\Art\Voxel'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Compact game-ready volume: X = width, Y = height, Z = depth.
$sizeX = 32
$sizeY = 74
$sizeZ = 30
$centerX = 16
$frontZ = 17

function Rgba([int]$r, [int]$g, [int]$b, [int]$a = 255) {
    return [System.BitConverter]::ToUInt32([byte[]]@($r, $g, $b, $a), 0)
}

$palette = @{
    Navy       = Rgba 37 43 73
    NavyLight  = Rgba 57 64 104
    NavyDark   = Rgba 22 25 47
    Brown      = Rgba 106 75 53
    BrownLight = Rgba 154 112 77
    Leather    = Rgba 81 54 38
    Gold       = Rgba 210 143 39
    GoldLight  = Rgba 255 196 79
    Steel      = Rgba 109 116 126
    SteelLight = Rgba 180 186 190
    SteelDark  = Rgba 51 57 70
    Wrap       = Rgba 177 138 101
    Glove      = Rgba 45 42 48
    Eye       = Rgba 219 155 255
}

$voxels = @{}

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
    $x0 = [math]::Floor($cx - $rx); $x1 = [math]::Ceiling($cx + $rx)
    $y0 = [math]::Floor($cy - $ry); $y1 = [math]::Ceiling($cy + $ry)
    $z0 = [math]::Floor($cz - $rz); $z1 = [math]::Ceiling($cz + $rz)
    for ($x = $x0; $x -le $x1; $x++) {
        for ($y = $y0; $y -le $y1; $y++) {
            for ($z = $z0; $z -le $z1; $z++) {
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

# Boots and wrapped lower legs.
Add-Box 8 13 0 3 8 15 'SteelDark'
Add-Box 19 24 0 3 8 15 'SteelDark'
Add-Box 9 13 4 7 9 14 'Leather'
Add-Box 19 23 4 7 9 14 'Leather'
Add-Box 9 13 8 11 9 14 'Wrap'
Add-Box 19 23 8 11 9 14 'Wrap'
Add-Box 10 12 12 13 9 14 'Leather'
Add-Box 20 22 12 13 9 14 'Leather'

# Baggy trousers, with darker cuffs and a strong split between the legs.
Add-Ellipsoid 11 20 11 6 8 5 'Navy'
Add-Ellipsoid 21 20 11 6 8 5 'Navy'
Add-Box 7 14 14 16 7 16 'NavyLight'
Add-Box 18 24 14 16 7 16 'NavyLight'
Add-Box 9 13 12 14 8 15 'NavyDark'
Add-Box 19 23 12 14 8 15 'NavyDark'

# Belt and side pouches.
Add-Box 8 24 26 29 8 16 'Leather'
Add-Box 9 23 27 29 15 17 'Brown'
Add-Box 4 8 25 31 12 17 'Brown'
Add-Box 5 7 31 32 13 17 'Leather'
Add-Box 24 28 25 31 12 17 'Brown'
Add-Box 25 27 31 32 13 17 'Leather'
Add-Box 14 18 27 29 17 18 'Gold'
Add-Box 15 17 28 29 17 18 'GoldLight'

# Main tunic and long front panel.
Add-Ellipsoid $centerX 40 12 7 12 5 'Navy'
Add-Box 12 20 29 49 14 17 'NavyLight'
Add-Box 13 19 30 49 17 18 'Navy'
# Extra rear volume so the character does not read as a flat cardboard cutout.
Add-Box 10 22 31 49 5 9 'NavyDark'
Add-Box 11 21 33 47 9 12 'Navy'
Add-Box 11 12 30 48 15 17 'Gold'
Add-Box 20 21 30 48 15 17 'Gold'
Add-Box 12 20 48 50 15 18 'Gold'
# Angular gold hem points.
Add-Box 11 13 46 49 16 18 'Gold'
Add-Box 19 21 46 49 16 18 'Gold'
Add-Box 13 14 49 53 16 18 'Gold'
Add-Box 18 19 49 53 16 18 'Gold'
Add-Box 15 17 52 55 16 18 'GoldLight'

# Arms: cloth, large metal shoulder pads, bracers and blocky gloves.
Add-Ellipsoid 7 43 12 3 8 4 'Wrap'
Add-Ellipsoid 25 43 12 3 8 4 'Wrap'
Add-Ellipsoid 6 49 12 4 5 4 'Steel'
Add-Ellipsoid 26 49 12 4 5 4 'Steel'
Add-Box 3 35 5 11 9 16 'SteelDark'
Add-Box 26 29 35 11 9 16 'SteelDark'
Add-Box 3 6 35 38 8 16 'Steel'
Add-Box 26 29 35 38 8 16 'Steel'
Add-Box 3 6 36 38 14 17 'SteelLight'
Add-Box 26 29 36 38 14 17 'SteelLight'
Add-Ellipsoid 5 31 9 3 4 3 'Glove'
Add-Ellipsoid 27 31 9 3 4 3 'Glove'
Add-Box 3 7 29 31 8 13 'Glove'
Add-Box 25 29 29 31 8 13 'Glove'

# Neck, wrapped scarf and hidden face.
Add-Box 12 20 49 53 9 16 'Brown'
Add-Box 11 21 51 56 8 16 'BrownLight'
Add-Box 12 20 53 57 10 16 'Brown'
Add-Ellipsoid $centerX 56 12 5 5 4 'Brown'
Add-Box 11 21 54 58 15 18 'NavyDark'
Add-Box 11 21 53 58 18 21 'NavyDark'
Add-Box 12 20 54 57 21 22 'NavyDark'

# Hat brim: wide, layered and visibly overhanging the face.
Add-Ellipsoid $centerX 59 12 13 2 6 'NavyDark'
Add-Box 4 27 59 60 9 15 'Navy'
Add-Box 7 24 61 61 10 14 'NavyLight'

# Stepped crooked cone and bent tip.
for ($y = 62; $y -le 69; $y++) {
    $t = ($y - 62) / 7.0
    $rx = [math]::Max(1, [math]::Floor(8 - (7 * $t)))
    $rz = [math]::Max(1, [math]::Floor(4 - (3 * $t)))
    $cx = [math]::Round($centerX - (1.5 * $t))
    Add-Box ($cx - $rx) ($cx + $rx) $y ( $y ) (12 - $rz) (12 + $rz) 'Navy'
    if (($y % 2) -eq 0) { Add-Box ($cx - $rx) ($cx + $rx) $y $y (12 + $rz) (12 + $rz) 'NavyLight' }
}
Add-Box 11 15 69 70 10 13 'Navy'
Add-Box 10 14 70 71 9 12 'NavyLight'
Add-Box 9 12 71 72 8 11 'Navy'
Add-Box 8 10 72 73 8 10 'NavyDark'

# Iconic glowing eyes under the brim.
Add-Box 13 14 55 56 21 22 'Eye'
Add-Box 18 19 55 56 21 22 'Eye'
Add-Box 13 14 55 55 20 20 'Eye'
Add-Box 18 19 55 55 20 20 'Eye'

# Gold triangular emblems on hat and chest.
Add-Triangle $centerX 63 5 3 19 'Gold'
Add-Box 15 16 65 66 20 20 'GoldLight'
Add-Triangle $centerX 42 5 3 19 'Gold'
Add-Box 15 16 44 45 20 20 'GoldLight'

$outPath = Join-Path $outDir 'ArcanistVoxel_v2.qb'
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
    $name = [System.Text.Encoding]::ASCII.GetBytes('ArcanistVoxel')
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
    asset = 'ArcanistVoxel_v2.qb'
    dimensions = @($sizeX, $sizeY, $sizeZ)
    coordinateSystem = 'X width, Y height, Z depth; front is positive Z'
    filledVoxels = $voxels.Count
    palette = [ordered]@{
        Navy = '#252B49'; NavyLight = '#394068'; NavyDark = '#16192F'; Brown = '#6A4B35'
        Gold = '#D28F27'; Steel = '#6D747E'; Wrap = '#B18A65'; Glove = '#2D2A30'; Eye = '#DB9BFF'
    }
    notes = 'Single editable matrix for Qubicle import; chunky silhouette optimized for game-scale readability.'
}
$metadata | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 (Join-Path $outDir 'ArcanistVoxel_v2.json')
Write-Output "Created $outPath ($($voxels.Count) filled voxels)"
