# Mod DualSense para o ReStreet: relatório final

Versão final, 21/10/2026. Aplicado e confirmado no jogo numa instalação do ReStreet 2.0.

## 1. Resumo

O ReStreet é a versão de Xbox 360 do FIFA Street (2012) recompilada pro PC, então toda a arte de controle é de Xbox. O mod troca essa arte pela do controle de PlayStation 5 (DualSense). Ele só altera **dados do jogo**: `data0.big`, `data1.big` e os índices `.bh` deles. O executável, o runtime e o launcher não mudam.

| Área | O que muda no jogo | Arquivo | Como | No jogo |
|---|---|---|---|---|
| Prompts de botão (menus e HUD) | A/B/X/Y → ✕/○/□/△, LB/RB → L1/R1, LT/RT → L2/R2, LS/RS → L/R, L3/R3, START → Options, BACK → Create, Guide → PS | `data0.big` | 35 texturas: o símbolo é redesenhado e o contorno original fica | Confirmado |
| Lista de skill moves | RS/RB/LB → R/R1/L1/R3 | `data1.big` | 38 entradas redirecionadas para o conjunto PS3 que o jogo já tem | Aplicado |
| Dicas de skill move na partida | Mesma troca, com o verde do HUD | `data1.big` | 37 texturas montadas a partir das imagens de PS3 | Aplicado |
| Seleção de lados | Controle de 360 → DualSense branco, com a barra de cor do jogador e a silhueta dos slots vazios | `data1.big` | 5 texturas em 2 arquivos | Confirmado |
| Telas de controles (loading ×2, Manual do Jogo, configurações) | Layout PlayStation, com as linhas de legenda apontando pros botões do DualSense | `data1.big` | 1 constante trocada em 5 telas + 4 fotos trocadas | Confirmado |

## 2. Como instalar

Tudo fica numa pasta só, `ReStreet-DualSense` (no projeto, `private/ReStreet-DualSense`, ~76 MB), gerada por `pack.ps1`:

```
ReStreet-DualSense/
  Instalar DualSense.cmd     dois cliques para aplicar
  Remover DualSense.cmd      dois cliques para voltar ao original
  ReStreetDualSense.exe      o patcher (exe único, já traz o .NET; ~67 MB)
  LEIA-ME.txt                instruções curtas
  DualSense/                 a arte que o instalador aplica (22 PNG)
  Relatorio/                 este relatório
  Codigo-fonte/              o código da ferramenta (compila sozinho: dotnet build)
  Imagens-originais/         as imagens de DualSense usadas, os recortes e os centros dos botões
  Referencias/               folhas de referência: templates, antes/depois, simulações
```

Pra gerar a pasta de novo:

```bash
powershell -ExecutionPolicy Bypass -File Tools/ReStreetAssetScan/pack.ps1 -Art private/dualsense-art -Output private/ReStreet-DualSense -Originals "private/dualsense*.png,private/dualsense-controles-points.json" -References "private/dualsense-reference/*.png,private/button-art/_sheets/*.png"
```

1. **Feche o FIFA Street.** O launcher pode ficar aberto. Se o jogo estiver rodando, o instalador avisa e não mexe em nada.
2. Dê dois cliques em **Instalar DualSense.cmd**. A instalação do ReStreet é encontrada sozinha:
   1. pela pasta arrastada pra cima do `.cmd`, se houver;
   2. pela pasta onde o exe estiver, se ele for colocado dentro do ReStreet;
   3. pelo atalho `ReStreet.lnk` da Área de Trabalho ou do Menu Iniciar;
   4. por uma busca rasa nos discos.

   Em todos os casos, o `settings.json` do launcher diz qual pasta `Game` usar. Se houver mais de uma instalação e o atalho não resolver, ele pergunta qual.
3. **Antes de atualizar ou reparar o ReStreet:** rode **Remover DualSense.cmd**, atualize pelo launcher e instale de novo.

