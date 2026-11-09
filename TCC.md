# TCC — Acompanhamento

Registro do que já foi feito no documento do TCC, das decisões tomadas e do que ainda falta.

## Documento

- **Arquivo:** [docs/TCC_AntRoute_Artigo.docx](docs/TCC_AntRoute_Artigo.docx)
- **Formato:** artigo científico FAESA (ABNT NBR 6022)
- **Tamanho atual:** 29 páginas, das quais **26 vêm antes das Referências — 1 acima do teto da FAESA (15 a 25)**
- **Título proposto:** *Otimização de rotas por colônia de formigas: desenvolvimento de uma aplicação web para o problema do caixeiro-viajante e comparação com outras heurísticas*
- **Materiais de referência:** [TCC Examples/](TCC%20Examples/) (esquema de formatação, Guia FAESA 2024 e o TCC de exemplo de 2022)

## Como regenerar o documento

Os scripts estão em [docs/tools/](docs/tools/):

| Arquivo | Função |
|---|---|
| [build_tcc.js](docs/tools/build_tcc.js) | Gera o `.docx` com docx-js. **Todo o texto do artigo está aqui.** |
| [figs.py](docs/tools/figs.py) | Desenha os diagramas com Pillow: `fig_arquitetura.png`, `fig_aco.png` e `fig_dataset.png` |
| [fig_sens.py](docs/tools/fig_sens.py) | Gera `fig_beta_rho.png` (Figura 8, mapa de calor β × ρ) a partir de `datasets/vitoria-es/runs.csv`. Sem o CSV, apenas avisa e mantém a figura existente |
| [fig_conv.py](docs/tools/fig_conv.py) | Gera `fig_convergencia.png` (Figura 7) a partir de `datasets/comparacao/convergence.csv` |

As Figuras 4, 5 e 6 são capturas de tela manuais (`fig_tela_mapa.png`, `fig_tela_visual.png`,
`fig_tela_comparacao.png`), feitas com a aplicação rodando localmente e os 8 pontos de Vitória
importados por CSV. Para refazê-las: subir `dotnet run --project AntOptimization.Server` e
`npm start`, importar o CSV, otimizar, capturar; a Figura 5 precisa ser capturada ~1 s após iniciar
o modo visual.
| [gerar.ps1](docs/tools/gerar.ps1) | Executa tudo: instala o `docx` se necessário, gera as figuras e o `.docx` e, com `-Pdf`, exporta o PDF pelo Word |
| [render.py](docs/tools/render.py) | Converte o PDF em PNGs, uma imagem por página, para conferência visual (requer `pip install pymupdf`) |

```powershell
cd docs\tools
.\gerar.ps1          # figuras + docs\TCC_AntRoute_Artigo.docx
.\gerar.ps1 -Pdf     # também gera docs\TCC_AntRoute_Artigo.pdf (precisa do Word instalado)
node build_tcc.js C:\caminho\teste.docx   # gera em outro lugar, sem sobrescrever
```

Requisitos: Node.js, Python com Pillow e, para o PDF, o Microsoft Word.

> ⚠️ **Atenção:** o `gerar.ps1` **sobrescreve** `docs/TCC_AntRoute_Artigo.docx`. Escolha um dos dois fluxos de trabalho:
> - **Editar o `build_tcc.js`** e regenerar o documento. É o melhor fluxo para mudanças grandes de estrutura e garante que a formatação continue padronizada.
> - **Editar direto no Word.** Depois que você fizer isso, **não regenere** o documento, ou as alterações serão perdidas. Se precisar regenerar mesmo assim, gere em outro caminho, como mostra o último comando acima.
>
> Para escrever no `build_tcc.js`: `**negrito**`, `~itálico~` e `[[MARCADOR]]`, que sai destacado em amarelo. As funções `P`, `H1`, `H2`, `Eq`, `Figure`, `FigurePlaceholder` e `Grid` (quadros e tabelas) numeram figuras, quadros, tabelas e equações automaticamente.

## Decisões tomadas

