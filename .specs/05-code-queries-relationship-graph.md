# Fase 4 — Expansão de grafo (até 2 níveis, todas as relações) em `code-queries`

**Status: concluído — requisito central deste projeto, entregue.** `IRelationshipGraphRepository`/
`RelationshipGraphRepository` (CTE recursiva por FK bigint), `CodeGraph`/`GraphNode`/`GraphEdge`,
`CodeQueryService` decorando `matches` com `graph`, contrato REST `{matches, graph:{nodes,
edges,truncated}}`. 29 testes verdes (12 unit incluindo os 2 novos de expansão de grafo em
`CodeQueryServiceTests`, 19 Testcontainers incluindo os 10 novos de
`RelationshipGraphRepositoryTests` cobrindo cada caso da lista de verificação, 4 HTTP
atualizados para o novo formato).

**⚠️ Correção em relação ao rascunho original**: o rascunho tinha `graph.nodes` incluindo os
próprios nós de `matches` (nível 0). Na implementação, decidido **excluir** os roots de
`graph.nodes` (eles já estão completamente descritos em `matches`; incluí-los duplicaria
dado sem necessidade) — `graph.nodes` contém só profundidade 1-2, mas `graph.edges` continua
incluindo as arestas que saem/chegam nos próprios matches (com `depth: 0` quando tocam
diretamente um root), já que essas arestas são informação nova mesmo quando o nó em si não é.

**Smoke test real contra `code3rag` (ver .specs/04, mesmo projeto `CodeRag.Api`, id 1)**:
pergunta "where is the code that handles a natural language code query request?" → match
top `CodeQueriesController.QueryAsync` (id 22). Grafo de 2 níveis a partir dos 3 matches
alcançou corretamente, entre outros, `RouteId.TryParsePositive` (nível 1, via `calls`) e, a
partir dele, **quem o chama de volta** — `ProjectsController.DeleteAsync/GetAsync/
UpdateAsync`, `CodeQueriesController.SubmitFeedbackAsync` (nível 2) — confirmando travessia
bidirecional real de 2 saltos contra dado de produção, incluindo várias arestas para
símbolos externos (`to_id: null`, ex. `System.Object.GetHashCode()`,
`Microsoft.AspNetCore.Mvc.ControllerBase`) preservadas como folhas conforme desenhado.

## Requisito (como pedido pelo usuário)

> O endpoint `/api/v1/projects/{projectId}/code-queries` deve incluir até dois níveis do
> grafo de relações com todas as relações.

Interpretação adotada: para cada entidade de código retornada pela busca semântica
(os "matches" — nível 0), o endpoint também devolve as entidades alcançáveis em até 2
saltos (hops) via `public.ciir_relations`, considerando **todo tipo de relação** (`calls`,
`reads`, `constructs`, `overrides`, `writes`, `implements`, `throws`, `inherits`,
`catches` — universo completo confirmado em produção, ver `01-schema-discovery.md`) e
**ambas as direções** (quem a entidade referencia, e quem referencia a entidade).

## Por que isso é um grafo, não uma árvore

Nível 1 de um match pode ter arestas entre si, e nível 2 de matches diferentes pode
convergir no mesmo nó. A resposta representa o grafo **desduplicado** por nó, com uma
lista de arestas à parte — não uma árvore com repetição.

## Contrato implementado

```json
{
  "matches": [
    {
      "id": 22,
      "kind": "method",
      "symbol_container": "CodeRag.Api.Controllers.CodeQueriesController",
      "symbol_name": "QueryAsync",
      "symbol_qualified_name": "CodeRag.Api.Controllers.CodeQueriesController.QueryAsync",
      "source_file": "src/CodeRag.Api/Controllers/CodeQueriesController.cs",
      "embedding_text": "...",
      "similarity": 0.83
    }
  ],
  "graph": {
    "nodes": [
      { "id": 61, "kind": "method", "symbol_container": "CodeRag.Api.Problems.RouteId", "symbol_name": "TryParsePositive", "symbol_qualified_name": "CodeRag.Api.Problems.RouteId.TryParsePositive", "source_file": "src/CodeRag.Api/Problems/RouteId.cs", "depth": 1 }
    ],
    "edges": [
      { "from_id": 22, "to_id": 61, "relation_type": "calls", "target_symbol": "CodeRag.Api.Problems.RouteId.TryParsePositive", "resolution_origin": "project", "depth": 0 },
      { "from_id": 22, "to_id": null, "relation_type": "calls", "target_symbol": "System.Threading.Tasks.Task.Run", "resolution_origin": "framework", "depth": 0 }
    ],
    "truncated": false
  }
}
```

Notas de desenho:
- **`graph.nodes` NÃO repete os próprios nós de `matches`** (nível 0) — eles já estão
  completamente descritos em `matches`; incluí-los de novo em `graph.nodes` duplicaria
  dado sem necessidade (⚠️ correção em relação ao rascunho original desta spec, que
  incluía o root em `graph.nodes` — revertido na implementação). `graph.nodes` contém só
  os nós de profundidade 1 e 2, **desde que resolvidos a um documento real**
  (`ciir_relations.target_document_id`/`source_document_id IS NOT NULL`).
