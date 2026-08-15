# Baseline de Regressão

Este documento registra a execução do motor contra a pasta pessoal do usuário (`G:\NOT SHARE`).

> [!WARNING]
> Este teste não é o teste autoritativo de regressão. O teste que garante que o motor funciona é o teste unitário isolado rodando sobre `tests/fixtures/`.
> Os arquivos em pastas reais do usuário mudam (são adicionados, deletados) e isso causou a invalidação da baseline anterior.

## Execução

- **Data**: 2026-08-15
- **Ferramenta**: `kHzAnalyzer.ConsoleTest`

## Resultados

Em 15/08/2026, a execução sobre a biblioteca pessoal do usuário resultou em:

```
Total: 218  ·  OK: 216  ·  Abaixo: 2  ·  Erros: 0
```

Faixas identificadas abaixo do critério:
1. `Fatsync, DOUG! - Fire!` — detectável apenas até ~19.730 Hz
2. `Gnarls Barkley - Crazy (GABSS AFTERNOON EDIT)` — detectável apenas até ~16.229 Hz
