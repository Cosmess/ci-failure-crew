# Adicionando uma falha conhecida

[English](../../en/guides/adding-a-known-failure.md) | **Português** | [简体中文](../../zh-CN/guides/adding-a-known-failure.md)

Algumas falhas sempre têm a mesma causa e nunca são culpa do código: um timeout do job, um runner que perdeu a
conexão, um feed de pacotes fora do ar. Elas são explicadas por regras em
[`KnownFailures.cs`](../../../src/CiFailureCrew.Core/LogAnalysis/KnownFailures.cs), sem chamar o modelo.

## Quando adicionar uma regra

- O log tem uma mensagem fixa que identifica a causa (`No space left on device`, `error NU1301`).
- A causa está fora do código, então a correção é sempre a mesma.
- A mensagem não aparece em falhas causadas pelo código. Uma regra que reconhece uma falha real do código a esconde.

Se a causa depende do código, deixe os agentes cuidarem dela.

## Passos

1. Adicione um `KnownFailure` em `KnownFailures.All`: um id em kebab-case, a categoria, o padrão e a explicação e a
   correção em inglês, português e chinês. As regras são testadas em ordem: coloque as específicas primeiro.
2. Adicione a mensagem em `LogTests.Recognizes_known_failures`.
3. Adicione um caso de avaliação com `"knownRule": "<id>"` (veja o [evals/README.pt-BR.md](../../../evals/README.pt-BR.md)).
   Os testes do dataset conferem que a regra o reconhece e que nenhum outro caso é reconhecido por ela.
4. Rode `dotnet test` e atualize a lista de falhas conhecidas nos READMEs e na [arquitetura](../architecture.md).