- `graph.edges`, ao contrário, **inclui as arestas que tocam os próprios matches**
  (`depth: 0` quando ligam diretamente a um root) — a aresta em si é informação nova
  (quem/o que o match chama ou é chamado por) mesmo quando um dos nós já é conhecido.
- **Arestas para alvos não resolvidos (`target_document_id IS NULL` — 57% dos dados reais
  medidos, ver `01-schema-discovery.md`) ainda entram em `graph.edges`**, com `to_id: null`
  e um campo `target_symbol` (nome qualificado do alvo, sempre presente mesmo sem
  documento) — descartá-las violaria "todas as relações". Elas são sempre **folhas**: o
  BFS não continua a partir de um alvo sem `document_id` (não há nó para expandir).
  Simetricamente, uma aresta pode ter `from_id: null` se o *destino* de uma travessia
  reversa (nível 2, chegando "de trás") não tiver `source_document_id` resolvido — caso
  raro na prática (a origem de uma relação é sempre algo indexado), mas o contrato precisa
  admitir ambos os lados como nulos por simetria de schema.
- `graph.edges[].depth` é a distância mínima (em saltos) a partir de **algum** nó de
  `matches`.
- Nenhum campo de similaridade nos nós do grafo além de `matches`.
- `relation_type` é `string` livre — confirmado como texto livre no schema real (coluna
  `ciir_relations.kind`), 9 valores observados em produção, sem enum fechado.
- Campos adicionais de `ciir_relations` que agregam valor e são baratos de incluir por
  aresta, já que a tabela já os tem: `resolution_origin` (`project`/`solution`/
  `framework`/`dependency`) — útil para o consumidor distinguir "isso chama algo do meu
  próprio código" de "isso chama algo do framework", especialmente relevante justamente
  nas arestas com `to_id: null`.

**Alternativa rejeitada, registrada para referência**: aninhar `related: [...]` dentro de
cada item de `matches`. Duplica nós quando matches compartilham vizinhos — descartada, ver
raciocínio completo na versão anterior deste documento (histórico de revisão do repo).

## Desenho da consulta (SQL — schema real, FKs bigint de verdade)

Diferente de um rascunho anterior deste documento (que assumia arestas ligadas só por
`document_id` texto sem FK, um achado que veio de `code2rag`, banco errado — ver
`01-schema-discovery.md`), o schema real de `code3rag` tem `ciir_relations.
source_document_id`/`target_document_id` como **FKs bigint de verdade** (`ON DELETE
CASCADE`) para `ciir_documents.id`. A travessia é direta, sem cast/join por texto:

```sql
WITH RECURSIVE graph(node_id, depth, path) AS (
    -- nível 0: os ids (bigint) dos próprios matches, ponto de partida do BFS
    SELECT unnest(@RootIds::bigint[]), 0, ARRAY[unnest(@RootIds::bigint[])]

    UNION ALL

    SELECT
        CASE WHEN r.source_document_id = g.node_id THEN r.target_document_id ELSE r.source_document_id END,
        g.depth + 1,
        g.path || CASE WHEN r.source_document_id = g.node_id THEN r.target_document_id ELSE r.source_document_id END
    FROM graph g
    JOIN public.ciir_relations r
      ON r.project_id = @ProjectId
     AND (r.source_document_id = g.node_id OR r.target_document_id = g.node_id)
    WHERE g.depth < 2
      -- só continua a travessia por arestas resolvidas a um documento nos dois lados
      AND r.source_document_id IS NOT NULL
      AND r.target_document_id IS NOT NULL
      -- guarda de ciclo: não revisitar um node_id já no caminho
      AND NOT (CASE WHEN r.source_document_id = g.node_id THEN r.target_document_id ELSE r.source_document_id END = ANY(g.path))
)
SELECT node_id, MIN(depth) AS depth
FROM graph
WHERE depth > 0  -- os próprios roots já estão em `matches`
GROUP BY node_id;
```

Uma segunda consulta traz **todas** as arestas tocando qualquer `node_id` alcançado
(incluindo as folhas com `target_document_id`/`source_document_id` nulo, que o `WHERE` da
CTE acima intencionalmente não segue, mas que ainda devem aparecer no `graph.edges` final):

```sql
SELECT r.source_document_id, r.target_document_id, r.kind, r.target_symbol, r.resolution_origin
FROM public.ciir_relations r
WHERE r.project_id = @ProjectId
  AND (r.source_document_id = ANY(@AllReachedIds) OR r.target_document_id = ANY(@AllReachedIds))
```

