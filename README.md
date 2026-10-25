# MiniInfos

Um quadradinho flutuante, sempre por cima das outras janelas, com uma página para Claude e outra para Codex (GPT). Pra trocar de página: as setinhas ou os ícones das IAs no rodapé, as teclas ←/→, um deslize de dois dedos pro lado no touchpad, ou Shift + roda do mouse. Os ícones são monocromáticos, com o selecionado branco e os demais cinza. A página escolhida fica salva.

Botão direito num ícone, **Abrir em**, como no app do Claude:

- **Visualização dividida:** o quadrado mostra as duas páginas lado a lado, com os dois ícones acesos, e fica com o dobro da largura. Cada coluna diz quando os seus limites foram atualizados, e o ↻ atualiza as duas. **Visualização única** (no mesmo menu), ou um clique simples num ícone, volta pra uma página.
- **Nova janela:** abre outro quadrado só com aquela página, com posição e tamanho próprios. Ele volta junto quando o MiniInfos abre de novo, aparece e some junto com o principal, e o ✕ dele fecha de vez. A bandeja mostra os números do quadrado principal.

Na página do Claude:

- **Janela 5h** e **Semanal**: % usado, barra (azul até 70%, amarela até 90%, vermelha acima), tokens gastos naquela janela e quanto falta pra reiniciar.
- **Tokens hoje** e **Tokens no total**, somando todos os chats do Claude Code (passe o mouse pra ver entrada, cache e saída).
- **Chat atual**: o chat que respondeu por último, com o contexto usado e o custo equivalente de API.

Na página do Codex, o mesmo desenho mostra os percentuais e horários de reinício da conta, os tokens das sessões locais nas duas janelas, os tokens de hoje e do total e a sessão local mais recente. Os totais de tokens vêm dos registros locais do Codex. Passe o mouse para ver o detalhamento. O app não transforma os percentuais do plano em custo em dinheiro.

Dois modos de ver, nos botões do canto inferior direito (como no Explorer):

- **Resumido** (quadradinho): 1,4B, 109M.
- **Detalhado** (linhas): o número inteiro, 1.412.345.678, subindo animado feito um odômetro enquanto os tokens são gastos.

O modo escolhido fica salvo.

Ele lê os registros locais a cada 3 segundos. Os limites da conta Codex são consultados em segundo plano a cada minuto, só enquanto a página do Codex está na tela, e a tela nunca espera por eles. O botão ↻, ao lado do ✕, força uma consulta na hora da página aberta (também em "Atualizar agora", no botão direito e no ícone da bandeja). Os % do Claude mudam quando algum chat do Claude Code recebe resposta, porque é dela que eles vêm.

Arraste o quadrado pra onde quiser, em qualquer direção. Pra mudar o tamanho, puxe uma borda ou um canto: tudo cresce ou encolhe junto, na mesma proporção. A posição e o tamanho ficam salvos. Botão direito: "Sempre por cima", "Voltar pro canto", "Tamanho padrão", "Esconder" e "Sair do MiniInfos".

## Junto com Claude ou Codex

O MiniInfos inicia com o Windows, escondido (atalho em `shell:startup` com o argumento legado `--with-claude`). Ele aparece quando a janela do app Claude ou do app Codex abre, e some quando o último fecha. O app do Codex da Microsoft Store roda como `ChatGPT.exe`: só conta o que vem do pacote do Codex, então abrir o ChatGPT comum não mostra o quadrado. Também conta como Claude aberto qualquer chat do Claude Code rodando no terminal ou num editor: o mod avisa "estou vivo" a cada 30 s e "terminei" ao fechar. Um chat que morre sem avisar conta como fechado depois de 90 s. Chats do Codex CLI sem janela própria não acionam a abertura automática; você ainda pode abrir o MiniInfos pelo Menu Iniciar ou pela bandeja.

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
| `mod/` (plugin `miniinfos`) | Roda dentro de cada chat do Claude Code. A cada resposta do modelo, grava em `~/.claude/miniinfos/sessions/<sessão>.json` os limites do plano (5h e semanal), o contexto e o custo daquele chat. |
| `widget/` (app WPF, .NET 10) | Lê os arquivos do mod e os históricos em `~/.claude/projects/**/*.jsonl`. Também lê as sessões locais do Codex em `~/.codex/sessions/**/*.jsonl` e consulta os limites da conta pelo `codex app-server`. |
| `tests/` | `MiniInfos.Tests` (xUnit, a lógica) e `MiniInfos.UiCheck` (abre o quadrado de verdade e confere a janela). |

Cada provedor (Claude, Codex) é uma classe que implementa `IUsageProvider` e devolve uma `UsagePage` pronta; cada coluna do quadrado é um `PageView`, que desenha qualquer página do mesmo jeito, e o rodapé gera um botão com o ícone de cada provedor. Quais páginas estão na tela (uma, ou duas lado a lado) é uma regra à parte, `PageSelection`, e os quadrados abertos (o principal e os de "Nova janela", cada um com seu arquivo em `%LOCALAPPDATA%\MiniInfos\windows`) ficam num `CardSet`. Um provedor novo é uma classe nova e uma linha em `UsageProviders.Create()`; o símbolo pode ser incluído em `ProviderIcons.xaml`, com um símbolo genérico como alternativa. Os dois livros-caixa usam as mesmas peças: `JsonlTail` (lê só o que foi acrescentado em cada `.jsonl`), `LedgerStore` (grava o livro-caixa) e `TokenSummary.Of` (hoje, janelas e total).

