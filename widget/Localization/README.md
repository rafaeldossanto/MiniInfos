# Idiomas do aplicativo

`Localizer` detecta o idioma de exibição do Windows ao iniciar. `Automatic` usa português para culturas `pt` e inglês para as demais. `Preferences.Language` permite substituir essa escolha, sem alterar a cultura de dados dos provedores, IDs de temas, nomes do manifesto ou arquivos do usuário.

As mensagens originais em português são as chaves de `en.json`, embutido no executável. No código, use `L.Text("mensagem")` ou `L.Format("mensagem {0}", valor)`; preserve todos os argumentos na tradução. A formatação visual passa por `UI/Formatting/Format`, usando `L.Culture`. Nomes de projetos, conversas e pacotes da comunidade continuam sendo conteúdo do usuário.

No XAML, use `Text="{loc:Text 'mensagem'}"` e o namespace `clr-namespace:MiniInfos.Localization`. A extensão mantém um binding vivo com eventos fracos. Controles montados em código acompanham `L.Current.Changed`; removem a inscrição ao fechar. Guarde chaves de mensagens, em vez de traduções já resolvidas, em campos estáticos.

Novas mensagens devem ser acrescentadas ao catálogo inglês. Uma chave ausente mantém o português como alternativa, sem impedir o aplicativo de funcionar. A validação dos idiomas fica no domínio `Localization`: `./tests/run.ps1 -Domain Localization -Ui` cobre detecção, persistência, números, argumentos e a troca ao vivo em configurações, menus, biblioteca, exemplos e atualizações. Os testes existentes usam português explicitamente; os testes que trocam a cultura compartilhada ficam numa coleção sem paralelismo.
