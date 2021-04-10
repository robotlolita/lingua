module Lingua.App

let parse = Lingua.Parser.parse
let generate g = Lingua.Codegen.generate g
let generateCrochet g = Lingua.CrochetCodegen.generate g