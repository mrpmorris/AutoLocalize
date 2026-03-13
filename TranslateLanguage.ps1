# IMPORTANT
# To use this script you need a ChatGPT API key
# the value of the key must be an environment variable named AutoLocalize_ChatGPTApiKey

# This script takes *.resx and uses Chat-GPT to translate them to *.{lang}.resx
# eg Area.resx -> Area.it.resx
# Simply run the script in the same folder as your *.resx files.
# The script will keep a source-checksum file per translation, so it knows if 
# each translation is up to date with the current resx file contents.
# For example.
# File in script folder = AppStrings.resx
# Language code requested = fr
# Created resx file = AppStrings.it.resx
# AppStrings.fr.resx.source-checksum = the MD5 hash of AppStrings.resx
# If the MD5 hash of AppStrings.resx is the same as in AppStrings.{lang}.resx-sourcechecksum
# then AppStrings.{lang}.resx file won't be re-processed.

# Prompt for localisation code (must be 2 chars, or (2chars)-(2chars))
$lang = Read-Host "Enter localisation code (e.g. it or pt-BR)"

if ($lang -notmatch '^[a-zA-Z]{2}(-[a-zA-Z]{2})?$') {
    Write-Host "Invalid localisation code. Must be 2 letters or 2 letters-2 letters (e.g. it or pt-BR)."
    exit 1
}

# Normalise: language lower, optional region upper (e.g. pt-BR)
if ($lang.Contains("-")) {
    $parts = $lang.Split("-", 2)
    $lang = ($parts[0].ToLowerInvariant() + "-" + $parts[1].ToUpperInvariant())
} else {
    $lang = $lang.ToLowerInvariant()
}

# OpenAI API key from environment
$apiKey = $env:AutoLocalize_ChatGPTApiKey
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    Write-Host "Missing environment variable: AutoLocalize_ChatGPTApiKey"
    exit 1
}

# Target relative directory
$targetDir = "."

if (-not (Test-Path $targetDir)) {
    Write-Host "Directory not found: $targetDir"
    exit 1
}

function Build-LocalizationPromptJson([string] $localisationCode, [string] $jsonMapText) {
@"
# SYSTEM INSTRUCTION (STRICT):

You are performing a DATA TRANSFORMATION task.
You are NOT answering a question.
You are NOT explaining anything.
You MUST ONLY transform input JSON into output JSON.

If you output anything except a JSON object, the result is INVALID.

--------------------------------------------------

Task:
You are a professional translator.
You are translating the VALUES in the provided JSON object into language "$localisationCode" for a software application.
The text in the JSON file is case-sensitive because the application is case sensitive, so
    your translated text MUST contain the same equivalent capitalization as the source.
The JSON file text is also sometimes fragments of sentences (e.g. ", and also "), so
    your translated text MUST represent this identically with all relevant punctuation (or lack therefore).
Keys MUST remain identical.
--------------------------------------------------

# INPUT FORMAT:

The input JSON is an object where each key maps to an object with:
- "english": the source English text to translate (always present)
- "translated": any existing translation for that key from *.$localisationCode.resx (may be empty or missing)

Example shape:
{
  "SomeKey": { "english": "Hello", "translated": "Ciao" },
  "OtherKey": { "english": "Cancel", "translated": "" }
}

--------------------------------------------------

# OUTPUT RULES (ABSOLUTE):

- Output EXACTLY ONE JSON OBJECT
- First character MUST be '{'
- Last character MUST be '}'
- NO markdown
- NO explanations
- NO examples
- NO code
- NO commentary
- NO surrounding text
- NO backticks

--------------------------------------------------

# KEY RULES:

- Do NOT add keys
- Do NOT remove keys
- Do NOT rename keys
- Every input key must appear once in output
- Do not preserve JSON formatting, I want the response as compact as possible

--------------------------------------------------

# TRANSLATION RULES:

- Translate user-facing text naturally for software UI
- Preserve placeholders {0}, {1}
- Preserve encoded HTML such as <br>
- Preserve escaped entities
- Preserve capitalization

- Read the "english" node for the source text.
- Pay special attention to capitalisation in the "english" node, the translation MUST match.
- Use the existing "translated" text across the whole JSON to infer prior terminology and keep it consistent.
- If an input key has a non-empty "translated" value and it is already a suitable translation
  for the current "english" value and preserves the upper/lower case of the english, then return it unchanged.
- Otherwise, produce a new translation whilst preserving upper/lower case of the english (important).

# Output format
- For each key, output ONLY the translated string (NOT an object).
  Example output:
  { "SomeKey": "Ciao", "OtherKey": "Annulla" }

# Culture localization rules
Localize culture-dependent formats to the target locale, including:
- currency formats
- decimal and thousands separators
- percentages
- date and time formats
- localized abbreviations
- the position of a currency symbol might night to change from the start of the string to the end when switching to a different currency (e.g. from $ to €).
- the currency symbol will most likely need to change to the currency of the country (e.g. from $ to €).
- when `p` is related to a monetary amount, it means `pence` (or `c` means `cents`) this will need to change to the currency of the country (e.g. `c` for countries that use the Euro)

Example (Italian):
    £1,234.56 -> 1,234.56 €
    Mon -> Lun

Do not change measurement units unless the target language requires it.

# FORMAT STRING RULE (CRITICAL)

When localizing numeric format masks, the  and . positions must remain where they are as they are magic strings:

English mask example:  #,##0.00
Italian mask must be:  #.##0,00

# Currency rules:
- thousands separator must change together with decimal separator
- you must never output the same symbol for both

--------------------------------------------------

# INPUT JSON (DATA ONLY - your response should contain only the following text but with values translated):

$jsonMapText
"@
}

