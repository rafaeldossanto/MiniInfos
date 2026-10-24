# MiniInfos

Um quadradinho flutuante, sempre por cima das outras janelas, com o uso do plano do Claude:

- **Janela 5h** e **Semanal**: % usado, barra (azul até 70%, amarela até 90%, vermelha acima), tokens gastos naquela janela e quanto falta pra reiniciar.
- **Tokens hoje** e **Tokens no total**, somando todos os chats do Claude Code (passe o mouse pra ver entrada, cache e saída).
- **Chat atual**: o chat que respondeu por último, com o contexto usado e o custo equivalente de API.

Dois modos de ver, nos botões do canto inferior direito (como no Explorer):

- **Resumido** (quadradinho): 1,4B, 109M.
- **Detalhado** (linhas): o número inteiro, 1.412.345.678, subindo animado feito um odômetro enquanto os tokens são gastos.

O modo escolhido fica salvo.

Ele se atualiza sozinho a cada 3 segundos. O botão ↻, ao lado do ✕, atualiza na hora (também em "Atualizar agora", no botão direito e no ícone da bandeja). Os % de 5h e semanal mudam quando algum chat do Claude Code recebe resposta, porque é dela que eles vêm.

Arraste o quadrado pra onde quiser. Pra mudar o tamanho, puxe uma borda ou um canto: tudo cresce ou encolhe junto, na mesma proporção. A posição e o tamanho ficam salvos. Botão direito: "Sempre por cima", "Voltar pro canto", "Tamanho padrão", "Esconder" e "Sair do MiniInfos".

## Junto com o Claude

O MiniInfos inicia com o Windows, escondido (atalho em `shell:startup` com `--with-claude`). Ele aparece quando o Claude abre e some quando o último fecha. Conta como Claude aberto a janela do app desktop ou qualquer chat do Claude Code rodando, no terminal ou num editor: o mod avisa "estou vivo" a cada 30 s e "terminei" ao fechar. Um chat que morre sem avisar conta como fechado depois de 90 s.

Os chats normais do claude.ai (aba Chat) já entram nos % de 5h e semanal, porque o limite é um só pra conta. Mas o % só se atualiza quando chega uma resposta do Claude Code, e os tokens desses chats não entram: o claude.ai não guarda nem mostra a contagem.

O ✕ (e o Alt+F4) só esconde: ele volta na próxima vez que o Claude abrir. "Sair do MiniInfos" encerra de vez, até o próximo login no Windows.

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
| `widget/` (app WPF, .NET 10) | Lê esses arquivos a cada 5 s. Também lê os históricos em `~/.claude/projects/**/*.jsonl` pra somar os tokens de todos os chats. |

Os limites são da conta, não de um chat. Cada chat guarda a leitura da última resposta que recebeu, então um chat parado fica com uma leitura velha. Dentro de uma mesma janela o uso só cresce, então o app fica com a maior leitura da janela que reinicia por último.

Os tokens ficam num livro-caixa próprio em `%LOCALAPPDATA%\MiniInfos\ledger.json`. Assim o total não cai quando o Claude Code apaga históricos antigos. Cada resposta conta uma vez só, pelo id da mensagem, e o total soma entrada, saída, cache lido e cache escrito.

## Privacidade

O MiniInfos não usa internet, não pede login e não lê credencial nenhuma. Tudo fica no próprio PC:

- **Lê:** os históricos do Claude Code (`~/.claude/projects`), só a contagem de tokens e o horário de cada resposta, e os arquivos que o mod escreve.
- **Guarda:** `%LOCALAPPDATA%\MiniInfos` (a contagem de tokens, a posição e o tamanho da janela) e `~/.claude/miniinfos` (o que cada chat publica: limites, contexto, custo, pasta e nome do chat).
- **Desinstalar** apaga as duas pastas.

O que é distribuído não leva nada da máquina onde foi gerado: a compilação Release sai sem arquivo de símbolos e com os caminhos de build trocados por `MiniInfos`.

## Variáveis de ambiente

Nenhuma é obrigatória.

| Variável | Quando | Para quê |
| --- | --- | --- |
| `MINIINFOS_PUBLISHER` | ao gerar o instalador | Nome que aparece como publicador e como autor do mod. Sem ela, "MiniInfos". |
| `CLAUDE_CONFIG_DIR` | ao rodar | A do próprio Claude Code: se ela mudar a pasta do Claude, o app e o mod seguem junto. |

## Instalador

`installer\dist\MiniInfos-Setup.exe` instala tudo com dois cliques: o app (com o .NET embutido, sem nada pra instalar antes), o atalho no menu Iniciar, a opção "Abrir junto com o Claude" e o mod nos chats do Claude Code. Não pede administrador. Ele aparece em "Adicionar ou remover programas", e desinstalar remove o app, os atalhos, o mod e os dados.

Pra gerar de novo depois de mudar o código (precisa do Inno Setup 6):

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

## Testes do mod

```bash
claude plugin test mod
```
