param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$catalogPath = Join-Path $ProjectRoot 'Assets\Cards\CardCatalog.asset'
$outputPath = Join-Path $ProjectRoot 'Assets\Resources\Shop\PackCatalog.json'

$catalogVersion = 2
$catalogSeed = 23082026
$packPrice = 25
$preferredPackSize = 82
$minimumPackSize = 40
$maximumPackSize = 85

function Decode-UnityYamlString([string]$value) {
    if ($null -eq $value) { return '' }
    $result = $value.Trim()
    if ($result.Length -ge 2 -and $result[0] -eq '"' -and $result[$result.Length - 1] -eq '"') {
        $result = $result.Substring(1, $result.Length - 2)
    }
    $result = [regex]::Replace($result, '\\x([0-9A-Fa-f]{2})', {
        param($match)
        [char][Convert]::ToInt32($match.Groups[1].Value, 16)
    })
    $result = [regex]::Replace($result, '\\u([0-9A-Fa-f]{4})', {
        param($match)
        [char][Convert]::ToInt32($match.Groups[1].Value, 16)
    })
    return $result.Replace('\"', '"').Replace('\\n', ' ').Replace('\\\\', '\')
}

function Normalize-ThemeText([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return '' }
    $decomposed = $value.Normalize([Text.NormalizationForm]::FormD)
    $builder = New-Object Text.StringBuilder
    foreach ($character in $decomposed.ToCharArray()) {
        $category = [Globalization.CharUnicodeInfo]::GetUnicodeCategory($character)
        if ($category -ne [Globalization.UnicodeCategory]::NonSpacingMark) {
            [void]$builder.Append($character)
        }
    }
    return ([regex]::Replace($builder.ToString().ToUpperInvariant(), '[^A-Z0-9]+', ' ')).Trim()
}

$stopWords = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
@(
    'THE','OF','AND','A','AN','TO','IN','ON','FOR','FROM','WITH','BY','AT','AS','IS',
    'O','A','OS','AS','DE','DO','DA','DOS','DAS','E','EM','NO','NA','NOS','NAS','COM','PARA',
    'QUE','POR','PELO','PELA','PELOS','PELAS','OU','SE','NAO','SIM','MAIS','MENOS','COMO','SEM',
    'SER','ESTAR','FOI','ERA','SAO','TEM','TER','QUANDO','ENQUANTO','ATE','APOS','ANTES','ENTRE',
    'CARD','CARDS','MONSTER','MONSTERS','SPELL','TRAP','TOKEN','NUMBER','NO','LORD','KING',
    'DRAGON','WARRIOR','MAGICIAN','BEAST','DARK','LIGHT','BLACK','WHITE','RED','BLUE',
    'NOVA','NEW','GREAT','LITTLE','MASTER','MAX','NEO','SUPER','ULTRA','TRUE','CYBER',
    'FIELD','DECK','HAND','GRAVEYARD','OPPONENT','PLAYER','EFFECT','SUMMON','SPECIAL','TARGET',
    'CAMPO','BARALHO','MAO','CEMITERIO','OPONENTE','JOGADOR','EFEITO','INVOQUE','ESPECIAL','ALVO',
    # Palavras de regras, tipos e adjetivos genericos nao identificam uma familia de cartas.
    'THIS','THAT','THESE','THOSE','YOU','YOUR','ITS','IT','CAN','CANNOT','MAY','MUST','ONLY','ONCE',
    'ONE','TWO','THREE','FIRST','SECOND','EACH','ALL','ANY','OTHER','ANOTHER','DURING','TURN','PHASE',
    'ATTACK','BATTLE','DAMAGE','DESTROY','DESTROYED','BANISH','BANISHED','SEND','SENT','ADD','DRAW',
    'CONTROL','CONTROLS','FACE','FACEUP','FACEDOWN','POSITION','LEVEL','RANK','LINK','MATERIAL','ZONE',
    'NORMAL','RITUAL','FUSION','SYNCHRO','XYZ','PENDULUM','CONTINUOUS','QUICK','EQUIP','COUNTER',
    'FIRE','WATER','EARTH','WIND','DIVINE','FAIRY','FIEND','MACHINE','PSYCHIC','ZOMBIE','DINOSAUR',
    'AQUA','INSECT','PLANT','ROCK','REPTILE','THUNDER','FISH','SEA','SERPENT','WINGED',
    'ALLY','JUSTICE','SWORD','FINAL','END','WORLD','UNIVERSE','STAR','KNIGHT','HERO','EXTRA',
    'CONTACT','CALL','LIFE','SOUL','DOUBLE','POWER','DEFENSE','DEFENCE','GATE','SPIRIT','ARMOR',
    'ESTE','ESTA','ESSE','ESSA','ISTO','ISSO','VOCE','SEU','SUA','SEUS','SUAS','PODE','APENAS','UMA',
    'UM','DOIS','TRES','PRIMEIRO','SEGUNDO','CADA','TODAS','TODOS','OUTRO','OUTRA','DURANTE','TURNO',
    'FASE','ATAQUE','BATALHA','DANO','DESTRUA','DESTRUIDO','BANA','BANIDO','ENVIE','ENVIADO',
    'ADICIONE','COMPRE','CONTROLE','CONTROLAR','FACE','CIMA','BAIXO','POSICAO','NIVEL','MATERIA',
    'ZONA','NORMAL','RITUAL','FUSAO','SINCRO','PENDULO','CONTINUA','RAPIDA','EQUIPAMENTO',
    'FOGO','AGUA','TERRA','VENTO','DIVINO','FADA','DEMONIO','MAQUINA','PSIQUICO','ZUMBI',
    'DINOSSAURO','AQUA','INSETO','PLANTA','ROCHA','REPTIL','TROVAO','PEIXE','SERPENTE','ALADA',
    'ALIADO','JUSTICA','ESPADA','FINAL','FIM','MUNDO','UNIVERSO','ESTRELA','CAVALEIRO','HEROI',
    'GRANDE','PEQUENO','NEGRO','NEGRA','BRANCO','BRANCA','VERMELHO','VERMELHA','AZUL','LUZ','TREVAS',
    'FORCA','DEFESA','PORTAO','ESPIRITO','ALMA','VIDA','DUPLO','CHAMADO','CONTATO','PODER',
    'MONSTRO','MONSTROS','MAGIA','MAGIAS','ARMADILHA','ARMADILHAS','CARTA','CARTAS'
) | ForEach-Object { [void]$stopWords.Add($_) }

function Get-NameTokens([string]$name) {
    $normalizedName = Normalize-ThemeText $name
    return @($normalizedName -split ' ' | Where-Object {
        $_.Length -ge 3 -and -not $stopWords.Contains($_) -and $_ -notmatch '^\d+$'
    })
}

function New-CardRecord {
    return [ordered]@{
        stableId = ''
        displayName = ''
        englishName = ''
        rarity = 0
        category = 0
        monsterFrame = 0
        officialCardId = ''
        typeName = ''
        raceName = ''
        effectText = ''
        officiallyRegistered = 0
        needsManualReview = 0
    }
}

$cards = New-Object Collections.Generic.List[object]
$current = $null
$multilineKey = ''
$multilineValue = ''
foreach ($line in [IO.File]::ReadLines($catalogPath)) {
    if (-not [string]::IsNullOrEmpty($multilineKey)) {
        $multilineValue += ' ' + $line.Trim()
        if ($line.TrimEnd().EndsWith('"')) {
            $current[$multilineKey] = Decode-UnityYamlString $multilineValue
            $multilineKey = ''
            $multilineValue = ''
        }
        continue
    }
    if ($line -match '^\s*-\s+stableId:\s*(.+?)\s*$') {
        if ($null -ne $current) { $cards.Add([pscustomobject]$current) }
        $current = New-CardRecord
        $current.stableId = Decode-UnityYamlString $Matches[1]
        continue
    }
    if ($null -eq $current) { continue }
    if ($line -match '^\s{4}(displayName|englishName|rarity|category|monsterFrame|officialCardId|typeName|raceName|effectText|officiallyRegistered|needsManualReview):\s*(.*?)\s*$') {
        $key = $Matches[1]
        $rawValue = $Matches[2]
        if ($key -eq 'effectText' -and $rawValue.StartsWith('"') -and -not $rawValue.EndsWith('"')) {
            $multilineKey = $key
            $multilineValue = $rawValue
            continue
        }
        $value = Decode-UnityYamlString $rawValue
        if ($key -in @('rarity','category','monsterFrame','officiallyRegistered','needsManualReview')) {
            $current[$key] = if ($value -match '^-?\d+$') { [int]$value } else { 0 }
        } else {
            $current[$key] = $value
        }
    }
}
if ($null -ne $current) { $cards.Add([pscustomobject]$current) }

$eligible = @($cards | Where-Object {
    $_.officiallyRegistered -eq 1 -and
    $_.needsManualReview -eq 0 -and
    $_.category -ne 0 -and
    $_.monsterFrame -ne 10 -and
    -not [string]::IsNullOrWhiteSpace($_.officialCardId)
})

$duplicateIds = @($eligible | Group-Object officialCardId | Where-Object Count -gt 1)
if ($duplicateIds.Count -gt 0) {
    throw "O catalogo elegivel contem IDs oficiais duplicados: $($duplicateIds[0].Name)"
}
if ($eligible.Count -lt $minimumPackSize) {
    throw "Catalogo insuficiente para gerar pacotes: $($eligible.Count) cartas."
}

# Frequencias de termos e bigramas identificam familias/arquetipos recorrentes.
# Nomes em portugues e ingles sao usados em conjunto para que referencias presentes
# nos textos de efeito aproximem suportes de suas familias mesmo quando o nome da
# propria carta nao contem o nome do arquetipo.
$termFrequency = @{}
$cardTerms = @{}
foreach ($card in $eligible) {
    $terms = New-Object Collections.Generic.List[string]
    foreach ($sourceName in @($card.englishName, $card.displayName)) {
        $tokens = @(Get-NameTokens $sourceName)
        for ($index = 0; $index -lt $tokens.Count; $index++) {
            $terms.Add($tokens[$index])
            if ($index + 1 -lt $tokens.Count) { $terms.Add("$($tokens[$index]) $($tokens[$index + 1])") }
        }
    }
    $uniqueTerms = @($terms | Select-Object -Unique)
    $cardTerms[$card.officialCardId] = $uniqueTerms
    foreach ($term in $uniqueTerms) {
        if (-not $termFrequency.ContainsKey($term)) { $termFrequency[$term] = 0 }
        $termFrequency[$term]++
    }
}

foreach ($card in $eligible) {
    $nameCandidates = @($cardTerms[$card.officialCardId] | Where-Object {
        $termFrequency[$_] -ge 2 -and $termFrequency[$_] -le $maximumPackSize
    })
    $effectTokens = @(Get-NameTokens $card.effectText)
    $effectTerms = New-Object Collections.Generic.List[string]
    for ($index = 0; $index -lt $effectTokens.Count; $index++) {
        $effectTerms.Add($effectTokens[$index])
        if ($index + 1 -lt $effectTokens.Count) {
            $effectTerms.Add("$($effectTokens[$index]) $($effectTokens[$index + 1])")
        }
    }
    $effectCandidates = @($effectTerms | Select-Object -Unique | Where-Object {
        $termFrequency.ContainsKey($_) -and
        $termFrequency[$_] -ge 2 -and
        $termFrequency[$_] -le $maximumPackSize
    })
    $candidates = @($nameCandidates + $effectCandidates | Select-Object -Unique)
    $theme = $candidates | Sort-Object @(
        @{ Expression = { if ($_ -match ' ') { 0 } else { 1 } }; Ascending = $true },
        @{ Expression = { if ($nameCandidates -contains $_) { 0 } else { 1 } }; Ascending = $true },
        @{ Expression = { $termFrequency[$_] }; Ascending = $false },
        @{ Expression = { $_ }; Ascending = $true }
    ) | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($theme)) {
        $race = Normalize-ThemeText $card.raceName
        $type = Normalize-ThemeText $card.typeName
        $theme = if (-not [string]::IsNullOrWhiteSpace($race)) { "RACA $race" } elseif (-not [string]::IsNullOrWhiteSpace($type)) { "TIPO $type" } else { "CATEGORIA $($card.category)" }
    }
    Add-Member -InputObject $card -NotePropertyName theme -NotePropertyValue $theme -Force
}

