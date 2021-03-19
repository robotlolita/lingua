module Lingua.Parser

open Fohm.Generated
open Lingua.AST

let parse source filename =
  match Lingua.parse "Grammar" source { filename = filename } with
  | Ok v -> v
  | Error e -> failwithf "%s" e