| Decisão | Escolha | Motivo |
|---|---|---|
| Norma de formatação | **Guia FAESA 2024** | Os dois guias divergem. O Guia 2024 é o mais recente. |
| Foco do artigo | ACO + comparação dos 4 algoritmos + módulo de dataset | O dataset é a contribuição acadêmica e a base para os trabalhos futuros (ML). |
| Resultados | Sensibilidade concluída; comparação pendente | O experimento de sensibilidade (330 instâncias, 320.760 execuções) foi executado em 09/11/2026. A comparação entre os quatro algoritmos ainda não. |
| Autores e orientador | Marcadores para preencher | — |

Regras do Guia 2024 que o documento segue:

- **Página e fonte:** Arial 12, cor preta, A4.
- **Margens:** superior e esquerda de 3 cm, inferior e direita de 2 cm.
- **Texto:** espaçamento simples, justificado, sem recuo e com uma linha em branco entre os parágrafos.
- **Títulos do artigo:** em português, 14 pt, negrito e maiúsculas; em inglês, igual, mas também em itálico.
- **Autores:** dados (curso, instituição e e-mail) em nota de rodapé de 10 pt, com uma linha "Orcid:" abaixo de cada nome.
- **Seções:** numeradas (`1 INTRODUÇÃO`, `2.1 SUBSEÇÃO`). Cada seção principal começa em página nova. REFERÊNCIAS e APÊNDICE ficam centralizados e sem número.
- **Ilustrações:** título acima em 11 pt, com o formato "Figura N – …"; fonte abaixo em 10 pt. Tabelas sem linhas verticais (padrão IBGE). Quadros fechados.
- **Paginação:** Arial 10, no canto superior direito.
- **Resumo:** de 100 a 250 palavras, sem primeira pessoa. Abstract todo em itálico.

## Estrutura do artigo

1. **Introdução:** contexto, perguntas de pesquisa, objetivo geral, cinco objetivos específicos, justificativa e organização do texto.
2. **Revisão da literatura:**
   - Problema do caixeiro-viajante (caminho aberto, matriz assimétrica)
   - Métodos exatos (força bruta e Held-Karp)
   - Vizinho Mais Próximo e 2-Opt
   - Algoritmo Genético
   - Colônia de formigas (Ant System, com as equações)
   - Ajuste de hiperparâmetros e meta-aprendizado
   - Roteamento com OSRM e distância de Haversine
3. **Metodologia:**
   - Tipo de pesquisa
   - Arquitetura (Figura 1)
   - Matriz de distâncias
   - Algoritmos (Figura 2, Quadro 2)
   - Interface
   - Módulo de dataset (Figura 3, Quadros 3 e 4)
   - Protocolo experimental
   - Testes e CI/CD
4. **Resultados e discussão:** telas da aplicação, comparação entre algoritmos (Tabela 1), convergência, sensibilidade a hiperparâmetros (Tabela 2) e limitações.
5. **Considerações finais:** retomada dos objetivos e trabalhos futuros.

Depois vêm as **Referências** (27 obras) e o **Apêndice A** (endpoints da API).

A Metodologia descreve o que está **implementado no código**:

- **ACO:** Ant System clássico.
  - Feromônio inicial τ₀ = 1, heurística η = 1/d e escolha por roleta.
  - Evaporação global seguida de depósito Q/L (Q = 1) por todas as formigas.
  - Sem elitismo.
- **2-Opt:** parte da rota do Vizinho Mais Próximo e usa *first improvement*, com ganho **exato** (a inversão do segmento também troca o sentido das arestas internas, que mudam de custo em matriz assimétrica) e distância final recalculada a partir da rota.
- **Algoritmo Genético:** seleção por torneio (k = 5), cruzamento OX e mutação por troca (*swap*). Não tem elitismo nem semente fixa.
- **Métodos exatos:** força bruta até 10 pontos e Held-Karp até 15, com limite de 60 s.
- **Dataset:**
  - 23 características por instância.
  - Grade de 324 configurações: α {0,5; 1; 2} × β {1; 2; 5} × m {10; 20; 40} × ρ {0,1; 0,3; 0,5; 0,7} × iterações {50; 100; 200}.
  - Sementes geradas com FNV-1a.
  - Uma tarefa cobre **vários tamanhos de instância** (lista de `pointCounts` × instâncias por tamanho).
  - Invariante verificado antes de salvar: NN e 2-Opt nunca podem ficar abaixo do ótimo exato; se ficarem, a instância falha.