function Unwrap-FencedCodeBlock([string] $text) {
    if ([string]::IsNullOrWhiteSpace($text)) { return $text }

    $trimmed = $text.Trim()
    if (-not $trimmed.StartsWith('```')) { return $text }

    $lines = $trimmed -split '(\r\n|\n|\r)'
    if ($lines.Count -lt 3) { return $text }

    $innerLines = $lines[1..($lines.Count - 2)]
    return [string]::Join([Environment]::NewLine, $innerLines)
}

function Invoke-ChatGptTranslateJson([string] $apiKey, [string] $promptText) {
    $endpoint = "https://api.openai.com/v1/responses"

    $headers = @{
        "Content-Type"  = "application/json"
        "Authorization" = "Bearer $apiKey"
    }

    $bodyObj = @{
        model = "gpt-5.2"
        input = @(
            @{
                role    = "user"
                content = @(
                    @{
                        type = "input_text"
                        text = $promptText
                    }
                )
            }
        )
        temperature = 0
    }

    $jsonBody = ($bodyObj | ConvertTo-Json -Depth 50 -Compress)
    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($jsonBody)

    try {
        return Invoke-RestMethod `
            -Uri $endpoint `
            -Method Post `
            -Headers $headers `
            -Body $bodyBytes
    }
    catch {
        $status = $null
        $detail = $null

        try { $status = $_.Exception.Response.StatusCode.value__ } catch { }
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $detail = $reader.ReadToEnd()
        } catch { }

        if ($null -ne $status) {
            throw "OpenAI API call failed (HTTP $status): $detail"
        } else {
            throw "OpenAI API call failed: $($_.Exception.Message)"
        }
    }
}

function Get-ResponseOutputText($resp) {
    if ($null -eq $resp -or $null -eq $resp.output) { return $null }

    $msgItems = @($resp.output | Where-Object { $_.type -eq "message" })
    if ($msgItems.Count -eq 0) { return $null }

    $textParts = @()
    foreach ($msg in $msgItems) {
        if ($null -eq $msg.content) { continue }
        foreach ($part in $msg.content) {
            if ($part.type -eq "output_text" -and $null -ne $part.text) {
                $textParts += [string]$part.text
            }
        }
    }

    if ($textParts.Count -eq 0) { return $null }
    return ($textParts -join "")
}

function Build-FlatMapFromResx([string] $resxXml) {
    try {
        [xml]$doc = $resxXml
    } catch {
        throw "RESX is not valid XML. $($_.Exception.Message)"
    }

    $map = @{}
    $dataNodes = @($doc.root.data)
    foreach ($d in $dataNodes) {
        $name = $d.GetAttribute("name")
        if ([string]::IsNullOrWhiteSpace($name)) { continue }

        $valueNode = $d.SelectSingleNode("value")
        if ($null -eq $valueNode) { continue }

        $map[$name] = [string]$valueNode.InnerText
    }

    return $map
}

