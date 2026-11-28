#Requires -Version 7.2
<#
.SYNOPSIS
    Captures real screenshots of the running Salt.er app for the documentation.

.DESCRIPTION
    Starts the published app, drives its flyout with UI Automation and captures each screen with GDI. Run it on a
    Windows desktop session nobody uses at the same time (for example a GitHub-hosted Windows runner): it changes
    the wallpaper, the display resolution, the Windows animation effects and the app's settings, and stops any
    running Salt.er.

    It is also the app's UI crash test. Windows animation effects are turned on first, as on a typical desktop
    (hosted runners have them off, which hides crashes in animated controls), and every interactive control is used
    at least once per theme.

    For each theme (Dark, Light) it writes settings.json with that theme and the "people" tag excluded, starts the
    app and captures:
      genre-<theme>.png           the Nature genre, nothing hovered (plus hero.png in Dark: the flyout above the
                                  taskbar)
      feed-<theme>.png            optional: the same feed with the pointer on the first wallpaper, showing its Set
                                  button; skipped with a warning where the runner cannot show the hover state
      detail-<theme>.png          the first wallpaper's details
      applied-<theme>.png         the toast after setting it as the wallpaper
      search-filters-<theme>.png  a search for "mountains" with the filters panel open, 4K+ chosen and the
                                  "people" exclusion chip shown
      settings-<theme>.png        the settings screen
    Stray system dialogs are closed first, and focus rectangles and hover states are cleared before each capture
    except where the state shows them. It writes manifest.json listing the files and their captions.

    After the captures, each theme run exercises every interactive control: three genre pills, every sort tab,
    refresh, the filters toggle, ratio, resolution and colour choices, adding and removing an excluded tag, Ctrl+K
    and Escape, the details with their display choice (when there are several displays), the start with Windows
    switch, the theme switch (Light, then Dark) and back navigation.

    After every state and step the script checks that the app is still running and that its log since the start
    has no fatal ([FTL]) or "Unhandled" entry. If either check fails it saves a full-screen image under
    diagnostics/, prints the last 60 lines of the app log and the recent Application Error and .NET Runtime events
    of the Windows Application log, and exits with 1: a crash always fails the run.

    Any other state that fails is logged, a full-screen image of the moment is saved under diagnostics/, and the
    script moves on. An optional state that fails only logs a warning. Without a crash, it exits with 1 only if no
    screenshot was produced.

.PARAMETER AppPath
    Path to the published Salter.App.exe.

.PARAMETER OutputDir
    Folder for the screenshots. Defaults to docs/assets/screenshots in the repository.

.PARAMETER ScreenWidth
    Display width to switch to before capturing, if the display allows it.

.PARAMETER ScreenHeight
    Display height to switch to before capturing, if the display allows it.

.PARAMETER SettleSeconds
    Time given to thumbnails to download and decode once the list shows, before a capture.

.EXAMPLE
    pwsh tools/screenshots/Capture-Screenshots.ps1 -AppPath artifacts/screenshot-app/Salter.App.exe
#>
using namespace System.Windows.Automation

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $AppPath,

    [string] $OutputDir = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'docs/assets/screenshots'),

    [ValidateRange(800, 7680)]
    [int] $ScreenWidth = 1920,

    [ValidateRange(600, 4320)]
    [int] $ScreenHeight = 1080,

    [ValidateRange(0, 60)]
    [int] $SettleSeconds = 6
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw 'The app is a WinUI 3 application; screenshots can only be captured on Windows.'
}

# --- Names the script looks for (the app's English UI text, from Strings/en-US/Resources.resw) ---------------------

$ProcessName = 'Salter.App'
$WindowTitle = 'Salt.er'
$Ui = @{
    Settings        = 'Settings'           # Feed_Settings: settings button in the feed header
    Filters         = 'Filters'            # Feed_Filters: filters toggle in the feed header
    Refresh         = 'Refresh'            # Feed_Refresh: the floating refresh button
    Search          = 'Search wallpapers'  # Search_Name: the search box
    Back            = 'Back'               # Detail_Back: back button of the detail and settings screens
    SetWallpaper    = 'Set as wallpaper'   # Detail_Set: apply button of the detail screen (and the quick Set button)
    Genre           = 'Nature'             # Genre_nature: a genre pill
    Resolution      = '4K+'                # Filter_4K: a minimum resolution pill in the filters panel
    ExcludeBox      = 'Tag to exclude'     # Filter_ExcludeName: the exclusion box in the filters panel
    RemoveFormat    = 'Remove {0}'         # Filter_RemoveExclusion: the x of an exclusion chip
    AllDisplays     = 'All displays'       # Detail_AllDisplays: first segment of the display choice
    LaunchAtStartup = 'Start with Windows' # Settings_LaunchAtStartup: the switch in the settings
    ThemeLight      = 'Light'              # Settings_ThemeLight: theme segment
    ThemeDark       = 'Dark'               # Settings_ThemeDark: theme segment
    SettingsHead    = 'Settings'           # Settings_Title: heading of the settings screen
}
# Exercised once per theme: genre pills (Genre_*), sort tabs (Sort_*, a SelectorBar), filter values (ratio labels,
# Filter_Native, Color_*).
$ExerciseGenres = @('Anime', 'Space', 'All')
$ExerciseSortings = @('Latest', 'Hot', 'Random', 'Toplist')
$ExerciseRatios = @('21:9', '16:9')
$ExerciseResolutions = @('Native')
$ExerciseColors = @('Blue', 'Teal')
$ExerciseExclusion = 'cars'
# A crash shows in the app log as a fatal entry or an unhandled exception (App.xaml.cs).
$CrashLogPattern = '\[FTL\]|Unhandled'

$SearchQuery = 'mountains'
# Seeded into settings.json, so the filters panel shows an exclusion chip ("#people").
$ExcludedTag = 'people'
# Thumbnails are buttons named after the wallpaper's resolution, such as "3840x2160".
$ThumbnailPattern = '^\d{3,5}\s*[x\u00D7]\s*\d{3,5}$'
# Toast texts: Toast_Applied / Toast_AppliedAll on success, Toast_Failed on failure.
$ToastSuccessPattern = '^Applied to '
$ToastFailurePattern = "^Couldn't set the wallpaper"

$Captions = [ordered]@{
    'hero'           = 'Salt.er opens as a translucent Windows 11 flyout above the tray.'
    'feed'           = 'The feed (Nature genre, Toplist order) with the Set button shown on the wallpaper under the pointer.'
    'genre'          = 'Genre pills, sort tabs and the Nature genre selected.'
    'search-filters' = 'Searching for "mountains" (all genres) with the filters panel open, the 4K+ resolution chosen and the people tag excluded.'
    'detail'         = 'Wallpaper details: preview, tags and the Set as wallpaper button.'
    'applied'        = 'The confirmation toast after setting a wallpaper.'
    'settings'       = 'Settings: API key, content, download folder, start with Windows and theme.'
}

