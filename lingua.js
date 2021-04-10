const fs = require("fs");
const path = require("path");
import * as lingua from "./build/source/App";
const yargs = require("yargs");
const prettier = require("prettier");

const argv = yargs.argv;

const [file, target0] = argv._;
const target = target0 ?? "typescript";

if (file == null) {
  console.error(`Usage: lingua <file.lingua> [typescript | crochet]`);
  process.exit(1);
}

const source = fs.readFileSync(file, "utf8");
const ast = lingua.parse(source)(file);

switch (target) {
  case "typescript": {
    const out = lingua.generate(ast);
    const pretty = prettier.format(out, { parser: "typescript" });
    console.log(pretty);
    break;
  }
  case "crochet": {
    const out = lingua.generateCrochet(ast);
    console.log(out);
    break;
  }
  default:
    throw new Error(`Unknown target ${target}`);
}