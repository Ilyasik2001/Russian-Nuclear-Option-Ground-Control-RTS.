# Отбирает из дампа ldstr (DllStrings ldstr) строки, похожие на текст интерфейса.
# Использование: powershell -File candidates.ps1 <gc_ldstr.tsv> <out.tsv>
param([string]$InFile, [string]$OutFile)

# Классы, где строки служебные: настройки, сеть, тесты, профилировщики, файлы.
$skipClass = '^(CommanderSettings|CommanderNetwork|CommanderFixTest|CommanderSoakTest|CommanderSupplySoakTest|CommanderNavalTest|CommanderTakeoffTest|CommanderUiShot|CommanderFrameProfiler|CommanderGameProfilerPatches|CommanderServiceRegistryCheck|CommanderServiceRegistry|CommanderMissionInstaller|CommanderPriceProfile|CommanderSessionLog|CommanderAirCrashRecorder|CommanderTakeoffRecorderPatches|CommanderLocalHeightMapBaker|CommanderStrategicHeightMap|CommanderScheduler)(/|::)'

# Куда строка уходит — если сюда, это идентификатор, а не текст.
$skipCall = '^(AccessTools|Traverse|Harmony|HarmonyMethod|Path|Directory|File|Type|Shader|Transform|Enum|CultureInfo|Material|MaterialPropertyBlock|CommandBuffer|Regex|Input|Player|CommanderGameInput|CommanderFrameProfiler|CommanderScheduler|CommanderNetwork|CommanderSettings|CommanderServiceRegistryCheck|Debug|ManualLogSource|Logger)::|^String::(op_Equality|op_Inequality|Equals|StartsWith|EndsWith|IndexOf|Contains|Replace|Split|ToUpperInvariant)$|^generic::(get_Item|set_Item|TryGetValue|ContainsKey|Remove|get_Key)$|^Object::set_name$|^GameObject::\.ctor$|^(Int32|Single|Double|Int64)::ToString$|::(LogOnce|TraceEatenPress|ContainsWeaponToken|ContainsAll|ContainsToken|FindInCatalog|OverrideInt|OverrideBool|Override|HasTag|IsHiddenTag|SetTag|SetValue|GetValue|FollowInMatch|Log|Find|Expect|Slot|Stagger)$'

$rows = Get-Content $InFile -Encoding UTF8 | ForEach-Object {
    $p = $_ -split "`t", 3
    [pscustomobject]@{ M = $p[0]; C = $p[1]; S = $p[2] }
}

function Test-UiText([string]$s) {
    if ($s -notmatch '[A-Za-z]{2,}') { return $false }
    if ($s -match '^[A-Za-z_][A-Za-z0-9_]*$') {
        # одно слово: оставляем КАПС и Обычные слова, отсекаем camelCase / PascalCase / snake_case
        if ($s -cmatch '^[A-Z][A-Z0-9]*$') { return $true }
        if ($s -cmatch '^[A-Z]?[a-z]+$') { return $true }
        return $false
    }
    if ($s -match '^[\w.]+\.[\w.]+$') { return $false }          # a.b.c, файлы
    if ($s -match '^[A-Za-z0-9_]+[:/\\][A-Za-z0-9_:/\\.]*$') { return $false }  # пути, ключи
    if ($s -match '^<\/?[a-z]+[^>]*>$') { return $false }        # одиночный rich-text тег
    return $true
}

$ok = $rows | Where-Object { $_.M -notmatch $skipClass -and $_.C -notmatch $skipCall -and (Test-UiText $_.S) }
$ok | Group-Object S -CaseSensitive | Sort-Object { $_.Group[0].M } | ForEach-Object {
    $where = ($_.Group | ForEach-Object { $_.M } | Select-Object -Unique) -join ','
    $calls = ($_.Group | ForEach-Object { $_.C } | Select-Object -Unique) -join ','
    "{0}`t{1}`t{2}" -f $where, $calls, $_.Name
} | Out-File $OutFile -Encoding utf8