function Build-JsonMapEnglishAndTranslated([hashtable] $englishMap, [hashtable] $translatedMapOrEmpty) {
    $combined = [ordered]@{}
    foreach ($k in $englishMap.Keys) {
        $t = ""
        if ($null -ne $translatedMapOrEmpty -and $translatedMapOrEmpty.ContainsKey($k)) {
            $t = [string]$translatedMapOrEmpty[$k]
        }

        $combined[$k] = [ordered]@{
            english    = [string]$englishMap[$k]
            translated = [string]$t
        }
    }
    return $combined
}

function Get-TextPreview([string] $text, [int] $maxLen = 2000) {
    if ($null -eq $text) { return "" }
    $t = [string]$text
    if ($t.Length -le $maxLen) { return $t }
    return $t.Substring(0, $maxLen) + "`n...[truncated]..."
}

function Convert-JsonTextToHashtable([string] $jsonText, [string] $contextForError, [string[]] $sourceKeys) {
    if ([string]::IsNullOrWhiteSpace($jsonText)) {
        throw "No JSON returned for $contextForError."
    }

    $jsonText = Unwrap-FencedCodeBlock -text $jsonText
    $s = $jsonText.Trim()

    if ($null -eq $sourceKeys -or $sourceKeys.Count -eq 0) {
        throw "Internal error: sourceKeys not provided for $contextForError."
    }

    $probeKeys = $sourceKeys | Select-Object -First 10
    $probes = @()
    foreach ($k in $probeKeys) {
        $probes += ('"' + $k.Replace('"','\"') + '":')
    }

    $candidates = New-Object System.Collections.Generic.List[string]

    $startIdx = 0
    while ($true) {
        $start = $s.IndexOf('{', $startIdx)
        if ($start -lt 0) { break }

        $depth = 0
        $inString = $false
        $escape = $false
        $end = -1

        for ($i = $start; $i -lt $s.Length; $i++) {
            $ch = $s[$i]

            if ($inString) {
                if ($escape) { $escape = $false; continue }
                if ($ch -eq '\') { $escape = $true; continue }
                if ($ch -eq '"') { $inString = $false; continue }
                continue
            } else {
                if ($ch -eq '"') { $inString = $true; continue }
                if ($ch -eq '{') { $depth++; continue }
                if ($ch -eq '}') {
                    $depth--
                    if ($depth -eq 0) { $end = $i; break }
                    continue
                }
            }
        }

        if ($end -gt $start) {
            $objText = $s.Substring($start, ($end - $start + 1))

            $looksLikeOurMap = $false
            foreach ($p in $probes) {
                if ($objText.IndexOf($p, [System.StringComparison]::Ordinal) -ge 0) {
                    $looksLikeOurMap = $true
                    break
                }
            }

            if ($looksLikeOurMap) {
                $candidates.Add($objText) | Out-Null
            }
        }

        $startIdx = $start + 1
    }

    if ($candidates.Count -eq 0) {
        $preview = Get-TextPreview -text $s -maxLen 5000
        throw ("GPT output did not contain a JSON object with expected keys for {0}.`n--- GPT RESPONSE START ---`n{1}`n--- GPT RESPONSE END ---" -f $contextForError, $preview)
    }

    $objText = ($candidates | Sort-Object Length -Descending | Select-Object -First 1)

    try {
        $obj = $objText | ConvertFrom-Json
    } catch {
        $preview = Get-TextPreview -text $s -maxLen 5000
        throw ("Failed to parse GPT JSON for {0}. {1}`n--- GPT RESPONSE START ---`n{2}`n--- GPT RESPONSE END ---" -f $contextForError, $_.Exception.Message, $preview)
    }

    $ht = @{}
    if ($obj -is [System.Collections.IDictionary]) {
        foreach ($k in $obj.Keys) { $ht[$k] = [string]$obj[$k] }
        return $ht
    }

    foreach ($p in $obj.PSObject.Properties) {
        $ht[$p.Name] = [string]$p.Value
    }

    return $ht
}

function Escape-ForRegexReplacement([string] $text) {
    if ($null -eq $text) { return "" }
    return $text.Replace('\', '\\').Replace('$', '$$')
}

function Escape-XmlTextForValueNode([string] $text) {
    if ($null -eq $text) { return "" }
    $t = $text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
    return $t
}

function Replace-ResxValuesUsingOriginalFormatting([string] $originalXml, [hashtable] $nameToValueMap) {
    if ([string]::IsNullOrWhiteSpace($originalXml)) { return $originalXml }
    if ($null -eq $nameToValueMap -or $nameToValueMap.Count -eq 0) { return $originalXml }

    $xmlText = $originalXml

    foreach ($key in $nameToValueMap.Keys) {
        $newValueRaw = $nameToValueMap[$key]

        $todoPattern = "(?s)(<data\b[^>]*\bname\s*=\s*`"" + [regex]::Escape($key) + "`"[^>]*>.*?<value>)(.*?)(</value>)"
        $todoMatch = [regex]::Match($xmlText, $todoPattern)
        if (-not $todoMatch.Success) { continue }

        $oldValue = [string]$todoMatch.Groups[2].Value
        if ($oldValue -like "*// TODO*") { continue }

        $xmlSafeValue = Escape-XmlTextForValueNode $newValueRaw
        $replacementValue = Escape-ForRegexReplacement $xmlSafeValue

        $pattern = "(?s)(<data\b[^>]*\bname\s*=\s*`"" + [regex]::Escape($key) + "`"[^>]*>.*?<value>)(.*?)(</value>)"
        $xmlText = [regex]::Replace(
            $xmlText,
            $pattern,
            { param($m) $m.Groups[1].Value + $xmlSafeValue + $m.Groups[3].Value },
            1
        )
    }

    return $xmlText
}

function Get-Md5ForFile([string] $filePath) {
    $h = Get-FileHash -LiteralPath $filePath -Algorithm MD5
    return ([string]$h.Hash).ToLowerInvariant()
}

function Read-WholeFileChecksum([string] $checksumPath) {
    if (-not (Test-Path -LiteralPath $checksumPath)) { return $null }
    $t = Get-Content -LiteralPath $checksumPath -Raw -Encoding UTF8
    if ($null -eq $t) { return $null }
    $t = $t.Trim()
    if ([string]::IsNullOrWhiteSpace($t)) { return $null }

    # Migration: older versions wrote per-key JSON. If we see JSON, treat as missing so we regenerate once.
    if ($t.StartsWith("{")) { return $null }

    return $t.ToLowerInvariant()
}

function Write-WholeFileChecksum([string] $checksumPath, [string] $md5) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($checksumPath, ($md5.ToLowerInvariant() + [Environment]::NewLine), $utf8NoBom)
}

