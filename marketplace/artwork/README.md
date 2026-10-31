# Fontes de ilustração

`community.buster-beaver/head-turns.png` contém 16 poses intermediárias geradas para o movimento de cabeça. A sequência é lida da esquerda para a direita, de cima para baixo, em uma grade 4 × 4 com transparência real. O empacotador coloca essas poses nas linhas Idle e Alert do atlas, alinhando todas com a mesma escala e o mesmo apoio dos pés. As outras oito linhas são preservadas pixel a pixel.

O aplicativo continua recebendo somente o pacote final em `marketplace/packages/`; estas fontes não fazem parte do pacote ou dos instaladores. O empacotador é idempotente e precisa de Node e `sharp` na ferramenta de arte local. Execute `node marketplace/artwork/build-buster.mjs`; se necessário, indique o módulo `sharp` disponível com a variável `MINIINFOS_SHARP_MODULE`.

Depois de reempacotar, valide o pacote com `dotnet run --project tools/MiniInfos.Catalog -c Release -- validate marketplace/packages/community.buster-beaver` e confira a animação na prévia do criador. Não publique um atlas que exceda os limites de pixels ou de tamanho do catálogo.
