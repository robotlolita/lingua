module Lingua.CrochetCodegen

open Fable.Core
open Lingua.AST
open Lingua.OhmCodegen

let id n = n
let down (n:string) = (n.Substring(0, 1).ToLower()) + (n.Substring(1))
let up (n:string) = (n.Substring(0, 1).ToUpper()) + (n.Substring(1))

let typeName (n:string) = (down n).Replace("_", "-").ToLower()
let varName (n:string) = (up (n.ToLower())).Replace("_", "-")


let rec genTypeApp t =
  match t with
  | TAName n -> typeName n
  | TAApply (n, _) -> genTypeApp n
  | TAProject (t, f) -> $"{genTypeApp t}--{typeName f}"
  | TAList t -> "list"
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
      $"""abstract {typeName n} is ast-node;{"\n"}{String.concat "\n" variants}"""

and genRecord n fs =
  if (Array.length fs) = 0 then
    $"""singleton {typeName n} is ast-node;"""
  else
    $"""type {typeName n}({genFields fs}) is ast-node;"""

and genVariant p (Variant (n, fs)) =
  if (Array.length fs) = 0 then
    $"singleton {typeName p}--{typeName n} is {typeName p};"
  else
    $"type {typeName p}--{typeName n}({genFields fs}) is {typeName p};"

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
  | AVar n -> typeName n
  | AProject (o, f) -> $"{genTypeName o}--{typeName f}"
  | _ -> failwithf "Not a valid type constructor"

let rec genExpr e =
  match e with
  | AMeta -> "(#lingua interval: Node)"
  | AMake (c, args) -> 
      if (Array.length args) = 0 then
        $"""{genTypeName c}"""
      else
        $"""(new {genTypeName c}({Seq.map genExpr args |> String.concat ", "})) """
  | AProject (o, f) ->  $"(({genExpr o}).{typeName f})"
  | AVar n -> varName n
  | AList xs ->
      $"""[{Seq.map genExpr xs |> String.concat ", "}]"""
  | ACons (hd, tl) ->
      $"""([{Seq.map genExpr hd |> String.concat ", "}] ++ {genExpr tl})"""
  | AApply (n, args) ->
      let args = Seq.map genExpr args |> String.concat ", "
      $"""(#lingua primitive: "{typeName n}")({args})"""
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

let topRule (g:Grammar) =
  let name = match Array.head g.Rules with
             | RDefine (_, n, _, _, _) -> n
             | ROverride (_, n, _, _) -> n
             | RExtend (_, n, _, _) -> n
  $"\"{name}\""
  

let generate (g:Grammar) =
  "% crochet" + $"""
// This file is generated from Lingua

open crochet.text.parsing.lingua;

// Type definitions
local abstract ast-node is node;
{generateTypes g.Types}

singleton {typeName g.Name};

// Grammar definition
local define grammar =
  lazy (#lingua grammar: {toString (generateGrammar g)});

local define to-ast =
  lazy ((force grammar) semantics: [
    {generateVisitors g}
  ]);

command {typeName g.Name} grammar = force grammar;

command {typeName g.Name} to-ast = force to-ast;

command {typeName g.Name} parse: (Input is text) -> result<{genTypeApp g.Top}, string> do
  let Tree = self grammar parse: Input rule: {topRule g};
  Tree map: {{ X in self to-ast transform: X }};
end
  """