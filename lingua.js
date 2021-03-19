const fs = require("fs");
const path = require("path");
import * as lingua from "./build/source/App";
const yargs = require("yargs");
const prettier = require("prettier");

const argv = yargs.argv;

const [file] = argv._;

if (file == null) {
  console.error(`Usage: lingua <file.lingua>`);
  process.exit(1);
}

const source = fs.readFileSync(file, "utf8");
const ast = lingua.parse(source)(file);
const out = lingua.generate(ast);
const pretty = prettier.format(out, { parser: "typescript" });

console.log(pretty);