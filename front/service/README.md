# Envio de criações

O site prepara temas e mascotes localmente. Este serviço opcional recebe o pacote, repete as verificações de formato e cria uma branch e um PR no monorepo. O workflow **Validar marketplace** usa o validador C# real do aplicativo. Só o mantenedor faz o merge. **Publicar catálogo** e **Front deploy** publicam a criação depois da aprovação. Não há merge automático nem escrita na main pelo serviço.

## Componentes

- Worker TypeScript, compartilhando `src/community/creation/package.ts` com a prévia. A conferência no navegador serve de feedback, não de autorização para publicar.
- R2 privado guarda comprovantes (UUID de acompanhamento, ID e versão do pacote, número do PR, SHA, data e estado), hashes de conteúdo, aliases, prazos de espera, reservas de operação e orçamento compartilhado de chamadas. Não guarda o ZIP, IP, email ou chaves.
- GitHub App instalado somente no MiniInfos. O serviço reaproveita tokens de instalação até um minuto antes do vencimento, exclusivamente em memória, e só escreve arquivos em `marketplace/packages/<id>/`, numa branch `community-submit/<id>`. Modos dos arquivos são sempre `100644`.
- Turnstile verificado no servidor, com hostname e action esperados. Limitadores separados para envio (3/minuto) e consulta (60/minuto). CORS aceita somente `SITE_ORIGIN`.
- Sem banco próprio, cadastro, cookies ou telemetria de visitantes. O criador pode preparar, testar e exportar mesmo sem o serviço configurado.

## Preparar e verificar sem publicar

Na pasta `front/`:

```powershell
npm ci
npm run service:check
npm run service:bundle
npm test
```

`service:bundle` usa `wrangler deploy --dry-run`: gera o bundle sem criar recursos ou publicar. `service:dev` inicia o Worker local. Sem bindings/chaves completas, `/config` informa `enabled: false`; o serviço recusa uploads. Não existe modo de produção que dispense a verificação contra abuso.

Os testes simulam os serviços externos e verificam a assinatura real dos JWTs, o escopo do token, os caminhos escritos, repetição do envio, concorrência, espera, recuperação após perda da resposta do PR, revisão, publicação e falhas. Também verificam ZIPs com metadados inconsistentes e expansão além do tamanho declarado. Nenhum teste abre PR, envia criação ou cria recurso remoto.

## Ativação pelo responsável

Esta é uma etapa de infraestrutura separada. O código não cria conta, bucket, GitHub App, credencial ou domínio automaticamente.

1. Crie um GitHub App privado e instale somente no repositório **MiniInfos**, ID **1432597159**. Permissões de repositório: **Contents: read and write**, **Pull requests: read and write**, **Checks: read** e **Actions: read**. Sem permissões de administração, workflows ou organização. Anote App ID e Installation ID. Ative a exigência de revisão do mantenedor e o check `catalog` na proteção da main; não conceda bypass ao App.
2. Gere a chave privada do App. O serviço aceita o PEM PKCS#1 fornecido pelo GitHub e PKCS#8. Guarde como secret `GITHUB_PRIVATE_KEY` no Worker, pelo painel Cloudflare ou `wrangler secret put GITHUB_PRIVATE_KEY --config service/wrangler.jsonc`. Nunca use `VITE_` para secrets, nem envie a chave ao chat ou ao GitHub.
3. Prepare um bucket **privado** R2, `miniinfos-community-receipts`, e mantenha o binding `RECEIPTS`. Não habilite acesso público ou domínio para o bucket. Os comprovantes são persistentes para os links continuarem funcionando; a retenção deve ser decidida antes de uma eventual limpeza. Os arquivos públicos ficam no PR, não nesse bucket.
4. Cadastre o Turnstile para `miniinfos.com`. Configure a site key pública e guarde a secret key em `TURNSTILE_SECRET`, também pelo painel ou `wrangler secret put`. A action do formulário é `community-submit`.
5. Preencha `GITHUB_APP_ID`, `GITHUB_INSTALLATION_ID`, `TURNSTILE_SITE_KEY` e `SITE_ORIGIN` em `wrangler.jsonc`. Publique o Worker somente após conferir a configuração e o plano Cloudflare. O processamento de pacotes grandes pode exigir aumentar o orçamento de CPU do Worker; os limites de pacote não dispensam os limites do plano escolhido.
6. Configure a variável pública do repositório `COMMUNITY_API_URL` com a URL HTTPS do Worker. O build usa `VITE_COMMUNITY_API`, que contém **somente a URL pública**. A hospedagem S3/CloudFront do site continua independente. Não é necessário mudar o DNS do domínio principal nem o bucket do site.
7. Publique a versão do site e teste um envio de tema e um de mascote de verdade. Confirme PR, check C#, revisão, merge, release `catalog` e atualização do site. Enquanto isso não for feito, não anuncie uploads como disponíveis em produção.

O Wrangler solicita uma sessão Cloudflare normal para publicar. Confira o plano e os custos antes de criar recursos; não há promessa de operação gratuita. A compilação e os testes locais não comprovam configuração de uma conta real.

## Contrato HTTP