> Se algum desses comportamentos mudar no código, atualize a seção 3 do artigo.

## Pendências

No Word, os trechos pendentes estão **destacados em amarelo**.

### Identificação
- [ ] Nomes completos dos autores e do orientador e Orcid de cada um
- [ ] Curso, e-mails e titulação do orientador (nas notas de rodapé)
- [ ] Confirmar o título com o orientador

### Experimentos (seção 3.7)
- [x] **Sensibilidade aos hiperparâmetros — executado em 09/11/2026.** 6 tarefas, 3 escalas da Grande
      Vitória (ES), n de 5 a 15, 330 instâncias, OSRM, grade de 324 configurações × 3 repetições =
      **320.760 execuções**, todas com ótimo exato. Dados em [datasets/vitoria-es/](datasets/vitoria-es/).
- [x] Hardware informado no texto: AMD Ryzen 5 5500 (6C/12T), 16 GB, Windows 11 Pro, .NET 10, Release.
- [x] **Comparação entre algoritmos — executado em 09/11/2026.** 10 instâncias para cada tamanho de
      10, 15, 30 e 50 pontos na Grande Vitória, OSRM, 10 repetições dos métodos estocásticos =
      40 instâncias e 880 execuções. Semente mestra 20251109. Dados em
      [datasets/comparacao/](datasets/comparacao/).
  - Resultado: a ACO vence nos quatro tamanhos (gap de 0,03% a 3,15%) e em 29 das 40 instâncias;
    o 2-Opt fica em 0,40%–5,54% a um custo desprezível; o Vizinho Mais Próximo em 8,25%–12,24%;
    o AG degrada de 0,64% (n=10) a 41,46% (n=50).
  - Em 30 e 50 pontos o gap é relativo ao melhor dos quatro, por estarem acima do limite do
    solver exato (15 pontos) — está dito na nota da Tabela 1.

### Resultados (seção 4)
- [x] Figura 4: tela do mapa com a rota otimizada (8 pontos em Vitória, 23,53 km)
- [x] Figura 5: modo visual em uma iteração intermediária (iteração 31/100)
- [x] Figura 6: dashboard de comparação para o mesmo exemplo
- [x] Figura 7: convergência da ACO por tamanho de instância — `fig_conv.py`, a partir do
      `convergence.csv` do benchmark
- [x] Figura 8: mapa de calor do gap médio por β × ρ — `fig_sens.py`, a partir do `runs.csv`
- [x] Tabela 1 (comparação entre algoritmos), Tabela 2 (gap por número de pontos) e Tabela 3
      (as 10 melhores configurações)
- [x] Seções 4.2, 4.3, 4.4 e 4.5 escritas com os números reais
- [x] Parágrafo de 4.1 preenchido com o exemplo das capturas (ilha de Vitória, 8 pontos, 23,53 km,
      6 ms; na comparação, ACO/2-Opt/AG empatam e o Vizinho Mais Próximo fica 5,61% acima)

### Resumo, conclusão e revisão final
- [x] Resumo e Abstract completos (247 e 249 palavras — o limite FAESA é 250, não há folga)
- [x] Conclusões sobre a comparação e sobre a sensibilidade nas Considerações Finais
- [ ] Conferir volumes e páginas das referências (foram escritas de memória) e a data de acesso da
      referência WHATWG
- [ ] Opcional: refazer as equações no editor de equações do Word
- [ ] Revisão final do orientador

### ⚠️ Orçamento de páginas — passou do teto
**26 páginas antes das Referências; o limite da FAESA é 25.** Falta cortar ~1 página. Candidatos,
do menos ao mais doloroso:
1. **Tabela 1** (16 linhas): juntar os quatro algoritmos em uma linha por tamanho, com as colunas
   lado a lado, economiza meia página.
2. **Apêndice A** (endpoints da API): não é exigido e sai inteiro.
3. **Revisão da literatura**: as seções 2.3 e 2.4 podem ser condensadas.
4. As capturas de tela (Figuras 4 a 6) estão a 12,4 cm de largura; reduzi-las piora a legibilidade,
   que já é limitada — é o último recurso.

