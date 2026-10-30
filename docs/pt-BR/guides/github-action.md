# Usando a GitHub Action

[English](../../en/guides/github-action.md) | **Português** | [简体中文](../../zh-CN/guides/github-action.md)

## Configuração mínima

1. Crie o secret `OPENAI_API_KEY` no repositório (*Settings → Secrets and variables → Actions*).
2. Crie `.github/workflows/ci-failure-crew.yml`:

```yaml
name: CI Failure Crew

on:
  workflow_run:
    workflows: [CI]          # os nomes dos workflows a observar
    types: [completed]

permissions:
  actions: read              # ler a execução e seus logs
  contents: read
  pull-requests: write       # comentar no pull request

jobs:
  explain:
    if: github.event.workflow_run.event == 'pull_request' && github.event.workflow_run.conclusion != 'cancelled'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: Cosmess/ci-failure-crew@v1
        with:
          openai-api-key: ${{ secrets.OPENAI_API_KEY }}
          language: pt-BR
```

3. Mescle na branch padrão: o GitHub só roda workflows de `workflow_run` a partir dela.

## Por que `workflow_run`

O workflow que falhou não consegue explicar a si mesmo: os logs só ficam completos quando ele termina, e em pull
requests de forks ele não tem secrets. O `workflow_run` começa depois que o workflow observado termina, no contexto
da branch padrão, com acesso aos secrets. Ali, o `actions/checkout` baixa a branch padrão, então o código do pull
request nunca é executado. As mudanças do pull request só chegam aos agentes como diff, como dados.

## O que acontece em cada conclusão

| Conclusão da execução observada | O que a Action faz |
|---|---|
| `failure`, `timed_out` | Explica os jobs que falharam e cria ou atualiza o comentário |
| `success` | Marca o comentário existente como resolvido, recolhendo o diagnóstico anterior |
| qualquer outra | Nada |

Mantenha `success` no `if` (ou remova a condição sobre a conclusão) para o comentário ser resolvido quando o CI passar.

## Inputs

| Input | Padrão | Descrição |
|---|---|---|
| `openai-api-key` | obrigatório | Chave da API da OpenAI |
| `github-token` | `github.token` | Token para ler a execução e os logs e comentar |
| `model` | `gpt-5-mini` | Modelo da OpenAI |
| `language` | vazio (usa a configuração, ou `en`) | `en`, `pt-BR` ou `zh-CN` |
| `config` | `.ci-failure-crew.yml` | Arquivo de configuração (ignorado se não existir) |
| `run-id` | `github.event.workflow_run.id` | Execução a explicar, para usar a Action fora do `workflow_run` |
| `post-comment` | `true` | Comenta no pull request |
| `read-repository` | `true` | Ferramentas MCP somente leitura sobre o repositório |

## Vários workflows

Liste-os em `workflows: [CI, E2E]`. Cada workflow mantém seu próprio comentário no pull request.

## Runners e tempo de início

| Runner | Como a Action roda | Início |
|---|---|---|
| Linux (amd64 ou arm64) com uma tag de versão (`@v1`, `@v1.0.0`) | Baixa a imagem pronta `ghcr.io/cosmess/ci-failure-crew` | poucos segundos |
| Windows, macOS, ou uma branch / SHA de commit | Compila a ferramenta a partir do código | cerca de 15 segundos nos runners do GitHub |

## Observações

- Execuções que passam não custam nada: nenhum modelo é chamado.
- Falhas conhecidas (timeouts, runner perdido, feed fora do ar...) são explicadas sem modelo.
- Um push numa branch sem pull request só escreve a explicação no resumo do job.
- Fixe uma versão (`@v1`, uma tag ou um SHA de commit) em vez de `@main`.
