# MiniInfos

**Para usar, escolha o pacote do seu sistema na última release publicada.** Windows usa `MiniInfos-Setup.exe`; macOS tem DMG para Apple Silicon e Intel; Linux tem `.tar.gz` para x64 e ARM64, com instalador por usuário. Os downloads do site são ativados quando cada arquivo é publicado. O catálogo de mascotes e temas é separado dos pacotes do app, mesmo estando no mesmo repositório. O código de Linux/macOS e os detalhes de distribuição estão em [desktop/README.md](desktop/README.md).

Um quadradinho flutuante, sempre por cima das outras janelas, com uma página para cada IA: Claude, Codex (GPT), Gemini e Kimi. Pra trocar de página: as setinhas ou os ícones das IAs no rodapé, as teclas ←/→, um deslize de dois dedos pro lado no touchpad, ou Shift + roda do mouse. Os ícones são monocromáticos, com o selecionado branco e os demais cinza. A página escolhida fica salva.

Cada página só aparece com o app dela instalado no PC: o app da Microsoft Store, o comando no terminal (`claude`, `codex`, `gemini`, `kimi`), a extensão no VS Code (ou Cursor, Windsurf), o app desktop do Kimi ou o app aberto. Desinstalado, o ícone some em até um minuto, ou na hora com ↻, e volta do mesmo jeito quando o app é instalado de novo. A pasta de dados não conta, porque ela fica no PC depois de desinstalar. Com um app só, o ícone dele continua no rodapé; se nenhum for encontrado, aparecem todas as páginas. Os ícones ficam sempre no centro do cartão, quantos forem.

Botão direito num ícone, **Abrir em**, como no app do Claude:

- **Visualização dividida:** o quadrado mostra as duas páginas lado a lado, com os dois ícones acesos, e fica com o dobro da largura. Cada coluna diz quando os seus limites foram atualizados, e o ↻ atualiza as duas. **Visualização única** (no mesmo menu), ou um clique simples num ícone, volta pra uma página.
- **Nova janela:** abre outro quadrado só com aquela página, com posição e tamanho próprios. Os ícones acesos mostram as páginas abertas em qualquer quadrado visível, então dois quadrados lado a lado acendem os dois ícones, como na visualização dividida. Ele volta junto quando o MiniInfos abre de novo, aparece e some junto com o principal, e o ✕ dele fecha de vez. A bandeja mostra os números do quadrado principal.

Na página do Claude:

- **Janela 5h** e **Semanal**: % usado, barra (azul até 70%, amarela até 90%, vermelha acima), tokens gastos naquela janela e quanto falta pra reiniciar.
- **Tokens hoje** e **Tokens no total**, somando todos os chats do Claude Code (passe o mouse pra ver entrada, cache e saída).
- **Chat atual**: o chat que respondeu por último, com o contexto usado e o custo equivalente de API.

Na página do Codex, o mesmo desenho mostra os percentuais e horários de reinício da conta, os tokens das sessões locais nas duas janelas, os tokens de hoje e do total e a sessão local mais recente. Os totais de tokens vêm dos registros locais do Codex. Passe o mouse para ver o detalhamento. O app não transforma os percentuais do plano em custo em dinheiro.

As páginas do **Gemini** e do **Kimi** mostram só tokens: nenhum dos dois guarda no computador a leitura dos limites da conta, e o MiniInfos não usa credenciais para consultá-los. As duas janelas contam os tokens das últimas 5 horas e dos últimos 7 dias, sem %, e o cabeçalho diz quando chegou a última resposta.

- **Gemini:** as conversas do Gemini CLI (`gemini` no terminal). O site gemini.google.com não deixa registros no PC. A linha de baixo mostra o modelo que respondeu por último.
- **Kimi:** as sessões do Kimi Code (`kimi` no terminal) e o modo agente do app desktop do Kimi, que roda o Kimi Code por dentro. O chat comum do app não deixa registros no PC. A linha de baixo mostra o projeto ou "App Kimi", com o contexto da conversa do agente.

Dois modos de ver, nos botões do canto inferior direito (como no Explorer):

- **Resumido** (quadradinho): 1,4B, 109M.
- **Detalhado** (linhas): o número inteiro, 1.412.345.678, subindo animado feito um odômetro enquanto os tokens são gastos.

O modo escolhido fica salvo.

## Cartão de conversas

O botão ao lado da engrenagem abre um segundo cartão, **Conversas**, com cada chat ou sessão e os tokens que gastou. A conversa que respondeu por último fica no topo, como na fila de temas: o ícone dela fica aceso e, enquanto responde, aparece um ponto azul ao lado de "agora". Cada linha mostra o nome, a pasta e há quanto tempo respondeu, os tokens da conversa (passe o mouse para ver entrada, cache e saída) e o contexto usado, quando a IA informa. Embaixo, o número de conversas e a soma delas. Os ícones das IAs no rodapé filtram a lista; clicar de novo mostra todas. Os botões Resumido e Detalhado funcionam como no cartão de uso. A lista mostra as 50 mais recentes.

- **Claude:** cada chat do Claude Code, com o nome dado no app e o contexto publicado pelo mod.
- **Codex:** cada sessão, com o nome que o app do Codex dá à conversa e o contexto.
- **Kimi:** cada conversa do modo agente do app (com a sessão que só gera o título somada a ela) ou do Kimi Code, nomeada pelo primeiro pedido.
- **Gemini:** cada conversa do Gemini CLI, pela pasta do projeto.

Em **Configurações → Geral → Cartão principal**, escolha **Uso** ou **Conversas**. O principal é o que abre junto com as IAs, com o Windows, pelo clique na bandeja e ao abrir o MiniInfos de novo; o outro abre pelo botão ao lado da engrenagem. Os dois somem juntos quando a última IA fecha. Com Conversas como principal, a faixa de atualização aparece quando o cartão de uso é aberto.

