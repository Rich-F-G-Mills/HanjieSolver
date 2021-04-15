# Hanjie Solver
[Hanjie](https://en.wikipedia.org/wiki/Nonogram)'s are a type of picture puzzle where clues along the horizontal and vertical axes uniquely specify a picture such as that shown below.

![enter image description here](http://www.anypuzzle.com/puzzles/logic/Hanjie/example-big.png)

The numbers represent the length of each clue on a given row/column. However, no indication is given as to the start position of each clue.

An additional constraint is that clues must be seperated by at least one unfilled square along that same slice.

In my prior attempts to solve these I used to generate all possible combinations of start positions for each clue with some additional (albeit basic) logic in order to wittle these down to something a bit more manageable.

A course on Coursera introduced me to the concept of Satisfiability Modulo Theories ([SMT](https://en.wikipedia.org/wiki/Satisfiability_modulo_theories)) which involve determining whether a solution exists for a series of logical propositions.

By creating boolean variables representing the following aspects of a puzzle:
 - Whether the cell at (i,j) is filled in (or not)
 - Whether the kth clue for the ith row starts in a particular column.
 - Whether the kth clue for the jth column starts in a particular row.
 - Which clue start positions are feasible depending on what prior clue has been selected.

With the above approach, it is possible to generate a series of logical propositions that, when solved, uniquely specifies the solution.

This project is implemented in F# and leverages the [Z3 library](https://github.com/Z3Prover/z3/wiki) which offers SMT solving functionality.

The program only requires that puzzles are entered in a particlar format. For example, the puzzle located [here](https://www.nonograms.org/nonograms/i/32131) would be encoded as:

> **ROW**
> 1,4
> 2,3,3
> ...
> 1,4
> **COL**
> 7 6,5
> ...
> 1,1,4

(The complete file can be found [here](https://github.com/DaMillch/HanjieSolver/blob/master/PUZZLES/32131.txt))

Even for a simple puzzle such as the above, the resulting list of propositions to solve can be extensive. An example of the result SMT code for the above puzzle can be found [here](https://github.com/DaMillch/HanjieSolver/blob/master/SMT%20Sample.txt); as can be seen, this 20x15 grid results in ~8,000 lines of SMT instructions.

However, efficiences within the Z3 engine mean that the puzzle is solved almost immediately.

The resulting console output is shown below.


```

  ██      ████████
  ████  ██████      ██████
  ██████      ████████
  ████  ██████  ████    ██████
████    ██  ████    ████████
████  ██    ██████  ████
██              ████    ██████
██  ██    ████    ████  ████
██  ██      ████    ██    ██
██        ██  ██    ████
██        ██  ████    ████
  ██    ██    ████        ██
  ██    ██  ████████
  ██    ██  ████████  ████
  ██  ████  ████████████
  ████████  ██████████
    ████    ████████        ██
              ██████        ██
              ████        ████
                ██    ████████
```
 **In conclusion...** This project showed one of the many uses of the Z3 SMT engine and how it can be applied to seemingly unrelated problems.