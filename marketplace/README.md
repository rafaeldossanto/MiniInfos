# Pacotes da comunidade

Este guia é para quem cria temas e mascotes. Para usar o aplicativo, baixe o `MiniInfos-Setup.exe` da última versão, como explicado no [README principal](../README.md). A release `catalog` é usada pelo app e não contém o instalador.

O MiniInfos aceita temas e mascotes criados por outras pessoas. Um pacote é um arquivo `.miniinfos` ou `.zip`, contendo `manifest.json` na raiz e seus arquivos PNG. O formato é público e versionado; instalar um novo visual não exige recompilar o aplicativo.

## Usar e compartilhar

- **Temas:** Configurações → Aparência → **Adicionar tema**. O tema importado é aplicado e aparece junto das opções instaladas.
- **Mascotes:** Configurações → Mascote → **Adicionar mascote**. A seleção troca a personagem da área de trabalho, mantendo o tamanho escolhido e as interações.
- **Exportar:** exporta o pacote selecionado, com sua autoria e licença. Com uma opção embutida selecionada, cria um modelo editável.
- **Remover:** remove o pacote da biblioteca. Ao remover a seleção ativa, o aplicativo volta ao tema embutido escolhido ou à Coruja de Atena.

Os pacotes instalados ficam em `%LOCALAPPDATA%\MiniInfos\packages\themes` e `mascots`. As escolhas ficam salvas nas preferências e valem para todos os cartões. Os temas preservam as fontes que o usuário escolheu separadamente. Um pacote ausente ou inválido na próxima abertura usa o visual embutido como alternativa.

A distribuição funciona por arquivos compartilháveis: autores podem publicá-los nos seus próprios repositórios, páginas ou comunidades. O MiniInfos importa os arquivos escolhidos pelo usuário.

O catálogo público fica neste mesmo repositório, em `marketplace/packages/`. A página **Comunidade** do aplicativo consulta a publicação de tag fixa `catalog`, que é um pré-lançamento e nunca substitui a release latest do app.

## Publicar no catálogo

Você também pode usar **Comunidade → Enviar criação** no site do MiniInfos para preparar e testar o pacote sem editar JSON. Quando o serviço de envio estiver habilitado, o formulário recebe os arquivos e abre a revisão automaticamente; não é necessário fazer um fork ou abrir o PR manualmente. O validador do aplicativo e a aprovação do mantenedor continuam obrigatórios antes da publicação. A prévia e a exportação local funcionam mesmo com o envio indisponível. O processo manual abaixo continua disponível.