Todas as chamadas incluem `Origin` igual a `SITE_ORIGIN`; respostas usam `no-store`, sem cookies. O endpoint base é exclusivo deste Worker.

- `GET /config`: disponibilidade e site key pública.
- `POST /submissions`: `Content-Type: application/zip`, até 8 MB. Cabeçalhos: `X-Turnstile-Token`, `X-Submission-Id` (UUID v4 gerado pelo navegador, preservado numa tentativa repetida) e `X-Rights-Confirmed: true`. Resposta 201 contém comprovante; 200 recupera um envio já recebido. 409 indica ID de pacote existente ou branch já reservada; não atualiza pacotes de terceiros.
- `GET /submissions/<uuid>`: comprovante e estado. O UUID é um link de acompanhamento: trate-o como privado. O site o coloca no fragmento da URL e não o transmite como referer. É salvo no navegador apenas um histórico dos dez últimos IDs, sem arquivos.
- `429` inclui `Retry-After` e `{ "code": "cooldown", "retryAfter": <segundos> }`. O formulário mostra a contagem regressiva e conserva a criação para tentar novamente. Comprovantes também incluem `retryAfter` para a próxima consulta.

Estados: `validating`, `review`, `changes`, `publishing`, `published`, `rejected`, `unavailable`. Somente o check `catalog` concluído com sucesso pelo GitHub Actions gera `review`; check ausente/em andamento continua `validating`, e falha/skipped/cancelado gera `changes`. Um merge gera `publishing`; `published` exige que o índice servido pelo site contenha o ID e a versão exatos.

Atualizações de pacotes existentes continuam passando pelo mantenedor, para não permitir que um upload anônimo assuma a autoria de um pacote já publicado. Não ofereça edição ou exclusão de terceiros sem uma futura forma de autenticação de autoria.

## Repetição, espera e recuperação

O serviço calcula SHA-256 dos nomes e conteúdos dos arquivos validados. O mesmo conteúdo reutiliza o comprovante e o PR, mesmo com outro UUID, compressão ou data do ZIP. Um UUID não pode ser reaproveitado para conteúdo diferente. Escritas condicionais no R2 reservam a operação por pacote por até dois minutos, inclusive entre instâncias do Worker. Tentativas simultâneas aguardam; não criam PRs paralelos. O pequeno backoff das escritas trata a limitação por chave do R2 sem retirar a condição de concorrência.

Falhas temporárias de envio impõem espera de 60 segundos e permitem nova tentativa depois. Quando o GitHub pede mais tempo, o prazo maior vale para todos os visitantes. O commit e o PR levam uma marca derivada do conteúdo e do hash do UUID, sem publicar o próprio UUID. Se a resposta se perder, a nova tentativa só recupera uma branch/PR com essa mesma marca; nunca sobrescreve uma branch conflitante. Um envio interrompido sem PR deixa de aparecer como validação interminável e orienta reenviar os arquivos após o prazo.

Consultas de estado usam cache persistente de 60 segundos, inclusive após falha, e uma única atualização por pacote. As chamadas ao GitHub compartilham um teto conservador de 3.000 por janela de uma hora: cada operação reserva seu máximo antes da primeira chamada (3 para consulta; quantidade de arquivos + 14 para envio/recuperação). A reserva não usada não é devolvida; ela privilegia preservar a cota. Essa proteção complementa os limites por IP, não depende apenas deles. Tokens, chaves e respostas internas do GitHub nunca entram no comprovante público.

Referências de persistência: [escritas condicionais no R2](https://developers.cloudflare.com/r2/api/workers/workers-api-reference/), [limites por chave](https://developers.cloudflare.com/r2/platform/limits/) e [código de erro de limitação](https://developers.cloudflare.com/r2/api/error-codes/).

## Limites e publicação

ZIP de até 8 MB; até 128 arquivos; 32 MB por arquivo e 64 MB descompactado; imagens PNG de até 8192 por lado e 16.777.216 pixels. ZIP64, entradas criptografadas, links simbólicos, caminhos externos, duplicados por diferença de maiúsculas e executáveis são recusados. JSON desconhecido, sprites fora da grade e animações inválidas são recusados. O validador C# continua sendo a autoridade de compatibilidade, incluindo decodificação das imagens, antes da revisão.

A extração confere os cabeçalhos locais e o diretório central como uma única estrutura, verifica CRC e descompacta DEFLATE em pequenos blocos, conferindo o tamanho real durante a leitura. Não entrega o ZIP completo a outro parser que possa interpretar metadados diferentes. ZIPs comuns, inclusive com data descriptors, continuam aceitos.

O serviço e suas dependências de desenvolvimento ficam dentro de `front/`, que já é excluída dos instaladores e do arquivo de código do aplicativo. O Vite não publica os fontes do serviço no `dist`, e bloqueia secrets locais, chaves PEM e diretórios de execução no servidor de desenvolvimento. `.dev.vars`, `.wrangler` e `.output` não entram no Git.

Referências: [GitHub Apps](https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/authenticating-as-a-github-app-installation), [R2 em Workers](https://developers.cloudflare.com/r2/api/workers/workers-api-usage/), [Turnstile no servidor](https://developers.cloudflare.com/turnstile/get-started/server-side-validation/), [rate limit bindings](https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/).
