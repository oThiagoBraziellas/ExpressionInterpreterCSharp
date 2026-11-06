# ExpressionInterpreter em C#

Interpretador de expressões matemáticas em C# (.NET 8), escrito do zero e sem dependências: **analisador léxico**, **parser de descida recursiva**, **árvore sintática abstrata (AST)**, variáveis, funções nativas e funções definidas pelo usuário (com recursão), operador condicional, dobra de constantes e um **REPL** com mensagens de erro que apontam a posição do problema.

## Por que este projeto

Lexer, parser e avaliador são a base de compiladores, linguagens de consulta, motores de regras, planilhas e ferramentas de configuração. Escrever um parser de descida recursiva, com precedência e associatividade corretas, é um exercício recorrente em entrevistas (Google, Meta, Microsoft) e mostra domínio de recursão, modelagem de dados (AST como tipos algébricos), tratamento de erros e testes dirigidos por tabela. É também uma peça reutilizável: calculadoras, validação de fórmulas e DSLs usam exatamente esse desenho.

## Recursos

- Números (`42`, `3.14`, `.5`, `1e-3`), identificadores, operadores `+ - * / % ^`, comparações `< <= > >= == !=`, lógicos `&& || !` (com curto-circuito) e condicional `cond ? a : b`.
- Precedência e associatividade corretas: `^` associa à direita e `-2^2` vale `-4`; `2^-1` funciona.
- Variáveis (`x = 5`) e constantes protegidas (`pi`, `e`, `tau`).
- Funções nativas: `sin cos tan asin acos atan atan2 sqrt cbrt abs exp log log2 log10 pow hypot floor ceil trunc round sign min max sum avg clamp`.
- Funções do usuário com recursão (`fact(n) = n <= 1 ? 1 : n * fact(n - 1)`), escopo local para parâmetros e limite de profundidade (200) que vira erro comum em vez de estouro de pilha.
- Extensível: `RegisterFunction` adiciona funções nativas.
- `ConstantFolder`: simplifica subárvores constantes sem esconder erros (`1/0` continua sendo reportado com a posição).
- Erros tipados com posição (`LexException`, `ParseException`, `EvaluationException`) e REPL que desenha um `^` sob o ponto do erro.
- Comandos do REPL: `:help`, `:vars`, `:funcs`, `:ast <expr>`, `:quit`.

## Como executar

Pré-requisito: .NET 8 SDK.

```bash
dotnet build
dotnet run --project src/ExpressionInterpreter.Cli                 # REPL
dotnet run --project src/ExpressionInterpreter.Cli -- "sqrt(2) * 2" # uma expressão
printf 'x = 4\nfact(n) = n <= 1 ? 1 : n * fact(n - 1)\nfact(10) + x\n' | dotnet run --project src/ExpressionInterpreter.Cli
```

Exemplo de sessão:

```
> x = 4
4
> sq(n) = n ^ 2
defined sq(n)
> sq(x) + 1
17
> :ast 1 + 2 * 3
(1 + (2 * 3))
> 1 + * 2
Parse error: Unexpected '*'
  1 + * 2
      ^
```

Como biblioteca:

```csharp
var interpreter = new Interpreter();
interpreter.Execute("area(w, h) = w * h");
double value = interpreter.Evaluate("area(3, 4) + 1"); // 13
```

## Como testar

```bash
dotnet test
```

São 112 testes: tokens e posições, tabelas de precedência/associatividade validadas pela AST impressa com parênteses, erros de sintaxe com posição exata, aritmética e funções, escopo, recursão (fatorial, Fibonacci), curto-circuito, erros de execução (divisão por zero, domínio, estouro, aridade), equivalência com e sem dobra de constantes e transcrições completas do REPL.

## Decisões de projeto

- **Gramática por níveis de precedência**, um método por nível. A associatividade à esquerda é tratada por um laço (`ParseLeftAssociative`); a potência é à direita porque seu operando direito é um `unary`, o que também permite `2^-1`. A gramática completa está documentada no código do `Parser`.
- **AST imutável** (`record`s). O avaliador percorre a árvore com `switch` por padrão; `AstPrinter` e `ConstantFolder` são outros "visitantes" sobre os mesmos tipos, sem alterar os nós.
- **Um único tipo de valor (`double`)**, com booleanos como 0/1. Simplifica a semântica; o custo é perder a distinção entre número e booleano.
- **Erros aritméticos viram exceções.** NaN e infinito (divisão por zero, `sqrt(-1)`, `log(0)`, overflow) lançam `EvaluationException` com a posição, em vez de propagar valores silenciosos.
- **Escopo.** Parâmetros ficam em um dicionário local por chamada; variáveis globais são visíveis dentro das funções (escopo dinâmico simples, avaliado no momento da chamada). Variáveis e funções têm namespaces separados na definição para evitar ambiguidades.
- **Complexidade.** Lexer e parser são O(n) no tamanho da entrada; avaliar é O(tamanho da árvore) por chamada. A recursão é limitada por `MaxCallDepth`.
- **Igualdade exata** (`==` compara `double` sem tolerância): `0.1 + 0.2 == 0.3` é falso, como na maioria das linguagens.

## Estrutura de pastas

```
ExpressionInterpreter/
├── ExpressionInterpreter.sln
├── src/
│   ├── ExpressionInterpreter/       # Lexer, Parser, Ast, Interpreter, ConstantFolder, AstPrinter, Repl
│   └── ExpressionInterpreter.Cli/   # executável (REPL e modo de linha de comando)
└── tests/
    └── ExpressionInterpreter.Tests/ # testes xUnit
```

## Limitações conhecidas

- Apenas números reais de dupla precisão; sem strings, listas, booleanos nativos ou inteiros de precisão arbitrária.
- Sem multiplicação implícita (`2x`), sem laços e sem funções anônimas ou de primeira classe.
- Funções do usuário não são fechamentos (*closures*): enxergam o estado global no momento da chamada.
- Sem bytecode ou compilação: a avaliação é por percurso da árvore, adequada para fórmulas, não para cargas pesadas.
- Os erros do lexer e do parser interrompem no primeiro problema (sem recuperação de erros).

## Licença

MIT. Veja o arquivo [LICENSE](LICENSE).
