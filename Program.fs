// Learn more about F# at http://fsharp.org

open System
open System.IO
open Microsoft.Z3
open System.Linq

type Mode =
    | Row
    | Column
    | None

type Clue =
    { Length: int;
      Starts: (int * BoolExpr) array }        

[<EntryPoint>]
let main argv =

    let (row_clue_lengths, col_clue_lengths) =

        let rec process_file mode ((row_clues, col_clues) as clues) lines =
            match lines with
            | [] -> (row_clues, col_clues)
            | line::_lines ->
                match line with
                | "ROW" -> process_file Row clues _lines
                | "COL" -> process_file Column clues _lines
                | _ ->
                    let lengths =
                        line.Split ","
                        |> Array.map Int32.Parse

                    let new_clues =
                        match mode with
                        | Row -> (Array.append row_clues [|lengths|], col_clues)
                        | Column -> (row_clues, Array.append col_clues [|lengths|])
                        | _ -> failwith "No mode selected."

                    process_file mode new_clues _lines                        

        //File.ReadAllLines @"PUZZLES\32131.TXT"
        File.ReadAllLines @"..\..\..\PUZZLES\FIERCE.TXT"
        //File.ReadAllLines @"PUZZLES\19043.TXT"
        |> Seq.filter (fun x -> not(x.StartsWith("#")))
        |> Seq.toList
        |> process_file None ([||], [||])

    let (num_rows, num_cols) as grid_size =
        (row_clue_lengths |> Array.length, col_clue_lengths |> Array.length)

    Console.WriteLine("Generating constraints...\n")

    use ctx = new Context()
    use solver = ctx.MkSolver()

    let grid_vars =
        (grid_size ||> Array2D.init) (fun row col -> ctx.MkBoolConst($"c({row})({col})"))

    let process_axis mode dim_size axis_lengths =
        let process_slice dim_idx lengths =
            // Determine the earliest and latest point at which a clue can start.
            let min_starts =
                lengths
                |> Array.map ((+) 1)
                |> Array.scan (+) 0
                |> Array.take lengths.Length

            let max_starts =
                lengths
                |> Array.rev
                |> Array.scan (fun s l -> s - 1 - l) (dim_size + 1)
                |> Array.skip 1
                |> Array.rev            

            let gen_start_vars clue_idx (length, min_start, max_start) =
                let start_vars =
                    seq { min_start .. max_start }
                    |> Seq.map (fun start -> (start, ctx.MkBoolConst($"{mode}({dim_idx})({clue_idx})({start})")))
                    |> Array.ofSeq

                // Generates assertions for a clue starting at a particular cell.
                let assert_cells (start, var) : unit =
                    let cells =
                        match mode with
                        | Row -> grid_vars.[dim_idx, start..(start+length-1)]
                        | Column -> grid_vars.[start..(start+length-1), dim_idx]
                        | None -> failwith "Unexpected."

                    solver.Assert(ctx.MkImplies(var, ctx.MkAnd(cells)))

                // Apply the above for each possible starting position of this clue.
                start_vars
                |> Array.iter assert_cells

                { Length = length; Starts = start_vars; }


            let assert_adj_clues (left, right) =
                // Determines the valid start positions for the next clue given the starting position of this one.
                let valid_subseq_clues start =
                    right.Starts
                    |> Array.where (fun x -> (fst x) > start + left.Length)
                    |> Array.map snd

                // Generate the above assertions for all possible start positions of the 'left' clue.
                left.Starts
                |> Array.map (fun x -> (snd x, valid_subseq_clues (fst x)))
                |> Array.iter (fun (l, rs) -> solver.Assert(ctx.MkImplies(l, ctx.MkOr(rs))))

            // Generate the starting vars for each clue and the corresponding assertions.
            let slice_starts =
                Array.zip3 lengths min_starts max_starts
                |> Array.mapi gen_start_vars

            // The starting position for a clue determines to allowable starting positions of the following clue.
            // The following generates the required assertions.
            slice_starts
            |> Array.pairwise
            |> Array.iter assert_adj_clues

            slice_starts

        // Generate the required assertions for all clues along the given axis.
        axis_lengths
        |> Array.mapi process_slice

    let row_starts = process_axis Row num_cols row_clue_lengths
    let col_starts = process_axis Column num_rows col_clue_lengths

    // For each clue along any axis, assert that only one of its starting variables MUST be selected.
    row_starts
    |> Array.append col_starts
    |> Array.collect (Array.map (fun x -> x.Starts |> Array.map snd))
    |> Array.iter (fun x ->
        solver.Assert(ctx.MkPBEq(x |> Array.map (fun _ -> 1), x, 1)))

    // Where a given cell is true, there are corresponding clues that could be selected by implication.
    // This section makes those assertions.
    let grid_assert row col cell =
        let curr_row_starts = row_starts.[row]
        let curr_col_starts = col_starts.[col]

        let get_valid_starts idx (clue: Clue) =  
            let is_valid_start =
                fun (start, _) -> idx >= start && idx < start + clue.Length

            clue.Starts
            |> Array.where is_valid_start
            |> Array.map snd
        
        let valid_row_starts =
            curr_row_starts
            |> Array.collect (get_valid_starts col)

        let valid_col_starts =
            curr_col_starts
            |> Array.collect (get_valid_starts row)

        solver.Assert(ctx.MkImplies(cell, ctx.MkOr(valid_row_starts)))
        solver.Assert(ctx.MkImplies(cell, ctx.MkOr(valid_col_starts)))

    // Make the above assertions for every cell in the grid.
    grid_vars
    |> Array2D.iteri grid_assert

    //File.WriteAllText("SMT.TXT", solver.ToString())

    Console.WriteLine("Checking satisfiability...\n")

    let status = solver.Check();

    printfn "Status = %A\n\n" status

    // Output the completed puzzle to the console.
    for row_idx in { 0..num_rows-1 } do
        grid_vars.[row_idx,*]
        |> Array.map (fun x -> solver.Model.Eval(x).BoolValue = Z3_lbool.Z3_L_TRUE)
        |> Array.iter (fun x -> Console.Write(if x then "\u2588\u2588" else "  "))

        Console.Write('\n')

    Console.WriteLine("\n")

    0 // return an integer exit code
