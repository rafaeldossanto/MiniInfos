# Landing page do MiniInfos

O site público do MiniInfos fica nesta pasta, separado do aplicativo, do mod e do marketplace. Ele apresenta o app e oferece os downloads publicados para Windows, macOS e Linux.

## Regras

- **Fora do aplicativo.** Nada desta pasta entra no instalador: o build do .NET e o `installer/build.ps1` recusam arquivos de `front/`. O zip de código das releases do app também deixa a pasta de fora.
- **Fora dos testes do app.** Uma mudança só em `front/` seleciona o domínio `Front`: executa as checagens do site, sem rodar o widget. O CI do front é independente, em `.github/workflows/front.yml`.
- **Download pela release.** Os botões levam às opções por sistema. Downloads diretos só são ativados para os arquivos publicados na release marcada como latest, nunca na release `catalog`.
- **Marcas.** Claude, Codex, ChatGPT, OpenAI, Gemini e Kimi aparecem só em texto, para dizer com quais apps o MiniInfos funciona. Use ícones neutros, sem os símbolos das empresas, e mantenha no rodapé o aviso de que o MiniInfos é independente, sem afiliação com a Anthropic, a OpenAI, o Google ou a Moonshot AI.
- **Dados de demonstração.** Prints e números de exemplo levam a marca "Demonstração" e não trazem dados pessoais.

## Desenvolvimento

Use Node.js 24 ou superior. Dentro de `front/`:

```powershell
npm ci
npm run dev
```

O endereço local aparece no terminal. `npm run check` valida os testes focados, os tipos TypeScript e o build. `npm run build` gera o site estático em `dist/`; `npm run preview` permite conferir essa saída. Na raiz do repositório, `./tests/run.ps1 -Domain Front` executa a mesma checagem sem iniciar testes .NET. `-All` continua sendo a regressão do app; combine domínios explicitamente quando necessário.

Não há framework de interface. Vite prepara os arquivos estáticos; o HTML contém o conteúdo da página, e TypeScript acrescenta os controles interativos. O envio opcional de criações usa o serviço separado em `service/`; o restante do site continua estático.

## Organização

- `index.html`: conteúdo, navegação, demonstrações e metadados.
- `src/components/`: demonstração reutilizável do cartão, com dados fictícios.
- `src/theme/`: preferência de tema e transição circular de 1,2 segundo.
- `src/localization/`: detecção do idioma, preferência salva e catálogo inglês. As mensagens em português são as chaves de `en.json`.
- `src/routing.ts`: nomes públicos das páginas e os arquivos HTML correspondentes; compartilhados pela localização, pelo build e pelo servidor local.
- `scripts/localized-pages.mjs`: gera as páginas inglesas a partir do mesmo HTML e catálogo; `en/` é gerada e não deve ser editada.
- `src/features/`: menu responsivo e entradas de conteúdo ao aparecer na tela.
- `src/styles/`: estilos compartilhados, página e cartão.
- `src/config.ts`: destinos oficiais de download, código e guia da comunidade.
- `pages/community.html`: conteúdo da página Comunidade; `scripts/community-page.mjs` reaproveita o cabeçalho, menu móvel e rodapé da landing para gerar `comunidade.html`.
- `src/community/`: leitura do catálogo, busca, filtros, ordenação, prévias e detalhes dos pacotes.
- `src/community/creation/`: envio de arquivos, validação local, prévia do pacote, edição de metadados e cores, exportação e acompanhamento da revisão.
- `service/`: API opcional em Cloudflare Workers, comprovantes em R2 privado e integração GitHub App. Não entra no bundle público ou nos instaladores; veja `service/README.md` para ativação.
- `src/site.ts`: inicialização compartilhada de navegação, idioma, tema e links.
- `public/art/`: artes de Atena, cabeçalho, sprites e imagem de compartilhamento.
- `public/fonts/`: fontes locais e suas licenças. O site não consulta serviços de fontes.
- `tests/`: persistência, geometria da transição, destino do instalador e integridade dos recursos.

O site começa no escuro. Uma escolha explícita pelo claro fica salva em `localStorage`; bloquear esse armazenamento não impede trocar o tema. Navegadores com View Transitions mostram a revelação circular; os demais recebem a troca de cores e imagens. A preferência por movimento reduzido é respeitada.

Os estilos compartilhados ficam em links `rel="stylesheet"` no `<head>` de cada página, seguidos dos estilos específicos daquela página. Não mova esses arquivos para imports dos módulos TypeScript: na navegação entre páginas, isso permitiria pintar o HTML e os ícones antes de carregar o CSS. O mesmo cabeçalho é preservado nas páginas inglesas; o Vite prepara esses links para o build. Os testes conferem os links e o carregamento real de CSS sem executar o módulo JavaScript.

O idioma automático segue `navigator.languages`, em ordem de preferência, aceitando português e inglês e usando inglês como alternativa. Os seletores **Auto**, **Português** e **English** ficam no topo em telas grandes, no menu móvel e no rodapé; nas páginas secundárias, ficam no topo. A escolha manual é salva em `miniinfos.site.language`, prevalece sobre a detecção e funciona mesmo sem armazenamento durante a visita. Trocar de idioma preserva o tema e a IA escolhida na demonstração.

