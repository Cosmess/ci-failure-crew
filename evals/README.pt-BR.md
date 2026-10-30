# Avaliação

[English](README.md) | **Português** | [简体中文](README.zh-CN.md)

A avaliação roda a equipe sobre execuções com falha rotuladas e mede com que frequência a explicação acerta. Ela chama
o modelo real, então roda sob demanda: localmente ou pelo workflow **Evaluation**.

```bash
export OPENAI_API_KEY=sk-...
dotnet run --project src/CiFailureCrew.Evals                       # todos os casos, explicações em inglês
dotnet run --project src/CiFailureCrew.Evals -- --filter flaky     # casos cujo nome contém "flaky"
dotnet run --project src/CiFailureCrew.Evals -- --language pt-BR --output relatorio.md
```

## Métricas

Cada verificação só conta nos jobs rotulados para ela.

| Métrica | O job passa quando |
|---|---|
| Categoria | a categoria é a esperada ou uma de `acceptCategories` |
| Causa raiz localizada | o arquivo é um de `rootCause` e a linha está dentro da `tolerance` |
| Origem | `relatedToPullRequest` bate |
| Explicação cita a causa | a explicação ou a correção contém uma das `keywords` (sem diferenciar maiúsculas e acentos) |
| Falhas conhecidas sem IA | o job foi explicado pela regra em `knownRule` |
| Totalmente correto | todas as verificações rotuladas passam |

A correção sugerida não é avaliada: julgar código automaticamente exigiria executá-lo.

## Um caso

```
evals/cases/<nome>/
  <job>.log       um por job com falha, no formato do GitHub (timestamps, cabeçalhos ##[group])
  diff.patch      o diff do pull request (opcional)
  history.json    o histórico do job na branch padrão (opcional): {"test": {"runs": 10, "failures": 3}}
  expected.json   os rótulos
```

```json
{
  "description": "A method renamed in the pull request is still called by a controller outside the diff.",
  "jobs": {
    "build": {
      "category": "Compilation",
      "acceptCategories": [],
      "relatedToPullRequest": true,
      "rootCause": [{ "file": "src/Shop.Core/OrderService.cs", "line": 8, "tolerance": 1 }],
      "keywords": ["GetTotal", "CalculateTotal"],
      "knownRule": null
    }
  }
}
```

As palavras-chave devem cobrir inglês, português e chinês quando a palavra muda entre idiomas, para o caso avaliar
explicações em qualquer um deles.

## Adicionando um caso

1. Crie a pasta com os arquivos acima. Logs reais são os melhores: baixe-os de uma execução com falha e remova o que
   for privado.
2. Confira os cabeçalhos dos hunks do `diff.patch`: contagens erradas deslocam os números de linha que os agentes veem.
3. Rode `dotnet test tests/CiFailureCrew.Tests`: os testes do dataset conferem que toda linha rotulada existe no diff
   ou no log, que os casos de regra são reconhecidos pela regra e os demais não.
4. Rode a avaliação e coloque os resultados no pull request.
