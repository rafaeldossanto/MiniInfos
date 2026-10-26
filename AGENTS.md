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

## Validação

- `dotnet test tests/MiniInfos.Tests -c Release`
- `dotnet run --project tests/MiniInfos.UiCheck -c Release` quando a mudança afetar janelas, recursos ou referências WPF. Esse comando abre janelas reais e usa dados temporários.
- `claude plugin test mod` quando o mod for alterado.
- `powershell -File installer/build.ps1` para gerar o instalador após validar o aplicativo.