$CaptureMargin = 16
$script:Produced = [System.Collections.Generic.List[object]]::new()
$script:Failures = [System.Collections.Generic.List[string]]::new()
$script:WallpaperFile = $null
$script:WallpaperInfo = $null
$script:RunStarted = Get-Date
# The app process of the current theme run, the log it writes and how long that log was when it started.
$script:AppProcess = $null
$script:LogBaseline = $null
# Set once a crash is detected: the run then stops and fails.
$script:Crash = $null

# --- Logging -------------------------------------------------------------------------------------------------------

function Write-Step([string] $Message) {
    Write-Host ('[{0:HH:mm:ss}] {1}' -f (Get-Date), $Message)
}

function Write-Problem([string] $Message) {
    # A GitHub Actions annotation, so the problem shows on the run summary as well.
    Write-Host "::warning::$Message"
}

# --- Setup ---------------------------------------------------------------------------------------------------------

function Initialize-Libraries {
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
    Add-Type -Path (Join-Path $PSScriptRoot 'ScreenshotNative.cs')
    # Before any coordinates are read: UI Automation, the cursor and the screen copy then all use physical pixels.
    $aware = [ScreenshotNative]::MakeDpiAware()
    Write-Step "DPI awareness set: $aware (false means it was already set)"
}

function Set-ScreenResolution {
    $before = '{0}x{1}' -f [ScreenshotNative]::ScreenWidth, [ScreenshotNative]::ScreenHeight
    if ($before -eq "${ScreenWidth}x${ScreenHeight}") {
        Write-Step "Screen is already $before"
        return
    }

    try {
        $code = [ScreenshotNative]::SetResolution($ScreenWidth, $ScreenHeight)
        Write-Step "ChangeDisplaySettings(${ScreenWidth}x${ScreenHeight}) returned $code (0 = success)"
        if ($code -ne 0 -and (Get-Command -Name Set-DisplayResolution -ErrorAction SilentlyContinue)) {
            Set-DisplayResolution -Width $ScreenWidth -Height $ScreenHeight -Force
        }
    }
    catch {
        Write-Problem "Could not change the screen resolution: $($_.Exception.Message)"
    }

    Start-Sleep -Seconds 2
    Write-Step ('Screen: {0} -> {1}x{2}' -f $before, [ScreenshotNative]::ScreenWidth, [ScreenshotNative]::ScreenHeight)
}

function Set-DesktopAppearance {
    # Transparency on, so the flyout's acrylic shows the wallpaper; dark taskbar for the hero image. Best effort.
    try {
        $personalize = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
        if (-not (Test-Path -LiteralPath $personalize)) {
            New-Item -Path $personalize -Force | Out-Null
        }

        Set-ItemProperty -LiteralPath $personalize -Name EnableTransparency -Value 1 -Type DWord
        Set-ItemProperty -LiteralPath $personalize -Name SystemUsesLightTheme -Value 0 -Type DWord
        # Fill the screen with the wallpaper, whatever its size.
        Set-ItemProperty -LiteralPath 'HKCU:\Control Panel\Desktop' -Name WallpaperStyle -Value '10'
        Set-ItemProperty -LiteralPath 'HKCU:\Control Panel\Desktop' -Name TileWallpaper -Value '0'
        # No Windows edition and build text on the desktop; it takes effect when the wallpaper is set.
        Set-ItemProperty -LiteralPath 'HKCU:\Control Panel\Desktop' -Name PaintDesktopVersion -Value 0 -Type DWord
        [ScreenshotNative]::BroadcastSettingChange('ImmersiveColorSet')
        Write-Step 'Desktop: transparency on, dark taskbar, wallpaper style fill, no version text'
    }
    catch {
        Write-Problem "Could not change the desktop appearance: $($_.Exception.Message)"
    }

    try {
        # Nothing else on the desktop: the hero image shows the flyout over the wallpaper.
        (New-Object -ComObject Shell.Application).MinimizeAll()
        Write-Step 'Minimized all windows'
    }
    catch {
        Write-Problem "Could not minimize windows: $($_.Exception.Message)"
    }
}

function Enable-AnimationEffects {
    # Hosted runners turn animation effects off, which skips every implicit transition and so hides the crashes
    # they can cause. A crash here fails the run, so this is not best effort.
    [ScreenshotNative]::SetAnimationEffects($true)
    $clientArea = [ScreenshotNative]::ClientAreaAnimationsEnabled
    $uiEffects = [ScreenshotNative]::UiEffectsEnabled
    Write-Step "Animation effects: client area animations $clientArea, UI effects $uiEffects"
    if (-not $clientArea) {
        throw 'Could not turn on client area animations (SPI_SETCLIENTAREAANIMATION).'
    }
}

function Get-DesktopWallpaper {
    # One SFW general wallpaper from wallhaven's toplist; the default wallpaper stays if anything fails.
    $search = 'https://wallhaven.cc/api/v1/search?categories=100&purity=100&sorting=toplist&topRange=1M' +
        '&atleast=1920x1080&ratios=16x9'
    try {
        Write-Step "Fetching a wallpaper: $search"
        $response = Invoke-RestMethod -Uri $search -TimeoutSec 30
        $first = @($response.data)[0]
        $extension = [System.IO.Path]::GetExtension(([uri] $first.path).AbsolutePath)
        $file = Join-Path (Get-TempRoot) "desktop-wallpaper$extension"
        Invoke-WebRequest -Uri $first.path -OutFile $file -TimeoutSec 120
        $script:WallpaperFile = $file
        $script:WallpaperInfo = [ordered]@{ id = $first.id; url = $first.url; resolution = $first.resolution }
        Write-Step "Downloaded wallpaper $($first.id) ($($first.resolution)) to $file"
    }
    catch {
        Write-Problem "Could not download a wallpaper, keeping the current one: $($_.Exception.Message)"
    }
}

function Set-DesktopWallpaper {
    if ($null -eq $script:WallpaperFile) {
        return
    }

    try {
        [ScreenshotNative]::SetWallpaper($script:WallpaperFile)
        Write-Step "Wallpaper set: $($script:WallpaperFile)"
    }
    catch {
        Write-Problem "Could not set the wallpaper: $($_.Exception.Message)"
    }
}

function Get-TempRoot {
    $root = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [System.IO.Path]::GetTempPath() }
    $folder = Join-Path $root 'salter-screenshots'
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    return $folder
}

# --- App lifecycle -------------------------------------------------------------------------------------------------

function Get-AppDataFolder {
    return Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Salt.er'
}

function Write-AppSettings([string] $Theme) {
    $folder = Get-AppDataFolder
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $downloads = Join-Path (Get-TempRoot) 'downloads'
    # The shape JsonSettingsStore reads: camel case, enums as names.
    $settings = [ordered]@{
        downloadFolder  = $downloads
        launchAtStartup = $false
        theme           = $Theme
        purity          = 'Sfw'
        excludedTags    = @($ExcludedTag)
    }
    $path = Join-Path $folder 'settings.json'
    [System.IO.File]::WriteAllText($path, ($settings | ConvertTo-Json))
    Write-Step "Wrote $path (theme $Theme, excluding '$ExcludedTag', downloads to $downloads)"
}

