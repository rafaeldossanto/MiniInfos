# Landing page do MiniInfos

O site público do MiniInfos fica nesta pasta, separado do aplicativo, do mod e do marketplace. Ele apresenta o app e leva ao download do `MiniInfos-Setup.exe` da última versão.

## Regras

- **Fora do aplicativo.** Nada desta pasta entra no instalador: o build do .NET e o `installer/build.ps1` recusam arquivos de `front/`. O zip de código das releases do app também deixa a pasta de fora.
- **Fora dos testes do app.** Uma mudança só em `front/` não roda os testes do widget, nem no executor local nem no CI. Quando o site tiver código próprio, ele ganha um domínio em `tests/domains.psd1`.
- **Download pela release.** O botão de download aponta para o `MiniInfos-Setup.exe` da release marcada como latest, nunca para a release `catalog`.
- **Marcas.** Claude, Codex, ChatGPT e OpenAI aparecem só em texto, para dizer com quais apps o MiniInfos funciona. Use ícones neutros, sem os símbolos das empresas, e mantenha no rodapé o aviso de que o MiniInfos é independente, sem afiliação com a Anthropic ou a OpenAI.
- **Dados de demonstração.** Prints e números de exemplo levam a marca "Demonstração" e não trazem dados pessoais.