# Mantem cada familia contigua e aproxima familias menores por funcao de jogo.
# Isso evita que o preenchimento matematico dos pacotes misture temas apenas por
# estarem proximos alfabeticamente.
$themeGroups = @($eligible | Group-Object theme | ForEach-Object {
    $groupCards = @($_.Group)
    $dominantRace = ($groupCards | Where-Object { -not [string]::IsNullOrWhiteSpace($_.raceName) } |
        Group-Object raceName | Sort-Object @(
            @{ Expression = { $_.Count }; Descending = $true },
            @{ Expression = { $_.Name }; Ascending = $true }
        ) | Select-Object -First 1).Name
    $dominantType = ($groupCards | Where-Object { -not [string]::IsNullOrWhiteSpace($_.typeName) } |
        Group-Object typeName | Sort-Object @(
            @{ Expression = { $_.Count }; Descending = $true },
            @{ Expression = { $_.Name }; Ascending = $true }
        ) | Select-Object -First 1).Name
    $dominantCategory = ($groupCards | Group-Object category |
        Sort-Object @(
            @{ Expression = { $_.Count }; Descending = $true },
            @{ Expression = { $_.Name }; Ascending = $true }
        ) | Select-Object -First 1).Name
    [pscustomobject]@{
        theme = $_.Name
        race = Normalize-ThemeText $dominantRace
        type = Normalize-ThemeText $dominantType
        category = [int]$dominantCategory
        cards = $groupCards
    }
})