## Experimento de comparação

Executor: **[AntOptimization.Benchmark](AntOptimization.Benchmark/)** (console, versionado).

```bash
dotnet run -c Release --project AntOptimization.Benchmark -- --out datasets/comparacao
```

Roda os quatro algoritmos sobre as mesmas instâncias e a mesma matriz, mede o tempo de parede e
grava `runs.csv`, `instances.csv`, `summary.csv`, `convergence.csv` e `metadata.json` em
`datasets/comparacao/` (também fora do versionamento). Pontos, sementes do ACO e do AG derivam todos
da semente mestra, então uma nova execução reproduz os mesmos números — para isso o
`GeneticAlgorithmEngine` passou a aceitar **semente opcional** (sem semente na aplicação interativa,
com semente nos experimentos).

## Conjunto de dados gerado

[datasets/vitoria-es/](datasets/vitoria-es/) — **não versionado** (265 MB, `/datasets/` está no
`.gitignore`), mas reproduzível pelas sementes mestras registradas no `metadata.json`.

| Arquivo | Conteúdo |
|---|---|
| `runs.csv` | 320.760 linhas, 51 colunas — tabela principal de treino |
| `instances.csv` | 330 instâncias, com coordenadas, matriz e tour ótimo |
| `configs_summary.csv` | 106.920 linhas por (instância, configuração) |
| `metadata.json` | região, sementes e grade de cada uma das 6 tarefas |
| `README.md` | esquema, receita de treino e vieses conhecidos |

Validação: 330/330 instâncias concluídas, nenhuma execução com falha e nenhum valor de NN, 2-Opt ou
ACO abaixo do ótimo exato.

Números que entraram no artigo: gap médio 1,456%, mediana 0%, p90 4,596%, máximo 55,277%; 67,6% das
execuções atingem o ótimo; taxa de acerto cai de 99,9% (n=5) para 20,9% (n=15); melhor configuração
0,566% contra 7,053% da pior; a configuração padrão da aplicação é a 139ª de 324.

> ⚠️ **Cuidado ao usar o conjunto para classificação.** Os empates no gap mínimo são maciços (mediana
> de 324 configurações empatadas em n=5 e 13 em n=15; só 16 das 330 instâncias têm vencedora única).
> O `argmin` por instância escolhe pela ordem das linhas, não pelo mérito. A formulação correta é
> regredir o `gap_percent` a partir de features + hiperparâmetros.

## Histórico

| Data | O que foi feito |
|---|---|
| 28/10/2026 | Leitura dos guias FAESA e do TCC de exemplo. Primeira versão completa do artigo gerada em `docs/`, com os três diagramas (arquitetura, fluxo do ACO e pipeline do dataset). Resultados ficaram pendentes. |
| 28/10/2026 | Scripts geradores salvos em `docs/tools/` (`build_tcc.js`, `figs.py`, `gerar.ps1` e `render.py`) e testados a partir do repositório. |
| 09/11/2026 | Módulo de dataset passou a aceitar **vários tamanhos de instância por tarefa**. Corrigido o cálculo do ganho do 2-Opt em matrizes assimétricas (que havia corrompido uma primeira geração) e acrescentado o invariante NN/2-Opt ≥ ótimo. Conjunto de dados de Vitória gerado e validado. |
| 09/11/2026 | Artigo atualizado: seções 3.4, 3.6, 3.7 e 3.8 revisadas; seções 4.3, 4.4 e 4.5 escritas com os dados reais; Tabelas 2 e 3 e Figura 8 preenchidas; Resumo, Abstract e Considerações Finais parcialmente completados. |
| 09/11/2026 | Criado o `AntOptimization.Benchmark` e executado o experimento de comparação (40 instâncias, 880 execuções). Tabela 1, seções 4.2 e 4.3, Figura 7, Resumo, Abstract e conclusões completados. `GeneticAlgorithmEngine` ganhou semente opcional (107 testes). |
| 09/11/2026 | Capturas de tela das Figuras 4, 5 e 6 e parágrafo de 4.1. Restam a identificação dos autores, a conferência das referências e o corte de uma página. |