Os símbolos vetoriais do rodapé vêm de [Simple Icons](https://github.com/simple-icons/simple-icons), distribuídos sob CC0: Claude da versão 16.0.0 e OpenAI da 11.15.0. Eles ficam embutidos no aplicativo, sem downloads durante o uso.

O `codex` é procurado no PATH (instalação nativa, ou o `codex.cmd` do npm) e, se não estiver lá, na cópia que o app do Codex guarda em `%LOCALAPPDATA%\OpenAI\Codex\bin`. É o caso do app da Microsoft Store, que não põe o `codex` no PATH.

Os limites são da conta, não de um chat. Cada chat guarda a leitura da última resposta que recebeu, então um chat parado fica com uma leitura velha. Dentro de uma mesma janela o uso só cresce, então o app fica com a maior leitura da janela que reinicia por último.

Os tokens do Claude ficam num livro-caixa próprio em `%LOCALAPPDATA%\MiniInfos\ledger.json`. Assim o total não cai quando o Claude Code apaga históricos antigos. Cada resposta conta uma vez só, pelo id da mensagem, e o total soma entrada, saída, cache lido e cache escrito.

Os tokens locais do Codex ficam em `%LOCALAPPDATA%\MiniInfos\codex-ledger.json`. Cada evento de uso registra o acumulado da sessão; o MiniInfos acrescenta somente a diferença em relação à leitura anterior, e o total segue o `total_tokens` do Codex, inclusive nos eventos (como os de compactação) que trazem só o total, sem entrada e saída. O cache já está incluído nos tokens de entrada do Codex, então não é somado duas vezes. Os percentuais são da conta e podem abranger atividade fora desta máquina; por isso os tokens locais nas janelas não são uma conversão direta desses percentuais. Sem `codex` instalado ou sem autenticação ativa, a página continua exibindo os tokens locais e deixa os limites indisponíveis.

## Privacidade

O MiniInfos não pede login e não lê credenciais. O widget acessa somente arquivos locais para os tokens; para os limites da conta Codex, ele inicia o `codex app-server`, que usa a autenticação já configurada no Codex e consulta o serviço da OpenAI:

- **Lê:** os históricos do Claude Code (`~/.claude/projects`), os registros do Codex (`~/.codex/sessions`) e os arquivos que o mod escreve. Extrai somente contagem, horário, contexto e pasta do projeto; não guarda nem exibe o texto das conversas.
- **Guarda:** `%LOCALAPPDATA%\MiniInfos` (contagens locais, posição, tamanho e página da janela) e `~/.claude/miniinfos` (o que cada chat Claude publica: limites, contexto, custo, pasta e nome do chat).
- **Desinstalar** apaga as duas pastas.

O que é distribuído não leva nada da máquina onde foi gerado: a compilação Release sai sem arquivo de símbolos e com os caminhos de build trocados por `MiniInfos`.

## Variáveis de ambiente

Nenhuma é obrigatória.

| Variável | Quando | Para quê |
| --- | --- | --- |
| `MINIINFOS_PUBLISHER` | ao gerar o instalador | Nome que aparece como publicador e como autor do mod. Sem ela, "MiniInfos". |
| `CLAUDE_CONFIG_DIR` | ao rodar | A do próprio Claude Code: se ela mudar a pasta do Claude, o app e o mod seguem junto. |
| `CODEX_HOME` | ao rodar | Se definido, indica a pasta dos registros locais do Codex. |
| `MINIINFOS_DATA_DIR` | ao rodar | Opcional para testes isolados: muda onde o MiniInfos guarda o livro-caixa e as preferências da janela. |

## Instalador

`installer\dist\MiniInfos-Setup.exe` instala tudo com dois cliques: o app (com o .NET embutido, sem nada pra instalar antes), o atalho no menu Iniciar, a opção "Abrir junto com Claude ou Codex" e o mod nos chats do Claude Code. Pros limites da conta Codex, basta o Codex instalado (app ou `codex` de terminal). Não pede administrador. Ele aparece em "Adicionar ou remover programas", e desinstalar remove o app, os atalhos, o mod e os dados do MiniInfos.

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

O instalador faz isso: registra o marketplace `miniinfos` apontando pra `%LOCALAPPDATA%\Programs\MiniInfos\claude-plugin` e instala o mod no escopo do usuário, que todo Claude Code dessa conta lê (app desktop, terminal, editores). Depois de mudar o mod, suba a `version` em `mod/.claude-plugin/plugin.json` e gere e rode o instalador de novo, ou, pra testar direto desta pasta, aponte o marketplace pra cá (`claude plugin marketplace remove miniinfos`, depois `claude plugin marketplace add <esta pasta>`) e rode:

```bash
claude plugin marketplace update miniinfos
```

```bash
claude plugin update miniinfos@miniinfos
```

Os chats abertos pegam a versão nova quando reiniciam.

## Testes

A lógica (livros-caixa, leitura dos registros, onde achar o `codex`, os limites do Codex, os textos e as contas da janela):

```bash
dotnet test tests/MiniInfos.Tests
```

A janela de verdade: abre o quadrado por uns 30 segundos, com os dados numa pasta temporária, confere bordas, bandeja, modos, ↻, páginas e o Codex, e salva um print de cada página na pasta que você indicar:

```bash
dotnet run --project tests/MiniInfos.UiCheck -- prints
```

O mod:

```bash
claude plugin test mod
```