$orderedCards = @(
    $themeGroups |
        Sort-Object @(
            @{ Expression = { $_.category }; Ascending = $true },
            @{ Expression = { $_.race }; Ascending = $true },
            @{ Expression = { $_.type }; Ascending = $true },
            @{ Expression = { $_.theme }; Ascending = $true }
        ) |
        ForEach-Object {
            $_.cards | Sort-Object @(
                @{ Expression = { $_.englishName }; Ascending = $true },
                @{ Expression = { $_.officialCardId }; Ascending = $true }
            )
        }
)

$packCount = [Math]::Ceiling($orderedCards.Count / [double]$preferredPackSize)
while ([Math]::Floor($orderedCards.Count / [double]$packCount) -lt $minimumPackSize) { $packCount-- }
while ([Math]::Ceiling($orderedCards.Count / [double]$packCount) -gt $maximumPackSize) { $packCount++ }

$baseSize = [Math]::Floor($orderedCards.Count / [double]$packCount)
$largerPackCount = $orderedCards.Count - ($baseSize * $packCount)
$packSizes = @(for ($index = 0; $index -lt $packCount; $index++) {
    if ($index -lt $largerPackCount) { $baseSize + 1 } else { $baseSize }
})

function Format-Theme([string]$theme) {
    $clean = $theme -replace '^(RACA|TIPO|CATEGORIA)\s+', ''
    $culture = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    return $culture.TextInfo.ToTitleCase($clean.ToLowerInvariant())
}

