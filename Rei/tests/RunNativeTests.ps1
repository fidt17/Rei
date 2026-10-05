param(
    [ValidateSet('All', 'Cpu', 'Gl', 'Integration')]
    [string]$Group = 'All',
    [switch]$NoBuild,
    [uint32]$Seed = 424242,
    [ValidateRange(1, 3600)]
    [int]$GroupTimeoutSeconds = 300,
    [string]$TestName = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe'
$runDirectory = Join-Path $repoRoot ('TestResults/native/runs/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

function Invoke-NativeBuild([string]$Name, [string[]]$BuildArguments) {
    & $msbuild @BuildArguments /m:1 /v:minimal /nologo *> (Join-Path $runDirectory "$Name-build.log")
    if ($LASTEXITCODE -ne 0) { throw "$Name build failed. Diagnostics: $runDirectory" }
}

function Remove-OwnedRunTemporary([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $boundary = $runDirectory + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw "Cleanup outside owned run directory: $resolved" }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}

function Wait-OwnedNativeChildren {
    $childReports = Join-Path $runDirectory 'child-reports'
    if (-not (Test-Path -LiteralPath $childReports)) { return }
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    Get-ChildItem -LiteralPath $childReports -Directory | ForEach-Object {
        $identityPath = Join-Path $_.FullName 'process.json'
        if (-not (Test-Path -LiteralPath $identityPath)) { return }
        $identity = Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json
        $childProcess = $null
        try { $childProcess = [Diagnostics.Process]::GetProcessById([int]$identity.ProcessId) }
        catch [ArgumentException] { return }
        try {
            # Acquiring handle before inspecting identity prevents PID reuse
            # between verification and waiting. Never wait/kill unrelated PIDs.
            $null = $childProcess.Handle
            if ($childProcess.HasExited) { return }
            if ($childProcess.StartTime.ToUniversalTime().ToFileTimeUtc() -ne [long]$identity.CreationFileTime) { return }
            $remaining = [Math]::Max(1, [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds)
            if (-not $childProcess.WaitForExit($remaining)) { throw "Owned child did not exit after root termination: $($identity.ProcessId)" }
        }
        catch [InvalidOperationException] { return } # Process exited before its handle was acquired.
        finally { $childProcess.Dispose() }
    }
}

function Write-NativeCaseManifest {
    $sourceCases = @{}
    Get-ChildItem -LiteralPath (Join-Path $repoRoot 'Rei/tests') -Filter '*Tests.cpp' | ForEach-Object {
        $sourceFile = $_.FullName
        $lines = [IO.File]::ReadAllLines($sourceFile)
        for ($line = 0; $line -lt $lines.Length; ++$line) {
            $match = [regex]::Match($lines[$line], '^TEST_CASE\("([^"]+)",\s*"([^"]*)"\)')
            if (-not $match.Success) { continue }
            $name = $match.Groups[1].Value
            if ($sourceCases.ContainsKey($name)) { throw "Duplicate testcase: $name" }
            $sourceCases[$name] = [pscustomobject]@{ Source = $sourceFile; Line = $line + 1; Tags = $match.Groups[2].Value }
        }
    }
    $manifest = @()
    foreach ($groupResult in $summary) {
        if (-not $groupResult.Report -or -not (Test-Path -LiteralPath $groupResult.Report)) { continue }
        try { [xml]$document = Get-Content -LiteralPath $groupResult.Report -Raw }
        catch { continue } # InfrastructureStatus already records partial XML.
        foreach ($case in $document.SelectNodes('/testsuites/testsuite/testcase')) {
            $source = $sourceCases[$case.name]
            if (-not $source) { throw "Unmapped native testcase: $($case.name)" }
            $failureNodes = @($case.SelectNodes('failure | error'))
            $result = if ($failureNodes.Count) { 'failed' } elseif ($case.SelectNodes('skipped').Count) { 'skipped' } else { 'passed' }
            $manifest += [pscustomobject]@{
                Test = $case.name; Group = $groupResult.Group; Result = $result; Source = $source.Source; Line = $source.Line
                Tags = $source.Tags; ProposedPolicy = $source.Tags -match '\[proposed-(contract|policy)\]'
                AddedInTask = $source.Tags.Contains('[coverage]'); Seed = $Seed; Report = $groupResult.Report
                Failures = @($failureNodes | ForEach-Object { [pscustomobject]@{ Type = $_.type; Message = $_.message; Details = $_.InnerText } })
            }
        }
    }
    ConvertTo-Json -InputObject @($manifest) -Depth 7 | Set-Content -LiteralPath (Join-Path $runDirectory 'coverage.json') -Encoding utf8
    ConvertTo-Json -InputObject @($manifest | Where-Object Result -eq 'failed') -Depth 7 | Set-Content -LiteralPath (Join-Path $runDirectory 'failures.json') -Encoding utf8
}

function Invoke-BoundedNativeTest([string]$Name, [string]$Filter, [string]$Report) {
    $start = [Diagnostics.ProcessStartInfo]::new($executable)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @($Filter, '--order', 'rand', '--rng-seed', [string]$Seed, '--reporter', 'junit', '--out', $Report)) { $start.ArgumentList.Add($argument) }
    $temporaryRoot = Join-Path $runDirectory ($Name + '-temporary')
    $start.Environment['REI_NATIVE_TEST_RUN_DIR'] = $runDirectory
    $start.Environment['REI_NATIVE_TEST_TEMP_ROOT'] = $temporaryRoot
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $started = $false
    try {
        if (-not $process.Start()) { throw 'Native root process did not start' }
        $started = $true
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $complete = $process.WaitForExit($GroupTimeoutSeconds * 1000)
        if (-not $complete) {
            # Only this owned root and descendants. Child Job handles also kill
            # on close; never terminate by process name.
            $process.Kill($true)
            if (-not $process.WaitForExit(5000)) { throw 'Owned native group did not terminate after deadline' }
        }
        $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $runDirectory ($Name + '-console.log')) -Encoding utf8
        $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $runDirectory ($Name + '-stderr.log')) -Encoding utf8
        return [pscustomobject]@{ ExitCode = $process.ExitCode; TimedOut = -not $complete }
    }
    finally {
        if ($started -and -not $process.HasExited) {
            $process.Kill($true)
            if (-not $process.WaitForExit(5000)) { throw 'Owned native root did not exit before cleanup' }
        }
        $process.Dispose()
        Wait-OwnedNativeChildren
        Remove-OwnedRunTemporary $temporaryRoot
        $childReports = Join-Path $runDirectory 'child-reports'
        if (Test-Path -LiteralPath $childReports) {
            Get-ChildItem -LiteralPath $childReports -Directory | ForEach-Object { Remove-OwnedRunTemporary (Join-Path $_.FullName 'temporary') }
        }
    }
}