function Stop-App {
    $running = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue)
    if ($running.Count -eq 0) {
        $script:AppProcess = $null
        return
    }

    Write-Step "Stopping $($running.Count) running $ProcessName process(es)"
    # Stopped on purpose: not a crash.
    $script:AppProcess = $null
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 20 -ErrorAction SilentlyContinue
}

function Start-App([switch] $Primary) {
    Write-Step "Starting $AppPath"
    if ($Primary) {
        # The log so far belongs to earlier runs; only what this process writes is checked for crashes.
        $log = Get-LatestLog
        $script:LogBaseline = if ($null -ne $log) { [pscustomobject]@{ Path = $log.FullName; Length = $log.Length } }
    }

    # Without --background the app opens its flyout; a second start while it runs opens the running flyout again
    # and exits.
    $process = Start-Process -FilePath $AppPath -WorkingDirectory (Split-Path -Parent $AppPath) -PassThru
    if ($Primary) {
        # Holding the handle keeps the exit code readable after the process ends.
        $null = $process.Handle
        $script:AppProcess = $process
        Write-Step "  app process $($process.Id)"
    }
}

function Get-LatestLog {
    $logs = Join-Path (Get-AppDataFolder) 'logs'
    return Get-ChildItem -LiteralPath $logs -Filter '*.log' -File -ErrorAction SilentlyContinue |
        Sort-Object -Property LastWriteTime | Select-Object -Last 1
}

function Read-SharedFile([string] $Path) {
    # The app keeps the file open; share it for reading.
    $stream = [System.IO.FileStream]::new(
        $Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        return [System.IO.StreamReader]::new($stream).ReadToEnd()
    }
    finally {
        $stream.Dispose()
    }
}

function Read-NewAppLog {
    # What the app logged since this theme run started it: the rest of the file it started with, or all of a newer
    # one (the log rolls over daily).
    $latest = Get-LatestLog
    if ($null -eq $latest) {
        return ''
    }

    $text = Read-SharedFile $latest.FullName
    if ($null -ne $script:LogBaseline -and $script:LogBaseline.Path -eq $latest.FullName) {
        # The length was taken in bytes; the log is UTF-8, so skip as many bytes, not characters.
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
        $skip = [Math]::Min([int64]$bytes.Length, [int64]$script:LogBaseline.Length)
        return [System.Text.Encoding]::UTF8.GetString($bytes, [int]$skip, [int]($bytes.Length - $skip))
    }

    return $text
}

function Write-AppLog {
    $latest = Get-LatestLog
    if ($null -eq $latest) {
        Write-Step "No app log in $(Join-Path (Get-AppDataFolder) 'logs')"
        return
    }

    Write-Step "Last lines of the app log $($latest.FullName):"
    (Read-SharedFile $latest.FullName) -split "`r?`n" | Select-Object -Last 60 | ForEach-Object { Write-Host "    $_" }
}

function Write-CrashEvents {
    # Native crashes (for example 0xc0000005 in Microsoft.ui.xaml.dll) leave an Application Error event; unhandled
    # managed exceptions a .NET Runtime one.
    try {
        $filter = @{
            LogName      = 'Application'
            ProviderName = 'Application Error', '.NET Runtime'
            StartTime    = $script:RunStarted
        }
        $events = @(Get-WinEvent -FilterHashtable $filter -MaxEvents 10 -ErrorAction Stop)
    }
    catch {
        Write-Step "No Application Error or .NET Runtime events since the run started ($($_.Exception.Message))"
        return
    }

    Write-Step "Application Error and .NET Runtime events since the run started ($($events.Count)):"
    foreach ($entry in $events) {
        Write-Host ('    {0:HH:mm:ss} {1} (id {2}):' -f $entry.TimeCreated, $entry.ProviderName, $entry.Id)
        $entry.Message -split "`r?`n" | Select-Object -First 25 | ForEach-Object { Write-Host "      $_" }
    }
}

function Get-CrashReason {
    $reasons = [System.Collections.Generic.List[string]]::new()
    $process = $script:AppProcess
    if ($null -ne $process) {
        $process.Refresh()
        if ($process.HasExited) {
            $code = try { '0x{0:X8}' -f $process.ExitCode } catch { 'unknown' }
            $reasons.Add("the app process $($process.Id) exited (exit code $code)")
        }
    }

    $log = Read-NewAppLog
    $hits = @([regex]::Matches($log, "^.*(?:$CrashLogPattern).*$", 'Multiline') | ForEach-Object { $_.Value.Trim() })
    if ($hits.Count -gt 0) {
        $reasons.Add("the app log has $($hits.Count) fatal or unhandled line(s), the first: $($hits[0])")
    }

    return $reasons
}

function Assert-AppAlive([string] $Context) {
    if ($null -ne $script:Crash) {
        throw "The app crashed earlier: $($script:Crash)"
    }

    $reasons = @(Get-CrashReason)
    if ($reasons.Count -eq 0) {
        return
    }

    $script:Crash = "$Context`: $($reasons -join '; ')"
    Write-Host "::error::The app crashed $($script:Crash)"
    Save-Diagnostics "crash"
    Write-AppLog
    Write-CrashEvents
    throw "The app crashed $($script:Crash)"
}

# --- UI Automation -------------------------------------------------------------------------------------------------

function Get-FlyoutWindow {
    # The flyout is the app's top-level window titled "Salt.er"; a hidden flyout is not in the tree.
    foreach ($process in @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue)) {
        $condition = [AndCondition]::new(
            [PropertyCondition]::new([AutomationElement]::ProcessIdProperty, $process.Id),
            [PropertyCondition]::new([AutomationElement]::ControlTypeProperty, [ControlType]::Window))
        foreach ($window in [AutomationElement]::RootElement.FindAll([TreeScope]::Children, $condition)) {
            if ($window.Current.Name -eq $WindowTitle -and -not $window.Current.IsOffscreen) {
                return $window
            }
        }
    }

    return $null
}