function Get-ContentHash([string]$packId, [string[]]$ids) {
    $payload = "$packId|$packPrice|$([string]::Join(',', $ids))"
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
        return -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('X2') })
    } finally {
        $sha.Dispose()
    }
}

$packs = New-Object Collections.Generic.List[object]
$offset = 0
for ($packIndex = 0; $packIndex -lt $packCount; $packIndex++) {
    $size = $packSizes[$packIndex]
    $slice = @($orderedCards[$offset..($offset + $size - 1)])
    $offset += $size
    $packId = 'thematic-pack-{0:D3}-v2' -f ($packIndex + 1)
    $dominantThemes = @(
        $slice |
            Group-Object theme |
            Sort-Object @(
                @{ Expression = { $_.Count }; Descending = $true },
                @{ Expression = { $_.Name }; Ascending = $true }
            ) |
            Select-Object -First 3
    )
    $themeLabels = @($dominantThemes | ForEach-Object { Format-Theme $_.Name })
    $displayTheme = [string]::Join(' & ', $themeLabels[0..([Math]::Min(1, $themeLabels.Count - 1))])
    $cardIds = @($slice | ForEach-Object { $_.officialCardId })
    $previewIds = @($slice | Sort-Object @(
        @{ Expression = { $_.rarity }; Descending = $true },
        @{ Expression = { $_.displayName }; Ascending = $true },
        @{ Expression = { $_.officialCardId }; Ascending = $true }
    ) | Select-Object -First 3 | ForEach-Object { $_.officialCardId })
    $packs.Add([ordered]@{
        packId = $packId
        displayName = "Pacote $displayTheme"
        description = "Selecao tematica de $size cartas: $([string]::Join(', ', $themeLabels))."
        cardIds = $cardIds
        previewCardIds = $previewIds
        priceCoins = $packPrice
        origin = 1
        generationBatchId = 'thematic-v2-23082026'
        generatorVersion = 2
        contentLockedAfterPublish = $true
        contentHash = Get-ContentHash $packId $cardIds
        countsForAutoCoverage = $true
        published = $true
        manualVisualOverride = $false
        needsPreviewReview = $false
    })
}

$root = [ordered]@{
    version = $catalogVersion
    seed = $catalogSeed
    packs = $packs
}
$json = $root | ConvertTo-Json -Depth 8
$utf8WithoutBom = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText($outputPath, $json + [Environment]::NewLine, $utf8WithoutBom)

$assigned = @($packs | ForEach-Object { $_.cardIds })
if ($assigned.Count -ne $eligible.Count -or @($assigned | Select-Object -Unique).Count -ne $eligible.Count) {
    throw 'Falha de cobertura: a distribuicao final nao e bijetiva.'
}

Write-Output ("Catalogo v{0}: {1} cartas em {2} pacotes ({3}-{4} cartas), cobertura exata." -f `
    $catalogVersion, $eligible.Count, $packCount, ($packSizes | Measure-Object -Minimum).Minimum, ($packSizes | Measure-Object -Maximum).Maximum)