$summary = @()
Push-Location $repoRoot
try {
    if ($TestName -and $Group -eq 'All') { throw 'TestName requires a specific Group' }
    if (-not $NoBuild) {
        Invoke-NativeBuild 'tests' @('Rei.sln', '/t:Rei', '/p:Configuration=Tests', '/p:Platform=x64')
        Invoke-NativeBuild 'rei-debug' @('Rei.sln', '/t:Rei', '/p:Configuration=Debug', '/p:Platform=x64')
        Invoke-NativeBuild 'sandbox-debug' @('Rei.sln', '/t:ReiSandbox', '/p:Configuration=Debug', '/p:Platform=x64')
        if ($Group -in @('All', 'Integration')) {
            foreach ($version in @(1, 2)) {
                Invoke-NativeBuild "project-v$version" @('Rei/tests/ProjectDll/NativeProject.vcxproj', '/p:Configuration=Debug', '/p:Platform=x64', "/p:NativeProjectVersion=$version")
            }
        }
    }
    $executable = Join-Path $repoRoot 'bin/x64Tests/Rei/Rei.exe'
    if (-not (Test-Path -LiteralPath $executable)) { throw "Native test executable unavailable: $executable" }
    $groups = [ordered]@{
        Cpu = '~[gl]~[engine-integration]'
        Gl = '[gl]~[engine-integration]'
        Integration = '[engine-integration]'
    }
    foreach ($entry in $groups.GetEnumerator()) {
        if ($Group -ne 'All' -and $Group -ne $entry.Key) { continue }
        $report = Join-Path $runDirectory ($entry.Key + '.xml')
        $filter = if ($TestName) { '"' + $TestName.Replace('\', '\\').Replace('"', '\"').Replace(',', '\,') + '"' + $entry.Value } else { $entry.Value }
        $execution = Invoke-BoundedNativeTest $entry.Key $filter $report
        $status = 'complete'
        $diagnostic = ''
        $cases = @()
        if ($execution.TimedOut) { $status = 'timeout'; $diagnostic = "Group exceeded $GroupTimeoutSeconds seconds" }
        if (-not (Test-Path -LiteralPath $report)) {
            if ($status -eq 'complete') { $status = 'missing_report' }
        }
        else {
            try {
                [xml]$junit = Get-Content -LiteralPath $report -Raw
                $cases = @($junit.SelectNodes('/testsuites/testsuite/testcase'))
            }
            catch {
                if ($status -eq 'complete') { $status = 'invalid_report' }
                $diagnostic = @($diagnostic, $_.Exception.Message) -join ' '
            }
        }
        $failures = @($cases | Where-Object { $_.failure -or $_.error })
        $skipped = @($cases | Where-Object { $_.skipped -ne $null })
        if ($status -eq 'complete' -and $cases.Count -eq 0) { $status = 'empty_report' }
        if ($status -eq 'complete' -and $execution.ExitCode -ne 0 -and $failures.Count -eq 0) { $status = 'unexplained_exit' }
        $summary += [pscustomobject]@{ Group = $entry.Key; Cases = $cases.Count; Passed = $cases.Count - $failures.Count - $skipped.Count; Failed = $failures.Count; Skipped = $skipped.Count; ExitCode = $execution.ExitCode; Seed = $Seed; Report = $report; InfrastructureStatus = $status; Diagnostic = $diagnostic }
    }
}
catch {
    $summary += [pscustomobject]@{ Group = 'Infrastructure'; Cases = 0; Passed = 0; Failed = 0; Skipped = 0; ExitCode = 1; Seed = $Seed; Report = ''; InfrastructureStatus = 'failed'; Diagnostic = $_.Exception.Message }
}
finally { Pop-Location }
$summary | ConvertTo-Json -AsArray | Set-Content -LiteralPath (Join-Path $runDirectory 'summary.json') -Encoding utf8
try { Write-NativeCaseManifest }
catch {
    $summary += [pscustomobject]@{ Group = 'Manifest'; Cases = 0; Passed = 0; Failed = 0; Skipped = 0; ExitCode = 1; Seed = $Seed; Report = ''; InfrastructureStatus = 'failed'; Diagnostic = $_.Exception.Message }
    $summary | ConvertTo-Json -AsArray | Set-Content -LiteralPath (Join-Path $runDirectory 'summary.json') -Encoding utf8
}
$summary | Format-Table Group, Cases, Passed, Failed, Skipped, ExitCode, InfrastructureStatus, Seed
Write-Output "Diagnostics: $runDirectory"
if (@($summary | Where-Object { $_.Failed -gt 0 -or $_.ExitCode -ne 0 -or $_.InfrastructureStatus -ne 'complete' }).Count -gt 0) { exit 1 }
