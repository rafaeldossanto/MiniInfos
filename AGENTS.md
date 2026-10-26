# Instruções do MiniInfos

Siga também as instruções do diretório pai: DRY, Clean Code, SOLID e código preparado para crescer.

## Organização do código

- A estrutura e as responsabilidades das pastas estão na seção "Organização do código" do `README.md`.
- Na raiz de `widget`, mantenha apenas o projeto e o ponto de entrada WPF (`App.xaml` e seu code-behind). Coloque cada implementação na pasta da sua responsabilidade.
- Use namespaces que acompanhem as pastas, começando por `MiniInfos`. Os testes seguem `MiniInfos.Tests` e espelham os grupos do aplicativo.
- Mantenha cada XAML junto do seu code-behind. Recursos visuais compartilhados ficam em `UI/Styles`, `UI/Themes`, `UI/Icons` ou `Assets`.
- Contratos e modelos usados por Claude e Codex ficam em `Providers`; implementações específicas ficam em `Providers/Claude` e `Providers/Codex`.
- Compartilhe leitura incremental, persistência e acesso ao Windows por meio dos componentes de infraestrutura existentes. Evite duplicá-los em cada funcionalidade ou criar uma pasta genérica de utilitários sem responsabilidade definida.
- Separe contratos e persistência dos controles visuais. Extraia responsabilidades quando necessário, sem introduzir camadas ou interfaces que não resolvam um problema concreto.
- Ao mover arquivos, atualize namespaces, referências XAML, URIs de recursos, documentação e caminhos usados pelo instalador.
- O formato de temas e mascotes da comunidade fica em `Community/` e é documentado em `marketplace/`. Preserve a compatibilidade do `schemaVersion`; novos recursos precisam acompanhar validação, JSON Schema e testes. Pacotes são dados declarativos locais: não carregue código, XAML ou recursos remotos fornecidos por um pacote.
- O `index.json` segue a mesma regra de compatibilidade de `schemaVersion` dos pacotes. O marketplace pertence ao mesmo monorepo, em `marketplace/`; o ID numérico do repositório é compartilhado com o atualizador. Releases do app nascem como rascunho no CI, ao enviar a tag `vX.Y.Z`, e são publicadas depois de revisadas, com `--draft=false --latest`; a tag `catalog` usa `--prerelease --latest=false`, nunca latest.

## Validação

- Valide o domínio alterado e seus consumidores reais. Não execute toda a regressão, todos os cenários visuais ou o instalador a cada alteração isolada.
- Use `./tests/run.ps1` para selecionar os domínios a partir dos arquivos alterados, incluindo arquivos novos e os dois caminhos de arquivos movidos. `./tests/run.ps1 -Plan` mostra a seleção sem executar testes.
- Para um escopo explicitamente conhecido, use `./tests/run.ps1 -Domain Mascot` ou uma lista, como `-Domain Claude,Codex`. A seleção manual não calcula impacto: em mudanças compartilhadas, prefira a seleção automática.
- Acrescente `-Ui` quando a alteração exigir verificar interação, layout ou recursos WPF. Exemplo: `./tests/run.ps1 -Domain Mascot -Ui` executa somente os testes e o cenário do mascote, sem percorrer temas ou fontes.
- `tests/domains.psd1` é o mapa compartilhado pelo executor local e pelo CI. Ao criar um domínio ou mudar uma dependência, atualize o mapa e valide com `./tests/TestSelection.Tests.ps1`. Caminhos sem regra selecionam a regressão completa por precaução.
- Reserve `./tests/run.ps1 -All` e `-All -Ui` para mudanças transversais, no executor compartilhado de testes, na estrutura do projeto ou para uma regressão de entrega. Na `main`, o CI mantém a regressão completa de unidade; na `dev` e em PRs, usa o impacto dos arquivos alterados.
- Os cenários visuais focados usam dados simulados e saída resumida. `Providers` e a execução visual completa mantêm a integração com os provedores reais. Guarde os detalhes para falhas; não repita verificações aprovadas sem uma nova mudança ou preocupação concreta.
- Execute `claude plugin test mod` somente quando o mod for alterado. O runner informa essa necessidade, pois o teste exige o ambiente do Claude Code.
- Gere o instalador com `powershell -File installer/build.ps1` quando houver uma entrega solicitada ou uma alteração de empacotamento a validar.
