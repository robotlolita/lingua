module Lingua.OhmCodegen

open Fable.Core
open Lingua.AST

let enumerate xs =
  Seq.zip {1..(Seq.length xs)} xs

[<Emit("JSON.stringify($0)")>]
let toString (s:string) = jsNative

// == Grammar language
let genDesc desc =
  match desc with
  | Some x -> $"({x})"
  | None -> ""

let genRuleParams ps =
  if Array.isEmpty ps then
    ""
  else
    $"""<{String.concat ", " ps}>"""

let rec genTerm t =
  match t with
  | TSeq ts -> Seq.map genTerm ts |> String.concat " "
  | TAlt ts -> Seq.map genTerm ts |> String.concat " | "
  | TStar t -> $"{genTerm t}*"
  | TPlus t -> $"{genTerm t}+"
  | TOpt t -> $"{genTerm t}?"
  | TNot t -> $"~{genTerm t}"
  | TLookahead t -> $"&{genTerm t}"
  | TLex t -> $"#{genTerm t}"
  | TApply (t, ps) ->
      if Array.isEmpty ps then
        t
      else
        $"""{t}<{String.concat ", " (Seq.map genTerm ps)}>"""
  | TRange (a, b) ->
      $"{toString a}..{toString b}"
  | TTerminal t ->
      toString t
  | TParens t ->
      $"({genTerm t})"

let genBinder b =
  match b with
  | BBound (_, t) -> genTerm t
  | BUnbound t -> genTerm t

let genBody (n: int, b:RuleBody) =
  let s = Seq.map genBinder b.Terms |> String.concat " "
  $"{s}  -- alt{n}\n"

let genBodies b =
  Seq.map genBody (enumerate b) |> String.concat " | "

let generateRule rule =
  match rule with
  | RDefine(_, n, ps, desc, b) ->
      $"{n}{genRuleParams ps} {genDesc desc} = {genBodies b}"
  | ROverride(_, n, ps, b) ->
      $"{n}{genRuleParams ps} := {genBodies b}"
  | RExtend(_, n, ps, b) ->
      $"{n}{genRuleParams ps} += {genBodies b}"

let generateRules rules =
  Seq.map generateRule rules

let generateGrammar (g:Grammar) =
  let rules = generateRules g.Rules
  $"""
  {g.Name} {{
    {String.concat "\n\n" rules}
  }}
  """