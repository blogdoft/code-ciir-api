# Fase 12 — `matches[].relations`: relações diretas por match

**Status: concluído.** Cada item de `matches` no response de `POST /api/v1/code-queries`
ganhou um campo `relations`: o conjunto completo de relações diretas (1 hop, ambas as
direções, todos os tipos) daquele match específico — sempre completo, independente do teto
`MaxGraphNodes` que pode truncar o `graph` de 2 hops. 152 testes verdes na solução inteira.

## Contexto

O usuário pediu para adicionar um campo `relations` ao response do endpoint. Como o
response já tinha `graph.edges` (as relações que tocam o grafo de 2 hops expandido a partir
de todos os matches juntos), ficou definida com o usuário a forma exata: um campo novo
**por match** (`matches[].relations`), não uma renomeação de `graph.edges` nem um campo
raiz — cada resultado da busca passa a trazer embutidas as suas próprias conexões diretas,
sem precisar cruzar `graph.edges`/`graph.nodes` por id.

## Por que uma consulta separada, não um filtro de `graph.edges`

`graph.edges` só inclui relações cujos dois extremos sobreviveram à expansão de 2 hops —
que pode ser truncada por `MaxGraphNodes` (100, favorecendo os nós de menor profundidade
primeiro). Isso significa que uma relação direta de um match poderia, em teoria, ficar de
fora de `graph.edges` se o outro extremo fosse cortado pelo truncamento — mesmo sendo uma
relação de profundidade 1, a mais "óbvia" de todas. `matches[].relations` é definido como
sempre completo e correto para aquele match específico, então é buscado com uma consulta
própria (`GetDirectRelationsAsync`), independente do tamanho do grafo de 2 hops.

## Contrato

```json
{
  "matches": [
    {
      "id": 1,
      ...,
      "relations": [
        { "from_id": 1, "to_id": 2, "relation_type": "calls", "target_symbol": "Foo.Bar.Qux", "resolution_origin": "project" }
      ]
    }
  ],
  "graph": { "nodes": [...], "edges": [...], "truncated": false }
}
```
Uma relação entre dois matches da mesma página (ex.: match A chama match B, ambos
retornados) aparece em `relations` de **ambos** — é, de fato, uma relação direta de cada um
deles. `to_id` é `null` quando o alvo está fora do universo indexado (externo/não
resolvido); `target_symbol` ainda o nomeia nesse caso. `relations` é sempre um array vazio
(nunca `null`) quando o match não tem relações diretas, ou quando `project_id` foi omitido
da requisição — mesma regra já aplicada a `graph` (Fase 9): `ciir_relations` é escopada por
`project_id`, então sem ele não há como buscar relações de forma correta.

## Implementação

- `MatchRelation` (novo tipo, `CodeCiir.Application.CodeQueries`) — mesmos campos de
  `GraphEdge`, sem `Depth` (não se aplica a uma lista plana de relações de 1 hop).
- `IRelationshipGraphRepository.GetDirectRelationsAsync(projectId, documentIds, ct)` — novo
  método, devolve `IReadOnlyDictionary<long, IReadOnlyList<MatchRelation>>` (ids sem
  relações diretas simplesmente não aparecem como chave). Implementado em
  `RelationshipGraphRepository` com uma consulta única cobrindo todos os ids pedidos de
  uma vez (`WHERE project_id = @ProjectId AND (source_document_id = ANY(@Ids) OR
  target_document_id = ANY(@Ids))`), reaproveitando o `EdgeRow`/`ToMatchRelation()` já
  existente ali.
- `CodeQueryService.QueryAsync` — depois de montar a página final de `results` (já
  rerankeada/truncada em `limit`), busca `GetDirectRelationsAsync` com os ids dessa página
  (mesma condição de `projectId is not null` do `graph`) e aplica `r with { Relations = ... }`.
- `CodeQueryResult.Relations` — novo parâmetro posicional opcional (`IReadOnlyList<MatchRelation>?
  = null`), mesma técnica de `RerankScore` (Fase 10) para não quebrar call sites posicionais
  existentes.
- Contratos: `CodeQueryRelationResponse` (Api) e `CodeQueryRelationToolResult` (Mcp), campo
  `Relations` (não-nulo, `[]` por padrão) adicionado a `CodeQueryResultResponse` e
  `CodeQueryMatchToolResult`.

## Fora de escopo

- Expandir `matches[].relations` além de 1 hop — é deliberadamente só o vizinho direto;
  para navegação mais profunda o `graph` de 2 hops continua sendo o caminho.
- Metadados extras por relação (ex. contagem, agrupamento por tipo).

## Verificação de saída desta fase

- Novos testes: `RelationshipGraphRepositoryTests` (Testcontainers, 7 casos: sem relações,
  saída, entrada, relação entre dois ids pedidos atribuída a ambos, relação de 2 hops não
  incluída, alvo externo não resolvido, lista de ids vazia); `CodeQueryServiceTests` (4
  casos: relações populadas via repositório, match sem relações → array vazio, `projectId`
  omitido → array vazio e repositório não chamado, sem matches → repositório não chamado);
  asserções de JSON em `CodeQueriesEndpointTests`/`CodeQueryToolsTests`.
- `dotnet build`/`dotnet test` na solução inteira: 152 testes verdes (0 falhas).