function Wait-Until {
    param(
        [Parameter(Mandatory)] [string] $Description,
        [Parameter(Mandatory)] [scriptblock] $Condition,
        [int] $TimeoutSeconds = 20,
        [int] $IntervalMilliseconds = 250
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastError = $null
    while ($true) {
        try {
            $result = & $Condition
            if ($result) {
                return $result
            }
        }
        catch [ElementNotAvailableException], [System.Runtime.InteropServices.COMException] {
            # The tree changed under the query; try again.
            $lastError = $_.Exception.Message
        }

        if ([DateTime]::UtcNow -ge $deadline) {
            $suffix = if ($lastError) { " (last error: $lastError)" } else { '' }
            throw "Timed out after $TimeoutSeconds s waiting for $Description$suffix."
        }

        Start-Sleep -Milliseconds $IntervalMilliseconds
    }
}

function Get-Flyout {
    # The flyout hides when it loses focus. If it is gone, starting the app again shows the running instance's
    # flyout on the screen it was on.
    $window = Get-FlyoutWindow
    if ($null -ne $window) {
        return $window
    }

    # A crashed app has no flyout either; starting it again would hide the crash.
    Assert-AppAlive 'while its flyout was hidden'
    Write-Problem 'The flyout is not visible; starting the app again to reopen it.'
    Start-App
    return Wait-Until -Description 'the flyout window' -TimeoutSeconds 30 -Condition { Get-FlyoutWindow }
}

function Find-All([AutomationElement] $Root, [ControlType] $Type) {
    $condition = [PropertyCondition]::new([AutomationElement]::ControlTypeProperty, $Type)
    return @($Root.FindAll([TreeScope]::Descendants, $condition) | Where-Object { -not $_.Current.IsOffscreen })
}

function Find-Named([AutomationElement] $Root, [ControlType] $Type, [string] $Name) {
    return Find-All $Root $Type | Where-Object { $_.Current.Name -eq $Name } | Select-Object -First 1
}

function Find-Text([AutomationElement] $Root, [string] $Pattern) {
    return Find-All $Root ([ControlType]::Text) | Where-Object { $_.Current.Name -match $Pattern } |
        Select-Object -First 1
}

function Get-Thumbnails([AutomationElement] $Root) {
    # Top to bottom, so the first is the one at the top of the list.
    return @(Find-All $Root ([ControlType]::Button) |
        Where-Object { $_.Current.Name -match $ThumbnailPattern } |
        Sort-Object -Property { $_.Current.BoundingRectangle.Top })
}

function Get-FirstThumbnail([AutomationElement] $Root) {
    $thumbnails = @(Get-Thumbnails $Root)
    if ($thumbnails.Count -eq 0) {
        throw 'No wallpaper thumbnail is shown.'
    }

    return $thumbnails[0]
}

function Find-ButtonWithText([AutomationElement] $Root, [string] $Text) {
    # A button whose content is a panel may have no name of its own; then its text is a child element.
    foreach ($button in Find-All $Root ([ControlType]::Button)) {
        if ($button.Current.Name -eq $Text) {
            return $button
        }

        $label = [PropertyCondition]::new([AutomationElement]::NameProperty, $Text)
        if ($null -ne $button.FindFirst([TreeScope]::Descendants, $label)) {
            return $button
        }
    }

    return $null
}

function Get-Center([AutomationElement] $Element) {
    $rect = $Element.Current.BoundingRectangle
    return [pscustomobject]@{ X = [int]($rect.X + $rect.Width / 2); Y = [int]($rect.Y + $rect.Height / 2) }
}

function Get-Pattern([AutomationElement] $Element, [AutomationPattern] $Pattern) {
    $provider = $null
    if ($Element.TryGetCurrentPattern($Pattern, [ref] $provider)) {
        return $provider
    }

    return $null
}

function Show-Element([AutomationElement] $Element) {
    # An element scrolled out of its ScrollViewer has an empty bounding rectangle; bring it into view first.
    $scrollItem = Get-Pattern $Element ([ScrollItemPattern]::Pattern)
    if ($null -ne $scrollItem) {
        $scrollItem.ScrollIntoView()
        Start-Sleep -Milliseconds 300
    }

    $rect = $Element.Current.BoundingRectangle
    if ($rect.IsEmpty -or [double]::IsNaN($rect.Width) -or $rect.Width -le 0 -or $rect.Height -le 0) {
        throw "'$($Element.Current.Name)' is not on screen, even after scrolling it into view."
    }
}

function Invoke-Click([AutomationElement] $Element) {
    Show-Element $Element
    $center = Get-Center $Element
    Write-Step "  mouse click at ($($center.X), $($center.Y)) on '$($Element.Current.Name)'"
    [ScreenshotNative]::Click($center.X, $center.Y)
}

function Invoke-Element([AutomationElement] $Element) {
    # The Invoke pattern clicks a button without moving the mouse or taking the focus.
    $invoke = Get-Pattern $Element ([InvokePattern]::Pattern)
    if ($null -ne $invoke) {
        Write-Step "  invoke '$($Element.Current.Name)'"
        $invoke.Invoke()
        return
    }

    Invoke-Click $Element
}

function Select-Element([AutomationElement] $Element) {
    # Chips are radio buttons; selecting one through UI Automation clicks it, which runs the view's handler.
    $selection = Get-Pattern $Element ([SelectionItemPattern]::Pattern)
    if ($null -ne $selection) {
        Write-Step "  select '$($Element.Current.Name)'"
        $selection.Select()
        Start-Sleep -Milliseconds 300
        if ($selection.Current.IsSelected) {
            return
        }

        Write-Problem "Selecting '$($Element.Current.Name)' had no effect; clicking it instead."
    }

    Invoke-Click $Element
}

function Show-Feed {
    # The detail and settings screens have a Back button; the feed has none.
    $back = Find-Named (Get-Flyout) ([ControlType]::Button) $Ui.Back
    if ($null -ne $back) {
        Invoke-Element $back
        $null = Wait-Until -Description 'the feed' -Condition {
            Find-Named (Get-Flyout) ([ControlType]::Button) $Ui.Settings
        }
    }
}

function Clear-FocusVisuals {
    # Focus rectangles show while the last input came from the keyboard (the Enter that submitted the search, for
    # example). A click on a blank spot of the flyout, just below its top edge in the middle, makes the last input a
    # pointer one again without activating anything; the cursor then leaves the flyout, so no hover state shows.
    $rect = (Get-Flyout).Current.BoundingRectangle
    [ScreenshotNative]::Click([int]($rect.X + $rect.Width / 2), [int]$rect.Y + 6)
    Move-CursorAway
    Start-Sleep -Milliseconds 400
}

function Close-StrayDialogs([switch] $ListWindows) {
    # The runner can show system message boxes (for example a "System Properties" paging file notice) that would
    # appear in the captures. Only standard dialogs (window class #32770) are closed: other windows, such as the
    # runner's own console, must stay.
    $closed = 0
    foreach ($window in [AutomationElement]::RootElement.FindAll([TreeScope]::Children, [Condition]::TrueCondition)) {
        try {
            $current = $window.Current
            if ($ListWindows) {
                Write-Step ("  top-level window: '{0}' class={1} pid={2}" -f $current.Name, $current.ClassName,
                    $current.ProcessId)
            }

            if ($current.ClassName -ne '#32770') {
                continue
            }

            Write-Step "  closing dialog '$($current.Name)'"
            [ScreenshotNative]::CloseWindow([IntPtr]$current.NativeWindowHandle)
            $closed++
        }
        catch {
            Write-Step "  could not inspect or close a window: $($_.Exception.Message)"
        }
    }

    if ($closed -gt 0) {
        Start-Sleep -Milliseconds 500
    }
}

function Move-CursorAway {
    # Parks the pointer away from the flyout, so no hover state shows in the next capture. Real mouse input, so a
    # hovered element gets PointerExited; SetCursorPos alone does not reach the flyout as pointer movement.
    [ScreenshotNative]::MovePointer(10, 10, 4)
}

function Get-RegionHash([System.Windows.Rect] $Rect) {
    $path = Join-Path (Get-TempRoot) "region-$([Guid]::NewGuid().ToString('N')).png"
    try {
        Save-ScreenRegion ([int]$Rect.X) ([int]$Rect.Y) ([int]$Rect.Width) ([int]$Rect.Height) $path
        return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
    finally {
        Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    }
}

function Test-SetButtonShown([AutomationElement] $Thumbnail) {
    # The overlay's Set button is always in the UI Automation tree (the overlay only fades), but it takes hits only
    # while the overlay is shown: then the element at its centre is the button (or its text), else the thumbnail.
    $setName = [PropertyCondition]::new([AutomationElement]::NameProperty, $Ui.SetWallpaper)
    $button = $Thumbnail.FindFirst([TreeScope]::Descendants, $setName)
    if ($null -eq $button) {
        Write-Step '  no Set button inside the first thumbnail'
        return $false
    }

    $center = Get-Center $button
    $hit = [AutomationElement]::FromPoint([System.Windows.Point]::new($center.X, $center.Y))
    $walker = [TreeWalker]::ControlViewWalker
    for ($i = 0; $null -ne $hit -and $i -lt 3; $i++) {
        if ($hit.Current.Name -eq $Ui.SetWallpaper -and $hit.Current.ControlType -eq [ControlType]::Button) {
            Write-Step "  Set button on screen at ($($center.X), $($center.Y)), offscreen: $($button.Current.IsOffscreen)"
            return $true
        }

        $hit = $walker.GetParent($hit)
    }

    return $false
}

function Show-HoverOverlay([AutomationElement] $Thumbnail) {
    # The thumbnail without hover, to compare with the capture once the overlay shows.
    $rect = $Thumbnail.Current.BoundingRectangle
    $before = Get-RegionHash $rect
    $center = Get-Center $Thumbnail
    Write-Step "  pointer onto the first wallpaper at ($($center.X), $($center.Y))"
    # Onto the wallpaper in small steps, then a last nudge inside it, so the flyout sees the pointer enter and move.
    [ScreenshotNative]::MovePointer($center.X, $center.Y - 30, 8)
    [ScreenshotNative]::MovePointer($center.X, $center.Y, 3)
    Start-Sleep -Milliseconds 400

    $shown = $false
    try {
        $null = Wait-Until -Description 'the Set button on the hovered wallpaper' -TimeoutSeconds 3 `
            -IntervalMilliseconds 200 -Condition { Test-SetButtonShown $Thumbnail }
        $shown = $true
    }
    catch {
        Write-Problem $_.Exception.Message
    }

    # Past the overlay's fade-in (0.18 s) before comparing and capturing.
    Start-Sleep -Milliseconds 300
    $changed = (Get-RegionHash $rect) -ne $before
    Write-Step "  hover overlay: Set button shown (UI Automation) = $shown, thumbnail changed on screen = $changed"
    if (-not $shown -or -not $changed) {
        throw 'Hovering the first wallpaper did not show its Set button.'
    }
}

function Write-UiTree([AutomationElement] $Root, [int] $MaxElements = 300) {
    Write-Step 'UI Automation tree of the flyout (control type, name, automation id, offscreen):'
    $walker = [TreeWalker]::ControlViewWalker
    $count = 0
    $stack = [System.Collections.Generic.Stack[object]]::new()
    $stack.Push(@($Root, 0))
    while ($stack.Count -gt 0 -and $count -lt $MaxElements) {
        $element, $depth = $stack.Pop()
        try {
            $current = $element.Current
            $type = $current.ControlType.ProgrammaticName -replace '^ControlType\.', ''
            Write-Host ('    {0}{1} "{2}" id="{3}" offscreen={4}' -f ('  ' * $depth), $type, $current.Name,
                $current.AutomationId, $current.IsOffscreen)
            $count++
            $children = [System.Collections.Generic.List[object]]::new()
            $child = $walker.GetFirstChild($element)
            while ($null -ne $child) {
                $children.Add($child)
                $child = $walker.GetNextSibling($child)
            }

            for ($i = $children.Count - 1; $i -ge 0; $i--) {
                $stack.Push(@($children[$i], ($depth + 1)))
            }
        }
        catch {
            Write-Host "    (element no longer available: $($_.Exception.Message))"
        }
    }
}

# --- Waiting for content -------------------------------------------------------------------------------------------

function Wait-Feed([int] $TimeoutSeconds = 45) {
    $null = Wait-Until -Description 'wallpaper thumbnails in the feed' -TimeoutSeconds $TimeoutSeconds -Condition {
        $window = Get-FlyoutWindow
        if ($null -eq $window) {
            return $false
        }

        return @(Get-Thumbnails $window).Count -ge 1
    }
    Write-Step "  thumbnails shown; waiting $SettleSeconds s for the images"
    Start-Sleep -Seconds $SettleSeconds
}

function Wait-FeedReload {
    # A new query empties the list while the first page loads.
    Start-Sleep -Milliseconds 1500
    Wait-Feed
}

# --- Capturing -----------------------------------------------------------------------------------------------------

function Save-ScreenRegion([int] $X, [int] $Y, [int] $Width, [int] $Height, [string] $Path) {
    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            # Plain SourceCopy: CopyFromScreen rejects combined flags such as SourceCopy|CaptureBlt as an undefined enum
            # value. With desktop composition (always on since Windows 8) the screen copy includes layered windows.
            $graphics.CopyFromScreen(
                [System.Drawing.Point]::new($X, $Y),
                [System.Drawing.Point]::Empty,
                [System.Drawing.Size]::new($Width, $Height),
                [System.Drawing.CopyPixelOperation]::SourceCopy)
        }
        finally {
            $graphics.Dispose()
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

function Get-FlyoutRegion([AutomationElement] $Window) {
    $rect = $Window.Current.BoundingRectangle
    $screenWidth = [ScreenshotNative]::ScreenWidth
    $screenHeight = [ScreenshotNative]::ScreenHeight
    $left = [Math]::Max(0, [int]$rect.X - $CaptureMargin)
    $top = [Math]::Max(0, [int]$rect.Y - $CaptureMargin)
    $right = [Math]::Min($screenWidth, [int]($rect.X + $rect.Width) + $CaptureMargin)
    $bottom = [Math]::Min($screenHeight, [int]($rect.Y + $rect.Height) + $CaptureMargin)
    return [pscustomobject]@{ X = $left; Y = $top; Width = $right - $left; Height = $bottom - $top }
}

function Add-Screenshot([string] $State, [string] $Theme, [pscustomobject] $Region) {
    $name = if ($Theme) { "$State-$($Theme.ToLowerInvariant())" } else { $State }
    $file = "$name.png"
    $path = Join-Path $OutputDir $file
    Save-ScreenRegion $Region.X $Region.Y $Region.Width $Region.Height $path
    $caption = $Captions[$State]
    if ($Theme) {
        $caption = "$caption ($($Theme.ToLowerInvariant()) theme)"
    }

    $script:Produced.Add([ordered]@{
            file    = $file
            state   = $State
            theme   = if ($Theme) { $Theme.ToLowerInvariant() } else { $null }
            caption = $caption
            width   = $Region.Width
            height  = $Region.Height
        })
    Write-Step "  saved $file ($($Region.Width)x$($Region.Height) at $($Region.X),$($Region.Y))"
}

function Save-Flyout([string] $State, [string] $Theme) {
    # A dialog that appeared meanwhile would cover the flyout, or hide it by taking the focus (Get-Flyout reopens it).
    Close-StrayDialogs
    $window = Get-Flyout
    Add-Screenshot $State $Theme (Get-FlyoutRegion $window)
}

function Save-Hero {
    # In 16:9: the flyout over the wallpaper, above the taskbar and tray, down to the bottom of the screen. The right
    # edge is the flyout's, which leaves out the system's watermark text at the bottom-right corner of the desktop.
    Close-StrayDialogs
    $window = Get-Flyout
    $rect = $window.Current.BoundingRectangle
    $screenHeight = [ScreenshotNative]::ScreenHeight
    $right = [Math]::Min([ScreenshotNative]::ScreenWidth, [int]($rect.X + $rect.Width) + 2)
    $top = [Math]::Max(0, [int]$rect.Y - 96)
    $height = $screenHeight - $top
    $width = [Math]::Min($right, [int]($height * 16 / 9))
    Add-Screenshot 'hero' $null ([pscustomobject]@{ X = $right - $width; Y = $top; Width = $width; Height = $height })
}

function Save-Diagnostics([string] $Name) {
    try {
        $folder = Join-Path $OutputDir 'diagnostics'
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        $path = Join-Path $folder "debug-$Name.png"
        Save-ScreenRegion 0 0 ([ScreenshotNative]::ScreenWidth) ([ScreenshotNative]::ScreenHeight) $path
        Write-Step "  saved diagnostics/debug-$Name.png (full screen)"
    }
    catch {
        Write-Problem "Could not save the diagnostic screenshot: $($_.Exception.Message)"
    }
}

function Invoke-State([string] $Name, [scriptblock] $Action, [switch] $Optional) {
    Write-Step "State $Name"
    try {
        & $Action
        Assert-AppAlive "in state $Name"
    }
    catch {
        # A crash fails the run at once; other failures are recorded and the run moves on.
        Assert-AppAlive "in state $Name"
        if ($Optional) {
            # Nice to have, and not reliably reachable on every runner: no failure, no diagnostics.
            Write-Host "::warning::Optional state $Name skipped: $($_.Exception.Message)"
            return
        }

        $script:Failures.Add($Name)
        Write-Problem "State $Name failed: $($_.Exception.Message)"
        Write-Host $_.ScriptStackTrace
        Save-Diagnostics $Name
        $window = Get-FlyoutWindow
        if ($null -ne $window) {
            Write-UiTree $window
        }
        else {
            Write-Step '  the flyout is not visible'
        }
    }
    finally {
        try {
            Move-CursorAway
        }
        catch {
            Write-Step "  could not move the cursor: $($_.Exception.Message)"
        }
    }
}

# --- The states ----------------------------------------------------------------------------------------------------

function Select-Genre([string] $Genre) {
    $chip = Wait-Until -Description "the $Genre genre chip" -Condition {
        Find-Named (Get-Flyout) ([ControlType]::RadioButton) $Genre
    }
    Select-Element $chip
    Wait-FeedReload
}

function Submit-Text([string] $BoxName, [string] $Text) {
    # Types into a text box and presses Enter (search and exclusions have no submit button), so the box needs the
    # keyboard focus and the flyout the foreground: UI Automation focus first, a click on the box if that leaves
    # another window in front.
    $window = Get-Flyout
    $box = Wait-Until -Description "the '$BoxName' box" -Condition {
        Find-Named (Get-Flyout) ([ControlType]::Edit) $BoxName
    }
    $handle = [IntPtr]$window.Current.NativeWindowHandle

    $path = 'UI Automation focus'
    try {
        $box.SetFocus()
    }
    catch {
        Write-Step "  SetFocus failed: $($_.Exception.Message)"
    }

    Start-Sleep -Milliseconds 300
    if ([ScreenshotNative]::ForegroundWindow() -ne $handle) {
        $path = 'click on the box'
        Invoke-Click $box
        Move-CursorAway
        Start-Sleep -Milliseconds 300
    }

    $value = Get-Pattern $box ([ValuePattern]::Pattern)
    if ($null -ne $value) {
        $value.SetValue($Text)
    }
    else {
        [ScreenshotNative]::TypeText($Text)
    }

    if (-not [ScreenshotNative]::BringToForeground($handle)) {
        Write-Problem 'The flyout is not in the foreground; Enter may not reach the box.'
    }

    Write-Step "  '$Text' into '$BoxName': Enter after $path"
    [ScreenshotNative]::PressKey(0x0D)
}

function Submit-Search([string] $Query) {
    Submit-Text $Ui.Search $Query
    Wait-FeedReload
}

# --- Exercising every control --------------------------------------------------------------------------------------

function Invoke-Step([string] $Description, [scriptblock] $Action) {
    # One interaction, then a pause for its animations to run, then the crash check. A step that fails without a
    # crash is recorded and the next one runs.
    Write-Step "  step: $Description"
    try {
        & $Action
        Start-Sleep -Milliseconds 600
        Assert-AppAlive "after '$Description'"
    }
    catch {
        Assert-AppAlive "after '$Description'"
        $script:Failures.Add("step: $Description")
        Write-Problem "Step '$Description' failed: $($_.Exception.Message)"
    }
}

function Find-Choice([string] $Name) {
    # A pill, tab or segment by its name: a radio button, a SelectorBar item (a list item or tab item) or a button,
    # preferring one that can be selected; never the text inside it, which has the same name.
    $condition = [PropertyCondition]::new([AutomationElement]::NameProperty, $Name)
    $candidates = @((Get-Flyout).FindAll([TreeScope]::Descendants, $condition) |
        Where-Object { $_.Current.ControlType.ProgrammaticName -ne 'ControlType.Text' })
    $selectable = $candidates | Where-Object { $null -ne (Get-Pattern $_ ([SelectionItemPattern]::Pattern)) } |
        Select-Object -First 1
    if ($null -ne $selectable) {
        return $selectable
    }

    $first = $candidates | Select-Object -First 1
    if ($null -eq $first) {
        throw "No control named '$Name' is shown."
    }

    return $first
}

function Select-Choice([string] $Name) {
    Select-Element (Find-Choice $Name)
}

function Set-FiltersOpen([bool] $Open) {
    $toggle = Find-Named (Get-Flyout) ([ControlType]::Button) $Ui.Filters
    if ($null -eq $toggle) {
        throw "No '$($Ui.Filters)' button found."
    }

    $pattern = Get-Pattern $toggle ([TogglePattern]::Pattern)
    if ($null -eq $pattern) {
        Invoke-Click $toggle
        return
    }

    $isOpen = $pattern.Current.ToggleState -eq [ToggleState]::On
    if ($isOpen -ne $Open) {
        Write-Step "  toggle '$($Ui.Filters)' (open: $isOpen -> $Open)"
        $pattern.Toggle()
    }
}

function Invoke-Exercises([string] $Theme) {
    Show-Feed

    foreach ($genre in $ExerciseGenres) {
        Invoke-Step "genre $genre" { Select-Choice $genre; Start-Sleep -Milliseconds 900 }
    }

    foreach ($sorting in $ExerciseSortings) {
        Invoke-Step "sort tab $sorting" { Select-Choice $sorting; Start-Sleep -Milliseconds 900 }
    }

    Invoke-Step 'refresh' {
        $refresh = Wait-Until -Description 'the refresh button' -Condition {
            Find-Named (Get-Flyout) ([ControlType]::Button) $Ui.Refresh
        }
        Invoke-Element $refresh
        Start-Sleep -Milliseconds 900
    }

    Invoke-Step 'filters toggle (close)' { Set-FiltersOpen $false }
    Invoke-Step 'filters toggle (open)' {
        Set-FiltersOpen $true
        $null = Wait-Until -Description 'the filters panel' -TimeoutSeconds 5 -Condition {
            Find-Named (Get-Flyout) ([ControlType]::RadioButton) $Ui.Resolution
        }
    }

    foreach ($choice in @($ExerciseRatios) + @($ExerciseResolutions) + @($ExerciseColors)) {
        Invoke-Step "filter $choice" { Select-Choice $choice; Start-Sleep -Milliseconds 600 }
    }

    Invoke-Step 'colour cleared by choosing it again' {
        # Selecting the selected swatch through UI Automation does nothing; a click runs its handler.
        Invoke-Click (Find-Choice $ExerciseColors[-1])
    }

    Invoke-Step "exclude #$ExerciseExclusion" {
        Submit-Text $Ui.ExcludeBox $ExerciseExclusion
        $null = Wait-Until -Description "the #$ExerciseExclusion chip" -TimeoutSeconds 5 -Condition {
            Find-Text (Get-Flyout) "^#$ExerciseExclusion$"
        }
    }

    Invoke-Step "remove #$ExerciseExclusion" {
        $name = $Ui.RemoveFormat -f $ExerciseExclusion
        $remove = Wait-Until -Description "the '$name' button" -TimeoutSeconds 5 -Condition {
            Find-Named (Get-Flyout) ([ControlType]::Button) $name
        }
        Invoke-Element $remove
        $null = Wait-Until -Description "the #$ExerciseExclusion chip to go" -TimeoutSeconds 5 -Condition {
            $null -eq (Find-Text (Get-Flyout) "^#$ExerciseExclusion$")
        }
    }

    Invoke-Step 'filters toggle (close again)' { Set-FiltersOpen $false }

    Invoke-Step 'Ctrl+K, then Escape' {
        $handle = [IntPtr](Get-Flyout).Current.NativeWindowHandle
        $null = [ScreenshotNative]::BringToForeground($handle)
        [ScreenshotNative]::PressKeyWith(0x11, 0x4B)
        Start-Sleep -Milliseconds 400
        # Escape hides the flyout (a focused box may take the first one); starting the app again reopens it.
        for ($i = 0; $i -lt 3 -and $null -ne (Get-FlyoutWindow); $i++) {
            [ScreenshotNative]::PressKey(0x1B)
            Start-Sleep -Milliseconds 700
        }

        Assert-AppAlive 'after Escape'
        if ($null -ne (Get-FlyoutWindow)) {
            throw 'Escape did not hide the flyout.'
        }

        Start-App
        $null = Wait-Until -Description 'the flyout window' -TimeoutSeconds 30 -Condition { Get-FlyoutWindow }
        Wait-Feed -TimeoutSeconds 30
    }

    Invoke-Step 'open the details' {
        Invoke-Element (Get-FirstThumbnail (Get-Flyout))
        $null = Wait-Until -Description 'the detail screen' -Condition {
            Find-Named (Get-Flyout) ([ControlType]::Button) $Ui.Back
        }
    }

    Invoke-Step 'display choice segments' {
        $segments = @(Find-All (Get-Flyout) ([ControlType]::RadioButton))
        if ($segments.Count -eq 0) {
            # Shown only with more than one display.
            Write-Step "  no display choice: a single display"
            return
        }

        foreach ($segment in $segments | Select-Object -Last 2) {
            Select-Element $segment
            Start-Sleep -Milliseconds 400
        }

        Select-Choice $Ui.AllDisplays
    }

    Invoke-Step 'back from the details' { Show-Feed }

    Invoke-Step 'open the settings' {
        Invoke-Element (Wait-Until -Description 'the settings button' -Condition {
                Find-Named (Get-Flyout) ([ControlType]::Button) $Ui.Settings
            })
        $null = Wait-Until -Description 'the settings screen' -Condition { Find-Text (Get-Flyout) "^$($Ui.SettingsHead)$" }
    }

    Invoke-Step 'start with Windows switch' {
        # The switch is the element of that name with the Toggle pattern (the card's title has the same name).
        $toggle = Wait-Until -Description "the '$($Ui.LaunchAtStartup)' switch" -Condition {
            $condition = [PropertyCondition]::new([AutomationElement]::NameProperty, $Ui.LaunchAtStartup)
            (Get-Flyout).FindAll([TreeScope]::Descendants, $condition) |
                ForEach-Object { Get-Pattern $_ ([TogglePattern]::Pattern) } |
                Where-Object { $null -ne $_ } | Select-Object -First 1
        }

        # On and off again: the run must not leave a Run key entry behind.
        $toggle.Toggle()
        Start-Sleep -Milliseconds 600
        $toggle.Toggle()
    }

    # Light, then Dark (in the Light run Dark first, so it ends on its own theme); the whole flyout re-themes.
    $themes = if ($Theme -eq 'Dark') { @($Ui.ThemeLight, $Ui.ThemeDark) } else { @($Ui.ThemeDark, $Ui.ThemeLight) }
    foreach ($name in $themes) {
        # A real click: the theme segments act on Click, which selecting through UI Automation may not raise.
        Invoke-Step "theme $name" { Invoke-Click (Find-Choice $name); Start-Sleep -Milliseconds 900 }
    }

    Invoke-Step 'back from the settings' { Show-Feed }
}

function Invoke-Theme([string] $Theme) {
    $suffix = $Theme.ToLowerInvariant()
    Write-Host "::group::Theme $Theme"
    try {
        Stop-App
        Write-AppSettings $Theme
        Set-DesktopWallpaper
        Close-StrayDialogs
        Move-CursorAway
        Start-App -Primary

        Invoke-State "start-$suffix" {
            $window = Wait-Until -Description 'the flyout window' -TimeoutSeconds 60 -Condition { Get-FlyoutWindow }
            $rect = $window.Current.BoundingRectangle
            Write-Step "  flyout at $rect, handle $($window.Current.NativeWindowHandle)"
            Wait-Feed
            if ($Theme -eq 'Dark') {
                Write-UiTree (Get-Flyout)
            }
        }

        # The feed, hero, detail and applied images show the Nature genre: the unfiltered toplist can hold images
        # unsuitable for a project page.
        # Without any hover state, so the hero shows the plain feed.
        Invoke-State "genre-$suffix" {
            Select-Genre $Ui.Genre
            Clear-FocusVisuals
            Save-Flyout 'genre' $Theme
            if ($Theme -eq 'Dark') {
                Save-Hero
            }
        }

        # Optional: synthetic pointer input does not always reach the flyout on a hosted runner, and no page uses it.
        Invoke-State "feed-$suffix" -Optional {
            # Pointing at the first wallpaper shows its resolution and the quick Set button.
            Show-HoverOverlay (Get-FirstThumbnail (Get-Flyout))
            Save-Flyout 'feed' $Theme
        }

        Invoke-State "detail-$suffix" {
            Invoke-Element (Get-FirstThumbnail (Get-Flyout))
            # The feed has no Back button (and its thumbnails hold Set buttons of their own).
            $null = Wait-Until -Description 'the detail screen' -Condition {
                Find-Named (Get-Flyout) ([ControlType]::Button) $Ui.Back
            }
            try {
                # The tags arrive with the wallpaper's details, after the screen opens.
                $null = Wait-Until -Description 'the tags' -TimeoutSeconds 20 -Condition { Find-Text (Get-Flyout) '^#' }
            }
            catch {
                Write-Problem "Detail screen without tags: $($_.Exception.Message)"
            }

            Start-Sleep -Seconds 2
            Clear-FocusVisuals
            Save-Flyout 'detail' $Theme
        }

        Invoke-State "applied-$suffix" {
            $button = Find-ButtonWithText (Get-Flyout) $Ui.SetWallpaper
            if ($null -eq $button) {
                throw "No '$($Ui.SetWallpaper)' button on the detail screen."
            }

            Invoke-Element $button
            # The toast shows for 2.4 s once the original has downloaded and been set.
            $toast = Wait-Until -Description 'the toast' -TimeoutSeconds 120 -IntervalMilliseconds 100 -Condition {
                Find-Text (Get-Flyout) "$ToastSuccessPattern|$ToastFailurePattern"
            }
            $message = $toast.Current.Name
            Write-Step "  toast: $message"
            # Let it fade in (0.18 s).
            Start-Sleep -Milliseconds 300
            if ($message -match $ToastSuccessPattern) {
                Save-Flyout 'applied' $Theme
            }
            else {
                Save-Diagnostics "applied-$suffix-toast"
                throw "Setting the wallpaper failed: $message"
            }
        }

        Invoke-State "search-filters-$suffix" {
            Show-Feed
            # Searching selects the All genre, as the app does for every search.
            Submit-Search $SearchQuery

            $toggle = Find-Named (Get-Flyout) ([ControlType]::Button) $Ui.Filters
            if ($null -eq $toggle) {
                throw "No '$($Ui.Filters)' button found."
            }

            $togglePattern = Get-Pattern $toggle ([TogglePattern]::Pattern)
            if ($null -ne $togglePattern) {
                Write-Step "  toggle '$($Ui.Filters)'"
                $togglePattern.Toggle()
            }
            else {
                Invoke-Click $toggle
            }

            $chip = Wait-Until -Description 'the filters panel' -TimeoutSeconds 5 -Condition {
                Find-Named (Get-Flyout) ([ControlType]::RadioButton) $Ui.Resolution
            }
            Select-Element $chip
            Wait-FeedReload
            try {
                # The chip of the tag excluded in settings.json, in the panel's Exclude row.
                $null = Wait-Until -Description "the #$ExcludedTag exclusion chip" -TimeoutSeconds 5 -Condition {
                    Find-Text (Get-Flyout) "^#$ExcludedTag$"
                }
            }
            catch {
                Write-Problem "Filters panel without the exclusion chip: $($_.Exception.Message)"
            }

            Clear-FocusVisuals
            Save-Flyout 'search-filters' $Theme
        }

        Invoke-State "settings-$suffix" {
            Show-Feed
            $settings = Wait-Until -Description 'the settings button' -Condition {
                Find-Named (Get-Flyout) ([ControlType]::Button) $Ui.Settings
            }
            Invoke-Element $settings
            $null = Wait-Until -Description 'the settings screen' -Condition {
                $window = Get-Flyout
                (Find-Named $window ([ControlType]::Button) $Ui.Back) -and (Find-Text $window "^$($Ui.SettingsHead)$")
            }
            # Clear of any toast from the previous states.
            Start-Sleep -Seconds 3
            Clear-FocusVisuals
            Save-Flyout 'settings' $Theme
        }

        # After the captures, so the images do not depend on it: every interactive control once.
        Invoke-State "exercise-$suffix" { Invoke-Exercises $Theme }
    }
    finally {
        Write-AppLog
        Write-Host '::endgroup::'
    }
}

# --- Main ----------------------------------------------------------------------------------------------------------

$AppPath = (Resolve-Path -LiteralPath $AppPath).Path
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$OutputDir = (Resolve-Path -LiteralPath $OutputDir).Path
# Only this run's images: a state that fails leaves no outdated picture behind.
Get-ChildItem -LiteralPath $OutputDir -Filter '*.png' -File | Remove-Item -Force
$diagnostics = Join-Path $OutputDir 'diagnostics'
if (Test-Path -LiteralPath $diagnostics) {
    Remove-Item -LiteralPath $diagnostics -Recurse -Force
}

Write-Step "App: $AppPath"
Write-Step "Output: $OutputDir"
Initialize-Libraries
Set-ScreenResolution
Set-DesktopAppearance
Enable-AnimationEffects
Get-DesktopWallpaper
Close-StrayDialogs -ListWindows

try {
    foreach ($theme in 'Dark', 'Light') {
        Invoke-Theme $theme
    }
}
catch {
    # A crash ends the run; anything else unexpected is rethrown as it is.
    if ($null -eq $script:Crash) {
        throw
    }
}
finally {
    Stop-App
}

$manifest = [ordered]@{
    screen      = [ordered]@{ width = [ScreenshotNative]::ScreenWidth; height = [ScreenshotNative]::ScreenHeight }
    wallpaper   = $script:WallpaperInfo
    screenshots = @($script:Produced)
}
$manifestPath = Join-Path $OutputDir 'manifest.json'
[System.IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 5) + "`n")
Write-Step "Wrote $manifestPath"

Write-Step "Screenshots: $($script:Produced.Count); failed states: $($script:Failures.Count)"
foreach ($failure in $script:Failures) {
    Write-Step "  failed: $failure"
}

if ($null -ne $script:Crash) {
    Write-Host "::error::The app crashed $($script:Crash). See the app log and Windows events above, and diagnostics/."
    exit 1
}

if ($script:Produced.Count -eq 0) {
    Write-Host '::error::No screenshot was produced.'
    exit 1
}
