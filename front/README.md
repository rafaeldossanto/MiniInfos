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

Não há framework de interface ou serviço de backend em tempo de execução. Vite prepara os arquivos estáticos; o HTML contém o conteúdo da página, e TypeScript acrescenta os controles interativos.

## Organização

- `index.html`: conteúdo, navegação, demonstrações e metadados.
- `src/components/`: demonstração reutilizável do cartão, com dados fictícios.
- `src/theme/`: preferência de tema e transição circular de 1,2 segundo.
- `src/localization/`: detecção do idioma, preferência salva e catálogo inglês. As mensagens em português são as chaves de `en.json`.
- `scripts/localized-pages.mjs`: gera as páginas inglesas a partir do mesmo HTML e catálogo; `en/` é gerada e não deve ser editada.
- `src/features/`: menu responsivo e entradas de conteúdo ao aparecer na tela.
- `src/styles/`: estilos compartilhados, página e cartão.
- `src/config.ts`: destinos oficiais de download, código e guia da comunidade.
- `public/art/`: artes de Atena, cabeçalho, sprites e imagem de compartilhamento.
- `public/fonts/`: fontes locais e suas licenças. O site não consulta serviços de fontes.
- `tests/`: persistência, geometria da transição, destino do instalador e integridade dos recursos.

O site começa no escuro. Uma escolha explícita pelo claro fica salva em `localStorage`; bloquear esse armazenamento não impede trocar o tema. Navegadores com View Transitions mostram a revelação circular; os demais recebem a troca de cores e imagens. A preferência por movimento reduzido é respeitada.

O idioma automático segue `navigator.languages`, em ordem de preferência, aceitando português e inglês e usando inglês como alternativa. Os seletores **Auto**, **Português** e **English** ficam no topo em telas grandes, no menu móvel e no rodapé; nas páginas secundárias, ficam no topo. A escolha manual é salva em `miniinfos.site.language`, prevalece sobre a detecção e funciona mesmo sem armazenamento durante a visita. Trocar de idioma preserva o tema e a IA escolhida na demonstração.

O HTML português continua na raiz. O build gera `en/index.html`, `en/privacy.html`, `en/terms.html` e `en/404.html`, inclusive títulos e acessibilidade em inglês; esses arquivos também são legíveis sem JavaScript. As URLs usam arquivos explícitos para funcionar no S3/CloudFront sem reescrita de diretórios. `npm run dev` prepara essas páginas; execute `npm run locales` após editar o HTML de origem para atualizar as páginas inglesas durante o desenvolvimento. As marcações `data-i18n` traduzem somente texto, e `data-i18n-*` identifica atributos; preserve os ícones e a estrutura fora do texto traduzido. O build falha quando falta uma mensagem em inglês.

O cartão tem controles próprios: os dois ícones neutros selecionam uma IA, o botão de colunas restaura a divisão e atualizar repete a entrada das barras. O cartão do hero acompanha o tema do site, incluindo a faixa em gravura ou mármore, sem alterar as estátuas grandes.

Na seção Aparência, `card-carousel` alterna Uso e Conversas no mesmo espaço, pelas setinhas, teclas esquerda/direita ou arraste horizontal com mouse/toque. O gesto vertical continua rolando a página. Somente o cartão ativo participa da navegação por teclado e da leitura assistiva; a troca respeita movimento reduzido e não avança sozinha. Os botões Simples, Clássico escuro, Clássico claro e Claro aplicam o mesmo tema às duas prévias, mantendo o tema do site independente. A coruja permanece abaixo do carrossel.

As duas prévias reutilizam `usage-widget`: `card="conversations"` apresenta quatro chats fictícios, permite filtrar por IA e escolher números detalhados ou resumidos. Mudar de cartão, tema ou idioma preserva essas escolhas.

## Publicação

O site fica em `https://miniinfos.com`, servido pelo CloudFront a partir de um bucket S3 privado; `www` redireciona para o domínio sem `www`. A infraestrutura é a stack de `infra/site.yml`, criada uma vez pelo console da AWS em `us-east-1`; o domínio é registrado na Cloudflare, com os registros em DNS only. Rotas inexistentes recebem `404.html` com status 404. As URLs canônicas e o `og:image` usam esse domínio: ao trocá-lo, atualize os HTML e o parâmetro `DomainName` da stack.

Cada push na `main` que altera `front/` dispara `.github/workflows/front-deploy.yml`: valida, gera o `dist/`, envia ao bucket e renova o cache. O job entra na AWS por OIDC, numa sessão de uma hora de uma role que só esta branch deste repositório assume e que só escreve neste bucket e renova esta distribuição. Não existe chave da AWS no repositório, nos secrets ou em máquina alguma; nunca crie uma. O repositório guarda os valores de saída da stack: `AWS_DEPLOY_ROLE_ARN` como secret, `SITE_BUCKET` e `SITE_DISTRIBUTION_ID` como variáveis. Sem eles o deploy é pulado.

Não são necessárias chaves de API. Não há analytics, formulários, consulta a contas ou dados pessoais nas demonstrações. Os textos de Privacidade e Termos descrevem essa página; devem acompanhar eventuais serviços adicionados na publicação.

## Downloads por sistema

`src/downloads/` identifica Windows, macOS ou Linux sem tentar adivinhar a arquitetura de Macs a partir do navegador. A seção oferece Windows x64, Mac Apple Silicon/Intel e Linux x64/ARM64. Ao chegar à área de downloads ou clicar em uma CTA, consulta uma vez a release pública `repositories/1432597159/releases/latest`. Só oferece uma URL direta se o nome exato do pacote aparecer em uma release publicada do aplicativo. Rascunhos, pré-lançamentos, catálogo e URLs externas são rejeitados. Uma plataforma ainda não publicada mostra esse estado e leva à página de releases; erro de rede mantém o mesmo caminho de consulta manual. Não use URLs de assets futuros como se já estivessem disponíveis.

O início da página oferece atalhos visíveis para os três sistemas. O botão principal identifica o sistema do visitante e leva direto ao cartão correspondente, com destaque para o sistema detectado. Sem JavaScript, o botão principal continua levando à seção de downloads e os atalhos continuam funcionando. Em macOS e Linux, a arquitetura é escolhida explicitamente pelo visitante.

A consulta não envia dados do aplicativo; a política de privacidade informa a conexão com o GitHub. Os estados de download acompanham Português/English e os cartões acompanham os temas claro/escuro.