Pelo terminal: `ReStreetDualSense.exe install|uninstall|status [pasta do ReStreet]`, ou, no modo avançado, `dualsense apply|revert|status|preview <pasta Game> --controller ps5 [--art pasta]`.

## 3. Como os dados do jogo estão organizados

**BIG** (EA):
- cabeçalho `BIG4`/`BIGF` com número de entradas e tamanho do diretório, em big-endian;
- uma lista de entradas com offset, tamanho e nome.

Cada `.big` tem um índice `.bh` (`ViV4`) com registros de 20 bytes. Offset e tamanho aparecem nos dois lugares, então os dois são atualizados juntos.

Formatos dentro dos BIG:
- **`chunklzx`:** LZX do Xbox, da mesma família do CAB da Microsoft, em blocos de 256 KB com frames de 32 KB e janela de 2¹⁷. Cada tela de UI é um **BIG interno** nesse formato, com o filme Apt, as geometrias e as texturas.
- **RefPack** (`10 FB`): o LZ77 da EA, sem codificação de entropia. É usado nas texturas e em alguns filmes dentro dos BIG internos.
- **`chunkzip`:** os mesmos blocos, com deflate. É usado nos DDS de skill move.

**Texturas XPR2** (Xbox 360): o cabeçalho traz o *fetch constant* da GPU Xenos (formato, endianness, tiling e dimensões). Os dados vêm em DXT5, com bytes trocados a cada 16 bits e layout *tiled* (o endereço é calculado por `XGAddress2DTiledOffset`).

**Apt**, o formato de UI da EA derivado do Flash:
- `0` é o filme, com bytecode parecido com o ActionScript 2;
- `1` é a **tabela de constantes**, com as strings usadas pelo código (nomes de clipes, caminhos, plataformas).

## 4. O que cada parte faz e por quê

### 4.1 Prompts de botão (`data0.big`, `…/platform/proxy/art/*.big`)

**Achado.** As telas importam os botões por caminho (`external/ion_sports07/platform/proxy/art/A` e assim por diante), e cada botão é uma textura só, compartilhada pelo jogo inteiro: o "A" é usado em 107 telas.

**Como.**
1. A textura é decodificada: tiling desfeito, bytes reordenados, DXT5 descomprimido.
2. Um *flood fill* a partir das bordas, que só passa por pixels não escuros, separa o lado de fora (contorno claro e transparência) da face preta do botão. A região fechada com mais pixels claros é a face com o símbolo do Xbox.
3. A face é repintada com o preto médio original e recebe o glifo do PlayStation, escalado, centralizado, com supersampling 4×4 e uma borda preta de 1 texel.
4. A textura volta pro mesmo formato, ordem de bytes, tiling e lugar.

O botão Guide é exceção: o logo encosta no contorno e não forma uma face fechada, então a face é o disco ajustado à área escura.

**Por quê.** Manter a textura original e trocar só o símbolo preserva o contorno, o sombreado e as bordas que a geometria de cada quadro espera. Os glifos (`DualSense/*.png`, gerados por `make_glyphs.py`) são arte original.

**Encoder DXT5.** As duas cores-base de cada bloco 4×4 partem do texel mais escuro e do mais claro e são refinadas por **mínimos quadrados**: os índices ficam fixos e resolve-se o sistema 2×2 das cores, em 4 iterações, ficando o melhor resultado. O alfa usa o modo de 8 níveis. Sem esse refinamento, as cores pequenas sumiam e o branco ficava com blocos.

### 4.2 Lista de skill moves (`data1.big`, `data/ui/imgassets/tricks/`)

O jogo traz as imagens em `xenon/` e em `ps3/`, com os mesmos nomes. As 38 entradas de `xenon/` passam a apontar, no diretório e no `.bh`, para os dados de `ps3/`. Nenhum byte de imagem é gravado: é a arte oficial do próprio jogo, e a troca custa 8 bytes por entrada.