Os tokens por conversa contam a partir das respostas que ainda estão nos históricos: na primeira abertura desta versão, o MiniInfos relê os históricos uma vez para descobrir a conversa de cada resposta, sem contar nenhuma duas vezes. Respostas de históricos já apagados continuam nos totais do cartão de uso, mas não aparecem em nenhuma conversa. No Linux e no macOS, o cartão de conversas ainda não existe; os dados por conversa já são os mesmos.

O aplicativo abre em português quando o idioma de exibição do sistema é português; nos demais idiomas, abre em inglês. Em **Configurações → Geral → Idioma**, escolha **Automático**, **Português** ou **English**. A escolha manual fica salva e atualiza janelas abertas, menus e números sem reiniciar. Ela não muda o tema selecionado. A landing page também detecta o idioma do navegador e oferece a escolha manual.

O instalador também inclui os dois idiomas e usa o idioma de exibição do Windows, com inglês como alternativa. A detecção usa a opção [`LanguageDetectionMethod=uilanguage` do Inno Setup](https://jrsoftware.org/ishelp/topic_setup_languagedetectionmethod.htm); a escolha manual dentro do app continua independente.

Na engrenagem, em **Configurações → Aparência → Tema**, **Simples** usa o cartão escuro com barras azuis; **Clássico** mantém a arte de Atena e o cabeçalho originais. Na **Biblioteca**, **Clássico escuro** e **Clássico claro** são opções adicionais com uma faixa de Atena, disco e oliveiras abaixo dos controles: gravura no escuro e mármore no claro. Atena fica centralizada sobre o cartão inteiro na visualização dividida. Os ícones das IAs ficam juntos, sem separador. A escolha vale para todas as janelas e fica salva; quem já usava Clássico continua com o original. As opções **Claro** e **Windows** também continuam disponíveis.

A fila de temas mostra os quatro escolhidos por último, o atual primeiro, sejam embutidos ou da comunidade. **Biblioteca**, o primeiro botão abaixo dela, abre todos os temas em cartões com prévia, como na página Comunidade: **Usar** aplica o tema e o leva para o início da fila, e **Mais temas na Comunidade** abre o catálogo de temas.

Ele lê os registros locais a cada 3 segundos. Os limites da conta Codex são consultados em segundo plano a cada minuto, só enquanto a página do Codex está na tela, e a tela nunca espera por eles. O botão ↻, ao lado do ✕, força uma consulta na hora da página aberta (também em "Atualizar agora", no botão direito e no ícone da bandeja). Os % do Claude mudam quando algum chat do Claude Code recebe resposta, porque é dela que eles vêm.

Arraste o quadrado pra onde quiser, em qualquer direção. Pra mudar o tamanho, puxe uma borda ou um canto: tudo cresce ou encolhe junto, na mesma proporção. A posição e o tamanho ficam salvos. Botão direito: "Sempre por cima", "Voltar pro canto", "Tamanho padrão", "Esconder" e "Sair do MiniInfos".

## Mascote na área de trabalho

Em **Configurações → Mascote**, ative **Mostrar mascote** para adotar a Coruja de Atena, que vem incluída no aplicativo. Outros mascotes chegam como pacotes, pelo **+** da galeria ou pela página **Comunidade**; a escolha fica salva. O mascote fica numa janela transparente própria, funciona com todos os temas e continua disponível com o cartão escondido. É opcional e começa desativado.

- Clique rapidamente em um dos olhos: o mascote protege o rosto com as asas ou as patas.
- Segure pela cabeça, por um dos braços ou asas, pela barriga ou pelos pés para mover. O mascote fica pendurado pela região escolhida; pelos pés, vira de cabeça para baixo. O ponto segurado acompanha o cursor.
- Enquanto segura, os movimentos do mouse aplicam impulso ao mascote. Movimentos circulares podem fazê-lo dar voltas completas ao redor do ponto segurado; ao parar o mouse, ele continua balançando e perde força aos poucos.
- Ao soltar, ele faz uma queda curta perto do cursor, preservando a posição, a inclinação e parte do embalo. A aterrissagem muda conforme chega pelos pés, de lado ou de cabeça para baixo; depois ele se levanta e fica no novo lugar, que é salvo. Você pode pegá-lo novamente durante a queda.
- Clique duas vezes para mostrar o MiniInfos.
- Escolha Pequeno, Médio ou Grande nas configurações, ou use Alt + roda do mouse sobre o mascote.
- Botão direito: mostrar o cartão, configurar o mascote, voltar pro canto, esconder o mascote ou sair do MiniInfos.

O mascote pisca em repouso, se anima quando a contagem de tokens aumenta, fica atento quando uma janela atual chega a 90% e dorme após dois minutos sem novos tokens. As reações usam as leituras que o MiniInfos já faz, inclusive com o cartão escondido; não identificam pensamento, conclusão ou erro de uma tarefa. O mascote não faz chamadas de IA nem gasta tokens. Ao sair do MiniInfos, ele fecha junto.

Em repouso, o mascote também respira e balança suavemente sem mudar a posição da janela. Cada estado usa oito poses desenhadas, interpoladas a até 60 quadros por segundo em repouso. Durante a interação, a queda e a recuperação, a animação acompanha os quadros de renderização do monitor, sem o limite de 60 Hz. As mudanças de estado partem da imagem que já está na tela, com uma transição de 300 ms. O relógio usa o tempo transcorrido, para não desacelerar a animação quando um quadro demora mais. O processamento para quando a janela do mascote fecha.

`PetInteraction` distingue o clique, um toque segurado de 220 ms e o arraste, identifica a região do mascote e mantém o ponto de apoio. O mouse fica capturado durante o gesto para permitir mover para fora da janela. Ao perder a captura ou soltar um arraste, começa a queda; depois da recuperação, o mascote volta ao estado de uso observado, sem alterar as contagens. As poses de interação da coruja ficam em `widget/Assets/athena-owl-interactions.png`; a asa direita é espelhada em memória, e a pose pelos pés usa uma rotação da animação de asas. Um mascote da comunidade traz as suas no próprio pacote.

`PetPendulum` simula a rotação ao redor do ponto de apoio usando gravidade, aceleração do cursor, inércia e amortecimento. O ângulo não fica limitado a uma pequena inclinação; a integração usa passos de até 1/240 s e limita impulsos de retomadas após pausas da interface. A posição do mouse é normalizada pela escala da coruja e da tela. A aceleração é filtrada e interpolada entre os passos para reduzir trancos. `PetGeometry` mantém as transições de rotação sem achatar a imagem e usa uma área transparente de tamanho estável durante o giro, evitando recriar a superfície da janela a cada ângulo. O ponto de apoio também compensa o arredondamento da posição da janela aos pixels da tela. `PetDrop` calcula a queda a partir da imagem e do movimento reais da soltura, com gravidade, giro amortecido, contato e recuperação conforme a orientação. A distância é proporcional ao tamanho do mascote e limitada pelo espaço até a borda inferior da área de trabalho. A área da janela volta ao normal no fim da recuperação, e somente a posição final é salva.

`PetActivity` cuida das reações, `DesktopPet` acompanha as preferências e mantém uma única `PetWindow`, `PetAppearances` resolve a aparência escolhida e `PetAnimationPlayer` interpola os quadros com transparência premultiplicada. `PetAtlas` compartilha o recorte e o alinhamento das artes embutidas, e `PetSprites` define as poses e os pontos de interação da coruja. A coruja foi gerada para o MiniInfos; os atlas ficam em `widget/Assets/athena-owl-*.png`, com dez estados de oito quadros, incluindo poses derivadas em memória. A posição fica em `%LOCALAPPDATA%\MiniInfos\mascot.json`.

## Temas e mascotes da comunidade

Em **Configurações → Aparência**, use **Adicionar tema** para importar um pacote `.miniinfos` ou `.zip`; em **Mascote**, use **Adicionar mascote**. O conteúdo importado aparece junto das opções instaladas (um tema entra no início da fila e na **Biblioteca**), pode ser selecionado na hora e fica salvo para as próximas aberturas. A seleção é independente para tema e mascote. Autor, versão e licença aparecem no tooltip da opção.

Na biblioteca de mascotes, o primeiro quadrado tem um bonequinho de contorno tracejado com **+** para adicionar um pacote. À direita ficam as miniaturas da Coruja de Atena e dos mascotes instalados; clique para selecionar. O escolhido fica destacado, e os demais ficam um pouco mais transparentes. As opções continuam em novas linhas quando necessário. A biblioteca é local: os pacotes e a seleção ficam salvos no computador, sem conta ou banco de dados online.

**Exemplo**, ao lado de Exportar e Remover, abre o modelo da Coruja de Atena como um pacote o vê: a grade do PNG, com o nome de cada linha no manifesto e o que ela anima (repouso, alerta, segurando pela cabeça…), e um trecho do `manifest.json`. **Salvar modelo** grava o `.miniinfos` completo para desenhar por cima e adicionar pelo **+**.

As miniaturas são recortadas pelo conteúdo visível e salvas em 128 × 128, fora dos pacotes, em `%LOCALAPPDATA%\MiniInfos\previews\mascots`. A instalação gera a prévia, e as Configurações reaproveitam esse PNG tanto na galeria quanto na imagem maior. Se o cache faltar ou estiver corrompido, a prévia é refeita em segundo plano; o quadrado fica vazio até a imagem ficar pronta. A exportação não inclui o cache.

**Exportar** permite compartilhar um pacote instalado. Com um tema ou mascote embutido selecionado, cria um modelo editável com o manifesto e as imagens da opção escolhida; o modelo de mascote inclui todas as poses e os pontos de interação. **Remover** fica disponível para pacotes instalados e volta à alternativa embutida. Um pacote ausente ou inválido não impede abrir o aplicativo.

O formato é aberto, versionado e feito de JSON e PNG. Autores podem criar suas próprias paletas, cabeçalhos, animações e anatomia do mascote, aproveitando a física e as interações do MiniInfos. Os arquivos são compartilháveis pelos canais da comunidade e importados localmente; o aplicativo não executa scripts ou XAML dos pacotes. Veja o [guia de criação](marketplace/README.md), o [JSON Schema](marketplace/manifest.schema.json) e o [tema Oceano de exemplo](marketplace/examples/ocean/manifest.json).

Em **Configurações → Comunidade**, escolha **Mascotes** ou **Temas** para explorar o catálogo. Os cartões mostram nome, autor, licença e versão. **Instalar** baixa e seleciona o pacote; **Atualizar** aparece quando a versão publicada é maior; **Instalado** identifica o que já está em dia. **Atualizar lista** permite tentar novamente ou buscar uma publicação recente. A página explica quando não há conexão, o GitHub limita consultas, a categoria está vazia ou o catálogo ainda não foi publicado.

O aplicativo consulta a release de tag fixa `catalog` no mesmo repositório do MiniInfos, sem banco de dados ou servidor próprio. Só o índice é baixado ao abrir a página; as prévias dos cartões visíveis chegam sob demanda, e o pacote completo só é baixado quando você escolhe instalar ou atualizar. Índice e prévias têm cache de uma hora. Os downloads são limitados e conferidos pelo SHA-256 do índice e pelo digest da release.

## Atualizações

Quando sai uma versão nova, aparece uma linha logo acima do cartão, numa janelinha própria: **Nova atualização disponível**, com o botão **Baixar** à direita. Um clique na linha ou no botão baixa o instalador da versão, confere o SHA-256 publicado junto com ela, instala em silêncio e abre o MiniInfos de novo, já atualizado. Sem espaço acima do cartão, a linha aparece embaixo; com o cartão escondido, ela some junto. O MiniInfos procura versões novas 20 segundos depois de abrir e, depois, a cada 6 horas.

`GitHubReleases` lê a última versão publicada (pelo número do repositório, então nenhum nome de conta vai no app), `Updater` baixa, confere e chama o instalador com `/RELAUNCH=1`, que reabre o app no fim, e `UpdateBanner` é a linha acima do cartão. As atualizações valem a partir da versão que traz o `Updater`: as anteriores precisam do instalador baixado à mão uma última vez.

## Junto com seus apps de IA

O MiniInfos inicia com o Windows, escondido (atalho em `shell:startup` com o argumento legado `--with-claude`). Ele aparece quando a janela do app Claude, do app Codex ou do app Kimi abre, e some quando o último fecha. O app do Codex da Microsoft Store roda como `ChatGPT.exe`: só conta o que vem do pacote do Codex, então abrir o ChatGPT comum não mostra o quadrado. Também conta como Claude aberto qualquer chat do Claude Code rodando no terminal ou num editor: o mod avisa "estou vivo" a cada 30 s e "terminei" ao fechar. Um chat que morre sem avisar conta como fechado depois de 90 s. Chats do Codex CLI, do Gemini CLI e do Kimi Code sem janela própria não acionam a abertura automática; você ainda pode abrir o MiniInfos pelo Menu Iniciar ou pela bandeja.

Os chats normais do claude.ai (aba Chat) já entram nos % de 5h e semanal, porque o limite é um só pra conta. Mas o % só se atualiza quando chega uma resposta do Claude Code, e os tokens desses chats não entram: o claude.ai não guarda nem mostra a contagem.

O ✕ (e o Alt+F4) só esconde: ele volta na próxima vez que uma janela acompanhada abrir. "Sair do MiniInfos" encerra de vez, até o próximo login no Windows.

## Ícone na bandeja

Enquanto roda, o MiniInfos fica na bandeja do Windows, perto do relógio. Se estiver na área escondida (a setinha `^`), dá pra arrastar o ícone pra barra de tarefas.

- **Clique:** mostra ou esconde o quadrado.
- **Mouse em cima:** mostra 5h, semanal e tokens de hoje, mesmo com o quadrado escondido.
- **Botão direito:** Mostrar/Esconder, Voltar pro canto e Sair do MiniInfos.

## Abrir na hora

- **Ícone na bandeja:** um clique.
- **Menu Iniciar:** tecla Windows, digite `MiniInfos` e Enter.
- **Em qualquer chat do Claude:** `/miniinfos`.

Se ele já estiver rodando, abrir de novo só traz a janela pra frente (e de volta pro canto, se ela tiver ido parar fora da tela).

## Como funciona

São duas partes:

| Parte | O que faz |
| --- | --- |
| `mod/` (plugin `miniinfos`) | Roda dentro de cada chat do Claude Code. A cada resposta do modelo, grava na pasta de configuração desse Claude os limites do plano (5h e semanal), o contexto e o custo daquele chat, e informa automaticamente sua localização ao widget. |
| `widget/` (app WPF, .NET 10) | Descobre as pastas de dados do Claude, do Codex, do Gemini e do Kimi, lê os históricos e os arquivos do mod e consulta os limites da conta pelo `codex app-server`. |
| `desktop/` (Linux/macOS, Avalonia e .NET 10) | Interface nativa, integração por sistema e pacotes. Compila os mesmos leitores, ledgers, preferências e regras de `widget/`. |
| `tests/` | `MiniInfos.Tests` (xUnit, a lógica) e `MiniInfos.UiCheck` (abre o quadrado de verdade e confere a janela). |
| `tools/MiniInfos.Catalog/` | Ferramenta separada para validar e gerar o catálogo da comunidade; não entra no instalador. |
| `front/` | Landing page do MiniInfos; fora do instalador, do zip de código das releases e dos testes do app (`front/README.md`). |
| `marketplace/` | Guia, contratos, exemplos e fontes dos pacotes publicados em `marketplace/packages/`. |

### Cobertura do rastreamento

Os tokens vêm dos históricos locais disponíveis, e não do consumo completo da conta do provedor. No Claude Code, a leitura inclui respostas e subagentes, soma entrada, saída, cache lido e cache escrito e evita contar cópias da mesma resposta duas vezes. Uma linha com campos inesperados não interrompe as respostas válidas seguintes.

Claude e Codex procuram pastas padrão, variáveis de ambiente, fontes já encontradas e registros publicados pelo mod. Uma pasta privada do Claude indicada por `CLAUDE_CONFIG_DIR` ou registrada pelo mod alimenta o mesmo leitor, sem depender de um feed de limites. Gemini lê `~/.gemini/tmp`; Kimi considera a pasta padrão, `KIMI_CODE_HOME` e os registros do agente do aplicativo desktop.

No Claude, encontrar o executável e conseguir instalar o mod são resultados separados. O instalador lembra uma cópia encontrada mesmo quando ela recusa o mod; o cartão reutiliza esse caminho enquanto ainda apontar para um Claude Code válido. Os tokens não exigem o mod. Percentuais dos limites, contexto informado pelo mod e acompanhamento automático de chats de terminal/editor dependem dele.

A descoberta local tem limites de profundidade e tempo. O aplicativo Windows não procura automaticamente dentro de WSL, contêineres ou máquinas remotas. Conversas do navegador e sessões que não gravam históricos locais não fornecem tokens a esses leitores. Registros apagados antes da primeira leitura não podem ser recuperados; o ledger preserva o que o MiniInfos já leu.

### Organização do código

Na raiz de `widget`, ficam `MiniInfos.csproj` e o ponto de entrada `App.xaml`/`App.xaml.cs`, que conecta os componentes e controla o ciclo de vida do aplicativo. As implementações ficam agrupadas por responsabilidade:

| Pasta em `widget/` | Responsabilidade |
| --- | --- |
| `Providers/` | Contrato `IUsageProvider`, catálogo `UsageProviders`, escolha das páginas instaladas `InstalledProviders`, o livro-caixa por resposta `ResponseLedger`, a lista `ConversationList` e modelos de tokens, limites, conversas e páginas compartilhados. |
| `Providers/Claude/` | Históricos, feeds, títulos, caminhos e detecção do Claude. |
| `Providers/Codex/` | Históricos, conta, app-server, caminhos e detecção do Codex. |
| `Providers/Gemini/` | Conversas, caminhos e detecção do Gemini CLI. |
| `Providers/Kimi/` | Sessões do Kimi Code e do agente do app, contexto, caminhos e detecção do Kimi. |
| `Sources/` | Descoberta e catálogo das pastas de registros, com os modelos de entrada e resultado da busca. |
| `Community/` | Contrato de pacotes, validação, biblioteca, importação, exportação e modelos para autores. |
| `Community/Catalog/` | Índice, consulta à publicação `catalog`, cache, prévias sob demanda e instalação verificada. |
| `Mascot/` | Aparências, estados, gestos, animação, sprites, posição e coordenação dos mascotes. |
| `Settings/` | Preferências compartilhadas, configurações dos cartões e escolhas de tema, fontes e idioma. |
| `Localization/` | Detecção e troca de idioma, catálogo inglês e bindings de tradução para o XAML. |
| `Updates/` | Contrato da fonte de versões, consulta ao GitHub, download e instalação. |
| `Infrastructure/Network/` | Identidade do repositório e cliente de releases e assets compartilhado pelo atualizador e pelo catálogo. |
| `UI/Windows/` | Janelas, coordenação dos cartões (o de uso, os extras e o de conversas, pelo cartão principal escolhido) e seleção de páginas. Cada XAML fica junto de seu code-behind. |
| `UI/Controls/` | Controles compartilhados, menus e apresentação animada dos números. |
| `UI/Formatting/` | Formatação dos números, horários, custos e legendas. |
| `UI/Themes/` | Dicionários de temas, `ThemeManager` e tipografia compartilhada. |
| `UI/Styles/` e `UI/Icons/` | Estilos e símbolos vetoriais compartilhados. |
| `Infrastructure/Storage/` | Caminhos do aplicativo, arquivos JSON, livros-caixa, leitura incremental e acesso defensivo aos arquivos. |
| `Infrastructure/Desktop/` | Processos, comandos no PATH, instalações e pastas nativas, compartilhados entre os sistemas. |
| `Infrastructure/Windows/` | Atalhos, bandeja, acompanhamento e geometria de janelas específicos do aplicativo WPF. |
| `Infrastructure/AppInfo.cs` | Versão do aplicativo obtida do assembly. |
| `Assets/` | Ícone do aplicativo, ilustrações, sprites, fontes e respectivas licenças. |

Os namespaces acompanham as pastas (`MiniInfos.Providers.Claude`, por exemplo). `tests/MiniInfos.Tests` espelha esses grupos, e `Support` contém o suporte compartilhado aos testes. Contratos usados por mais de um provedor ficam fora das implementações específicas; a persistência compartilhada fica em infraestrutura. As convenções para futuras mudanças também estão no `AGENTS.md` do projeto.

Cada provedor (Claude, Codex, Gemini, Kimi) é uma classe que implementa `IUsageProvider` e devolve uma `UsagePage` pronta; cada coluna do quadrado é um `PageView`, que desenha qualquer página do mesmo jeito, e o rodapé gera um botão com o ícone de cada provedor. Quais páginas estão na tela (uma, ou duas lado a lado) é uma regra à parte, `PageSelection`, e os quadrados abertos (o principal e os de "Nova janela", cada um com seu arquivo em `%LOCALAPPDATA%\MiniInfos\windows`) ficam num `CardSet`. Um provedor novo é uma classe nova e uma linha em `UsageProviders.Create()`, e diz onde a instalação dele aparece num `AppFootprint` (pacote da Store, comando, extensão, arquivo, processo), que `InstalledApps` confere; `InstalledProviders` deixa no cartão só os instalados, olhando de novo a cada minuto e no ↻; o símbolo pode ser incluído em `ProviderIcons.xaml`, com um símbolo genérico como alternativa. Os livros-caixa usam as mesmas peças: `JsonlTail` (lê só o que foi acrescentado em cada `.jsonl`), `LedgerStore` (grava o livro-caixa) e `TokenSummary.Of` (hoje, janelas e total). Claude, Gemini e Kimi registram uma linha por resposta e compartilham o `ResponseLedger`: cada provedor só diz como ler uma linha. O Codex registra acumulados por sessão e tem o livro-caixa próprio.

Os símbolos vetoriais do rodapé vêm de [Simple Icons](https://github.com/simple-icons/simple-icons), distribuídos sob CC0: Claude da versão 16.0.0, OpenAI da 11.15.0 e Google Gemini e Kimi da 16.35.0. Eles ficam embutidos no aplicativo, sem downloads durante o uso.

Os temas ficam em `widget/UI/Themes`, com recursos dinâmicos compartilhados pelo cartão, menus e configurações. Cada tema é um arquivo com os mesmos pincéis (`Simple.xaml`, `Classic.xaml`, `Light.xaml`), e `Design.xaml` guarda a fonte das configurações e o cabeçalho comuns a todos. `ThemeManager` aplica a escolha a todas as janelas. A arte de Atena foi gerada para o MiniInfos.

Em **Configurações → Aparência → Fontes**, textos e números têm seletores separados, com prévia imediata. As escolhas valem para todos os cartões, inclusive a visualização dividida, ficam salvas e continuam iguais ao trocar o tema. Combinações das prévias:

| Estilo | Textos | Números |
| --- | --- | --- |
| Atual | Manrope | Manrope |
| Monoespaçada | Manrope | Geist Mono |
| Monumental | Cinzel | Bodoni Moda |
| Digital | Chakra Petch | Orbitron |

Também é possível misturar essas fontes. Percentuais, totais, horários e contagens nas legendas usam a fonte dos números; o restante usa a fonte dos textos. `AppFonts` mantém o catálogo e os recursos compartilhados, e `NumericText` separa números de letras sem alterar os valores ou a animação das contagens. As fontes ficam embutidas no aplicativo, sem instalação ou downloads durante o uso. Suas licenças SIL Open Font License acompanham a distribuição em `Assets/Fonts/OFL*.txt`.

O `codex` é procurado no PATH (instalação nativa, ou o `codex.cmd` do npm), nos processos de CLI em execução e na cópia que o app do Codex guarda em `%LOCALAPPDATA%\OpenAI\Codex\bin`, inclusive dentro do pacote da Microsoft Store. O app-server recebe a pasta de configuração detectada, respeitando `CODEX_HOME` quando definido.

### Descoberta automática dos registros

`UsageSources` mantém um catálogo compartilhado pelas duas páginas. A busca roda em segundo plano ao abrir e a cada 10 minutos; **Atualizar agora** também refaz a busca na hora. Ela considera `CLAUDE_CONFIG_DIR` e `CODEX_HOME` do processo, do usuário e do sistema, as pastas padrão, os dados de instalações da Microsoft Store e pastas próximas dos aplicativos em execução. Uma busca limitada por estrutura no perfil do usuário e nos discos locais também reconhece pastas movidas, pelos arquivos que só o Claude Code (`.credentials.json`, `.claude.json`) e o Codex (`installation_id`, `.codex-global-state.json`) guardam; uma pasta de projeto comum com `projects` ou `sessions` não é confundida com elas. Ela evita pastas de sistema, dependências e links recursivos; não faz uma varredura recursiva completa dos discos.

Quando um chat do Claude usa um `CLAUDE_CONFIG_DIR` próprio, o mod publica somente o caminho em `%LOCALAPPDATA%\MiniInfos\sources`. O widget lê esses avisos, inclusive os redirecionados pelo pacote da Store. As localizações encontradas ficam em `%LOCALAPPDATA%\MiniInfos\usage-sources.json`, para continuarem disponíveis nas próximas aberturas. Uma pasta inacessível ou ausente não interrompe as outras fontes nem apaga as contagens salvas.

Os leitores acompanham todas as fontes descobertas, sem precisar reiniciar ou preencher caminhos nas configurações. No Claude, usam `projects` e `miniinfos/sessions`; no Codex, `sessions` e `archived_sessions`. A busca depende de pastas locais acessíveis e de sinais reconhecíveis, como as variáveis, os avisos do mod e a estrutura de dados dessas ferramentas.

Os limites são da conta, não de um chat. Cada chat guarda a leitura da última resposta que recebeu, então um chat parado fica com uma leitura velha. Dentro de uma mesma janela o uso só cresce, então o app fica com a maior leitura da janela que reinicia por último.

Os tokens do Claude ficam num livro-caixa próprio em `%LOCALAPPDATA%\MiniInfos\ledger.json`. Assim o total não cai quando o Claude Code apaga históricos antigos. Cada resposta conta uma vez só, pelo id da mensagem, e o total soma entrada, saída, cache lido e cache escrito.

Os tokens do Gemini ficam em `gemini-ledger.json` e os do Kimi em `kimi-ledger.json`, na mesma pasta. O Gemini CLI grava cada conversa em `~/.gemini/tmp/<projeto>/chats/*.jsonl` e acrescenta a resposta de novo, inteira, quando os tokens chegam: cada resposta conta uma vez, pelo id dentro da sessão. A entrada do Gemini já inclui o cache, que o MiniInfos separa; os tokens de raciocínio contam como saída. O Kimi Code grava um `usage.record` por resposta em `sessions/wd_*/<sessão>/agents/<agente>/wire.jsonl`, tanto na pasta dele (`KIMI_CODE_HOME`, senão `~/.kimi-code`) quanto na cópia que o app desktop roda em `%APPDATA%\kimi-desktop\daimon-share\daimon\runtime\kimi-code\home`; cada agente registra só as próprias respostas, então somar os arquivos não conta nada duas vezes.

Os tokens locais do Codex ficam em `%LOCALAPPDATA%\MiniInfos\codex-ledger.json`. Cada evento de uso registra o acumulado da sessão; o MiniInfos acrescenta somente a diferença em relação à maior leitura daquela sessão, identificada pelo ID nos metadados ou pelo UUID do arquivo. Mover, arquivar ou copiar a mesma sessão para outra fonte não soma seus tokens novamente. O histórico das versões anteriores é preservado. O total segue o `total_tokens` do Codex, inclusive nos eventos (como os de compactação) que trazem só o total, sem entrada e saída. O cache já está incluído nos tokens de entrada do Codex, então não é somado duas vezes. Os percentuais são da conta e podem abranger atividade fora desta máquina; por isso os tokens locais nas janelas não são uma conversão direta desses percentuais. Sem autenticação ativa (ou sem o `codex`, quando nenhum app é encontrado e as duas páginas aparecem), a página continua exibindo os tokens locais e deixa os limites indisponíveis.

## Privacidade

O MiniInfos não pede login e não lê credenciais. O widget acessa somente arquivos locais para os tokens; para os limites da conta Codex, ele inicia o `codex app-server`, que usa a autenticação já configurada no Codex e consulta o serviço da OpenAI:

- **Lê:** os históricos do Claude Code, as sessões ativas e arquivadas do Codex, as conversas do Gemini CLI, as sessões do Kimi Code (inclusive as do agente do app Kimi, com o medidor de contexto que o app guarda) e os arquivos que o mod escreve, nas pastas descobertas automaticamente. Os arquivos de login do app Kimi e do Gemini CLI nunca são abertos. Examina nomes e estrutura das pastas locais para localizar essas fontes. Extrai somente contagem, horário, contexto e pasta do projeto; não guarda nem exibe o texto das conversas.
- **Guarda:** `%LOCALAPPDATA%\MiniInfos` (contagens locais, catálogo de fontes, posição, tamanho, preferências, pacotes instalados e caches de prévias e catálogo) e `<pasta do Claude>/miniinfos` (o que cada chat Claude publica: limites, contexto, custo, pasta e nome do chat).
- **Internet:** além do `codex app-server`, procura versões novas do MiniInfos no GitHub a cada 6 horas e baixa o instalador quando você clica para atualizar. A página Comunidade baixa a lista e as prévias dos cartões visíveis do catálogo no GitHub quando você a abre ou atualiza; um pacote só é baixado ao clicar em Instalar ou Atualizar. Nada seu é enviado. Não há consultas de catálogo em segundo plano nem atualização automática de pacotes.
- **Desinstalar** apaga os dados do MiniInfos em `%LOCALAPPDATA%\MiniInfos` e os arquivos do mod no caminho padrão `~/.claude/miniinfos`.

O que é distribuído não leva nada da máquina onde foi gerado: a compilação Release sai sem arquivo de símbolos e com os caminhos de build trocados por `MiniInfos`.

### Desenvolvimento sem baixar os pacotes

Para trabalhar no app com uma cópia local do repositório, deixe os pacotes fora do checkout:

```powershell
git sparse-checkout set --no-cone '/*' '!/marketplace/packages/'
```

O ZIP automático de código das releases também exclui `marketplace/packages/` por meio de `.gitattributes`. O instalador publica somente o app e o mod; o build falha se encontrar arquivos do marketplace ou da ferramenta em `installer/out`.

### Publicações no GitHub

As releases do aplicativo usam a tag `vX.Y.Z` e têm um único asset: `MiniInfos-Setup.exe`. Ao receber a tag, o workflow `release-app.yml` confere a versão, gera o instalador a partir dela, confere o conteúdo e cria a release **como rascunho**, com as notas geradas pelo GitHub. Rascunho não aparece para a atualização pela faixa: depois de revisada, a release é publicada com `gh release edit vX.Y.Z --draft=false --latest`, **sempre com `--latest`**.

O catálogo usa somente a tag **`catalog`**, marcada com **`--prerelease --latest=false`**, com o título **Catálogo da comunidade (usado pelo app, não é o instalador)**. São publicados `index.json`, uma prévia PNG por pacote e um `.miniinfos` por pacote; não há um ZIP com o catálogo inteiro. Os workflows do marketplace validam o mesmo commit e publicam apenas quando `marketplace/packages/` muda na `main`. A publicação remota só acontece depois que essas mudanças forem enviadas ao GitHub.

## Variáveis de ambiente

Nenhuma é obrigatória.

| Variável | Quando | Para quê |
| --- | --- | --- |
| `MINIINFOS_PUBLISHER` | ao gerar o instalador | Nome que aparece como publicador e como autor do mod. Sem ela, "MiniInfos". |
| `CLAUDE_CONFIG_DIR` | ao rodar | A do próprio Claude Code: se ela mudar a pasta do Claude, o app e o mod seguem junto. |
| `CODEX_HOME` | ao rodar | Se definido, indica a pasta dos registros locais do Codex. |
| `KIMI_CODE_HOME` | ao rodar | A do próprio Kimi Code: se definida, indica a pasta das sessões dele, além da cópia do app desktop. |
| `MINIINFOS_DATA_DIR` | ao rodar | Opcional para testes isolados: muda onde o MiniInfos guarda o livro-caixa e as preferências da janela. |

## Instalador

`installer\dist\MiniInfos-Setup.exe` instala tudo com dois cliques: o app (com o .NET embutido, sem nada pra instalar antes), o atalho no menu Iniciar, a opção "Abrir junto com seus apps de IA" e o mod nos chats do Claude Code. Pros limites da conta Codex, basta o Codex instalado (app ou `codex` de terminal). Não pede administrador. Ele aparece em "Adicionar ou remover programas", e desinstalar remove o app, os atalhos, o mod e os dados do MiniInfos.

A versão fica num lugar só, o `<Version>` de `widget/MiniInfos.csproj`: o build do instalador lê dali, e o app informa a mesma ao `codex`. Pra gerar de novo depois de mudar o código (precisa do Inno Setup 6):

```bash
powershell -File installer/build.ps1
```

O arquivo não é assinado, então o Windows mostra "O Windows protegeu o computador" na primeira vez: clique em "Mais informações" e depois em "Executar assim mesmo".

**Cuidado ao testar daqui de dentro do Claude:** o app desktop é um app da Microsoft Store, e o que roda dentro dele (o terminal do Claude Code, inclusive) tem as gravações em `AppData` desviadas pra `%LOCALAPPDATA%\Packages\Claude_*\LocalCache`. Um instalador ou atalho criado de dentro do Claude só existe nessa bolha. Pra instalar de verdade, abra pelo Explorer. O mesmo vale pra achar o Claude Code do app desktop: ele mora em `Packages\Claude_*\LocalCache\Roaming\Claude\claude-code`.

## Instalar o app à mão

O app fica instalado em `%LOCALAPPDATA%\Programs\MiniInfos`, que é pra onde o atalho do menu Iniciar e o `/miniinfos` apontam. Depois de mudar o código, feche o MiniInfos e publique de novo:

```bash
dotnet publish widget -c Release -o "$LOCALAPPDATA/Programs/MiniInfos"
```

Os atalhos ficam em `%APPDATA%\Microsoft\Windows\Start Menu\Programs\MiniInfos.lnk` (menu Iniciar) e `...\Programs\Startup\MiniInfos.lnk` (início do Windows, com `--with-claude`). Pra ele parar de iniciar com o Windows, apague o segundo.

## Instalar o mod em todos os chats

O instalador faz isso pedindo ao próprio app (`MiniInfos.exe --install-mod`; ao desinstalar, `--remove-mod`): registra o marketplace `miniinfos` apontando pra `%LOCALAPPDATA%\Programs\MiniInfos\claude-plugin` e instala o mod no escopo do usuário, que todo Claude Code dessa conta lê (app desktop, terminal, editores). Pra achar um Claude Code, o app olha o PATH e, sem nada lá, varre todos os arquivos da pasta do usuário e depois de todos os discos locais, inclusive pastas ocultas (`ClaudeCode.Find`). Não executa nada pra descobrir o que achou: o `claude.exe` do Claude Code se identifica nas informações de versão (o `claude.exe` do app desktop não), e o `claude.cmd` do npm aponta pro pacote `@anthropic-ai/claude-code`. Cada cópia achada é tentada até uma aceitar o mod, e essa fica lembrada em `%LOCALAPPDATA%\MiniInfos\claude-code.txt` pras próximas atualizações e pra desinstalação. Só quando nenhuma aceita o instalador avisa que o mod ficou de fora. Depois de mudar o mod, suba a `version` em `mod/.claude-plugin/plugin.json` e gere e rode o instalador de novo, ou, pra testar direto desta pasta, aponte o marketplace pra cá (`claude plugin marketplace remove miniinfos`, depois `claude plugin marketplace add <esta pasta>`) e rode:

```bash
claude plugin marketplace update miniinfos
```

```bash
claude plugin update miniinfos@miniinfos
```

Os chats abertos pegam a versão nova quando reiniciam.

## Testes

A validação cotidiana seleciona os domínios afetados pelos arquivos alterados, comparando o diretório de trabalho com `HEAD`:

```powershell
./tests/run.ps1
```

O mapa fica em `tests/domains.psd1` e é usado tanto localmente quanto no CI. Ele inclui consumidores de componentes compartilhados: uma mudança em `JsonlTail`, por exemplo, seleciona armazenamento e todos os provedores; uma mudança em `PetInteraction` seleciona somente o mascote. Documentação não seleciona testes. Código sem regra, bootstrap e alterações do projeto selecionam a regressão completa, evitando ignorar uma funcionalidade nova por engano.

Para conferir a seleção ou informar um escopo conhecido:

```powershell
./tests/run.ps1 -Plan
./tests/run.ps1 -Domain Mascot
./tests/run.ps1 -Domain Claude,Codex
./tests/run.ps1 -Base origin/dev -Plan
```

A opção `-Base` compara o estado atual com outro commit ou branch. A seleção considera arquivos novos e ambos os caminhos de arquivos movidos. O escopo manual testa exatamente os domínios informados; para código compartilhado, prefira a seleção automática ou informe todos os consumidores afetados.

Acrescente `-Ui` para conferir interação ou apresentação visual. Os cenários focados usam dados simulados, não leem históricos e não consultam contas. Um cenário de mascote não executa a bateria de temas ou fontes:

```powershell
./tests/run.ps1 -Domain Mascot -Ui
./tests/run.ps1 -Domain Themes -Ui
```

`MiniInfos.UiCheck` separa cartões, configurações, temas, fontes, mascote, atualizações e integração com provedores. A saída do runner mostra somente falhas e resumos por cenário. Para investigar detalhes ou salvar imagens numa pasta específica, execute o cenário diretamente:

```powershell
dotnet run --project tests/MiniInfos.UiCheck -c Release -- prints --suite Mascot
```

Os arquivos dos cenários ficam em pastas próprias dentro de `tests/MiniInfos.UiCheck`; `Support` concentra a preparação e os auxiliares compartilhados. Os dados e atalhos dos testes ficam isolados em uma pasta temporária. Para consultar contas e registros reais, use `--suite Providers`; esse cenário e a execução completa mantêm a integração local, incluindo a verificação de um app Claude aberto.

A regressão completa fica disponível para mudanças transversais e entregas:

```powershell
./tests/run.ps1 -All
./tests/run.ps1 -All -Ui
```

O `dotnet test` compila o aplicativo antes de executar a seleção: todos os arquivos ainda precisam compilar, mesmo quando somente um domínio é testado. No GitHub (`.github/workflows/tests.yml`), a `dev` e os PRs usam a seleção por arquivos; a `main` executa todos os testes de unidade. Os testes visuais ficam locais porque abrem janelas reais.

Ao alterar o mapa ou o executor, valide também a seleção:

```powershell
./tests/TestSelection.Tests.ps1
```

Quando o mod mudar, o runner informa a necessidade de executar seu teste no ambiente do Claude Code:

```bash
claude plugin test mod
```

## Marcas e afiliação

O MiniInfos é um projeto independente e de código aberto, sem afiliação, patrocínio ou endosso da Anthropic, da OpenAI, do Google ou da Moonshot AI. Claude é marca da Anthropic; ChatGPT, Codex e OpenAI são marcas da OpenAI; Gemini é marca do Google; Kimi é marca da Moonshot AI. Os nomes aparecem só para indicar com quais apps o MiniInfos funciona. Os símbolos dessas marcas mostrados no app pertencem às respectivas empresas e não fazem parte da licença deste repositório.
