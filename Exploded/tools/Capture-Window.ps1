# Captures a window belonging to a running process.
#
# PrintWindow with PW_RENDERFULLCONTENT is occlusion-proof, but on a Skia GL
# desktop window it can return TRUE and still hand back a blank bitmap. A success
# return is not evidence of pixels, so the result is sampled and falls back to a
# foreground CopyFromScreen when it comes back effectively uniform.
#
# System.Drawing types are kept out of the Add-Type string on purpose: they sit
# behind type-forwards that Add-Type cannot resolve from source under .NET 10.

param(
    [Parameter(Mandatory = $true)][string]$ProcessName,
    [Parameter(Mandatory = $true)][string]$OutPath
)

Add-Type -AssemblyName System.Drawing

if (-not ('Win32Capture' -as [type])) {
    Add-Type -Name Win32Capture -Namespace Native -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool PrintWindow(System.IntPtr hwnd, System.IntPtr hdc, uint flags);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool GetClientRect(System.IntPtr hwnd, out RECT rect);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool GetWindowRect(System.IntPtr hwnd, out RECT rect);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool SetForegroundWindow(System.IntPtr hwnd);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool ShowWindow(System.IntPtr hwnd, int cmd);

public struct RECT { public int Left, Top, Right, Bottom; }
'@
}

$process = Get-Process -Name $ProcessName -ErrorAction Stop | Select-Object -First 1
$hwnd = $process.MainWindowHandle

if ($hwnd -eq [System.IntPtr]::Zero) {
    throw "Process $ProcessName has no main window handle. On a dev-channel Uno.Sdk a Debug build launched without a devserver keeps its window hidden; use a Release build."
}

$rect = New-Object Native.Win32Capture+RECT
[void][Native.Win32Capture]::GetWindowRect($hwnd, [ref]$rect)
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top

$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
$ok = [Native.Win32Capture]::PrintWindow($hwnd, $hdc, 2)   # PW_RENDERFULLCONTENT
$graphics.ReleaseHdc($hdc)
$graphics.Dispose()

# Sample the result rather than trusting the return value.
$distinct = New-Object 'System.Collections.Generic.HashSet[int]'
for ($x = 4; $x -lt $width - 4; $x += [Math]::Max(1, [int]($width / 40))) {
    for ($y = 4; $y -lt $height - 4; $y += [Math]::Max(1, [int]($height / 40))) {
        [void]$distinct.Add($bitmap.GetPixel($x, $y).ToArgb())
    }
}

if ($distinct.Count -lt 6) {
    Write-Host "PrintWindow returned $ok but the bitmap is effectively uniform ($($distinct.Count) distinct samples); falling back to screen copy."
    $bitmap.Dispose()

    [void][Native.Win32Capture]::ShowWindow($hwnd, 9)      # SW_RESTORE
    [void][Native.Win32Capture]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 600

    [void][Native.Win32Capture]::GetWindowRect($hwnd, [ref]$rect)
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top

    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($width, $height)))
    $graphics.Dispose()
}

$bitmap.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
Write-Host "Captured ${width}x${height} to $OutPath"
