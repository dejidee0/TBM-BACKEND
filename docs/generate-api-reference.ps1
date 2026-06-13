param(
    [string]$OutputDir = 'docs',
    [string]$BaseName = 'TBM-API-Documentation',
    [string]$SnapshotDate = $(Get-Date -Format 'yyyy-MM-dd')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputDirFull = Join-Path $repoRoot $OutputDir
if (-not (Test-Path $outputDirFull)) { New-Item -ItemType Directory -Path $outputDirFull | Out-Null }

$controllersRoot = Join-Path $repoRoot 'src/TBM.API/Controllers'
$dtoRoots = @(
    (Join-Path $repoRoot 'src/TBM.Application/DTOs'),
    (Join-Path $repoRoot 'src/TBM.Core/DTOs')
)
$enumRoot = Join-Path $repoRoot 'src/TBM.Core/Enums'

function Escape-XmlText { param([string]$Text) $Text.Replace('&','&amp;').Replace('<','&lt;').Replace('>','&gt;').Replace('"','&quot;') }

function To-CamelCase {
    param([string]$Name)
    if ([string]::IsNullOrEmpty($Name) -or -not [char]::IsUpper($Name[0])) { return $Name }
    $chars = $Name.ToCharArray()
    for ($i=0; $i -lt $chars.Length; $i++) {
        $hasNext = ($i + 1) -lt $chars.Length
        if ($i -eq 0 -or ($hasNext -and [char]::IsUpper($chars[$i + 1]))) { $chars[$i] = [char]::ToLowerInvariant($chars[$i]); continue }
        break
    }
    -join $chars
}

function Split-TopLevelComma {
    param([string]$Text)
    $parts = New-Object System.Collections.Generic.List[string]
    $sb = New-Object System.Text.StringBuilder
    $angle = 0; $paren = 0; $bracket = 0; $brace = 0; $inString = $false; $stringChar = [char]0
    for ($i=0; $i -lt $Text.Length; $i++) {
        $c = $Text[$i]
        if ($inString) {
            [void]$sb.Append($c)
            if ($c -eq $stringChar) {
                $escaped = $false; $j = $i - 1
                while ($j -ge 0 -and $Text[$j] -eq '\') { $escaped = -not $escaped; $j-- }
                if (-not $escaped) { $inString = $false }
            }
            continue
        }
        if ($c -eq '"' -or $c -eq [char]39) { $inString = $true; $stringChar = $c; [void]$sb.Append($c); continue }
        switch ($c) {
            '<' { $angle++; [void]$sb.Append($c); continue }
            '>' { if ($angle -gt 0) { $angle-- }; [void]$sb.Append($c); continue }
            '(' { $paren++; [void]$sb.Append($c); continue }
            ')' { if ($paren -gt 0) { $paren-- }; [void]$sb.Append($c); continue }
            '[' { $bracket++; [void]$sb.Append($c); continue }
            ']' { if ($bracket -gt 0) { $bracket-- }; [void]$sb.Append($c); continue }
            '{' { $brace++; [void]$sb.Append($c); continue }
            '}' { if ($brace -gt 0) { $brace-- }; [void]$sb.Append($c); continue }
            ',' { if ($angle -eq 0 -and $paren -eq 0 -and $bracket -eq 0 -and $brace -eq 0) { $parts.Add($sb.ToString().Trim()); [void]$sb.Clear(); continue }; [void]$sb.Append($c); continue }
            default { [void]$sb.Append($c); continue }
        }
    }
    $tail = $sb.ToString().Trim()
    if ($tail.Length -gt 0) { $parts.Add($tail) }
    ,$parts.ToArray()
}

function Extract-StringArg { param([string]$Line) if ($Line -match '\(\s*"([^"]*)"') { $Matches[1] } }
function Extract-InnerText { param([string]$Line) if ($Line -match '\((.*)\)') { $Matches[1].Trim() } }

function Resolve-ControllerToken { param([string]$ControllerClassName) (($ControllerClassName -replace 'Controller$','').ToLowerInvariant()) }

function Combine-Routes {
    param([string]$BaseRoute,[string]$ActionTemplate)
    if ([string]::IsNullOrWhiteSpace($ActionTemplate)) { $combined = $BaseRoute }
    elseif ($ActionTemplate.StartsWith('~/')) { $combined = $ActionTemplate.Substring(1) }
    elseif ($ActionTemplate.StartsWith('/')) { $combined = $ActionTemplate }
    else { $combined = if ([string]::IsNullOrWhiteSpace($BaseRoute)) { '/' + $ActionTemplate } else { $BaseRoute.TrimEnd('/') + '/' + $ActionTemplate.TrimStart('/') } }
    if (-not $combined.StartsWith('/')) { $combined = '/' + $combined }
    $combined
}

function Test-IgnoreApi { param([string[]]$Attributes) [bool]($Attributes | Where-Object { $_ -match '^\[ApiExplorerSettings\(.*IgnoreApi\s*=\s*true.*\)\]' } | Select-Object -First 1) }

function Parse-Authorize {
    param([string[]]$Attributes)
    if ($Attributes | Where-Object { $_ -match '^\[AllowAnonymous\]' } | Select-Object -First 1) { return 'Public' }
    $authLine = $Attributes | Where-Object { $_ -match '^\[Authorize' } | Select-Object -First 1
    if (-not $authLine) { return $null }
    if ($authLine -match 'Roles\s*=\s*"([^"]+)"') { return "Roles: $($Matches[1].Trim())" }
    'JWT'
}

function Parse-HttpAttribute {
    param([string[]]$Attributes)
    foreach ($attr in $Attributes) {
        if ($attr -match '^\[(HttpGet|HttpPost|HttpPut|HttpPatch|HttpDelete)(?:\((.*)\))?\]$') {
            return [pscustomobject]@{ Method = $Matches[1].Substring(4).ToUpperInvariant(); Template = (Extract-StringArg $attr) }
        }
    }
}

function Extract-ParameterInfo {
    param([string]$ParamText,[string]$HttpMethod,[string]$RouteTemplate)
    $raw = $ParamText.Trim(); if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
    $attrs = @()
    while ($raw -match '^\s*\[([^\]]+)\]\s*(.*)$') { $attrs += $Matches[1]; $raw = $Matches[2] }
    $default = $null; if ($raw -match '^(.*)\s*=\s*(.*)$') { $raw = $Matches[1].Trim(); $default = $Matches[2].Trim() }
    $raw = ($raw -replace '^\s*(ref|out|in|params)\s+','').Trim()
    $tokens = $raw -split '\s+'; if ($tokens.Length -lt 2) { return $null }
    $name = $tokens[-1]; $type = ($tokens[0..($tokens.Length-2)] -join ' ')
    $binding = if ($attrs -match '^FromBody') { 'body' } elseif ($attrs -match '^FromForm') { 'form' } elseif ($attrs -match '^FromRoute') { 'route' } elseif ($attrs -match '^FromQuery') { 'query' } elseif ($type -match '\bIFormFile\b') { 'form' } elseif ($RouteTemplate -match "\{$name([:\}]|\})") { 'route' } elseif ($HttpMethod -in @('GET','DELETE')) { 'query' } else { 'body' }
    $queryNameOverride = $null
    foreach ($a in $attrs) { if ($a -match 'FromQuery\s*\(\s*Name\s*=\s*"([^"]+)"\s*\)') { $queryNameOverride = $Matches[1]; break } }
    [pscustomobject]@{ Name=$name; Type=$type; Default=$default; Binding=$binding; QueryNameOverride=$queryNameOverride }
}

function Format-ParamList {
    param([pscustomobject[]]$Params,[string]$Binding)
    $filtered = $Params | Where-Object { $_.Binding -eq $Binding }
    if (-not $filtered) { return 'none' }
    ($filtered | ForEach-Object {
        $name = if ($Binding -eq 'query' -and $_.QueryNameOverride) { $_.QueryNameOverride } else { $_.Name }
        $name = To-CamelCase $name
        if ($null -ne $_.Default) { "${name}: $($_.Type) = $($_.Default)" } else { "${name}: $($_.Type)" }
    }) -join ', '
}

function Get-ClassInfo {
    param([string[]]$Lines)
    $attrs = New-Object System.Collections.Generic.List[string]
    $className = $null; $baseRoute = $null; $classAuth = $null; $rateLimit = $null; $ignoreApi = $false
    foreach ($line in $Lines) {
        $t = $line.Trim()
        if ($t.StartsWith('[')) { $attrs.Add($t); continue }
        if ($t -match '^public\s+(?:abstract\s+)?(?:sealed\s+|partial\s+|static\s+)*class\s+(\w+)') {
            $className = $Matches[1]
            $routeLine = $attrs | Where-Object { $_ -match '^\[Route\(' } | Select-Object -First 1
            if ($routeLine) { $baseRoute = Extract-StringArg $routeLine }
            $classAuth = Parse-Authorize $attrs.ToArray()
            $rateLimitLine = $attrs | Where-Object { $_ -match '^\[EnableRateLimiting\(' } | Select-Object -First 1
            if ($rateLimitLine) { $rateLimit = Extract-StringArg $rateLimitLine }
            $ignoreApi = Test-IgnoreApi $attrs.ToArray()
            break
        }
    }
    if ($className -and $baseRoute) { $controllerToken = Resolve-ControllerToken $className; $baseRoute = $baseRoute.Replace('[controller]',$controllerToken).Replace('[Controller]',$controllerToken) }
    [pscustomobject]@{ ClassName=$className; BaseRoute=$baseRoute; ClassAuth=$classAuth; RateLimitPolicy=$rateLimit; IgnoreApi=$ignoreApi }
}

function Get-DtoSchemas {
    param([string[]]$Roots)
    $schemas = @()
    foreach ($root in $Roots) {
        if (-not (Test-Path $root)) { continue }
        foreach ($file in (Get-ChildItem -LiteralPath $root -Recurse -Filter *.cs -File)) {
            $text = Get-Content -LiteralPath $file.FullName -Raw
            $nsMatch = [regex]::Match($text, '(?m)^\s*namespace\s+([A-Za-z0-9_\.]+)\s*(?:;|\{)'); $ns = if ($nsMatch.Success) { $nsMatch.Groups[1].Value.Trim() } else { '' }
            foreach ($cd in [regex]::Matches($text, '(?m)^\s*public\s+(?:sealed\s+|abstract\s+|partial\s+|static\s+)*class\s+(\w+)\b')) {
                $typeName = $cd.Groups[1].Value; $fullName = if ($ns) { "$ns.$typeName" } else { $typeName }
                $startIdx = $text.IndexOf('{', $cd.Index); if ($startIdx -lt 0) { continue }
                $depth = 0; $endIdx = -1
                for ($i = $startIdx; $i -lt $text.Length; $i++) {
                    if ($text[$i] -eq '{') { $depth++ } elseif ($text[$i] -eq '}') { $depth--; if ($depth -eq 0) { $endIdx = $i; break } }
                }
                if ($endIdx -lt 0) { continue }
                $body = $text.Substring($startIdx + 1, $endIdx - $startIdx - 1)
                $props = @()
                foreach ($pm in [regex]::Matches($body, '(?m)^\s*public\s+(?:required\s+)?([A-Za-z0-9_<>,\[\]\?\.\|]+)\s+(\w+)\s*\{\s*get;\s*(set;|init;)\s*\}')) {
                    $props += [pscustomobject]@{ Name=$pm.Groups[2].Value.Trim(); Type=$pm.Groups[1].Value.Trim() }
                }
                $schemas += [pscustomobject]@{ Kind='class'; FullName=$fullName; FilePath=$file.FullName.Substring($repoRoot.Length + 1).Replace('\','/'); Properties=$props }
            }
        }
    }
    $schemas
}

function Get-EnumSchemas {
    param([string]$Root)
    $schemas = @(); if (-not (Test-Path $Root)) { return $schemas }
    foreach ($file in (Get-ChildItem -LiteralPath $Root -Filter *.cs -File)) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $nsMatch = [regex]::Match($text, '(?m)^\s*namespace\s+([A-Za-z0-9_\.]+)\s*(?:;|\{)'); $ns = if ($nsMatch.Success) { $nsMatch.Groups[1].Value.Trim() } else { '' }
        foreach ($ed in [regex]::Matches($text, '(?m)^\s*public\s+enum\s+(\w+)\b')) {
            $typeName = $ed.Groups[1].Value; $fullName = if ($ns) { "$ns.$typeName" } else { $typeName }
            $block = [regex]::Match($text, "(?s)public\s+enum\s+$typeName\b\s*\{(.*?)\}"); $values = @()
            if ($block.Success) { foreach ($line in ($block.Groups[1].Value -split "`r?`n")) { $clean = ($line -replace '//.*$','').Trim(); if ($clean -match '^([A-Za-z0-9_]+)') { $values += $Matches[1] } } }
            $schemas += [pscustomobject]@{ Kind='enum'; FullName=$fullName; FilePath=$file.FullName.Substring($repoRoot.Length + 1).Replace('\','/'); Values=$values }
        }
        foreach ($sd in [regex]::Matches($text, '(?m)^\s*public\s+static\s+class\s+(\w+)\b')) {
            $typeName = $sd.Groups[1].Value; $fullName = if ($ns) { "$ns.$typeName" } else { $typeName }
            $block = [regex]::Match($text, "(?s)public\s+static\s+class\s+$typeName\b\s*\{(.*?)\}"); $values = @()
            if ($block.Success) { foreach ($cm in [regex]::Matches($block.Groups[1].Value, '(?m)^\s*public\s+const\s+string\s+\w+\s*=\s*"([^"]*)"\s*;')) { $values += $cm.Groups[1].Value } }
            if ($values.Count -gt 0) { $schemas += [pscustomobject]@{ Kind='staticClass'; FullName=$fullName; FilePath=$file.FullName.Substring($repoRoot.Length + 1).Replace('\','/'); Values=$values } }
        }
    }
    $schemas
}

function New-ParagraphXml {
    param([string]$Text,[int]$Size = 22,[switch]$Bold)
    if ([string]::IsNullOrEmpty($Text)) { return '<w:p/>' }
    $escaped = Escape-XmlText $Text
    $runXml = if ($Bold) { "<w:r><w:rPr><w:b/><w:sz w:val=`"$Size`"/></w:rPr><w:t xml:space=`"preserve`">$escaped</w:t></w:r>" } else { "<w:r><w:t xml:space=`"preserve`">$escaped</w:t></w:r>" }
    "<w:p>$runXml</w:p>"
}

function Generate-DocxFromMarkdownLines {
    param([string[]]$Lines,[string]$DocxPath)
    Add-Type -AssemblyName System.IO.Compression | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $contentTypesXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
</Types>
'@
    $rootRelsXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>
'@
    $docXml = New-Object System.Text.StringBuilder
    [void]$docXml.AppendLine('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
    [void]$docXml.AppendLine('<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">')
    [void]$docXml.AppendLine('  <w:body>')
    foreach ($line in $Lines) {
        if ($line -match '^#\s+(.*)$') { [void]$docXml.AppendLine('    ' + (New-ParagraphXml $Matches[1] 32 -Bold)); continue }
        if ($line -match '^##\s+(.*)$') { [void]$docXml.AppendLine('    ' + (New-ParagraphXml $Matches[1] 28 -Bold)); continue }
        if ($line -match '^###\s+(.*)$') { [void]$docXml.AppendLine('    ' + (New-ParagraphXml $Matches[1] 24 -Bold)); continue }
        if ([string]::IsNullOrEmpty($line)) { [void]$docXml.AppendLine('    <w:p/>'); continue }
        [void]$docXml.AppendLine('    ' + (New-ParagraphXml $line))
    }
    [void]$docXml.AppendLine('    <w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440"/></w:sectPr>')
    [void]$docXml.AppendLine('  </w:body>')
    [void]$docXml.AppendLine('</w:document>')
    if (Test-Path $DocxPath) { Remove-Item $DocxPath -Force }
    $zip = [System.IO.Compression.ZipFile]::Open($DocxPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entrySpec in @(@{ Name='[Content_Types].xml'; Content=$contentTypesXml }, @{ Name='_rels/.rels'; Content=$rootRelsXml }, @{ Name='word/document.xml'; Content=$docXml.ToString() })) {
            $entry = $zip.CreateEntry($entrySpec.Name); $stream = $entry.Open()
            try { $writer = New-Object System.IO.StreamWriter($stream, $utf8NoBom); try { $writer.Write($entrySpec.Content) } finally { $writer.Dispose() } }
            finally { $stream.Dispose() }
        }
    }
    finally { $zip.Dispose() }
}

if (-not (Test-Path $controllersRoot)) { throw "Controllers root not found: $controllersRoot" }

$controllerFiles = Get-ChildItem -LiteralPath $controllersRoot -Recurse -Filter '*Controller.cs' -File | Where-Object { $_.Name -ne 'BaseAdminController.cs' } | Sort-Object FullName
$allEndpoints = @()
foreach ($cf in $controllerFiles) {
    $lines = Get-Content -LiteralPath $cf.FullName
    $classInfo = Get-ClassInfo $lines
    if (-not $classInfo.ClassName) { continue }
    $pendingAttributes = New-Object System.Collections.Generic.List[string]
    $pendingSummary = $null; $inSummary = $false; $summaryBuffer = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $t = $lines[$i].Trim()
        if ($t.StartsWith('///')) {
            if ($t -match '<summary>') {
                $inSummary = $true
                $after = ($t -replace '^\s*///\s*','') -replace '.*<summary>\s*',''
                $after = $after.Trim()
                if ($after.Length -gt 0 -and $after -ne '</summary>') { $summaryBuffer.Add($after) }
                continue
            }
            if ($inSummary) {
                $clean = (($t -replace '^\s*///\s*','').Trim() -replace '</summary>\s*$','').Trim()
                if ($clean.Length -gt 0) { $summaryBuffer.Add($clean) }
                if ($t -match '</summary>') { $inSummary = $false; $pendingSummary = (($summaryBuffer -join ' ') -replace '\s+',' ').Trim(); $summaryBuffer.Clear() }
            }
            continue
        }
        if ($t.StartsWith('[')) { $pendingAttributes.Add($t); continue }
        if ($pendingAttributes.Count -eq 0) { continue }
        if ($t -match '^public\s+') {
            $sig = $t
            while ($sig -notmatch '\)' -and ($i + 1) -lt $lines.Length) { $i++; $sig += ' ' + $lines[$i].Trim() }
            $http = Parse-HttpAttribute $pendingAttributes.ToArray()
            if (-not $http) { $pendingAttributes.Clear(); $pendingSummary = $null; continue }
            $paramText = ''; $mParams = [regex]::Match($sig, '\((.*)\)'); if ($mParams.Success) { $paramText = $mParams.Groups[1].Value }
            $baseRoute = if ($classInfo.BaseRoute) { $classInfo.BaseRoute } else { '' }
            $path = Combine-Routes $baseRoute $http.Template
            $actionAuth = Parse-Authorize $pendingAttributes.ToArray()
            $effectiveAuth = if ($actionAuth) { $actionAuth } elseif ($classInfo.ClassAuth) { $classInfo.ClassAuth } else { 'Public' }
            $rateLimit = $classInfo.RateLimitPolicy; $rateLimitLine = $pendingAttributes | Where-Object { $_ -match '^\[EnableRateLimiting\(' } | Select-Object -First 1; if ($rateLimitLine) { $rateLimit = Extract-StringArg $rateLimitLine }
            $consumes = @(); foreach ($a in $pendingAttributes) { if ($a -match '^\[Consumes\(') { $c = Extract-StringArg $a; if ($c) { $consumes += $c } } }
            $requestSizeLimit = $null; foreach ($a in $pendingAttributes) { if ($a -match '^\[RequestSizeLimit\(') { $requestSizeLimit = Extract-InnerText $a; break } }
            $ignoreApi = $classInfo.IgnoreApi -or (Test-IgnoreApi $pendingAttributes.ToArray())
            $paramList = @(); foreach ($p in (Split-TopLevelComma $paramText)) { $pi = Extract-ParameterInfo $p $http.Method $path; if ($pi) { $paramList += $pi } }
            $allEndpoints += [pscustomobject]@{ ControllerFile=$cf.FullName.Substring($repoRoot.Length + 1).Replace('\','/'); ControllerClass=$classInfo.ClassName; HttpMethod=$http.Method; Path=$path; Summary=$pendingSummary; Auth=$effectiveAuth; RateLimitPolicy=$rateLimit; Consumes=$consumes; RequestSizeLimit=$requestSizeLimit; HiddenFromSwagger=$ignoreApi; Params=$paramList }
            $pendingAttributes.Clear(); $pendingSummary = $null
        }
    }
}

$dtoSchemas = Get-DtoSchemas $dtoRoots | Sort-Object FullName
$enumSchemas = Get-EnumSchemas $enumRoot | Sort-Object FullName
$endpointCount = $allEndpoints.Count

$md = New-Object System.Collections.Generic.List[string]
$md.Add('# TBM Digital Platform API Reference'); $md.Add(''); $md.Add("Generated from the current codebase snapshot on $SnapshotDate."); $md.Add(''); $md.Add('This document was derived from:'); $md.Add('- `src/TBM.API/Controllers/**/*.cs`'); $md.Add('- `src/TBM.Application/DTOs/**/*.cs`'); $md.Add('- `src/TBM.Core/DTOs/**/*.cs`'); $md.Add('- `src/TBM.Core/Enums/*.cs`'); $md.Add('- `src/TBM.API/Program.cs`'); $md.Add('- `src/TBM.API/Middleware/*.cs`'); $md.Add(''); $md.Add("Current API surface in controllers: $endpointCount endpoints."); $md.Add(''); $md.Add('## 1. Overview'); $md.Add(''); $md.Add('- Primary API prefixes: `/api/v1/*`, `/api/admin/*`, `/api/webhooks/*`'); $md.Add('- Swagger UI is exposed at `/`; Swagger JSON at `/swagger/v1/swagger.json`.'); $md.Add('- Route matching is case-insensitive.'); $md.Add('- Roles: `Customer`, `Vendor`, `Admin`, `SuperAdmin`.'); $md.Add('- `RequestContextMiddleware` emits `X-Request-ID` and `X-Correlation-ID`.'); $md.Add('- Rate limiting uses `DynamicPolicy` and `WebhookPolicy`; maintenance mode returns `503` for non-admin, non-swagger traffic.'); $md.Add(''); $md.Add('## 2. Endpoint Index'); $md.Add(''); $md.Add('| Method | Path | Auth | Swagger | Query | Route | Body/Form |'); $md.Add('|---|---|---|---|---|---|---|')
foreach ($e in ($allEndpoints | Sort-Object Path, HttpMethod)) {
    $query = Format-ParamList $e.Params 'query'; $route = Format-ParamList $e.Params 'route'; $body = Format-ParamList $e.Params 'body'; $form = Format-ParamList $e.Params 'form'
    $bodyOrForm = if ($form -ne 'none') { "form: $form" } elseif ($body -ne 'none') { "body: $body" } else { 'none' }
    $swagger = if ($e.HiddenFromSwagger) { 'hidden' } else { 'visible' }
    $md.Add("| $($e.HttpMethod) | ``$($e.Path)`` | $($e.Auth) | $swagger | $query | $route | $bodyOrForm |")
}
$md.Add(''); $md.Add('## 3. Endpoint Reference'); $md.Add('')
$byController = $allEndpoints | Group-Object ControllerClass | Sort-Object Name
$controllerIndex = 1
foreach ($group in $byController) {
    $controllerFile = ($group.Group | Select-Object -First 1).ControllerFile
    $md.Add("### 3.$controllerIndex $($group.Name)"); $md.Add(''); $md.Add("Controller file: ``$controllerFile``"); $md.Add('')
    $md.Add('| Method | Path | Auth | Swagger | Query | Route | Body/Form | Consumes | Rate Limit | Request Size | Summary |')
    $md.Add('|---|---|---|---|---|---|---|---|---|---|---|')
    foreach ($e in ($group.Group | Sort-Object Path, HttpMethod)) {
        $query = Format-ParamList $e.Params 'query'; $route = Format-ParamList $e.Params 'route'; $body = Format-ParamList $e.Params 'body'; $form = Format-ParamList $e.Params 'form'
        $bodyOrForm = if ($form -ne 'none') { "form: $form" } elseif ($body -ne 'none') { "body: $body" } else { 'none' }
        $consumes = if ($e.Consumes -and $e.Consumes.Count -gt 0) { $e.Consumes -join ', ' } else { 'application/json' }
        $rateLimit = if ($e.RateLimitPolicy) { $e.RateLimitPolicy } else { 'none' }
        $requestSize = if ($e.RequestSizeLimit) { $e.RequestSizeLimit } else { 'none' }
        $swagger = if ($e.HiddenFromSwagger) { 'hidden' } else { 'visible' }
        $summary = if ($e.Summary) { $e.Summary } else { '' }
        $md.Add("| $($e.HttpMethod) | ``$($e.Path)`` | $($e.Auth) | $swagger | $query | $route | $bodyOrForm | $consumes | $rateLimit | $requestSize | $summary |")
    }
    $md.Add(''); $controllerIndex++
}
$md.Add('## 4. Schemas')
$md.Add('')
$md.Add('### 4.1 DTO Classes')
$md.Add('')
foreach ($schema in $dtoSchemas) {
    $md.Add(('`{0}`' -f $schema.FullName))
    $md.Add(('Source: `{0}`' -f $schema.FilePath))
    if (-not $schema.Properties -or $schema.Properties.Count -eq 0) {
        $md.Add('- (no public auto-properties detected)')
    }
    else {
        foreach ($p in $schema.Properties) {
            $propName = To-CamelCase $p.Name
            $md.Add(('- `{0}: {1}`' -f $propName, $p.Type))
        }
    }
    $md.Add('')
}

$md.Add('### 4.2 Enums and Static Constants')
$md.Add('')
foreach ($schema in $enumSchemas) {
    $md.Add(('`{0}`' -f $schema.FullName))
    $md.Add(('Source: `{0}`' -f $schema.FilePath))
    foreach ($v in $schema.Values) {
        $md.Add(('- `{0}`' -f $v))
    }
    $md.Add('')
}

$mdPath = Join-Path -Path $outputDirFull -ChildPath ($BaseName + '.md')
$docxPath = Join-Path -Path $outputDirFull -ChildPath ($BaseName + '.docx')
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$mdContent = $md -join [Environment]::NewLine
[System.IO.File]::WriteAllText($mdPath, $mdContent, $utf8NoBom)
Generate-DocxFromMarkdownLines -Lines ($md.ToArray()) -DocxPath $docxPath
Write-Host 'Wrote:'
Write-Host (' - ' + $mdPath)
Write-Host (' - ' + $docxPath)