### 4.3 Dicas de skill move na partida (`proxy/art/tricks.big`)

Esse conjunto só existe na versão Xbox. Comparando a transparência, a textura `2k` corresponde a `trick_(38+k)` (36 de 37 batem exatamente; a outra é idêntica a uma vizinha).

A troca é uma *transferência de diferença*: `saída = HUD_xbox + (lista_PS3 − lista_xbox)`, com limite em 0–255. As setas são iguais nas duas listas, então ali a diferença é zero e o verde do HUD fica intacto. Onde os rótulos mudam, a diferença carrega o rótulo do PlayStation.

### 4.4 Seleção de lados

**Achado.** A tela offline usa `data/ui/game/components/fifasideselector.big`:
- **33:** o controle (256×128);
- **30:** uma faixa branca que o jogo pinta com a cor de cada jogador;
- **8:** a silhueta dos slots vazios.

O mesmo arquivo traz um conjunto PS3 que o Xbox não usa. O `proxy/art/controllers.big` tem uma cópia menor, provavelmente a da versão online; as duas são trocadas.

**Como.**
- `cutout.py` remove o xadrez de "transparência falsa" que vinha salvo nos pixels da imagem, com um flood fill pelas bordas, por pixels claros e neutros, parando no contorno do controle.
- `pad_from_image.py` encaixa a imagem sem distorcer onde ficava o 360, com alfa pré-multiplicado e supersampling (sem halo), e gera a silhueta a partir da transparência, no cinza translúcido original.
- **A faixa de cor** tem posição fixa em relação ao controle, então foi **medida nos prints do jogo**. A medição foi validada reproduzindo a tela com a arte original do 360. Como a faixa só aparece no quarto de baixo do quadro, ela virou uma barra de jogador no vão entre as alças (opção B).

### 4.5 Telas de controles: o layout PlayStation

| Tela (`data1.big`) | Onde aparece | Como pega a foto |
|---|---|---|
| `screens/settings/controllersetup.big` | Configurações > Controle | Carrega `controller_xbox360.swf` / `controller_PS3.swf` pelo caminho |
| `screens/settings/controllerconfiguration.big` | Configuração de botões | Idem |
| `overlays/overlay_79.big` | Loading ("CONTROLES" + "DICAS") | Embute as duas fotos (47 = 360, 74 = DualShock 3) |
| `overlays/overlay_87.big` | Loading, outra variante | Embute as duas (50 = 360, 80 = DualShock 3) |
| `screens/misc/ingamemanual.big` | Pausa > Manual do Jogo | Embute as duas (61 = 360, 17 = DualShock 3) |

**Achado.** O bytecode escolhe o layout pela plataforma. Decompilado de forma aproximada, em `controllersetup`:

```
linhas = PLATFORM == "PS3" ? mcLinesPS3 : mcLines
se PS3: imagem = controller_PS3.swf, escala 75%
senão:  remove mcLineLayer; imagem = controller_xbox360.swf
```

No Xbox, `PLATFORM` vale `XENON`.

**Como.** Na tabela de constantes de cada tela, a entrada `"PS3"` passa a apontar pra uma string `"XENON"` acrescentada no fim. O teste dá verdadeiro e **só essas telas** seguem o ramo PlayStation.

**Por quê.** As linhas do Xbox apontam pro layout do 360 (analógico esquerdo em cima, direcional embaixo), o contrário do DualSense. O ramo PS3 já traz linhas pro layout PlayStation, que é o do DualSense. Trocar uma constante evita mexer no bytecode e nos offsets de desvio.

### 4.6 O DualSense no lugar da foto do DualShock 3

**Achado.**
- As três cópias embutidas são a mesma textura de 512×256, e o jogo desenha os texels (0,0)–(400,250).
- O `controller_ps3.big` é a mesma foto ampliada 1,45× (registro pela transparência, diferença média de 1,5/255), em 1024×512, com área visível até (548,297).
- As linhas terminam em 11 botões da foto: L2, L1, R2, R1, direcional, △□○✕, L3 e R3.