(onde `@AllReachedIds` é a união dos `node_id` da primeira consulta com os `@RootIds`),
e uma terceira resolve os dados descritivos (`kind`, `symbol_container`, `symbol_name`,
`symbol_qualified_name`, `source_path`) de cada `node_id` via `SELECT ... FROM
ciir_documents WHERE id = ANY(@AllReachedIds)` — sem necessidade de `LEFT JOIN`/tratamento
de "nó fantasma": como a travessia só segue FKs bigint reais com `ON DELETE CASCADE`, todo
`node_id` alcançado **necessariamente** existe em `ciir_documents` (o próprio banco
garante essa integridade, ao contrário do achado inicial equivocado contra `code2rag`).

**Sem filtro de `kind`/tipo de relação em nenhum ponto** — requisito explícito de "todas
as relações", e `ciir_relations.kind` é texto livre, confirmado.

## Limites de segurança (dimensionados com dado real de produção)

Medido em `code3rag` (ver `01-schema-discovery.md`): grau de saída médio **8.53**, máximo
**48**; grau de entrada máximo **29**; total de 4429 arestas, 901 documentos. Fan-out real
é modesto — um BFS de 2 níveis a partir de poucos matches dificilmente se aproxima de um
limite problemático nos dados atuais, mas mantém-se a constante de segurança como boa
prática defensiva (projeto futuro maior/mais denso, ou um hub como uma interface
amplamente implementada):

- `Graph:MaxNodesPerQuery` (proposto: **100**, generoso frente ao pior caso observado —
  um match isolado com grau 48 nos dois níveis ainda fica bem abaixo disso; revisar para
  cima só se um projeto real futuro se mostrar muito mais denso) — se o BFS ultrapassar
  esse total de nós únicos, trunca (menor `depth` primeiro, depois por `node_id`) e
  sinaliza via `graph.truncated: true`.
- `Graph:MaxDepth` — fixo em 2 por requisito, constante nomeada.

## Camadas afetadas (espelhando o padrão do code-rag-api)

- **`Application`**: novo `IRelationshipGraphRepository.GetGraphAsync(IReadOnlyList<long>
  rootIds, long projectId, int maxDepth, int maxNodes, CancellationToken)` →
  `GraphResult { Nodes, Edges, Truncated }`. Como a travessia já é por `id` bigint (não
  por uma chave lógica separada como se pensava antes da confirmação do schema real),
  `CodeQueryService.QueryAsync` não precisa buscar nenhum campo extra na Fase 3 além do
  `id` que já retorna — simplificação real em relação ao rascunho anterior.
- **`Infrastructure.Database`**: `RelationshipGraphRepository`, SQL da seção acima.
- **`Api`**: `CodeQueryResponse` novo (substitui o array simples), contratos
  `CodeQueryGraphResponse`/`CodeQueryGraphNodeResponse`/`CodeQueryGraphEdgeResponse`
  (`to_id`/`from_id` nullable `long?`, `target_symbol` nullable string).
- **`Mcp`**: `query_project_code` passa a devolver a mesma forma (`matches`/`graph`) —
  documentar isso claramente no `[Description]` da tool.

## Fora de escopo nesta fase

- Filtro opcional para o consumidor desligar o grafo por chamada (`include_graph: false`)
  — não foi pedido; extensão barata a considerar só se o custo do BFS se mostrar alto na
  prática (dado atual não sugere isso).
- Filtrar relações por tipo — explicitamente contrário ao requisito.
- Modelar a relação implícita de containment (`symbol_container`) como aresta adicional —
  não existe como `kind` em `ciir_relations`; fora do escopo de "todas as relações", que
  se refere às relações que já existem na tabela.

## Verificação de saída desta fase

- Teste de integração (Testcontainers, schema real de `ciir_documents`/`ciir_relations`):
  nó sem relações (grafo vazio), relações só de nível 1, nível 2 alcançando um nó já
  visto em nível 1 (sem duplicar, guarda de ciclo funcionando), ciclo direto A→B→A (sem
  loop infinito), aresta com `target_document_id IS NULL` aparecendo em `graph.edges` com
  `to_id: null` e `target_symbol` preenchido (sem tentar expandir a partir dela), e um caso
  de fan-out acima de `MaxNodesPerQuery` confirmando truncamento sinalizado.
- Teste confirmando que os 9 tipos de relação reais (`calls`, `reads`, `constructs`,
  `overrides`, `writes`, `implements`, `throws`, `inherits`, `catches`) aparecem quando
  presentes no fixture — nenhum filtrado silenciosamente.
- Smoke test manual contra `code3rag` real: `code-queries` para um projeto como
  `CodeRag.Api` (id a confirmar na hora, um dos 21 já existentes) com uma pergunta que
  bata em `CodeQueriesController.QueryAsync` deve trazer, no grafo, pelo menos as chamadas
  a `ICodeQueryService.QueryAsync`/`IFeedbackService.SubmitAsync` (nível 1, `calls`) e o
  que essas chamam por baixo (nível 2) — dado real já existe para validar isso hoje.