# Only process files like: Area.resx (exactly one '.' in the FULL filename)
$files = Get-ChildItem -Path $targetDir -Filter *.resx -File | Where-Object {
    ($_.Name.ToCharArray() | Where-Object { $_ -eq '.' }).Count -eq 1
}

if ($files.Count -eq 0) {
    Write-Host "No base .resx files found in: $targetDir"
    exit 0
}

Write-Host "Localisation code: $lang"
Write-Host "Found $($files.Count) base .resx file(s)."

$toTranslate = @()
foreach ($file in $files) {
    $newName = "{0}.{1}.resx" -f $file.BaseName, $lang
    $destination = Join-Path $file.DirectoryName $newName

    $toTranslate += [pscustomobject]@{
        FileFullName = $file.FullName
        FileName     = $file.Name
        Destination  = $destination
        NewName      = $newName
    }
}

$toTranslate = @(
    $toTranslate |
        Sort-Object { (Get-Item -LiteralPath $_.FileFullName).Length }, FileName
)

if ($toTranslate.Count -eq 0) {
    Write-Host "No files to process."
    exit 0
}

Write-Host "Will process $($toTranslate.Count) file(s)."
Write-Host ""

$failures = New-Object System.Collections.Generic.List[object]
$successes = New-Object System.Collections.Generic.List[object]
$skipped =  New-Object System.Collections.Generic.List[object]

