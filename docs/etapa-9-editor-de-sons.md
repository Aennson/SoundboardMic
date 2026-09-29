# Etapa 9 — Editor de sons (corte de áudios)

**Status:** ✅ Implementado — decisões confirmadas (ver [Pontos a validar](#pontos-a-validar)).

## Objetivo

Permitir **cortar áudios já salvos no app** para remover ruídos desnecessários no início
ou encerrar o som antes do fim, sem precisar de um editor externo. O usuário escolhe um
áudio, visualiza sua **forma de onda**, seleciona o trecho que quer manter e decide entre
**criar um novo áudio** a partir do corte ou **substituir o original**.

## Contexto técnico (fatos do código atual)

- Cada `Audio` guarda o arquivo como **BLOB no SQLite** (`ArquivoConteudo`, fonte de
  verdade) e um **cache em disco** (`CaminhoArquivo`) regenerado a partir do BLOB.
- Formatos aceitos na importação: **.mp3, .wav, .ogg** (`AudioFileDecoder`).
- `Audio.DuracaoMs` é exibido nos cards; `VolumePadrao`, `Icone`, `Cor`, `CategoriaId` e
  `Ordem` compõem o restante da identidade do som. O atalho fica em `Mapeamento`.
- A janela principal alterna as telas por `Visibility` a partir da barra lateral
  (`Meus sons`, `Sons da Web`, `Configurações`); as views permanecem carregadas.
- O preview do editor atual (`AudioEditViewModel`) toca via `IPlaybackService` no
  dispositivo de **monitoramento** (`MonitorDeviceId`), sem passar pelo microfone virtual.
- A grade de "Meus sons" é sincronizada no lugar (`GradeSync`): só o card afetado anima.

## Requisitos funcionais

### RF1 — Nova aba "Editor de sons"
- Novo item na barra lateral chamado **Editor de sons**, posicionado entre
  **Sons da Web** e **Configurações**, com ícone no mesmo estilo dos demais
  (sugestão: glifo de tesoura/corte do Segoe Fluent).
- Segue o mesmo padrão de navegação das outras abas (view carregada, alternada por
  `Visibility`) e a mesma linguagem visual (cards, tipografia, cores do tema).

### RF2 — Lista de áudios pesquisável
- Lista com **todos os áudios cadastrados em "Meus sons"**.
- Campo de **busca por nome** (filtro imediato, sem diferenciar maiúsculas/acentos
  do mesmo jeito que a busca da grade principal).
- Cada item mostra: ícone/cor do áudio, **nome**, **categoria** e **duração**.
- A lista reflete na hora inclusões, edições e exclusões feitas em outras abas
  (mesma coleção de áudios do `MainViewModel`, com view de filtro própria para não
  interferir no filtro da grade — como a barra rápida já faz).
- Estado vazio: mensagem orientando a adicionar sons em "Meus sons".

### RF3 — Visualização da forma de onda
- Ao selecionar um áudio, a área **abaixo da lista** exibe a **forma de onda** do som
  (estilo da imagem de referência: barras de amplitude em cor de destaque sobre fundo
  escuro), ocupando toda a largura disponível.
- Áudios estéreo são exibidos como **uma única onda** (pico máximo entre canais) —
  prioriza legibilidade; ver pontos a validar.
- Régua de tempo abaixo da onda (marcações em segundos) e duração total visível.
- A onda é calculada em segundo plano (picos min/máx por coluna de pixel) com
  indicador de carregamento; a UI não pode travar com arquivos longos.
- Redimensionar a janela recalcula a onda para a nova largura.

### RF4 — Seleção do trecho
- O trecho selecionado é **um único intervalo contínuo** [início, fim]; tudo fora dele
  é descartado. (Remover um pedaço do meio fica fora do escopo — ver RNF/escopo.)
- Formas de seleção:
  - **Arrastar** sobre a onda para criar a seleção;
  - **Alças** (handles) de início e fim arrastáveis para ajuste;
  - **Campos numéricos** de início e fim (`mm:ss.mmm`) para ajuste fino, sincronizados
    com as alças;
  - Botão **"Selecionar tudo"** para resetar.
- A região **fora** da seleção fica escurecida/dessaturada; a região selecionada
  mantém a cor de destaque.
- Mostrar a **duração resultante** do trecho selecionado.
- Seleção mínima: **100 ms**. Início sempre < fim; valores fora do áudio são ajustados.
- Ao abrir um áudio, a seleção inicial é o **áudio inteiro**.

### RF5 — Pré-escuta
- Botão **Ouvir seleção** (play/stop) que toca **apenas o trecho selecionado** no
  dispositivo de **monitoramento** (fones), nunca no microfone virtual.
- Durante a pré-escuta, um **cursor de reprodução** percorre a onda.
- Opcional: clicar na onda (fora das alças) posiciona o cursor e toca a partir dali.
- A pré-escuta aplica o `VolumePadrao` do áudio, como o preview do editor atual.

### RF6 — Salvar como novo áudio
- Botão **Criar novo áudio**. Pede (ou sugere editável) o nome, padrão
  **"<nome original> (corte)"**.
- O novo áudio herda do original: **categoria, volume, ícone e cor**.
  É posicionado **logo após o original** na mesma categoria.
- **Não herda o atalho** (evita conflito de teclas); o usuário define depois no editor.
- Aparece em "Meus sons" com a animação de entrada **somente no card novo**.

### RF7 — Substituir o original
- Botão **Substituir original**, com **confirmação** explícita
  ("Esta ação sobrescreve o áudio e não pode ser desfeita").
- Mantém **Id, nome, categoria, ordem, volume, ícone, cor e atalho**; troca apenas o
  conteúdo (`ArquivoConteudo`), o cache em disco e a `DuracaoMs`.
- Se o áudio estiver **tocando ou em loop**, ele é parado antes da substituição
  (o arquivo de cache não pode estar em uso).
- Após salvar, o card correspondente em "Meus sons" atualiza duração e reanima
  apenas ele; o editor recarrega a onda com o novo conteúdo.

### RF8 — Feedback e erros
- Botões de salvar ficam desabilitados enquanto não há áudio selecionado, a seleção é
  inválida ou uma gravação está em andamento (com indicador de progresso).
- Falhas (arquivo corrompido, falta de espaço, erro de banco) aparecem no banner de erro
  padrão do app, sem perder o áudio original.
- Sucesso: aviso discreto ("Novo áudio criado" / "Áudio substituído").

## Requisitos não funcionais

- **Formato de saída:** o trecho é gravado como **WAV PCM 16-bit**, mantendo a taxa de
  amostragem e o número de canais do original. Evita perda por recodificação de MP3/OGG
  e não depende de encoders externos. O `ArquivoNomeOriginal` passa a ter extensão
  `.wav`. (O aumento de tamanho é aceitável para sons curtos de soundboard.)
- **Sem cliques nos cortes:** aplicar **micro fade-in/fade-out de ~5 ms** nas bordas do
  trecho para evitar estalos causados pelo corte fora de um cruzamento por zero.
- **Precisão:** corte com precisão de amostra, alinhado ao início de um frame (todos os
  canais).
- **Desempenho:** decodificação e cálculo da onda fora da thread de UI; áudios de até
  10 min devem abrir em menos de ~1 s em máquina comum.
- **Atomicidade:** a substituição grava banco e cache de forma que, se algo falhar, o
  áudio original permaneça íntegro (gerar o novo conteúdo em memória/arquivo temporário
  antes de sobrescrever).
- **Acessibilidade:** alças e campos com `AutomationProperties.Name`; ajuste por teclado
  (setas movem a alça focada em 10 ms, `Shift`+setas em 100 ms).
- **Arquitetura:** a lógica de corte/cálculo de picos fica no **Core**
  (`SoundboardMic.Core`, ex.: `AudioTrimmer` e `WaveformPeaks`), testável sem WPF; a UI
  fica em `EditorDeSonsView` + `EditorDeSonsViewModel` no App.

## Fora do escopo (desta etapa)

- Remover trechos do meio / múltiplas regiões.
- Zoom e rolagem horizontal na forma de onda.
- Efeitos (normalizar, fade configurável, ganho por trecho), desfazer/histórico.
- Editar sons da aba "Sons da Web" antes de importá-los.
- Manter cópia de segurança do original após "Substituir".

## Critérios de aceite

1. A aba **Editor de sons** aparece na barra lateral e abre a tela de edição.
2. A busca filtra a lista por nome em tempo real, sem afetar a grade de "Meus sons".
3. Selecionar um áudio exibe sua forma de onda com a seleção cobrindo o áudio inteiro.
4. Arrastar alças ou digitar tempos atualiza a seleção e a duração resultante.
5. **Ouvir seleção** toca só o trecho, nos fones, com cursor acompanhando.
6. **Criar novo áudio** gera um som novo com a duração do trecho, mesma categoria,
   logo após o original, sem atalho — e só esse card anima em "Meus sons".
7. **Substituir original** (após confirmação) mantém atalho/categoria/ordem, atualiza
   a duração do card e toca o áudio cortado pelo atalho existente.
8. O resultado não apresenta estalos nas bordas do corte.
9. Testes automatizados cobrem: corte por intervalo (duração/amostras exatas, formatos
   mp3/wav/ogg), micro fade, cálculo de picos, validação de seleção, criação e
   substituição no repositório.

## Pontos a validar

Todas as decisões abaixo foram confirmadas e implementadas:

1. ✅ **Um único trecho contínuo** (sem remoção de pedaços do meio).
2. ✅ **Saída em WAV 16-bit** (mesma taxa e canais do original).
3. ✅ **Novo áudio não herda o atalho** do original (herda categoria, volume, ícone e cor).
4. ✅ **Substituir é definitivo** (apenas confirmação, sem backup/desfazer).
5. ✅ **Forma de onda única** (canais combinados).
6. ✅ **Sem zoom** na primeira versão.
7. ✅ **Pré-escuta pelos fones** (monitoramento), nunca pelo microfone virtual.
8. ✅ **Posição da aba**: entre "Sons da Web" e "Configurações".
9. ✅ Botão **"Cortar"** no card de "Meus sons" (ao lado de editar/excluir) abre o editor
   já com o áudio selecionado.

## Implementação

- Core: `AudioTrimmer` (corte + micro fade de 5 ms → WAV PCM16), `WaveformPeaks`
  (picos por coluna, cancelável), `SelecaoCorte` (normalização/parse/formatação `m:ss.mmm`),
  `PlaybackService.PlayRange`/`Position` para a pré-escuta.
- App: `AudioTrimService` (criar novo logo após o original / substituir mantendo Id,
  atalho e ordem, com rollback do cache), `WaveformView` (onda, alças arrastáveis,
  teclado: setas 10 ms, Shift+setas 100 ms, Home/End, Tab troca a alça),
  `EditorDeSonsViewModel` e `EditorDeSonsView`.
- Testes: `AudioTrimmerTests`, `WaveformPeaksTests`, `AudioTrimServiceTests`,
  `EditorDeSonsViewModelTests` e `EditorDeSonsViewTests`.