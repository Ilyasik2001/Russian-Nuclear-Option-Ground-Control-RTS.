# Проверка словарей перевода против дампа строк мода.
#  * ключи, которых нет среди литералов (опечатки или устаревшие строки);
#  * расхождение плейсхолдеров {0} между оригиналом и переводом;
#  * список кандидатов без перевода.
# Использование: powershell -File validate.ps1 <gc_ldstr.tsv> <gc_cand.tsv> <untranslated_out.txt>
param([string]$LdstrFile, [string]$CandFile, [string]$OutFile)

Add-Type -AssemblyName System.Web.Extensions
$json = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$json.MaxJsonLength = 100MB

# Хэш-таблицы PowerShell по умолчанию не различают регистр, а "BUILD" и "Build" — разные строки.
$all = New-Object System.Collections.Hashtable ([StringComparer]::Ordinal)
Get-Content $LdstrFile -Encoding UTF8 | ForEach-Object {
    $p = $_ -split "`t", 3
    $s = $p[2] -replace '\\n', "`n" -replace '\\r', "`r" -replace '\\t', "`t" -replace '\\\\', '\'
    $all[$s] = 1
}

$keys = New-Object System.Collections.Hashtable ([StringComparer]::Ordinal)
$dir = Join-Path $PSScriptRoot '..\translations'
# display_*.json и game_*.json — текст игры и экранные подписи, их в литералах мода нет.
foreach ($f in Get-ChildItem $dir -Filter gc_*.json) {
    $map = $json.DeserializeObject([IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8))
    foreach ($k in $map.Keys) {
        $plain = if ($k -match '^[\w/<>.`]+::[^|]+\|') { $k.Substring($k.IndexOf('|') + 1) } else { $k }
        $keys[$plain] = 1
        if (-not $all.ContainsKey($plain)) { "UNKNOWN KEY [$($f.Name)]: $k" }
        $a = ([regex]::Matches($plain, '\{\d+[^}]*\}') | ForEach-Object Value | Sort-Object) -join ' '
        $b = ([regex]::Matches($map[$k], '\{\d+[^}]*\}') | ForEach-Object Value | Sort-Object) -join ' '
        if ($a -ne $b -and $b -ne '') {
            # перевод может опускать аргумент (например, окончание множественного числа), но не добавлять новый
            $extra = ([regex]::Matches($map[$k], '\{\d+[^}]*\}') | ForEach-Object Value) | Where-Object { $a -notmatch [regex]::Escape($_) }
            if ($extra) { "PLACEHOLDER [$($f.Name)]: $k  ->  $($map[$k])" }
        }
    }
}

Get-Content $CandFile -Encoding UTF8 | ForEach-Object {
    $p = $_ -split "`t", 3
    $s = $p[2] -replace '\\n', "`n" -replace '\\r', "`r" -replace '\\t', "`t" -replace '\\\\', '\'
    if (-not $keys.ContainsKey($s)) { "{0}`t{1}" -f (($p[0] -split ',')[0]), $p[2] }
} | Out-File $OutFile -Encoding utf8