O HTML português continua na raiz. O build gera `en/index.html`, `en/privacy.html`, `en/terms.html` e `en/404.html`, inclusive títulos e acessibilidade em inglês; esses arquivos também são legíveis sem JavaScript. Os arquivos são internos: as URLs públicas usam `/`, `/politica-de-privacidade` e `/termos-de-uso`, com `/en/`, `/en/privacy-policy` e `/en/terms-of-use` em inglês. A página de erro também tem `/pagina-nao-encontrada` e `/en/page-not-found`. O Vite e o CloudFront resolvem esses caminhos para os HTML, sem expor a extensão. Links antigos com `.html` e variantes com barra final recebem 301 para a URL canônica, preservando os parâmetros. `npm run dev` prepara essas páginas; execute `npm run locales` após editar o HTML de origem para atualizar as páginas inglesas durante o desenvolvimento. As marcações `data-i18n` traduzem somente texto, e `data-i18n-*` identifica atributos; preserve os ícones e a estrutura fora do texto traduzido. O build falha quando falta uma mensagem em inglês.

O cartão tem controles próprios: os dois ícones neutros selecionam uma IA, o botão de colunas restaura a divisão e atualizar repete a entrada das barras. O cartão do hero acompanha o tema do site, incluindo a faixa em gravura ou mármore, sem alterar as estátuas grandes.

Na seção Aparência, `card-carousel` alterna Uso e Conversas no mesmo espaço, pelas setinhas, teclas esquerda/direita ou arraste horizontal com mouse/toque. O gesto vertical continua rolando a página. Somente o cartão ativo participa da navegação por teclado e da leitura assistiva; a troca respeita movimento reduzido e não avança sozinha. Os botões Simples, Clássico escuro, Clássico claro e Claro aplicam o mesmo tema às duas prévias, mantendo o tema do site independente. A coruja permanece abaixo do carrossel.

Cada cartão ocupa a altura do próprio conteúdo. O cartão inativo continua disponível para a transição lateral, mas não determina a altura do carrossel nem estica o espaço entre os totais e os controles do cartão ativo.

As duas prévias reutilizam `usage-widget`: `card="conversations"` apresenta quatro chats fictícios, permite filtrar por IA e escolher números detalhados ou resumidos. Mudar de cartão, tema ou idioma preserva essas escolhas.

## Publicação

O site fica em `https://miniinfos.com`, servido pelo CloudFront a partir de um bucket S3 privado; `www` redireciona para o domínio sem `www`. A infraestrutura é a stack de `infra/site.yml`, criada uma vez pelo console da AWS em `us-east-1`; o domínio é registrado na Cloudflare, com os registros em DNS only. Rotas inexistentes recebem `404.html` com status 404. As URLs canônicas e o `og:image` usam esse domínio: ao trocá-lo, atualize os HTML e o parâmetro `DomainName` da stack.

Ao introduzir as URLs sem extensão, atualize a stack existente com `infra/site.yml` **antes de publicar o novo front**. A função `WwwRedirect` passa também a resolver as páginas e redirecionar os endereços antigos; continua com o mesmo nome e associação. A atualização do front sozinha envia arquivos e invalida o cache, mas não altera a stack. A tabela de rotas da função deve acompanhar `src/routing.ts`: os testes executam o JavaScript real do template para impedir divergências. Não é necessário tornar o bucket público ou adicionar permissões à role de deploy.

Cada push na `main` que altera `front/` dispara `.github/workflows/front-deploy.yml`: valida, gera o `dist/`, envia ao bucket e renova o cache. O job entra na AWS por OIDC, numa sessão de uma hora de uma role que só esta branch deste repositório assume e que só escreve neste bucket e renova esta distribuição. Não existe chave da AWS no repositório, nos secrets ou em máquina alguma; nunca crie uma. O repositório guarda os valores de saída da stack: `AWS_DEPLOY_ROLE_ARN` como secret, `SITE_BUCKET` e `SITE_DISTRIBUTION_ID` como variáveis. Sem eles o deploy é pulado.

As demonstrações e os downloads não usam chaves de API ou analytics. O envio opcional de criações exige secrets mantidos exclusivamente no Worker; nunca use uma variável `VITE_` para uma chave. Privacidade e Termos explicam o processamento local, a verificação Turnstile, os arquivos públicos do PR e os comprovantes privados.

## Comunidade

`/comunidade` e `/en/community` apresentam o catálogo público sem banco próprio ou cadastro. O menu da landing leva à página, que mantém o idioma e o tema do site. Busca aceita nomes, autores e descrições, sem diferenciar acentos; filtros separam temas e mascotes. A ordenação oferece destaques, nome e data de publicação do arquivo. A visão inicial mostra até três itens por seção; **Ver todos** abre a categoria e **Mostrar mais** pagina os resultados.

