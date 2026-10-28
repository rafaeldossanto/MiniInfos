# MiniInfos para Linux e macOS

Interface desktop nativa em Avalonia 12.1.4 / .NET 10. O Windows continua usando o aplicativo WPF em `widget/` e seu instalador atual. Não é necessário instalar o .NET para usar os pacotes publicados.

## Código compartilhado

`MiniInfos.Desktop.csproj` compila as mesmas fontes de `widget/Providers`, `Sources`, `Settings`, `Localization`, `Infrastructure/Storage`, `Infrastructure/Network` e `Infrastructure/Desktop`. Os ledgers e a descoberta dos históricos têm uma implementação só. O formato dos dados e dos pacotes da comunidade permanece compatível com a versão Windows.

O projeto nativo fornece seus controles em `UI/`, integração de login e instância única em `Platform/` e rasterização Skia em `Mascot/`. Os gestos, interpolação de quadros, anatomia, pêndulo e queda do mascote são compartilhados com WPF. `#if AVALONIA` fica limitado às diferenças de geometria e decodificação de imagens. Não há referências WPF/Windows Forms nos binários de Linux/macOS.

As cores dos temas vêm dos mesmos arquivos confiáveis de `widget/UI/Themes`. Esse XML é lido como dados de cores e metadados, nunca executado como XAML. O Clássico original, o Clássico escuro e o Clássico claro continuam separados. Os pacotes da comunidade são validados pelo mesmo contrato schemaVersion 1; importação, exportação, remoção e catálogo usam a mesma implementação do Windows. O catálogo continua na tag fixa `catalog`, com download do índice, das prévias da página visível e do pacote solicitado.

## Executar e testar

```sh
dotnet run --project desktop
dotnet test tests/MiniInfos.Desktop.Tests
python -m unittest discover -s desktop/packaging/tests -v
node --test mod/tests/platforms.test.mjs
```

O teste completo dos hooks continua sendo `claude plugin test mod`, dentro do ambiente do Claude Code. Os testes Node verificam os caminhos e os comandos de abertura por sistema; não substituem a execução dos hooks no host.

No PowerShell, `./desktop/test.ps1 -Base <commit>` reutiliza o mapa de domínios para testar só os consumidores nativos afetados. `-All` faz a regressão nativa. Mudanças isoladas em `desktop/UI` não executam testes WPF. O workflow `desktop-native.yml` valida Linux x64/ARM64 e Mac Intel/Apple Silicon em runners nativos. `--smoke-ui` exercita a interface com dados simulados, incluindo temas, fontes, dois idiomas, divisão e mascote; exige `MINIINFOS_DATA_DIR` para isolar os dados do teste.

`MiniInfos.Desktop --diagnostics` faz uma leitura real sem interface e imprime SO, arquitetura e contagens, sem conversas nem credenciais. Para testar sem alterar os caches normais, defina `MINIINFOS_DATA_DIR` para uma pasta temporária.

## Gerar pacotes

```sh
python desktop/packaging/package.py --runtime linux-x64
python desktop/packaging/package.py --runtime linux-arm64
python desktop/packaging/package.py --runtime osx-x64
python desktop/packaging/package.py --runtime osx-arm64
```

Saída em `desktop/dist/`, fora do Git. Cada pacote acompanha SHA-256. O script aceita compilação cruzada. Em Windows/Linux, Mac gera `*-unsigned.app.zip`, com permissões Unix no arquivo ZIP. Em um Mac, gera DMG com `MiniInfos.app` e o atalho para Aplicativos. O `.app` contém ícone, Info.plist, executável nativo e runtime. O Linux usa `.tar.gz` com executável, iniciador e `install.sh`, que instala no diretório de dados do usuário e cria a entrada no menu de aplicativos.

O script valida a fronteira de publicação: os pacotes não incluem a landing page, ferramentas de criação, fontes dos testes ou pacotes do marketplace. O mod acompanha o aplicativo, sem seus testes. Os pacotes são gerados a partir de `dotnet publish`, com o mesmo número de versão do projeto Windows.

## Publicação e assinatura Apple

Uma tag `vX.Y.Z` gera o instalador Windows e os quatro pacotes nativos. O job final verifica os cinco arquivos e seus hashes antes de criar uma release **draft**, sem latest. Publicar continua sendo uma ação manual após a revisão; a release `catalog` não participa desse fluxo.

Para assinar/notarizar os DMGs de produção, configure os secrets do repositório:

- `MACOS_CERTIFICATE_P12`: certificado Developer ID Application exportado em P12, codificado em Base64.
- `MACOS_CERTIFICATE_PASSWORD` e `MACOS_SIGN_IDENTITY`.
- `APPLE_ID`, `APPLE_APP_PASSWORD` e `APPLE_TEAM_ID`: credenciais de notarização.

O CI importa o certificado em um keychain temporário, assina os componentes Mach-O, habilita o JIT necessário ao .NET, notariza, grampeia o ticket e verifica o aplicativo com Gatekeeper. O keychain é removido ao fim do job. Sem certificado, o DMG recebe assinatura ad hoc para revisão; **não equivale a um aplicativo notarizado e pode ser bloqueado pelo Gatekeeper**. Não habilite o início no login antes de mover o aplicativo do DMG para Aplicativos.

## Sistemas e particularidades

- macOS 14 ou posterior, Intel x64 ou Apple Silicon ARM64. A validação nativa automatizada usa macOS 15. Para suporte de produção, revise a matriz de suporte do Avalonia e do .NET junto de cada atualização.
- Linux com glibc, x64 ou ARM64, X11 ou XWayland. Validação automatizada em Ubuntu 24.04. Dependências gráficas: `libx11-6`, `libice6`, `libsm6`, `libfontconfig1`; também são necessárias as dependências nativas do .NET 10, incluindo ICU, OpenSSL e zlib. Não são pacotes para Alpine/musl.
- Posição absoluta, ordem das janelas e ícone de bandeja dependem do gerenciador de janelas. O backend distribuído é X11; não é um backend Wayland nativo. No GNOME, a bandeja pode exigir suporte a AppIndicator. Abrir o aplicativo novamente restaura os cartões pela comunicação de instância única.
- A identificação de aplicativos abertos no Unix usa processos, pois não há uma API de janelas portável equivalente ao Windows. A opção de seguir aplicativos pode ser desativada nas configurações.
- Claude exige o mod para percentuais do plano. Tokens locais funcionam sem ele. Codex consulta o app-server da instalação local. Não são incluídos os históricos das versões web de Gemini/Kimi.

Dados no Mac: `~/Library/Application Support/MiniInfos`. Dados no Linux: `${XDG_DATA_HOME:-~/.local/share}/MiniInfos`. `CLAUDE_CONFIG_DIR`, `CODEX_HOME` e `KIMI_CODE_HOME` são respeitados; o mod registra configurações movidas para a interface encontrá-las mesmo quando aberta fora do terminal. O PATH inclui instalações usuais por usuário, Homebrew e NVM.

Referências: [macOS](https://docs.avaloniaui.net/docs/deployment/macos), [Linux](https://docs.avaloniaui.net/docs/deployment/linux), [plataformas](https://docs.avaloniaui.net/docs/supported-platforms).