**A imagem precisa ter o ângulo da foto do DS3**, de cima e na diagonal, com os gatilhos aparecendo atrás dos bumpers e os analógicos no pé da imagem. Numa imagem frontal, o melhor encaixe errava 22–23 texels em média: os ombros ficavam 47–69 texels no ar. Com a imagem no ângulo certo:

| Botões | Desvio |
|---|---|
| Direcional, △□○✕ | 3–10 texels |
| L3, R3 | 9–10 texels |
| L2, R2 | 15 texels |
| L1, R1 | 19 texels (o ponto cai no topo do bloco do gatilho) |
| **Média** | **9 texels** |

**Como** (`controls_from_image.py`):
1. Escala praticamente uniforme (até 8% de diferença entre os eixos) e posição por mínimos quadrados ponderados sobre os 11 pontos, com peso 0,5 nos ombros, sempre dentro da área visível.
2. Na versão grande, encaixar o controle inteiro em y ≤ 297 faria as linhas errarem 26 texels. Então ela usa o mesmo encaixe da pequena, convertido por ×1,45, e o jogo corta as pontas das alças. Entre "linhas certas" e "alças inteiras", ficaram as linhas certas.
3. O patcher grava as 4 texturas na mesma remontagem que troca a constante. Sem os arquivos `controls*.png`, as telas ficam com a foto do DS3.

## 5. Como o patch é gravado e desfeito

**Gravação.** As entradas alteradas são **acrescentadas no fim** do `.big`, sem compressão, como o patch de créditos do instalador do ReStreet já faz. O diretório do BIG e o `.bh` passam a apontar pra elas. Os BIG internos são remontados com as entradas trocadas e as demais intactas.

**Perfil do controle.** O `apply` exige `--controller`, gravado como **um byte**:
- `0x05` = PS5 DualSense: a arte que existe hoje, e o que o `Instalar DualSense.cmd` usa;
- `0x04` = PS4 DualShock 4: reconhecido, mas recusado até a arte existir.

O byte fica no backup (formato `RSD2`: `"RSD2"`, o byte, e depois o conteúdo do backup). O `status` lê esse byte e o `revert` também aceita o formato antigo (`RSDS`).

**Segurança.**
- Antes de escrever, o diretório, o `.bh` e o tamanho originais vão para `<arquivo>.dualsense-backup`. O `revert` regrava os dois e corta o arquivo no tamanho original.
- Entradas e índices são conferidos antes de gravar. Se o formato do arquivo não for o esperado (uma versão nova do jogo, por exemplo), o patch para com erro em vez de gravar errado.
- O jogo precisa estar fechado: o instalador verifica isso, e os arquivos são abertos com acesso exclusivo.

## 6. Testes feitos

- **Ida e volta:** `apply` e depois `revert` numa cópia dos quatro arquivos, repetidos a cada mudança. Os arquivos voltam **idênticos byte a byte** (SHA-256). O mesmo vale na instalação real, conferido antes e depois de um update do ReStreet; o update não altera `data0`/`data1`.
- **Leitura de volta:** depois do `apply`, todas as entradas alteradas são lidas e decodificadas pela própria ferramenta (texturas, constantes e redirecionamentos).
- **Flag do controle:** sem a flag, com `ps4` e com um valor desconhecido, o patcher recusa sem tocar em nada. Com `ps5`, o backup começa com `RSD2 05` e o `status` confirma.
- **Pacote:** a detecção automática acha a pasta `Game` da instalação 2.0 pelo atalho do launcher, mesmo com uma instalação antiga no mesmo disco. Uma pasta errada é recusada, e com o jogo aberto o instalador se recusa a mexer nos arquivos. O exe do pacote acha toda a arte da própria pasta (`preview`: 35 prompts, 38 + 37 skill moves, seleção de lados e as 4 fotos).
- **No jogo** (pelo usuário): prompts, seleção de lados, loading e Manual do Jogo.
- **Ainda não feito:** rodar o `install` e o `uninstall` pelo pacote com o jogo fechado. Eles chamam as mesmas operações já testadas, mas esse caminho exato ainda não rodou.