1. Crie e teste seu pacote seguindo o [guia do formato abaixo](#criar-seu-primeiro-pacote). Use arte própria ou que você tenha permissão para redistribuir; mantenha os créditos e cumpra a licença das fontes usadas.
2. Faça um fork deste repositório e adicione `marketplace/packages/<id>/`, com `manifest.json`, PNGs e, se necessário, créditos em `README.md` ou um arquivo de licença. O nome da pasta precisa ser igual ao ID do manifesto.
3. Preencha autor, versão e uma licença real. `UNLICENSED` é apenas o marcador do modelo. Um pacote tem até 8 MB compactado; IDs são únicos entre temas e mascotes.
4. Rode `validate` com a [ferramenta do catálogo](#ferramenta-do-catálogo), importe o `.miniinfos` no MiniInfos e confira o resultado.
5. Abra um pull request. O CI compila o validador do mesmo commit, valida os pacotes alterados e confere que as versões aumentaram em relação à publicação atual. Para atualizar um pacote, mantenha seu ID e aumente a versão.
6. Aguarde a revisão do mantenedor. Após o merge na `main`, o CI gera os pacotes, as prévias e o índice e atualiza a release `catalog`. Os usuários baixam apenas as prévias visíveis e os pacotes que escolherem.

Para pedir remoção, abra uma issue identificando o ID do pacote e o motivo, com informações de autoria ou licença quando aplicável. Um PR que remova a pasta também passa por revisão. Depois do merge, os assets removidos saem do catálogo; instalações locais existentes permanecem no computador do usuário.

Contribuições apenas em `marketplace/` executam as checagens da ferramenta do catálogo e não a bateria de testes do aplicativo. Não é necessário criar outro repositório nem manter um servidor.

## Criar seu primeiro pacote

1. Selecione um tema embutido ou a Coruja de Atena e clique em **Exportar** na página correspondente. Para mascotes, **Exemplo** mostra antes a grade, o que cada linha anima e um trecho do manifesto, e também salva o modelo.
2. Faça uma cópia do arquivo e troque sua extensão para `.zip` para extraí-lo.
3. Edite `manifest.json`. Troque `id`, `name`, `author`, `version`, `license` e `description`. Cada projeto deve usar um ID próprio, como `seunome.corujinha`; o mesmo ID identifica futuras atualizações.
4. Altere as cores e imagens ou desenhe sua personagem na grade PNG. O modelo de mascote já traz todos os estados e a anatomia.
5. Compacte o **conteúdo** da pasta, mantendo `manifest.json` na raiz. Use o script abaixo ou seu compactador preferido.
6. Importe o resultado no MiniInfos e confira todas as animações e interações antes de compartilhar.

```powershell
./marketplace/pack.ps1 -Source C:\meus-pacotes\corujinha -Output C:\meus-pacotes\corujinha.miniinfos
```

O modelo usa `UNLICENSED` como marcador. Preencha a autoria e a licença do seu pacote antes de publicá-lo, inclusive a identificação das ilustrações que utilizar. O MiniInfos exibe autor, versão e licença no tooltip da opção instalada.

## Manifesto, versão 1

```json
{
  "schemaVersion": 1,
  "kind": "Theme",
  "id": "seunome.oceano",
  "name": "Oceano",
  "author": "Seu nome",
  "version": "1.0.0",
  "license": "CC0-1.0",
  "description": "Tons de azul e verde para o MiniInfos.",
  "theme": {
    "base": "Simple",
    "colors": {
      "CardBackground": "#14262D",
      "PrimaryText": "#F0F8FA",
      "Accent": "#38C9BA"
    }
  }
}
```

`schemaVersion`, `kind`, `id`, `name`, `author`, `version` e `license` são obrigatórios. `kind` é `Theme` ou `Mascot`. Cada manifesto deve conter somente a definição correspondente, `theme` ou `mascot`. `description` e `$schema` são opcionais. O campo `$schema` é apenas uma indicação para o editor; o aplicativo não acessa seu endereço.

O ID tem de 3 a 80 caracteres: letras minúsculas, números, pontos, hífens e sublinhados. Não pode começar com `miniinfos.`, terminar com ponto ou usar nomes de dispositivos do Windows. A versão segue o formato numérico `1.0.0`. Campos desconhecidos e versões de formato não suportadas são rejeitados. O [JSON Schema](manifest.schema.json) descreve o contrato canônico para editores.

## Temas

`theme.base` escolhe a base: `Simple`, `Light` ou `Classic`. As cores não informadas são herdadas dela. Os valores de `theme.colors` aceitam somente `#RRGGBB` e `#AARRGGBB`.

Chaves disponíveis:

| Grupo | Chaves |
| --- | --- |
| Cartão e textos | `CardBackground`, `CardBorder`, `PrimaryText`, `DimText`, `Divider` |
| Barras | `Track`, `Accent`, `AccentText`, `Warning`, `Danger` |
| Seleção e ícones | `Hover`, `Selected`, `SelectedBorder`, `IconOther` |
| Menus | `MenuBackground`, `MenuBorder`, `MenuHighlight`, `MenuDisabled` |

Para uma ilustração no cabeçalho, inclua um PNG e informe `headerImage`, como `images/header.png`. `headerHeight` aceita de 44 a 160 unidades e `headerTitleSize`, de 10 a 32. Esses dois valores são aplicados quando há `headerImage`; seus padrões são 96 e 20. A ilustração permanece centralizada também na tela dividida.

Há um exemplo pronto em [examples/ocean/manifest.json](examples/ocean/manifest.json).

## Mascotes

O PNG é uma grade de células de tamanho igual, com transparência. Cada célula é apresentada num canvas de 320 × 320 unidades. Desenhe os quadros com alinhamento consistente e espaço transparente suficiente para a personagem não encostar nas bordas. As dimensões da imagem precisam ser divisíveis por `columns` e `rows`.

```json
{
  "schemaVersion": 1,
  "kind": "Mascot",
  "id": "seunome.companheiro",
  "name": "Companheiro",
  "author": "Seu nome",
  "version": "1.0.0",
  "license": "UNLICENSED",
  "mascot": {
    "spriteSheet": "sprites.png",
    "columns": 4,
    "rows": 2,
    "states": {
      "idle": { "row": 0, "durationsMs": [1400, 120, 100, 120], "blendMs": 100 },
      "active": { "row": 1, "frameDurationMs": 100, "blendMs": 0 }
    }
  }
}
```

Cada estado informa `row`, começando em zero. Sem `frames`, usa todas as colunas na ordem. Um vetor `frames`, como `[0, 1, 2, 1]`, escolhe as colunas da sequência e permite repetir quadros. `durationsMs` define uma duração por quadro da sequência; sem ele, `frameDurationMs` vale para todos, com padrão de 160 ms. `blendMs` define a transição para o próximo quadro; zero interpola durante toda a duração do quadro. O padrão é 120 ms, limitado à duração do quadro.

| Estado | Uso |
| --- | --- |
| `idle` | Repouso; obrigatório. |
| `active` | Aumento de tokens ou reação a um toque. |
| `alert` | Uso atual de uma janela em 90% ou mais. |
| `sleep` | Dois minutos sem novos tokens. |
| `protectEyes` | Toque rápido numa região de olho. |
| `heldHead` | Segurando pela cabeça ou por um olho. |
| `heldLeftWing`, `heldRightWing` | Segurando pelos lados. |
| `heldFeet` | Segurando pelos pés. |
| `heldBody` | Segurando pelo corpo. |

Estados ausentes usam `idle`. `protectEyes` usa `active` quando disponível. Sem `heldFeet`, o aplicativo vira a animação de `active` ou `idle` de cabeça para baixo. Sem `heldRightWing`, uma pose `heldLeftWing` disponível é espelhada. Quando uma pose lateral não existe, o ponto original clicado é preservado.

### Anatomia e física

`mascot.anatomy` é opcional. Sem ela, usa a anatomia da Coruja de Atena. Para outras proporções, edite os dados que o modelo exportado já inclui:

```json
{
  "regions": [
    { "part": "leftEye", "x": 120, "y": 150, "radiusX": 20, "radiusY": 25 },
    { "part": "rightEye", "x": 195, "y": 150, "radiusX": 20, "radiusY": 25 },
    { "part": "head", "x": 160, "y": 120, "radiusX": 80, "radiusY": 90 },
    { "part": "leftWing", "x": 70, "y": 220, "radiusX": 35, "radiusY": 70 },
    { "part": "rightWing", "x": 250, "y": 220, "radiusX": 35, "radiusY": 70 },
    { "part": "feet", "x": 160, "y": 290, "radiusX": 50, "radiusY": 20 },
    { "part": "body", "x": 160, "y": 220, "radiusX": 85, "radiusY": 70 }
  ],
  "anchors": {
    "heldHead": { "x": 160, "y": 80 },
    "heldLeftWing": { "x": 80, "y": 40 },
    "heldRightWing": { "x": 240, "y": 40 },
    "heldFeet": { "x": 160, "y": 25 },
    "heldBody": { "x": 160, "y": 225 }
  },
  "massCenter": { "x": 160, "y": 220 },
  "upsideDownMassCenter": { "x": 160, "y": 100 }
}
```

As regiões são elipses, testadas na ordem do vetor; coloque olhos e partes específicas antes do corpo. Uma área sem região usa a divisão padrão de cabeça, lados, corpo e pés. Os pontos ficam entre 0 e 320, independentemente da resolução do PNG. As âncoras posicionam as poses laterais; cabeça e corpo preservam o ponto clicado, e os pés refletem esse ponto na pose invertida. O centro de massa controla o balanço e o giro. A captura do mouse, a física, a transparência e as mudanças de tamanho continuam sendo responsabilidade do MiniInfos.

Ao soltar, o MiniInfos aplica uma queda curta perto do cursor, conservando a posição, a inclinação e parte do movimento. O contato e a recuperação variam conforme o mascote chega pelos pés, de lado ou de cabeça para baixo. Essa animação reaproveita as poses existentes (`active` e, para proteger a cabeça, `protectEyes`) e seus substitutos; não exige novos estados nem alterações nos pacotes atuais.

## Limites e compatibilidade

- Até 128 entradas no ZIP e 64 MB descompactados, com até 32 MB por arquivo.
- PNG de até 16.777.216 pixels, com cada dimensão de até 8192 pixels.
- Grade de até 16 colunas e 10 linhas; sequências de até 32 quadros e durações de 16 a 60000 ms.
- São aceitos JSON, PNG, Markdown e texto. Caminhos absolutos, navegação para fora do pacote, nomes de dispositivos, links e arquivos executáveis não fazem parte do formato.
- O aplicativo não carrega scripts, DLLs, XAML ou recursos remotos de pacotes. Tema e mascote são dados declarativos.
- Uma atualização usa o mesmo ID e substitui o pacote validado anterior. Uma importação inválida preserva a instalação existente.

Para ampliar o contrato no futuro, incremente `schemaVersion` e mantenha uma leitura compatível com os pacotes anteriores. Adicionar novos estados, propriedades ou recursos deve acompanhar documentação, validação e testes do domínio `Community` e de seus consumidores.

## Ferramenta do catálogo

`tools/MiniInfos.Catalog` valida pastas de pacotes e prepara os arquivos de um catálogo público, sem fazer upload. A ferramenta usa Windows e .NET 10 e compartilha o validador, a compactação e o gerador de prévias do aplicativo.

Cada pacote-fonte fica em `packages/<id>/`, com `manifest.json` e seus arquivos. A pasta precisa ter o mesmo nome do ID. A licença deve estar preenchida e ser diferente de `UNLICENSED`, e o arquivo compactado precisa ter até 8 MB.

```powershell
dotnet run --project tools/MiniInfos.Catalog -c Release -- validate C:\catalogo\packages\autor.mascote
dotnet run --project tools/MiniInfos.Catalog -c Release -- build C:\catalogo\packages C:\catalogo\saida
dotnet run --project tools/MiniInfos.Catalog -c Release -- build C:\catalogo\packages C:\catalogo\saida --previous C:\catalogo\anterior\index.json
```

`validate` retorna código diferente de zero e uma mensagem em português se encontrar um problema. `build` gera um `.miniinfos` e uma prévia PNG de 128 × 128 por pacote, além de `index.json`. Os arquivos do ZIP são ordenados e têm datas fixas: os mesmos conteúdos geram o mesmo SHA-256. O índice registra a data da geração e inclui os tamanhos e SHA-256 de pacotes e prévias. Seu contrato está em [catalog.schema.json](catalog.schema.json); o formato inicial usa `schemaVersion: 1`, até 2000 entradas e no máximo 1 MB.

Com `--previous`, todo pacote já presente no índice anterior precisa ter uma versão maior, comparada como versão numérica. O comando recusa a mesma versão ou uma menor antes de escrever a saída. A pasta de saída precisa ficar fora da árvore dos pacotes-fonte. A ferramenta não entra no instalador.
