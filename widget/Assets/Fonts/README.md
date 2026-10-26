# Fontes do MiniInfos

Arquivos locais embutidos como recursos WPF. Nenhuma fonte precisa estar instalada no Windows.

| Família | Origem | Pesos incluídos |
| --- | --- | --- |
| Manrope | https://github.com/google/fonts/tree/main/ofl/manrope | 400, 500, 600 |
| Geist Mono | https://github.com/google/fonts/tree/main/ofl/geistmono | 400, 600 |
| Cinzel | https://github.com/google/fonts/tree/main/ofl/cinzel | 400, 600 |
| Bodoni Moda | https://github.com/google/fonts/tree/main/ofl/bodonimoda | 400, 600, tamanho óptico 11 |
| Chakra Petch | https://github.com/google/fonts/tree/main/ofl/chakrapetch | 400, 600 |
| Orbitron | https://github.com/theleagueof/orbitron | Light (300), Medium (500) |

Manrope, Geist Mono, Cinzel e Bodoni Moda são instâncias estáticas dos arquivos variáveis do Google Fonts, geradas com `fontTools.varLib.instancer`. As instâncias de Geist Mono, Cinzel e Bodoni Moda têm nomes de família e estilo normalizados para o agrupamento de pesos no WPF. Bodoni Moda não mantém a tabela variável `STAT`, para que o WPF use o nome estático em vez de criar outra família por tamanho óptico. Chakra Petch e Orbitron são os arquivos estáticos originais, sem modificações. Orbitron usa os arquivos `Orbitron Light.ttf` e `Orbitron Medium.ttf` do autor, preservando o nome reservado.

Cada família acompanha sua licença OFL; `OFL.txt` corresponde a Manrope e os demais arquivos identificam a família no nome. Preserve esses avisos na distribuição.