## 7. Caminho descartado: a pasta Mods

O runtime do ReStreet lê `Mods/<arquivo>@<offset>.bin` e entrega esses bytes no lugar dos originais, sem alterar os arquivos. Só que ele **não deixa o arquivo crescer**, e as entradas do mod, sem compressão, ficam 6–10× maiores que o espaço original.

Cheguei a escrever encoders LZX e RefPack que ficavam só 2,9% maiores que os da EA. Mesmo assim, ainda faltava encaixar as entradas nos espaços livres, e o `data0.big` quase não tem sobra. A instalação 2.0 também não tem mais a pasta `Mods`. Ficou decidido usar a substituição direta, mais simples e já validada, e esse código foi removido.

## 8. Ferramenta

Pasta `Tools/ReStreetAssetScan` (ainda não commitada):

| Arquivo | Papel |
|---|---|
| `Program.cs` | Leitura dos BIG; `list`, `extract`, `buttons` |
| `EasyInstall.cs` | Modo jogador: menu, `install`/`uninstall`/`status`, detecção da instalação, checagem do jogo aberto |
| `DualSense.cs` | O patch: `preview`/`apply`/`revert`/`status`, `--controller`, `--art` |
| `Textures.cs` | XPR2/DDS ↔ RGBA (tiling Xenos, DXT1/3/5, encoder DXT5), PNG |
| `DualSense/make_glyphs.py` | Glifos (arte original) e um DualSense desenhado, usado quando não há imagem |
| `DualSense/cutout.py` | Remove o xadrez de "transparência falsa" |
| `DualSense/pad_from_image.py` | Texturas da seleção de lados a partir de uma imagem |
| `DualSense/controls_from_image.py` | Fotos das telas de controles a partir de uma imagem e dos centros dos botões |
| `pack.ps1` | Monta o pacote (exe único + arte + `.cmd` + LEIA-ME) |

As imagens de DualSense usadas e tudo o que foi gerado delas ficam em `private/`, que não vai pro Git:
- `dualsense.png`, `dualsense-controles.png` e os recortes;
- `dualsense-controles-points.json`;
- `dualsense-art/`;
- o pacote `ReStreet-DualSense/`.

## 9. Limites e pendências

- **Imagens da Sony.** As fotos do DualSense parecem renders oficiais da Sony. Pra uso próprio, tudo bem. Pra distribuir o pacote a outras pessoas, o seguro é montar o pacote só com a arte original (`pack.ps1` sem `-Art`). Aí a seleção de lados usa o DualSense desenhado e as telas de controles ficam com o DS3, até existir uma arte de DualSense própria.
- **L1/R1 nas telas de controles** ficam ~19 texels acima do bumper. Corrigir exige editar o posicionamento das linhas no filme Apt.
- **Alças cortadas** nas telas de configuração de controle (versão grande da foto), pelo motivo da seção 4.6.
- **Ramo PS3 das telas de controles.** Ele também chama funções de PlayStation (por exemplo `GetAdvanceButton`). Nada de errado apareceu no jogo, mas um comportamento estranho nessas telas viria daí, e o `revert` desfaz.
- **DualShock 4 (PS4).** O byte `0x04` já está reservado. Falta a arte: glifos (Options/Share no lugar de Options/Create, touchpad), a imagem da seleção de lados e a imagem das telas de controles.
- **Updates do ReStreet.** Sempre remover antes de atualizar e instalar de novo depois. Se um update mudar o `data1.big` com o mod aplicado, o `revert` recusa (o arquivo não bate com o backup) em vez de estragar.