$index = 0
foreach ($item in $toTranslate) {
    $index++

    $fileInfo = Get-Item -LiteralPath $item.FileFullName
    $fileKb = [math]::Ceiling($fileInfo.Length / 1KB)

    Write-Host "[$index/$($toTranslate.Count)] Translating $($item.FileName) -> $($item.NewName) (source size: $fileKb KB)"

    try {
        $checksumPath = ($item.Destination + ".source-checksum") # e.g. Area.it.resx.checksum
        $currentMd5 = Get-Md5ForFile -filePath $item.FileFullName
        $previousMd5 = Read-WholeFileChecksum -checksumPath $checksumPath

        if ($previousMd5 -and ($previousMd5 -eq $currentMd5) -and (Test-Path -LiteralPath $item.Destination)) {
            Write-Host "    Skipping API call (source MD5 unchanged; checksum file matches)."
            $skipped.Add([pscustomobject]@{
                FileName    = $item.FileName
                Destination = $item.Destination
                Success     = $true
                Error       = $null
            }) | Out-Null

            Write-Host ""
            continue
        }

        # 1) Read English resx (keys + english text)
        $sourceXml = Get-Content -Path $item.FileFullName -Raw -Encoding UTF8
        $englishMap = Build-FlatMapFromResx -resxXml $sourceXml

        # 2) Read existing translated resx (if any)
        $translatedMap = @{}
        if (Test-Path -LiteralPath $item.Destination) {
            $existingTranslatedXml = Get-Content -Path $item.Destination -Raw -Encoding UTF8
            $translatedMap = Build-FlatMapFromResx -resxXml $existingTranslatedXml
        }

        # 3) Build JSON: key -> { english, translated }
        $combinedMap = Build-JsonMapEnglishAndTranslated -englishMap $englishMap -translatedMapOrEmpty $translatedMap
        $combinedJson = $combinedMap | ConvertTo-Json -Depth 10 -Compress

        # 4) Prompt instructs to use translated values for terminology consistency and reuse when suitable
        $promptText = Build-LocalizationPromptJson -localisationCode $lang -jsonMapText $combinedJson

        Write-Host ("Calling GPT for: {0}" -f $item.FileName)

        $resp = Invoke-ChatGptTranslateJson -apiKey $apiKey -promptText $promptText
        $outText = Get-ResponseOutputText $resp

        if ([string]::IsNullOrWhiteSpace($outText)) {
            throw "No text output returned from API."
        }

        # 5) AI returns full key->translated-string map for ALL keys
        $sourceKeys = @($englishMap.Keys)
        $finalTranslatedMap = Convert-JsonTextToHashtable -jsonText $outText -contextForError $item.FileName -sourceKeys $sourceKeys

        # 6) Replace values in the English template and write *.{lang}.resx
        $xmlWithoutComments = $sourceXml -replace '(?ms)^\s*<comment>\s*.*?\s*</comment>\r?\n', ''
        $finalXml = Replace-ResxValuesUsingOriginalFormatting -originalXml $xmlWithoutComments -nameToValueMap $finalTranslatedMap

        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($item.Destination, $finalXml, $utf8NoBom)

        # 7) Write/update checksum file (MD5 of entire English *.resx)
        Write-WholeFileChecksum -checksumPath $checksumPath -md5 $currentMd5

        Write-Host "    Wrote $($item.NewName)"
        Write-Host "    Wrote $([System.IO.Path]::GetFileName($checksumPath))"

        $successes.Add([pscustomobject]@{
            FileName    = $item.FileName
            Destination = $item.Destination
            Success     = $true
            Error       = $null
        }) | Out-Null
    }
    catch {
        $msg = $_.Exception.Message
        Write-Host "    ERROR: $msg"
        Write-Host "    Skipping write for $($item.NewName)"

        $failures.Add([pscustomobject]@{
            FileName    = $item.FileName
            Destination = $item.Destination
            Success     = $false
            Error       = $msg
        }) | Out-Null
    }

    Write-Host ""
}

Write-Host "Done."
Write-Host "Successful: $($successes.Count)"
Write-Host "Skipped:    $($skipped.Count)"
Write-Host "Failed:     $($failures.Count)"
Write-Host ""

if ($failures.Count -gt 0) {
    Write-Host "Failed files:"
    foreach ($f in $failures) {
        Write-Host "  $($f.FileName) - $($f.Error)"
    }
    exit 1
}

exit 0