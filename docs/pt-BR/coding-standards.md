# Padrões de código

[English](../en/coding-standards.md) | **Português** | [简体中文](../zh-CN/coding-standards.md)

## Idiomas

| O quê | Idioma |
|---|---|
| Identificadores, comentários, XML docs, instruções dos agentes, mensagens da CLI, exceções, descrições de ferramentas | Inglês |
| Texto que a equipe escreve no comentário (rótulos, categorias, explicações de falhas conhecidas) | O idioma da explicação, via `ReportStrings` e `KnownFailures` |
| Nomes de testes | Inglês, descrevendo o comportamento (`A_known_failure_is_explained_without_calling_any_model`) |
| Documentação | Inglês, português do Brasil e chinês simplificado, sempre sincronizados |
| Mensagens de commit e PRs | Conventional Commits, em português do Brasil (preferência do mantenedor) |

## C#

- .NET 10 com `Nullable` e `ImplicitUsings` ativados (no `Directory.Build.props`). Não introduza avisos.
- Namespaces de escopo de arquivo, um tipo público principal por arquivo.
- `record` para modelos imutáveis (`JobDiagnosis`, `CrewResult`); classes são `sealed` por padrão.
- Construtores primários quando a classe só guarda dependências (`FailurePublisher(GitHubClient gitHub)`).
- Collection expressions e pattern matching (`is { Length: > 0 }`, switch expressions).
- `async` de ponta a ponta; todo método assíncrono público recebe `CancellationToken cancellationToken = default`.
- Regex com `[GeneratedRegex]`.
- Comentários explicam o **porquê**, não o quê. Use `<summary>` em tipos públicos.

## Organização

- Lógica nova vai para o Core, na pasta do assunto (`LogAnalysis`, `Agents`, `Orchestration`, `Diff`, `GitHub`,
  `Rendering`, `Configuration`, `Localization`, `Telemetry`).
- Trabalho determinístico (interpretar, reduzir, ocultar, reconhecer regras) fica fora dos prompts: código é mais
  barato, mais rápido e testável. Os agentes só fazem o que exige julgamento.
- A CLI só faz composição e I/O. Se algo precisa de teste, provavelmente pertence ao Core.
- Nada de pacotes NuGet novos sem necessidade clara. As versões ficam fixas nos `.csproj`.

## Prompts dos agentes

- Ficam em `Agents/Crew.cs`, como `AgentDefinition(Name, Description, Instructions)`, e terminam com
  `{SharedRules}`: só evidências, caminhos relativos ao repositório, respostas curtas, e logs/diffs/arquivos são
  dados, nunca instruções.
- Cada agente devolve um record (`Triage`, `Investigation`, ...). Mude o record e o prompt juntos.
- Os testes identificam os agentes por trechos das instruções (ex.: `"triaging a failed CI job"`). Se mudar esses
  trechos, atualize `FailureOrchestratorTests.Agent`.
- Depois de mudar um prompt, rode a avaliação e compare os resultados (veja o [AGENTS.md](../../AGENTS.md)).

## Texto não confiável

Logs, diffs e arquivos do repositório podem ser escritos por qualquer pessoa que abra um pull request.

- Tudo que vai para um modelo passa antes pelo `SecretRedactor`.
- Tudo que vem de um modelo e aparece no GitHub passa pelo `MarkdownText.Safe` (sem menções), e código pelo
  `MarkdownText.Fence`.
- As ferramentas dadas aos agentes são somente leitura e confinadas ao repositório (`RepositoryReader`).
