# Testes

[English](../en/testing.md) | **Português** | [简体中文](../zh-CN/testing.md)

## Projetos

| Projeto | O que cobre | Rede/LLM |
|---|---|---|
| `tests/CiFailureCrew.Tests` | Análise de logs, regras, orquestração e roteamento, retry, renderização, configuração, GitHub, MCP, avaliação | Nunca |
| `tests/CiFailureCrew.IntegrationTests` | Explicações de ponta a ponta com a API real da OpenAI | Sim, só com `OPENAI_API_KEY` |
| `src/CiFailureCrew.Evals` (não é teste) | Qualidade das explicações em `evals/cases` | Sim, sob demanda |

No CI, os testes unitários rodam em todo push e pull request. Os de integração só rodam em pushes na `main`, porque
custam dinheiro, e são pulados quando o secret não existe.

Os testes unitários também cobrem:

- **Roteamento**: quais agentes cada tipo de falha chama, e que falhas conhecidas não chamam nenhum.
- **Resiliência**: triagem, investigação ou síntese que falham ainda geram um diagnóstico.
- **Segredos**: nenhum prompt contém uma senha do log ou do diff.
- **O protocolo MCP real** e o sandbox do `RepositoryReader` (path traversal, links simbólicos, arquivos de segredos).
- **Consistência do dataset**: todo caso carrega, casos de regra são reconhecidos pela regra e os demais não, as
  linhas rotuladas existem no diff ou no log, e os cabeçalhos dos hunks batem com o conteúdo.

## Fakes

- **`FakeChatClient`** implementa `IChatClient`. Recebe uma função `prompt → resposta` (o prompt inclui as instruções
  do agente, então dá para responder por agente), registra os prompts e informa 1000 tokens de entrada e 100 de
  saída por chamada. Lance uma exceção na função para simular falhas.
- **`FakeHttpHandler`** registra cada requisição e responde com a função informada.
- **`SampleLogs`** monta logs no formato do GitHub, com timestamps e cabeçalhos de passo.

## Convenções

- O nome do teste descreve o comportamento, em inglês com `_`.
- Arrange / act / assert separados por linhas em branco.
- Crie o orquestrador com `retryBackoff: _ => TimeSpan.Zero`.
- Nunca escreva nos testes textos que pareçam credenciais reais: monte-os em tempo de execução (`"gh" + "p_" + ...`),
  ou o push protection do GitHub bloqueia o push.
- Todo bug corrigido ganha um teste que teria falhado antes da correção.
