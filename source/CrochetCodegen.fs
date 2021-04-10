module Lingua.CrochetCodegen

open Fable.Core
open Lingua.AST
open Lingua.OhmCodegen

let id n = n
let down (n:string) = (n.Substring(0, 1).ToLower()) + (n.Substring(1))
let up (n:string) = (n.Substring(0, 1).ToUpper()) + (n.Substring(1))

let typeName (n:string) = (down n).Replace("_", "-")
let varName (n:string) = (up n).Replace("_", "-")


let rec genTypeApp t =
  match t with
  | TAName n -> typeName n
  | TAApply (n, _) -> genTypeApp n
  | TAProject (t, f) -> $"{genTypeApp t}--{typeName f}"
  | TAList t -> "tuple"
  | TAMaybe t -> "any"

let genField (Field (f, t)) =
   $"{typeName f} is {genTypeApp t}"

let genFields fs =
  fs |> Seq.map genField
     |> String.concat ", "


let rec generateType t =
  match t with
  | TRecord(n, _, fs) -> genRecord n fs
  | TUnion(n, _, vs) ->
      let variants = Seq.map (genVariant n) vs
      $"""abstract {typeName n} is node;{"\n"}{String.concat "\n" variants}"""

and genRecord n fs =
  $"""type {typeName n} is node({genFields fs});"""

and genVariant p (Variant (n, fs)) =
  $"type {typeName p}--{typeName n} is {typeName p}({genFields fs});"

let generateTypes ts =
  ts |> Seq.map generateType
     |> String.concat "\n\n"

let isSingletonRule (b:RuleBody) =
  (Array.length b.Terms = 1) && (Option.isNone b.Expr)

let isImmaterial t =
  match t with
  | TNot _ -> true
  | TLookahead _ -> true
  | _ -> false

let genVisitorBinder (n, b) =
  match b with
  | BBound (_, t) when isImmaterial t -> []
  | BUnbound t when isImmaterial t -> []
  | BBound (name, _) -> [varName name]
  | BUnbound (_) -> ["_"]

let genVisitorParams binders =
  Seq.collect genVisitorBinder (enumerate binders)
  |> String.concat ", "

let rec genTypeName e =
  match e with
  | AVar n -> id n
  | AProject (o, f) -> $"{genTypeName o}--{typeName f}"
  | _ -> failwithf "Not a valid type constructor"

let rec genExpr e =
  match e with
  | AMeta -> "(#lingua interval: Node)"
  | AMake (c, args) -> 
      $"""(new {genTypeName c}({Seq.map genExpr args |> String.concat ", "})) """
  | AProject (o, f) ->  $"(({genExpr o}).{typeName f})"
  | AVar n -> varName n
  | AList xs ->
      $"""[{Seq.map genExpr xs |> String.concat ", "}]"""
  | ACons (hd, tl) ->
      $"""([{Seq.map genExpr hd |> String.concat ", "}] ++ {genExpr tl})"""
  | ANull ->
      "nothing"

let genVisitorEffect n expr =
  match expr with
  | None -> $"""assert not "Undefined rule {n}";"""
  | Some e ->
      genExpr e

let genAltVisitor tk n (i, b:RuleBody) =
  let name = toString (n + "_alt" + (string i))
  if tk then
    $"""  {name} -> #lingua visitor-source,"""
  else if isSingletonRule b then
    $"""  {name} -> #lingua visitor-singleton,"""
  else 
    let parameters = genVisitorParams b.Terms
    let effect = genVisitorEffect n b.Expr
    $"""  {name} -> #lingua visitor-lambda: {{ Node, {parameters} in {effect} }},"""

let genRuleVisitor tk n b =
  $"  {toString n} -> #lingua visitor-identity," + "\n" +
  ((Seq.map (genAltVisitor tk n) (enumerate b)
  |> String.concat "\n"))

let genVisitor rule =
  match rule with
  | RDefine (tk, n, _, _, b) -> genRuleVisitor tk n b
  | ROverride (tk, n, _, b) -> genRuleVisitor tk n b
  | RExtend (tk, n, _, b) -> genRuleVisitor tk n b

let generateVisitors (g:Grammar) =
  Seq.map genVisitor g.Rules
  |> String.concat "\n"

let generate (g:Grammar) =
  "% crochet" + $"""
// This file is generated from Lingua

open crochet.text.parsing.lingua;

// Type definitions
abstract node;
{generateTypes g.Types}

// Grammar definition
define grammar = lazy (#lingua grammar: {toString (generateGrammar g)});

define to-ast = lazy ((force grammar) semantics: [
{generateVisitors g}
]);
  """