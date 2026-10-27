# Landing page do MiniInfos

O site público do MiniInfos fica nesta pasta, separado do aplicativo, do mod e do marketplace. Ele apresenta o app e leva ao download do `MiniInfos-Setup.exe` da última versão.

## Regras

- **Fora do aplicativo.** Nada desta pasta entra no instalador: o build do .NET e o `installer/build.ps1` recusam arquivos de `front/`. O zip de código das releases do app também deixa a pasta de fora.
- **Fora dos testes do app.** Uma mudança só em `front/` seleciona o domínio `Front`: executa as checagens do site, sem rodar o widget. O CI do front é independente, em `.github/workflows/front.yml`.
- **Download pela release.** O botão de download aponta para o `MiniInfos-Setup.exe` da release marcada como latest, nunca para a release `catalog`.
- **Marcas.** Claude, Codex, ChatGPT e OpenAI aparecem só em texto, para dizer com quais apps o MiniInfos funciona. Use ícones neutros, sem os símbolos das empresas, e mantenha no rodapé o aviso de que o MiniInfos é independente, sem afiliação com a Anthropic ou a OpenAI.
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
- `src/features/`: menu responsivo e entradas de conteúdo ao aparecer na tela.
- `src/styles/`: estilos compartilhados, página e cartão.
- `src/config.ts`: destinos oficiais de download, código e guia da comunidade.
- `public/art/`: artes de Atena, cabeçalho, sprites e imagem de compartilhamento.
- `public/fonts/`: fontes locais e suas licenças. O site não consulta serviços de fontes.
- `tests/`: persistência, geometria da transição, destino do instalador e integridade dos recursos.

O site começa no escuro. Uma escolha explícita pelo claro fica salva em `localStorage`; bloquear esse armazenamento não impede trocar o tema. Navegadores com View Transitions mostram a revelação circular; os demais recebem a troca de cores e imagens. A preferência por movimento reduzido é respeitada.

O cartão tem controles próprios: os dois ícones neutros selecionam uma IA, o botão de colunas restaura a divisão e atualizar repete a entrada das barras. Os botões Simples, Clássico e Claro da seção Aparência alteram apenas o cartão de exemplo, sem confundir a aparência do app com o tema do site.

## Publicação

Publique o conteúdo de `dist/` em uma hospedagem estática com HTTPS, a partir da raiz do domínio. Configure a hospedagem para servir `404.html` com status 404 para rotas inexistentes. Os metadados de compartilhamento já têm arte e descrição; ao definir o domínio definitivo, use uma URL absoluta para `og:image` e acrescente a URL canônica.

Não são necessárias chaves de API. Não há analytics, formulários, consulta a contas ou dados pessoais nas demonstrações. Os textos de Privacidade e Termos descrevem essa página; devem acompanhar eventuais serviços adicionados na publicação.
