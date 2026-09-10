$ErrorActionPreference = 'Stop'

$outDir = Join-Path $PSScriptRoot '..\Assets\Art\Voxel'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sizeX = 32
$sizeY = 48
$sizeZ = 20
$voxels = @{}

function Rgba([int]$r, [int]$g, [int]$b, [int]$a = 255) {
    return [System.BitConverter]::ToUInt32([byte[]]@($r, $g, $b, $a), 0)
}

$palette = @{
    Skin       = Rgba 190 145 105
    SkinLight  = Rgba 224 178 132
    Shirt      = Rgba 61 91 132
    ShirtLight = Rgba 86 121 163
    Pants      = Rgba 39 48 72
    PantsLight = Rgba 57 68 96
    Glove      = Rgba 45 48 58
    Boot       = Rgba 78 57 45
    BootLight  = Rgba 111 79 56
    Joint      = Rgba 105 111 122
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

# Feet and boots: separated, wide enough to read clearly from front and side.
Add-Box 8 14 0 5 4 13 'Boot'
Add-Box 18 24 0 5 4 13 'Boot'
Add-Box 9 14 1 4 13 16 'BootLight'
Add-Box 18 23 1 4 13 16 'BootLight'
Add-Box 10 13 5 7 6 12 'Joint'
Add-Box 19 22 5 7 6 12 'Joint'

# Two legs and a compact pelvis.
Add-Box 10 14 8 18 6 13 'Pants'
Add-Box 18 22 8 18 6 13 'Pants'
Add-Box 11 14 9 15 13 15 'PantsLight'
Add-Box 18 21 9 15 13 15 'PantsLight'
Add-Box 10 22 17 22 6 14 'Pants'
Add-Box 12 20 18 21 14 16 'PantsLight'

# Torso with real depth and a readable shirt front.
Add-Ellipsoid 16 28 10 7 8 5 'Shirt'
Add-Box 11 21 23 33 14 16 'ShirtLight'
Add-Box 12 20 24 32 16 17 'Shirt'
Add-Box 15 17 25 27 17 18 'SkinLight'

# Arms with clear joints, forearms and hands.
Add-Box 6 10 24 31 6 14 'Shirt'
Add-Box 22 26 24 31 6 14 'Shirt'
Add-Ellipsoid 8 22 10 3 3 4 'Joint'
Add-Ellipsoid 24 22 10 3 3 4 'Joint'
Add-Box 5 9 16 22 6 14 'Skin'
Add-Box 23 27 16 22 6 14 'Skin'
Add-Box 5 9 17 20 13 15 'SkinLight'
Add-Box 23 27 17 20 13 15 'SkinLight'
Add-Box 4 9 12 16 5 14 'Glove'
Add-Box 23 28 12 16 5 14 'Glove'
Add-Box 4 7 13 14 14 16 'Glove'
Add-Box 25 28 13 14 14 16 'Glove'

# Neck and simple cubic head. The neutral head makes this a reusable base.
Add-Box 13 19 33 35 7 13 'Joint'
Add-Box 11 21 35 42 6 14 'Skin'
Add-Box 12 20 36 41 14 16 'SkinLight'
Add-Box 13 14 38 39 16 17 'SkinLight'
Add-Box 18 19 38 39 16 17 'SkinLight'

$outPath = Join-Path $outDir 'BaseHumanoidVoxel.qb'
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
    $name = [System.Text.Encoding]::ASCII.GetBytes('BaseHumanoidVoxel')
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
    asset = 'BaseHumanoidVoxel.qb'
    dimensions = @($sizeX, $sizeY, $sizeZ)
    filledVoxels = $voxels.Count
    coordinateSystem = 'X width, Y height, Z depth; front is positive Z'
    purpose = 'Neutral reusable humanoid base for later character variants'
    palette = [ordered]@{
        Skin = '#BE9169'; Shirt = '#3D5B84'; Pants = '#273048'; Glove = '#2D303A'; Boot = '#4E392D'; Joint = '#696F7A'
    }
}
$metadata | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 (Join-Path $outDir 'BaseHumanoidVoxel.json')
Write-Output "Created $outPath ($($voxels.Count) filled voxels)"