Os temas embutidos e a Coruja de Atena são identificados como **Incluído no app** e explicam onde selecioná-los nas configurações. Seus cartões usam o mesmo `usage-widget` da landing, com ícones neutros. Não ofereça um pacote de download fictício para esses itens. Os pacotes publicados apresentam a autoria e a licença declaradas pelos autores; **Baixar** aponta para o arquivo real `.miniinfos` e os detalhes explicam como importá-lo.

**Enviar criação** abre o criador na própria página. Aceita `.miniinfos`, ZIP ou os arquivos `manifest.json` e PNGs, mostra mensagens de validação, permite editar metadados e visualizar as animações do mascote. **Começar um tema** prepara um tema simples e oferece edição visual de cores. **Baixar meu pacote** exporta o conteúdo conferido, mesmo sem backend. O envio exige confirmação de autoria/licença e cria uma revisão pública por meio do serviço; o usuário não precisa lidar com fork ou PR. O mantenedor aprova o merge. O status e os dez últimos links de acompanhamento ficam acessíveis pelo criador; `#envio=<uuid>` permite retomá-lo em outro navegador.

Reenviar o mesmo conteúdo recupera a mesma revisão, sem criar outro PR. Falhas temporárias e excesso de tentativas mostram uma contagem regressiva antes de tentar novamente; o pacote continua disponível para exportação. O serviço compartilha cache de status, reservas por pacote e limite de chamadas ao GitHub entre visitantes.

Configure `COMMUNITY_API_URL` nas variáveis do repositório para o deploy, ou `VITE_COMMUNITY_API` no build local, com a URL HTTPS pública do Worker. A página consulta `/config` ao abrir o criador; sem serviço habilitado, mantém prévia e exportação e informa que o envio não está disponível. Não simule confirmação de publicação. O criador só baixa os arquivos escolhidos, e seu conteúdo nunca é executado. A integração não faz merge automático nem permite substituir IDs existentes anonimamente. Configuração e roteiro de conferência real em [`service/README.md`](service/README.md).

`npm run catalog` consulta exclusivamente `repositories/1432597159/releases/tags/catalog`, exige o pré-lançamento publicado e valida o índice, os nomes, tamanhos e a presença dos arquivos na release. Baixa somente os metadados e o `index.json`, nunca os arquivos de todos os pacotes. O resultado fica em `public/community/catalog.json`; esta cópia validada também permite builds sem conexão quando o GitHub estiver indisponível. Não usa `releases/latest`, que continua reservado aos downloads do aplicativo.

O build atualiza a cópia estática antes de gerar o site. Depois que **Publicar catálogo** termina com sucesso na `main`, `front-deploy.yml` também executa uma nova publicação do site, sem ampliar as permissões da role da AWS. O workflow usa o código da branch padrão, não artefatos ou código de PRs. Essa ligação começa a valer quando os workflows estiverem na `main`.

No navegador, a página lê apenas o JSON do próprio site. As imagens PNG das releases são solicitadas conforme as prévias se aproximam da tela; os arquivos `.miniinfos` só são baixados por escolha explícita. Falha na leitura do índice preserva os itens embutidos e oferece **Tentar novamente**. Nomes e descrições da comunidade são tratados como texto, sem HTML ou scripts. As traduções cobrem a interface; autoria, licença e descrições de terceiros são preservadas como publicadas.

Ao publicar a página pela primeira vez, atualize também a stack `infra/site.yml`: as novas rotas precisam da função do CloudFront, assim como as demais URLs sem extensão.

## Downloads por sistema

`src/downloads/` identifica Windows, macOS ou Linux sem tentar adivinhar a arquitetura de Macs a partir do navegador. A seção oferece Windows x64, Mac Apple Silicon/Intel e Linux x64/ARM64. Ao chegar à área de downloads ou clicar em uma CTA, consulta uma vez a release pública `repositories/1432597159/releases/latest`. Só oferece uma URL direta se o nome exato do pacote aparecer em uma release publicada do aplicativo. Rascunhos, pré-lançamentos, catálogo e URLs externas são rejeitados. Uma plataforma ainda não publicada mostra esse estado e leva à página de releases; erro de rede mantém o mesmo caminho de consulta manual. Não use URLs de assets futuros como se já estivessem disponíveis.

O início da página oferece atalhos visíveis para os três sistemas. O botão principal identifica o sistema do visitante e leva direto ao cartão correspondente, com destaque para o sistema detectado. Sem JavaScript, o botão principal continua levando à seção de downloads e os atalhos continuam funcionando. Em macOS e Linux, a arquitetura é escolhida explicitamente pelo visitante.

A recomendação usa o sistema informado por `userAgentData.platform`, quando disponível, com alternativa em `navigator.platform` e `userAgent`. Não solicita informações adicionais de arquitetura nem envia esses sinais a um servidor. O botão e o selo **Recomendado para seu sistema** acompanham o idioma da página. Celulares, iPads que se apresentam como Mac, Chrome OS e plataformas desconhecidas mantêm **Baixar MiniInfos** e a escolha manual, sem recomendar um instalador incompatível.

A consulta não envia dados do aplicativo; a política de privacidade informa a conexão com o GitHub. Os estados de download acompanham Português/English e os cartões acompanham os temas claro/escuro.
